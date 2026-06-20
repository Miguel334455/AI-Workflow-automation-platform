using AITaskService.API.Services;
using MassTransit;
using Shared.Contracts.Events;

namespace AITaskService.API.Consumers;

public class AiTaskRequestedConsumer : IConsumer<AITaskRequestedEvent>
{
    private readonly IAiClient _aiClient;
    private readonly ILogger<AiTaskRequestedConsumer> _logger;

    public AiTaskRequestedConsumer(IAiClient aiClient, ILogger<AiTaskRequestedConsumer> logger)
    {
        _aiClient = aiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AITaskRequestedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Processing AI task '{TaskType}' for run {RunId}, node {NodeId}",
            msg.TaskType, msg.RunId, msg.NodeId);

        try
        {
            var output = await _aiClient.RunTaskAsync(msg.TaskType, msg.Prompt, msg.InputJson, context.CancellationToken);

            await context.Publish(new AITaskCompletedEvent
            {
                RunId = msg.RunId,
                NodeExecutionId = msg.NodeExecutionId,
                NodeId = msg.NodeId,
                Success = true,
                OutputJson = output
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI task '{TaskType}' failed for run {RunId}, node {NodeId}",
                msg.TaskType, msg.RunId, msg.NodeId);

            await context.Publish(new AITaskCompletedEvent
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
