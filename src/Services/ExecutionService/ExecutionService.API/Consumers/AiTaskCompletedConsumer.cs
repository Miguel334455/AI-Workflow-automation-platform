using ExecutionService.API.Engine;
using MassTransit;
using Shared.Contracts.Events;

namespace ExecutionService.API.Consumers;

public class AiTaskCompletedConsumer : IConsumer<AITaskCompletedEvent>
{
    private readonly WorkflowExecutionEngine _engine;
    private readonly ILogger<AiTaskCompletedConsumer> _logger;

    public AiTaskCompletedConsumer(WorkflowExecutionEngine engine, ILogger<AiTaskCompletedConsumer> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AITaskCompletedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Received AITaskCompletedEvent for run {RunId}, node {NodeId}, success={Success}",
            msg.RunId, msg.NodeId, msg.Success);

        await _engine.ResumeAfterNodeCompletionAsync(
            msg.RunId, msg.NodeId, msg.Success, msg.OutputJson, msg.ErrorMessage, context.CancellationToken);
    }
}
