namespace Shared.Contracts.Events;

/// <summary>
/// Published by the Trigger Service when a workflow's trigger fires
/// (manual, webhook, or scheduled). Consumed by the Execution Service.
/// </summary>
public record WorkflowTriggeredEvent
{
    public Guid RunId { get; init; } = Guid.NewGuid();
    public Guid WorkflowId { get; init; }
    public Guid TriggerId { get; init; }
    public string TriggerType { get; init; } = string.Empty; // Manual | Webhook | Schedule
    public string? PayloadJson { get; init; }
    public DateTime TriggeredAtUtc { get; init; } = DateTime.UtcNow;
}
