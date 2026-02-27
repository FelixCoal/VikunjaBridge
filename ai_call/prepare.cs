using System.Text;

public class PrepareService
{
    private readonly IVikunjaApi _vikunja;
    private readonly ILogger<PrepareService> _logger;

    public PrepareService(IVikunjaApi vikunja, ILogger<PrepareService> logger)
    {
        _vikunja = vikunja;
        _logger = logger;
    }

    /// <summary>
    /// Fetches context from Vikunja and builds the LLM prompt messages.
    /// Returns (messages, projects, labels) so downstream steps can reuse the context.
    /// </summary>
    public async Task<(List<LlmMessage> Messages, List<VikunjaProject> Projects, List<VikunjaLabel> Labels)> PrepareAsync(string freetext)
    {
        _logger.LogInformation("Preparing LLM prompt for freetext input");

        // Fetch context from Vikunja in parallel
        var projectsTask = _vikunja.GetProjectsAsync();
        var labelsTask = _vikunja.GetLabelsAsync();

        List<VikunjaTask> existingTasks;
        try
        {
            var tasksTask = _vikunja.GetTasksAsync(page: 1, per_page: 20);
            await Task.WhenAll(projectsTask, labelsTask, tasksTask);
            existingTasks = tasksTask.Result;
        }
        catch
        {
            await Task.WhenAll(projectsTask, labelsTask);
            existingTasks = new List<VikunjaTask>();
            _logger.LogWarning("Failed to fetch existing tasks, continuing without them");
        }

        var projects = projectsTask.Result;
        var labels = labelsTask.Result;

        _logger.LogInformation("Fetched {ProjectCount} projects, {LabelCount} labels, {TaskCount} existing tasks",
            projects.Count, labels.Count, existingTasks.Count);

        // Build context sections
        var context = new StringBuilder();

        context.AppendLine("## Available Projects");
        foreach (var p in projects)
            context.AppendLine($"- id: {p.Id}, title: \"{p.Title}\"");

        context.AppendLine();
        context.AppendLine("## Available Labels");
        if (labels.Count > 0)
        {
            foreach (var l in labels)
                context.AppendLine($"- id: {l.Id}, title: \"{l.Title}\"");
        }
        else
        {
            context.AppendLine("(no labels exist yet)");
        }

        context.AppendLine();
        context.AppendLine("## Sample Existing Tasks (for reference)");
        foreach (var t in existingTasks.Take(10))
            context.AppendLine($"- \"{t.Title}\" (project_id: {t.ProjectId})");

        var systemPrompt = $$"""
            You are a task extraction assistant. Given free text from a user, extract one or more actionable tasks.

            Output ONLY valid JSON matching this exact schema (no markdown, no explanation):
            {
              "tasks": [
                {
                  "title": "string (required, concise task title)",
                  "description": "string or null (optional, extra details)",
                  "project_id": number or null (optional, must be an id from the projects list below),
                  "label_ids": [number] or null (optional, must be ids from the labels list below),
                  "due_date": "ISO 8601 datetime string" or null (optional, e.g. "2026-03-01T00:00:00Z"),
                  "priority": number or null (optional, 0=unset, 1=low, 2=medium, 3=high, 4=urgent, 5=DO NOW)
                }
              ]
            }

            Rules:
            - Extract ALL tasks mentioned in the text.
            - If the user mentions a project by name, match it to the closest project_id from the list.
            - If the user mentions labels/tags, match them to label_ids from the list.
            - If you cannot confidently match a project or label, omit that field (set to null).
            - For relative dates like "tomorrow", "next Monday", calculate from today's date.
            - Today's date is: {{DateTime.UtcNow:yyyy-MM-dd}}

            {{context}}
            """;

        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = freetext }
        };

        return (messages, projects, labels);
    }
}

// Shared message model for the LLM call
public class LlmMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
