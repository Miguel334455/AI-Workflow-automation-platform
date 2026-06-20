using ExecutionService.API.Domain;
using ExecutionService.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExecutionService.API.Controllers;

public class NodeExecutionResponse
{
    public Guid NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? OutputJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public class WorkflowRunResponse
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public List<NodeExecutionResponse> NodeExecutions { get; set; } = new();

    public static WorkflowRunResponse From(WorkflowRun run) => new()
    {
        Id = run.Id,
        WorkflowId = run.WorkflowId,
        TriggerType = run.TriggerType,
        Status = run.Status.ToString(),
        ErrorMessage = run.ErrorMessage,
        CreatedAtUtc = run.CreatedAtUtc,
        StartedAtUtc = run.StartedAtUtc,
        CompletedAtUtc = run.CompletedAtUtc,
        NodeExecutions = run.NodeExecutions.Select(n => new NodeExecutionResponse
        {
            NodeId = n.NodeId,
            NodeName = n.NodeName,
            NodeType = n.NodeType,
            Status = n.Status.ToString(),
            OutputJson = n.OutputJson,
            ErrorMessage = n.ErrorMessage,
            StartedAtUtc = n.StartedAtUtc,
            CompletedAtUtc = n.CompletedAtUtc
        }).ToList()
    };
}

[ApiController]
[Route("api/runs")]
[Authorize]
public class RunsController : ControllerBase
{
    private readonly ExecutionDbContext _db;

    public RunsController(ExecutionDbContext db)
    {
        _db = db;
    }

    /// <summary>List recent workflow runs, optionally filtered by workflow.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkflowRunResponse>>> GetAll([FromQuery] Guid? workflowId, [FromQuery] int take = 50)
    {
        var query = _db.WorkflowRuns.Include(r => r.NodeExecutions).AsQueryable();
        if (workflowId.HasValue) query = query.Where(r => r.WorkflowId == workflowId.Value);

        var runs = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(take)
            .ToListAsync();

        return Ok(runs.Select(WorkflowRunResponse.From));
    }

    /// <summary>Get the full status (including per-node results) of a single run.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowRunResponse>> GetById(Guid id)
    {
        var run = await _db.WorkflowRuns
            .Include(r => r.NodeExecutions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (run is null) return NotFound();

        return Ok(WorkflowRunResponse.From(run));
    }
}
