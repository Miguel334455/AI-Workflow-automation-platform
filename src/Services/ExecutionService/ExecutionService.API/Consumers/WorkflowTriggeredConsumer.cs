using ExecutionService.API.Engine;
using MassTransit;
using Shared.Contracts.Events;

namespace ExecutionService.API.Consumers;

public class WorkflowTriggeredConsumer : IConsumer<WorkflowTriggeredEvent>
{
    private readonly WorkflowExecutionEngine _engine;
    private readonly ILogger<WorkflowTriggeredConsumer> _logger;

    public WorkflowTriggeredConsumer(WorkflowExecutionEngine engine, ILogger<WorkflowTriggeredConsumer> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<WorkflowTriggeredEvent> context)
    {
        _logger.LogInformation("Received WorkflowTriggeredEvent for run {RunId}, workflow {WorkflowId}",
            context.Message.RunId, context.Message.WorkflowId);

        await _engine.StartRunAsync(context.Message, context.CancellationToken);
    }
}
