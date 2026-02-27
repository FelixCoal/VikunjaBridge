using System.Text.Json.Serialization;
using Refit;

// ── Vikunja API DTOs ──────────────────────────────────────────────

public class VikunjaProject
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

public class VikunjaLabel
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

public class VikunjaTask
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("project_id")]
    public long? ProjectId { get; set; }

    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }

    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    [JsonPropertyName("labels")]
    public List<VikunjaLabel>? Labels { get; set; }
}

public class LabelBulkRequest
{
    [JsonPropertyName("labels")]
    public List<LabelId> Labels { get; set; } = new();
}

public class LabelId
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}

// ── LLM output DTOs ──────────────────────────────────────────────

public class LlmResponse
{
    [JsonPropertyName("tasks")]
    public List<LlmTaskOutput> Tasks { get; set; } = new();
}

public class LlmTaskOutput
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("project_id")]
    public long? ProjectId { get; set; }

    [JsonPropertyName("label_ids")]
    public List<long>? LabelIds { get; set; }

    [JsonPropertyName("due_date")]
    public string? DueDate { get; set; }

    [JsonPropertyName("priority")]
    public int? Priority { get; set; }
}

// ── API request/response ─────────────────────────────────────────

public record TaskRequest(string Freetext);

public class TaskResult
{
    public long TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public long? ProjectId { get; set; }
    public int LabelsAdded { get; set; }
    public string? Error { get; set; }
}

// ── Refit interface ──────────────────────────────────────────────

public interface IVikunjaApi
{
    [Get("/api/v1/projects")]
    Task<List<VikunjaProject>> GetProjectsAsync();

    [Get("/api/v1/labels")]
    Task<List<VikunjaLabel>> GetLabelsAsync();

    [Get("/api/v1/tasks")]
    Task<List<VikunjaTask>> GetTasksAsync([Query] int page = 1, [Query] int per_page = 50);

    [Put("/api/v1/projects/{projectId}/tasks")]
    Task<VikunjaTask> CreateTaskAsync(long projectId, [Body] VikunjaTask task);

    [Post("/api/v1/tasks/{taskId}/labels/bulk")]
    Task BulkUpdateLabelsAsync(long taskId, [Body] LabelBulkRequest labels);
}