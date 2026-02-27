public class TransformService
{
    private readonly ILogger<TransformService> _logger;

    public TransformService(ILogger<TransformService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Transforms LLM output into VikunjaTask objects with their associated label IDs.
    /// Validates project/label IDs against the actual Vikunja data.
    /// </summary>
    public List<(VikunjaTask Task, List<long> LabelIds)> Transform(
        LlmResponse llmResponse,
        List<VikunjaProject> projects,
        List<VikunjaLabel> labels)
    {
        var validProjectIds = projects.Select(p => p.Id).ToHashSet();
        var validLabelIds = labels.Select(l => l.Id).ToHashSet();
        var defaultProjectId = projects.FirstOrDefault()?.Id;

        var results = new List<(VikunjaTask, List<long>)>();

        foreach (var llmTask in llmResponse.Tasks)
        {
            // Validate project ID — fall back to default if invalid or missing
            long? projectId = llmTask.ProjectId;
            if (projectId.HasValue && !validProjectIds.Contains(projectId.Value))
            {
                _logger.LogWarning("LLM returned invalid project_id {ProjectId}, falling back to default", projectId);
                projectId = defaultProjectId;
            }
            else if (!projectId.HasValue)
            {
                projectId = defaultProjectId;
            }

            if (!projectId.HasValue)
            {
                _logger.LogError("No valid project found for task '{Title}', skipping", llmTask.Title);
                continue;
            }

            // Validate label IDs — keep only known ones
            var taskLabelIds = llmTask.LabelIds?
                .Where(id => validLabelIds.Contains(id))
                .ToList() ?? new List<long>();

            if (llmTask.LabelIds != null && taskLabelIds.Count < llmTask.LabelIds.Count)
            {
                var invalidIds = llmTask.LabelIds.Except(taskLabelIds);
                _logger.LogWarning("Dropped invalid label_ids: {InvalidIds}", string.Join(", ", invalidIds));
            }

            var vikunjaTask = new VikunjaTask
            {
                Title = llmTask.Title.Trim(),
                Description = llmTask.Description?.Trim(),
                ProjectId = projectId,
                DueDate = llmTask.DueDate,
                Priority = llmTask.Priority
            };

            results.Add((vikunjaTask, taskLabelIds));
        }

        _logger.LogInformation("Transformed {Count} tasks", results.Count);
        return results;
    }
}
