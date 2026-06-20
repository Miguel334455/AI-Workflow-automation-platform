using TriggerService.API.Domain;

namespace TriggerService.API.DTOs;

public class CreateTriggerRequest
{
    public Guid WorkflowId { get; set; }
    public TriggerType Type { get; set; }
    public string? CronExpression { get; set; }
    public string ConfigJson { get; set; } = "{}";
}

public class TriggerResponse
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public TriggerType Type { get; set; }
    public bool IsActive { get; set; }
    public string? CronExpression { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastTriggeredAtUtc { get; set; }

    public static TriggerResponse From(Trigger t) => new()
    {
        Id = t.Id,
        WorkflowId = t.WorkflowId,
        Type = t.Type,
        IsActive = t.IsActive,
        CronExpression = t.CronExpression,
        ConfigJson = t.ConfigJson,
        CreatedAtUtc = t.CreatedAtUtc,
        LastTriggeredAtUtc = t.LastTriggeredAtUtc
    };
}

public class FireTriggerRequest
{
    /// <summary>Optional JSON payload passed through to the workflow run.</summary>
    public string? PayloadJson { get; set; }
}

public class FireTriggerResponse
{
    public Guid RunId { get; set; }
}
