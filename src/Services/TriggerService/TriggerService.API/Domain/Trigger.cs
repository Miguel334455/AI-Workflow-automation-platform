namespace TriggerService.API.Domain;

public enum TriggerType
{
    Manual = 0,
    Webhook = 1,
    Schedule = 2
}

public class Trigger
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public TriggerType Type { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// For Schedule triggers: a standard cron expression (e.g. "0 */5 * * * ?").
    /// Unused for Manual/Webhook.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Arbitrary JSON configuration (e.g. webhook secret, default payload).
    /// </summary>
    public string ConfigJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAtUtc { get; set; }
}
