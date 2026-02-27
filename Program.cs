using System.Net.Http.Headers;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// ── Register Refit client for Vikunja API ────────────────────────
builder.Services
    .AddRefitClient<IVikunjaApi>()
    .ConfigureHttpClient((sp, client) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        client.BaseAddress = new Uri(config["Vikunja:BaseUrl"]!);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config["Vikunja:ApiToken"]);
    });

// ── Register HttpClient for Together.ai ──────────────────────────
builder.Services.AddHttpClient<AskService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri("https://api.together.xyz/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", config["TogetherAi:ApiKey"]);
});

// ── Register services ────────────────────────────────────────────
builder.Services.AddScoped<PrepareService>();
// AskService is registered via AddHttpClient above
builder.Services.AddScoped<CleanService>();
builder.Services.AddScoped<TransformService>();
builder.Services.AddScoped<LoadService>();

var app = builder.Build();

// ── Health endpoint (no auth) ────────────────────────────────────
app.MapGet("/health", () => "Healthy");

// ── Add task endpoint ────────────────────────────────────────────
app.MapPost("/add-task", async (
    TaskRequest request,
    HttpContext httpContext,
    IConfiguration config,
    PrepareService prepare,
    AskService ask,
    CleanService clean,
    TransformService transform,
    LoadService load,
    ILogger<Program> logger) =>
{
    // Auth check
    var expectedKey = config["Api:ApiKey"];
    var providedKey = httpContext.Request.Headers["X-Api-Key"].FirstOrDefault();
    if (string.IsNullOrEmpty(providedKey) || providedKey != expectedKey)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Freetext))
    {
        return Results.BadRequest(new { error = "Freetext is required" });
    }

    try
    {
        logger.LogInformation("Processing add-task request");

        // 1. Prepare: Fetch Vikunja context & build LLM prompt
        var (messages, projects, labels) = await prepare.PrepareAsync(request.Freetext);

        // 2. Ask: Call Together.ai LLM
        var rawResponse = await ask.AskAsync(messages);

        // 3. Clean: Parse LLM response into structured data
        var llmResponse = clean.CleanAsync(rawResponse);

        // 4. Transform: Convert to VikunjaTask DTOs with validated IDs
        var taskList = transform.Transform(llmResponse, projects, labels);

        if (taskList.Count == 0)
        {
            return Results.BadRequest(new { error = "No valid tasks could be extracted from the input" });
        }

        // 5. Load: Create tasks in Vikunja (with label orchestration)
        var results = await load.CreateTasksAsync(taskList);

        var succeeded = results.Where(r => r.Error == null).ToList();
        var failed = results.Where(r => r.Error != null).ToList();

        logger.LogInformation("Completed: {Succeeded} succeeded, {Failed} failed",
            succeeded.Count, failed.Count);

        return Results.Ok(new
        {
            message = $"Created {succeeded.Count} task(s)",
            tasks = results
        });
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "External API error");
        return Results.Json(new { error = "External service error", details = ex.Message },
            statusCode: 502);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError(ex, "Processing error");
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error processing request");
        return Results.Json(new { error = "Internal server error" }, statusCode: 500);
    }
});

app.Run();