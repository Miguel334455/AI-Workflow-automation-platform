namespace WorkflowService.API.Domain;

public class Workflow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public List<WorkflowNode> Nodes { get; set; } = new();
    public List<WorkflowConnection> Connections { get; set; } = new();
}

public class WorkflowNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public Workflow? Workflow { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// "trigger" | "http" | "ai" | "notification" | "condition"
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// JSON configuration specific to the node type.
    /// </summary>
    public string ConfigJson { get; set; } = "{}";

    public int Order { get; set; }
}

public class WorkflowConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public Workflow? Workflow { get; set; }

    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }

    /// <summary>
    /// Optional branch label, e.g. "true" / "false" for condition nodes.
    /// </summary>
    public string? Condition { get; set; }
}
