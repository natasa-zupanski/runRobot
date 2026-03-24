using runRobot.Api.Jobs;
using runRobot.Api.Models;
using runRobot.Models;

namespace runRobot.Api.Endpoints;

public static class AnalyzeEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/analyze", StartAnalysis)
           .WithName("StartAnalysis")
           .WithSummary("Submit a video for analysis. Returns a job ID immediately.");

        app.MapGet("/analyze/{jobId:guid}", GetStatus)
           .WithName("GetAnalysisStatus")
           .WithSummary("Poll analysis status. Result is included once status is 'done'.");
    }

    // POST /analyze
    private static IResult StartAnalysis(AnalyzeRequest req, AnalysisJobStore store)
    {
        if (string.IsNullOrWhiteSpace(req.VideoPath))
            return Results.BadRequest(new { error = "videoPath is required." });

        var job = store.Create();

        // Fire-and-forget: run analysis on a background thread.
        _ = Task.Run(async () =>
        {
            job.Status = JobStatus.Running;
            try
            {
                string scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "pose_analyzer.py");
                var pipeline      = new AnalysisPipeline(scriptPath);
                var settings      = req.Settings ?? new SettingsPreset();
                var progress      = new Progress<string>(msg => job.Progress = msg);

                job.CoreResult = await pipeline.RunAsync(
                    videoPath:   req.VideoPath,
                    aspectRatio: req.AspectRatio,
                    settings:    settings,
                    profile:     req.Profile,
                    progress:    progress);

                job.Status = JobStatus.Done;
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.Error  = ex.Message;
            }
        });

        return Results.Accepted($"/analyze/{job.JobId}", new { jobId = job.JobId });
    }

    // GET /analyze/{jobId}
    private static IResult GetStatus(Guid jobId, AnalysisJobStore store)
    {
        var job = store.Get(jobId);
        if (job is null) return Results.NotFound(new { error = $"Job {jobId} not found." });
        return Results.Ok(new JobStatusResponse(job));
    }
}
