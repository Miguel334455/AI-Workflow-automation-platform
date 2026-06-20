namespace Shared.Contracts.Events;

/// <summary>
/// Published by the Execution Service when a workflow run reaches an AI node.
/// Consumed by the AI Task Service.
/// </summary>
public record AITaskRequestedEvent
{
    public Guid RunId { get; init; }
    public Guid NodeExecutionId { get; init; }
    public Guid NodeId { get; init; }

    /// <summary>
    /// The AI operation to perform: "summarize", "classify", "generate", "extract", etc.
    /// </summary>
    public string TaskType { get; init; } = string.Empty;

    /// <summary>
    /// Prompt template / instructions, with placeholders already resolved
    /// by the Execution Service using prior node outputs.
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// Optional raw input data (e.g. text to summarize) as JSON.
    /// </summary>
    public string? InputJson { get; init; }
}

/// <summary>
/// Published by the AI Task Service when an AI task finishes (success or failure).
/// Consumed by the Execution Service.
/// </summary>
public record AITaskCompletedEvent
{
    public Guid RunId { get; init; }
    public Guid NodeExecutionId { get; init; }
    public Guid NodeId { get; init; }
    public bool Success { get; init; }
    public string? OutputJson { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
}
