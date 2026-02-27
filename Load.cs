public class LoadService
{
    private readonly IVikunjaApi _vikunja;
    private readonly ILogger<LoadService> _logger;

    public LoadService(IVikunjaApi vikunja, ILogger<LoadService> logger)
    {
        _vikunja = vikunja;
        _logger = logger;
    }

    /// <summary>
    /// Creates tasks in Vikunja and adds labels via a separate bulk API call.
    /// Single function that handles the full create-task + add-labels orchestration.
    /// </summary>
    public async Task<List<TaskResult>> CreateTasksAsync(List<(VikunjaTask Task, List<long> LabelIds)> tasks)
    {
        var results = new List<TaskResult>();

        foreach (var (task, labelIds) in tasks)
        {
            var result = new TaskResult { Title = task.Title, ProjectId = task.ProjectId };

            try
            {
                // Step 1: Create the task
                var projectId = task.ProjectId ?? throw new InvalidOperationException("ProjectId is required");
                var created = await _vikunja.CreateTaskAsync(projectId, task);
                result.TaskId = created.Id;

                _logger.LogInformation("Created task {TaskId}: '{Title}' in project {ProjectId}",
                    created.Id, task.Title, projectId);

                // Step 2: Add labels (if any) via separate bulk call
                if (labelIds.Count > 0)
                {
                    var bulkRequest = new LabelBulkRequest
                    {
                        Labels = labelIds.Select(id => new LabelId { Id = id }).ToList()
                    };

                    await _vikunja.BulkUpdateLabelsAsync(created.Id, bulkRequest);
                    result.LabelsAdded = labelIds.Count;

                    _logger.LogInformation("Added {LabelCount} labels to task {TaskId}",
                        labelIds.Count, created.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create/label task '{Title}'", task.Title);
                result.Error = ex.Message;
            }

            results.Add(result);
        }

        _logger.LogInformation("Load completed: {Success}/{Total} tasks created successfully",
            results.Count(r => r.Error == null), results.Count);

        return results;
    }
}
