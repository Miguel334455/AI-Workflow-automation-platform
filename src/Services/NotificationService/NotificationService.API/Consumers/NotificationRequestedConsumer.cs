using MassTransit;
using NotificationService.API.Services;
using Shared.Contracts.Events;

namespace NotificationService.API.Consumers;

public class NotificationRequestedConsumer : IConsumer<NotificationRequestedEvent>
{
    private readonly INotificationSender _sender;
    private readonly ILogger<NotificationRequestedConsumer> _logger;

    public NotificationRequestedConsumer(INotificationSender sender, ILogger<NotificationRequestedConsumer> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NotificationRequestedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Processing notification ({Channel}) for run {RunId}, node {NodeId}",
            msg.Channel, msg.RunId, msg.NodeId);

        try
        {
            await _sender.SendAsync(msg.Channel, msg.Target, msg.Subject, msg.Body, context.CancellationToken);

            await context.Publish(new NotificationCompletedEvent
            {
                RunId = msg.RunId,
                NodeExecutionId = msg.NodeExecutionId,
                NodeId = msg.NodeId,
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification ({Channel}) failed for run {RunId}, node {NodeId}",
                msg.Channel, msg.RunId, msg.NodeId);

            await context.Publish(new NotificationCompletedEvent
            {
                RunId = msg.RunId,
                NodeExecutionId = msg.NodeExecutionId,
                NodeId = msg.NodeId,
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}
