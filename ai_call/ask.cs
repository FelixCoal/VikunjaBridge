using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class AskService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<AskService> _logger;

    public AskService(HttpClient httpClient, IConfiguration config, ILogger<AskService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Sends the prepared messages to Together.ai and returns the raw content string.
    /// </summary>
    public async Task<string> AskAsync(List<LlmMessage> messages)
    {
        var model = _config["TogetherAi:Model"] ?? "meta-llama/Llama-3.3-70B-Instruct-Turbo";

        var requestBody = new TogetherAiRequest
        {
            Model = model,
            Messages = messages.Select(m => new TogetherAiMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList(),
            Temperature = 0.2,
            ResponseFormat = new ResponseFormat { Type = "json_object" }
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        _logger.LogInformation("Sending request to Together.ai (model: {Model})", model);

        var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Together.ai returned {StatusCode}: {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"Together.ai returned {response.StatusCode}: {responseBody}");
        }

        // Parse the response to extract the content
        using var doc = JsonDocument.Parse(responseBody);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Together.ai returned an empty response");

        _logger.LogInformation("Received LLM response ({Length} chars)", content.Length);
        return content;
    }
}

// ── Together.ai request models ───────────────────────────────────

internal class TogetherAiRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<TogetherAiMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("response_format")]
    public ResponseFormat? ResponseFormat { get; set; }
}

internal class TogetherAiMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal class ResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_object";
}
