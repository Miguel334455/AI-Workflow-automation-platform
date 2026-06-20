using Shared.Contracts.Dtos;

namespace WorkflowService.API.DTOs;

public class WorkflowNodeRequest
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public int Order { get; set; }
}

public class WorkflowConnectionRequest
{
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public string? Condition { get; set; }
}

public class CreateWorkflowRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<WorkflowNodeRequest> Nodes { get; set; } = new();
    public List<WorkflowConnectionRequest> Connections { get; set; } = new();
}

public class UpdateWorkflowRequest : CreateWorkflowRequest
{
    public bool IsActive { get; set; }
}

public class WorkflowResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public List<WorkflowNodeRequest> Nodes { get; set; } = new();
    public List<WorkflowConnectionRequest> Connections { get; set; } = new();

    /// <summary>
    /// Converts this response into the shared DTO consumed by the Execution Service.
    /// </summary>
    public WorkflowDefinitionDto ToDefinitionDto() => new()
    {
        WorkflowId = Id,
        Name = Name,
        IsActive = IsActive,
        Nodes = Nodes.Select(n => new WorkflowNodeDto
        {
            NodeId = n.Id ?? Guid.Empty,
            Name = n.Name,
            Type = n.Type,
            ConfigJson = n.ConfigJson,
            Order = n.Order
        }).ToList(),
        Connections = Connections.Select(c => new WorkflowConnectionDto
        {
            FromNodeId = c.FromNodeId,
            ToNodeId = c.ToNodeId,
            Condition = c.Condition
        }).ToList()
    };
}
