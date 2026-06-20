namespace Shared.Contracts.Dtos;

/// <summary>
/// Snapshot of a workflow definition used by the Execution Service.
/// Mirrors the structure owned by the Workflow Service.
/// </summary>
public class WorkflowDefinitionDto
{
    public Guid WorkflowId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<WorkflowNodeDto> Nodes { get; set; } = new();
    public List<WorkflowConnectionDto> Connections { get; set; } = new();
}

public class WorkflowNodeDto
{
    public Guid NodeId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Node type: "trigger", "http", "ai", "notification", "condition", etc.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Arbitrary JSON configuration for the node (URL, prompt, condition expression, etc.)
    /// </summary>
    public string ConfigJson { get; set; } = "{}";

    /// <summary>
    /// Execution order hint (used for simple linear graphs).
    /// </summary>
    public int Order { get; set; }
}

public class WorkflowConnectionDto
{
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }

    /// <summary>
    /// Optional condition label for branching (e.g. "true" / "false").
    /// </summary>
    public string? Condition { get; set; }
}
