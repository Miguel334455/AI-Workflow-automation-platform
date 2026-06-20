using System.Text.Json;
using ExecutionService.API.Domain;
using ExecutionService.API.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Dtos;
using Shared.Contracts.Events;

namespace ExecutionService.API.Engine;

/// <summary>
/// Walks a workflow's node graph starting from the trigger node, executing
/// nodes in dependency order. Synchronous node types (http, condition) run
/// inline; async node types (ai, notification) are dispatched via RabbitMQ
/// and execution pauses until the corresponding *Completed event arrives
/// (handled by AiTaskCompletedConsumer / NotificationCompletedConsumer).
/// </summary>
public class WorkflowExecutionEngine
{
    private readonly ExecutionDbContext _db;
    private readonly WorkflowDefinitionClient _definitionClient;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly HttpClient _httpClient;
    private readonly ILogger<WorkflowExecutionEngine> _logger;

    public WorkflowExecutionEngine(
        ExecutionDbContext db,
        WorkflowDefinitionClient definitionClient,
        IPublishEndpoint publishEndpoint,
        IHttpClientFactory httpClientFactory,
        ILogger<WorkflowExecutionEngine> logger)
    {
        _db = db;
        _definitionClient = definitionClient;
        _publishEndpoint = publishEndpoint;
        _httpClient = httpClientFactory.CreateClient("generic-http-node");
        _logger = logger;
    }

    /// <summary>
    /// Starts a new run for a workflow that was just triggered. Persists the
    /// run + pending node executions, then begins processing from the
    /// trigger node's successors.
    /// </summary>
    public async Task StartRunAsync(WorkflowTriggeredEvent evt, CancellationToken ct)
    {
        var definition = await _definitionClient.GetDefinitionAsync(evt.WorkflowId, ct);
        if (definition is null)
        {
            _logger.LogWarning("Cannot start run {RunId}: workflow {WorkflowId} not found", evt.RunId, evt.WorkflowId);
            return;
        }

        if (!definition.IsActive)
        {
            _logger.LogInformation("Skipping run {RunId}: workflow {WorkflowId} is inactive", evt.RunId, evt.WorkflowId);
            return;
        }

        var run = new WorkflowRun
        {
            Id = evt.RunId,
            WorkflowId = evt.WorkflowId,
            TriggerId = evt.TriggerId,
            TriggerType = evt.TriggerType,
            Status = RunStatus.Running,
            InputPayloadJson = evt.PayloadJson,
            StartedAtUtc = DateTime.UtcNow,
            NodeExecutions = definition.Nodes
                .Where(n => n.Type != "trigger") // trigger node itself doesn't execute
                .Select(n => new NodeExecution
                {
                    RunId = evt.RunId,
                    NodeId = n.NodeId,
                    NodeName = n.Name,
                    NodeType = n.Type,
                    Status = NodeExecutionStatus.Pending
                }).ToList()
        };

        _db.WorkflowRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        // Find nodes with no incoming connection from another non-trigger node
        // (i.e. roots of the executable graph) and start there.
        var triggerNode = definition.Nodes.FirstOrDefault(n => n.Type == "trigger");
        var startNodeIds = triggerNode is not null
            ? definition.Connections.Where(c => c.FromNodeId == triggerNode.NodeId).Select(c => c.ToNodeId).ToList()
            : definition.Nodes.Where(n => n.Type != "trigger" && n.Order == 0).Select(n => n.NodeId).ToList();

        foreach (var nodeId in startNodeIds)
        {
            await ExecuteNodeAsync(run, definition, nodeId, ct);
        }

        await TryCompleteRunAsync(run.Id, definition, ct);
    }

    /// <summary>
    /// Resumes processing after an async node (AI task / notification) completes.
    /// </summary>
    public async Task ResumeAfterNodeCompletionAsync(Guid runId, Guid nodeId, bool success, string? outputJson, string? errorMessage, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns.FindAsync(new object[] { runId }, ct);
        if (run is null)
        {
            _logger.LogWarning("ResumeAfterNodeCompletion: run {RunId} not found", runId);
            return;
        }

        var nodeExecution = _db.NodeExecutions.Local
            .FirstOrDefault(n => n.RunId == runId && n.NodeId == nodeId)
            ?? await _db.NodeExecutions
                .Where(n => n.RunId == runId && n.NodeId == nodeId)
                .FirstOrDefaultAsync(ct);

        if (nodeExecution is null)
        {
            _logger.LogWarning("ResumeAfterNodeCompletion: node execution for {NodeId} in run {RunId} not found", nodeId, runId);
            return;
        }

        nodeExecution.Status = success ? NodeExecutionStatus.Succeeded : NodeExecutionStatus.Failed;
        nodeExecution.OutputJson = outputJson;
        nodeExecution.ErrorMessage = errorMessage;
        nodeExecution.CompletedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var definition = await _definitionClient.GetDefinitionAsync(run.WorkflowId, ct);
        if (definition is null)
        {
            _logger.LogWarning("Cannot resume run {RunId}: workflow {WorkflowId} not found", runId, run.WorkflowId);
            return;
        }

        if (!success)
        {
            await FailRunAsync(run, $"Node '{nodeExecution.NodeName}' failed: {errorMessage}", ct);
            return;
        }

        // Continue to successor nodes.
        var nextNodeIds = definition.Connections
            .Where(c => c.FromNodeId == nodeId)
            .Select(c => c.ToNodeId)
            .ToList();

        foreach (var nextNodeId in nextNodeIds)
        {
            await ExecuteNodeAsync(run, definition, nextNodeId, ct);
        }

        await TryCompleteRunAsync(run.Id, definition, ct);
    }

    /// <summary>
    /// Executes a single node based on its type. Synchronous node types
    /// complete immediately and recurse into successors; async node types
    /// dispatch an event and return (execution resumes via consumer).
    /// </summary>
    private async Task ExecuteNodeAsync(WorkflowRun run, WorkflowDefinitionDto definition, Guid nodeId, CancellationToken ct)
    {
        var node = definition.Nodes.FirstOrDefault(n => n.NodeId == nodeId);
        if (node is null)
        {
            _logger.LogWarning("Run {RunId}: node {NodeId} not found in definition", run.Id, nodeId);
            return;
        }

        var nodeExecution = _db.NodeExecutions.Local.FirstOrDefault(n => n.RunId == run.Id && n.NodeId == nodeId)
            ?? await _db.NodeExecutions.Where(n => n.RunId == run.Id && n.NodeId == nodeId).FirstOrDefaultAsync(ct);

        if (nodeExecution is null)
        {
            _logger.LogWarning("Run {RunId}: no NodeExecution record for node {NodeId}", run.Id, nodeId);
            return;
        }

        if (nodeExecution.Status != NodeExecutionStatus.Pending)
        {
            // Already started/completed (e.g. revisited via multiple branches) - skip.
            return;
        }

        nodeExecution.Status = NodeExecutionStatus.Running;
        nodeExecution.StartedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        switch (node.Type)
        {
            case "http":
                await ExecuteHttpNodeAsync(run, definition, node, nodeExecution, ct);
                break;

            case "condition":
                await ExecuteConditionNodeAsync(run, definition, node, nodeExecution, ct);
                break;

            case "ai":
                await DispatchAiNodeAsync(run, node, nodeExecution, ct);
                break;

            case "notification":
                await DispatchNotificationNodeAsync(run, node, nodeExecution, ct);
                break;

            default:
                _logger.LogWarning("Run {RunId}: unknown node type '{Type}' for node {NodeId}", run.Id, node.Type, nodeId);
                nodeExecution.Status = NodeExecutionStatus.Skipped;
                nodeExecution.CompletedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                await ContinueToSuccessorsAsync(run, definition, nodeId, ct);
                break;
        }
    }

    private async Task ContinueToSuccessorsAsync(WorkflowRun run, WorkflowDefinitionDto definition, Guid nodeId, CancellationToken ct)
    {
        var nextNodeIds = definition.Connections.Where(c => c.FromNodeId == nodeId).Select(c => c.ToNodeId);
        foreach (var nextId in nextNodeIds)
        {
            await ExecuteNodeAsync(run, definition, nextId, ct);
        }
    }

    /// <summary>
    /// "http" node: calls an external URL synchronously. Config JSON shape:
    /// { "method": "GET|POST", "url": "...", "body": "..." }
    /// </summary>
    private async Task ExecuteHttpNodeAsync(WorkflowRun run, WorkflowDefinitionDto definition, WorkflowNodeDto node, NodeExecution nodeExecution, CancellationToken ct)
    {
        try
        {
            var config = JsonSerializer.Deserialize<HttpNodeConfig>(node.ConfigJson) ?? new HttpNodeConfig();
            var method = string.IsNullOrWhiteSpace(config.Method) ? "GET" : config.Method.ToUpperInvariant();

            using var request = new HttpRequestMessage(new HttpMethod(method), config.Url);
            if (!string.IsNullOrWhiteSpace(config.Body) && method != "GET")
            {
                request.Content = new StringContent(config.Body, System.Text.Encoding.UTF8, "application/json");
            }

            using var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            nodeExecution.OutputJson = JsonSerializer.Serialize(new
            {
                statusCode = (int)response.StatusCode,
                body = responseBody
            });

            if (response.IsSuccessStatusCode)
            {
                nodeExecution.Status = NodeExecutionStatus.Succeeded;
                nodeExecution.CompletedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                await ContinueToSuccessorsAsync(run, definition, node.NodeId, ct);
            }
            else
            {
                nodeExecution.Status = NodeExecutionStatus.Failed;
                nodeExecution.ErrorMessage = $"HTTP {(int)response.StatusCode}";
                nodeExecution.CompletedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                await FailRunAsync(run, $"Node '{node.Name}' returned HTTP {(int)response.StatusCode}", ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId}: http node {NodeId} failed", run.Id, node.NodeId);
            nodeExecution.Status = NodeExecutionStatus.Failed;
            nodeExecution.ErrorMessage = ex.Message;
            nodeExecution.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await FailRunAsync(run, $"Node '{node.Name}' threw: {ex.Message}", ct);
        }
    }

    /// <summary>
    /// "condition" node: evaluates a simple expression against prior node
    /// outputs and follows the matching "true"/"false" connection.
    /// Config JSON shape: { "expression": "..." } — evaluation is intentionally
    /// minimal; replace with a proper expression evaluator (e.g. NCalc) as needed.
    /// </summary>
    private async Task ExecuteConditionNodeAsync(WorkflowRun run, WorkflowDefinitionDto definition, WorkflowNodeDto node, NodeExecution nodeExecution, CancellationToken ct)
    {
        try
        {
            var config = JsonSerializer.Deserialize<ConditionNodeConfig>(node.ConfigJson) ?? new ConditionNodeConfig();

            // Placeholder evaluation: treat a non-empty, non-"false" expression as true.
            // Replace with NCalc/JsonPath-based evaluation against prior node outputs.
            var result = !string.IsNullOrWhiteSpace(config.Expression)
                          && !config.Expression.Trim().Equals("false", StringComparison.OrdinalIgnoreCase);

            nodeExecution.OutputJson = JsonSerializer.Serialize(new { result });
            nodeExecution.Status = NodeExecutionStatus.Succeeded;
            nodeExecution.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            var branch = result ? "true" : "false";
            var nextNodeIds = definition.Connections
                .Where(c => c.FromNodeId == node.NodeId &&
                            (c.Condition == null || c.Condition.Equals(branch, StringComparison.OrdinalIgnoreCase)))
                .Select(c => c.ToNodeId);

            foreach (var nextId in nextNodeIds)
            {
                await ExecuteNodeAsync(run, definition, nextId, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId}: condition node {NodeId} failed", run.Id, node.NodeId);
            nodeExecution.Status = NodeExecutionStatus.Failed;
            nodeExecution.ErrorMessage = ex.Message;
            nodeExecution.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await FailRunAsync(run, $"Node '{node.Name}' threw: {ex.Message}", ct);
        }
    }

    /// <summary>
    /// "ai" node: publishes AITaskRequestedEvent for the AI Task Service and
    /// returns. Execution resumes via AiTaskCompletedConsumer.
    /// Config JSON shape: { "taskType": "summarize|classify|generate", "prompt": "..." }
    /// </summary>
    private async Task DispatchAiNodeAsync(WorkflowRun run, WorkflowNodeDto node, NodeExecution nodeExecution, CancellationToken ct)
    {
        var config = JsonSerializer.Deserialize<AiNodeConfig>(node.ConfigJson) ?? new AiNodeConfig();

        nodeExecution.InputJson = JsonSerializer.Serialize(new { config.Prompt, config.TaskType });
        await _db.SaveChangesAsync(ct);

        await _publishEndpoint.Publish(new AITaskRequestedEvent
        {
            RunId = run.Id,
            NodeExecutionId = nodeExecution.Id,
            NodeId = node.NodeId,
            TaskType = config.TaskType,
            Prompt = config.Prompt,
            InputJson = run.InputPayloadJson
        }, ct);
    }

    /// <summary>
    /// "notification" node: publishes NotificationRequestedEvent for the
    /// Notification Service and returns. Execution resumes via
    /// NotificationCompletedConsumer.
    /// Config JSON shape: { "channel": "Email|Slack|Webhook", "target": "...", "subject": "...", "body": "..." }
    /// </summary>
    private async Task DispatchNotificationNodeAsync(WorkflowRun run, WorkflowNodeDto node, NodeExecution nodeExecution, CancellationToken ct)
    {
        var config = JsonSerializer.Deserialize<NotificationNodeConfig>(node.ConfigJson) ?? new NotificationNodeConfig();

        nodeExecution.InputJson = JsonSerializer.Serialize(config);
        await _db.SaveChangesAsync(ct);

        await _publishEndpoint.Publish(new NotificationRequestedEvent
        {
            RunId = run.Id,
            NodeExecutionId = nodeExecution.Id,
            NodeId = node.NodeId,
            Channel = config.Channel,
            Target = config.Target,
            Subject = config.Subject,
            Body = config.Body
        }, ct);
    }

    private async Task FailRunAsync(WorkflowRun run, string errorMessage, CancellationToken ct)
    {
        run.Status = RunStatus.Failed;
        run.ErrorMessage = errorMessage;
        run.CompletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _publishEndpoint.Publish(new WorkflowRunCompletedEvent
        {
            RunId = run.Id,
            WorkflowId = run.WorkflowId,
            Status = "Failed",
            CompletedAtUtc = DateTime.UtcNow
        }, ct);
    }

    /// <summary>
    /// Marks the run as Succeeded once every NodeExecution is in a terminal state.
    /// </summary>
    private async Task TryCompleteRunAsync(Guid runId, WorkflowDefinitionDto definition, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns.FindAsync(new object[] { runId }, ct);
        if (run is null || run.Status != RunStatus.Running) return;

        var executions = _db.NodeExecutions.Local.Where(n => n.RunId == runId).ToList();
        if (executions.Count == 0)
        {
            executions = await _db.NodeExecutions.Where(n => n.RunId == runId).ToListAsync(ct);
        }

        var allTerminal = executions.All(n =>
            n.Status is NodeExecutionStatus.Succeeded or NodeExecutionStatus.Failed or NodeExecutionStatus.Skipped);

        if (!allTerminal) return;

        var anyFailed = executions.Any(n => n.Status == NodeExecutionStatus.Failed);

        run.Status = anyFailed ? RunStatus.Failed : RunStatus.Succeeded;
        run.CompletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _publishEndpoint.Publish(new WorkflowRunCompletedEvent
        {
            RunId = run.Id,
            WorkflowId = run.WorkflowId,
            Status = run.Status.ToString(),
            CompletedAtUtc = DateTime.UtcNow
        }, ct);
    }
}

// --- Node config shapes (deserialized from WorkflowNode.ConfigJson) ---

public class HttpNodeConfig
{
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = string.Empty;
    public string? Body { get; set; }
}

public class ConditionNodeConfig
{
    public string Expression { get; set; } = string.Empty;
}

public class AiNodeConfig
{
    public string TaskType { get; set; } = "generate";
    public string Prompt { get; set; } = string.Empty;
}

public class NotificationNodeConfig
{
    public string Channel { get; set; } = "Email";
    public string Target { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
