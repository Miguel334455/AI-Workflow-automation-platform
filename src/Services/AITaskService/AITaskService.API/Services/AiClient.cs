namespace AITaskService.API.Services;

public interface IAiClient
{
    Task<string> RunTaskAsync(string taskType, string prompt, string? inputJson, CancellationToken ct);
}

/// <summary>
/// Calls an external LLM provider (e.g. OpenAI, Azure OpenAI, Anthropic).
/// Configure via "AiProvider" section in appsettings. Falls back to a stub
/// response if no API key is configured, so the platform runs end-to-end
/// without external dependencies during development.
/// </summary>
public class AiClient : IAiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiClient> _logger;

    public AiClient(HttpClient httpClient, IConfiguration configuration, ILogger<AiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> RunTaskAsync(string taskType, string prompt, string? inputJson, CancellationToken ct)
    {
        var apiKey = _configuration["AiProvider:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogInformation("AiProvider:ApiKey not configured; returning stub response for task '{TaskType}'", taskType);
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                taskType,
                prompt,
                result = $"[stub] Result for {taskType}: {prompt}"
            });
        }

        // Example wiring for an OpenAI-compatible /v1/chat/completions endpoint.
        // Adjust request/response shapes for your chosen provider.
        var baseUrl = _configuration["AiProvider:BaseUrl"] ?? "https://api.openai.com";
        var model = _configuration["AiProvider:Model"] ?? "gpt-4o-mini";

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = $"You are an AI workflow node performing a '{taskType}' task." },
                new { role = "user", content = prompt + (inputJson is not null ? $"\n\nInput data:\n{inputJson}" : "") }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = System.Net.Http.Json.JsonContent.Create(requestBody);

        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"AI provider returned {(int)response.StatusCode}: {body}");
        }

        return body;
    }
}
