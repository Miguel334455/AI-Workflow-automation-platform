using ExecutionService.API.Engine;
using MassTransit;
using Shared.Contracts.Events;

namespace ExecutionService.API.Consumers;

public class NotificationCompletedConsumer : IConsumer<NotificationCompletedEvent>
{
    private readonly WorkflowExecutionEngine _engine;
    private readonly ILogger<NotificationCompletedConsumer> _logger;

    public NotificationCompletedConsumer(WorkflowExecutionEngine engine, ILogger<NotificationCompletedConsumer> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NotificationCompletedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Received NotificationCompletedEvent for run {RunId}, node {NodeId}, success={Success}",
            msg.RunId, msg.NodeId, msg.Success);

        await _engine.ResumeAfterNodeCompletionAsync(
            msg.RunId, msg.NodeId, msg.Success, outputJson: null, msg.ErrorMessage, context.CancellationToken);
    }
}
