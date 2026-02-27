using System.Text.Json;
using System.Text.RegularExpressions;

public class CleanService
{
    private readonly ILogger<CleanService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CleanService(ILogger<CleanService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses the raw LLM response into a structured LlmResponse.
    /// Handles common LLM quirks like markdown code fences.
    /// </summary>
    public LlmResponse CleanAsync(string rawResponse)
    {
        _logger.LogInformation("Cleaning LLM response");

        var json = rawResponse.Trim();

        // Try direct deserialization first
        try
        {
            var result = JsonSerializer.Deserialize<LlmResponse>(json, JsonOptions);
            if (result?.Tasks != null && result.Tasks.Count > 0)
            {
                return Validate(result);
            }
        }
        catch (JsonException)
        {
            _logger.LogWarning("Direct JSON parse failed, attempting to extract from markdown fences");
        }

        // Try extracting JSON from markdown code fences
        var fencePattern = new Regex(@"```(?:json)?\s*\n?(.*?)\n?\s*```", RegexOptions.Singleline);
        var match = fencePattern.Match(json);
        if (match.Success)
        {
            json = match.Groups[1].Value.Trim();
            try
            {
                var result = JsonSerializer.Deserialize<LlmResponse>(json, JsonOptions);
                if (result?.Tasks != null && result.Tasks.Count > 0)
                {
                    return Validate(result);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse extracted JSON from code fence");
            }
        }

        // Last resort: try to find a JSON object in the response
        var braceStart = json.IndexOf('{');
        var braceEnd = json.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
        {
            json = json[braceStart..(braceEnd + 1)];
            try
            {
                var result = JsonSerializer.Deserialize<LlmResponse>(json, JsonOptions);
                if (result?.Tasks != null && result.Tasks.Count > 0)
                {
                    return Validate(result);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON after brace extraction");
            }
        }

        throw new InvalidOperationException(
            $"Could not parse LLM response into tasks. Raw response: {rawResponse[..Math.Min(500, rawResponse.Length)]}");
    }

    private LlmResponse Validate(LlmResponse response)
    {
        // Remove tasks with empty titles
        response.Tasks = response.Tasks
            .Where(t => !string.IsNullOrWhiteSpace(t.Title))
            .ToList();

        if (response.Tasks.Count == 0)
            throw new InvalidOperationException("LLM returned tasks but none had a valid title");

        _logger.LogInformation("Cleaned {TaskCount} tasks from LLM response", response.Tasks.Count);
        return response;
    }
}
