namespace Shared.Contracts.Events;

/// <summary>
/// Published by the Execution Service when a workflow run reaches a notification node.
/// Consumed by the Notification Service.
/// </summary>
public record NotificationRequestedEvent
{
    public Guid RunId { get; init; }
    public Guid NodeExecutionId { get; init; }
    public Guid NodeId { get; init; }

    /// <summary>
    /// Channel: "Email", "Slack", "Webhook".
    /// </summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>
    /// Channel-specific target (email address, Slack webhook URL, etc.)
    /// </summary>
    public string Target { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}

/// <summary>
/// Published by the Notification Service after attempting delivery.
/// Consumed by the Execution Service to mark the node complete.
/// </summary>
public record NotificationCompletedEvent
{
    public Guid RunId { get; init; }
    public Guid NodeExecutionId { get; init; }
    public Guid NodeId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published by the Execution Service whenever a workflow run finishes
/// (succeeded or failed). Other services (e.g. Workflow Service for stats,
/// Notification Service for run-summary alerts) may subscribe.
/// </summary>
public record WorkflowRunCompletedEvent
{
    public Guid RunId { get; init; }
    public Guid WorkflowId { get; init; }
    public string Status { get; init; } = string.Empty; // Succeeded | Failed
    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
}
