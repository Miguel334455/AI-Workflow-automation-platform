using Microsoft.EntityFrameworkCore;
using Quartz;
using TriggerService.API.Domain;
using TriggerService.API.Infrastructure;
using TriggerService.API.Services;

namespace TriggerService.API.Jobs;

/// <summary>
/// Runs every minute, checks Schedule-type triggers whose cron expression
/// matches "now", and fires them. A simple approach suitable for a small
/// number of triggers; for high volume, register one Quartz job per trigger
/// with its own cron schedule instead.
/// </summary>
[DisallowConcurrentExecution]
public class ScheduledTriggerPollingJob : IJob
{
    private readonly TriggerDbContext _db;
    private readonly TriggerPublisher _publisher;
    private readonly ILogger<ScheduledTriggerPollingJob> _logger;

    public ScheduledTriggerPollingJob(
        TriggerDbContext db,
        TriggerPublisher publisher,
        ILogger<ScheduledTriggerPollingJob> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTime.UtcNow;

        var scheduledTriggers = await _db.Triggers
            .Where(t => t.Type == TriggerType.Schedule && t.IsActive && t.CronExpression != null)
            .ToListAsync(context.CancellationToken);

        foreach (var trigger in scheduledTriggers)
        {
            try
            {
                var cron = new CronExpression(trigger.CronExpression!);

                // Fire if the trigger hasn't run yet, or if a cron-scheduled
                // time falls between the last run and now.
                var referenceTime = trigger.LastTriggeredAtUtc ?? now.AddMinutes(-1);
                var nextFire = cron.GetNextValidTimeAfter(referenceTime);

                if (nextFire.HasValue && nextFire.Value.UtcDateTime <= now)
                {
                    await _publisher.FireAsync(trigger, payloadJson: null, context.CancellationToken);
                    _logger.LogInformation("Fired scheduled trigger {TriggerId} for workflow {WorkflowId}",
                        trigger.Id, trigger.WorkflowId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed evaluating/firing scheduled trigger {TriggerId}", trigger.Id);
            }
        }
    }
}
