namespace ExecutionService.API.Domain;

public enum RunStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3
}

public enum NodeExecutionStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Skipped = 4
}

public class WorkflowRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public Guid TriggerId { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public RunStatus Status { get; set; } = RunStatus.Pending;

    public string? InputPayloadJson { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public List<NodeExecution> NodeExecutions { get; set; } = new();
}

public class NodeExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public WorkflowRun? Run { get; set; }

    public Guid NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;

    public NodeExecutionStatus Status { get; set; } = NodeExecutionStatus.Pending;

    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
