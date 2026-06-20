using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Shared.Contracts.Dtos;

namespace ExecutionService.API.Infrastructure;

/// <summary>
/// Fetches workflow definitions (nodes + connections) from the Workflow Service,
/// caching results in Redis to avoid a round-trip on every run.
/// </summary>
public class WorkflowDefinitionClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly ILogger<WorkflowDefinitionClient> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public WorkflowDefinitionClient(HttpClient httpClient, IDistributedCache cache, ILogger<WorkflowDefinitionClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<WorkflowDefinitionDto?> GetDefinitionAsync(Guid workflowId, CancellationToken ct = default)
    {
        var cacheKey = $"workflow-definition:{workflowId}";

        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<WorkflowDefinitionDto>(cached);
        }

        var response = await _httpClient.GetAsync($"/api/workflows/{workflowId}/definition", ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to fetch workflow definition {WorkflowId}: {StatusCode}",
                workflowId, response.StatusCode);
            return null;
        }

        var definition = await response.Content.ReadFromJsonAsync<WorkflowDefinitionDto>(cancellationToken: ct);
        if (definition is not null)
        {
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(definition),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration },
                ct);
        }

        return definition;
    }
}
