using MassTransit;
using Shared.Contracts.Events;
using TriggerService.API.Domain;
using TriggerService.API.Infrastructure;

namespace TriggerService.API.Services;

/// <summary>
/// Encapsulates the logic of firing a trigger: updates LastTriggeredAtUtc
/// and publishes a WorkflowTriggeredEvent for the Execution Service.
/// </summary>
public class TriggerPublisher
{
    private readonly TriggerDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;

    public TriggerPublisher(TriggerDbContext db, IPublishEndpoint publishEndpoint)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Guid> FireAsync(Trigger trigger, string? payloadJson, CancellationToken ct = default)
    {
        trigger.LastTriggeredAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var runId = Guid.NewGuid();

        await _publishEndpoint.Publish(new WorkflowTriggeredEvent
        {
            RunId = runId,
            WorkflowId = trigger.WorkflowId,
            TriggerId = trigger.Id,
            TriggerType = trigger.Type.ToString(),
            PayloadJson = payloadJson,
            TriggeredAtUtc = DateTime.UtcNow
        }, ct);

        return runId;
    }
}
