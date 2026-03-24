using runRobot.Api.Jobs;

namespace runRobot.Api.Models;

/// <summary>
/// Body returned by GET /analyze/{jobId}.
/// </summary>
public record JobStatusResponse
{
    public Guid    JobId    { get; init; }
    public string  Status   { get; init; } = "";
    public string  Progress { get; init; } = "";
    public AnalyzeResultDto? Result { get; init; }
    public string? Error    { get; init; }

    public JobStatusResponse(AnalysisJob job)
    {
        JobId    = job.JobId;
        Status   = job.Status.ToString().ToLowerInvariant();
        Progress = job.Progress;
        Error    = job.Error;
        Result   = job.CoreResult is null ? null : new AnalyzeResultDto(job.CoreResult);
    }
}

/// <summary>
/// Slim, serialisable summary of an <see cref="AnalysisResult"/>.
/// Omits large intermediate collections (projected frames, velocities).
/// </summary>
public record AnalyzeResultDto
{
    public int     EstimatedSteps  { get; init; }
    public double? Speed           { get; init; }   // world units/s
    public double? ScaleFactor     { get; init; }   // metres per world unit
    public double? Calories        { get; init; }
    public double? AverageMet      { get; init; }
    public double? DurationMinutes { get; init; }
    public double? AverageSpeedMph { get; init; }
    public double? RunningFraction { get; init; }
    public List<double?> SpeedPerFrame { get; init; } = [];
    public string?       StepDebugLog  { get; init; }

    public AnalyzeResultDto(AnalysisResult result)
    {
        EstimatedSteps  = result.EstimatedSteps;
        Speed           = result.SpeedResult?.Speed;
        ScaleFactor     = result.SpeedResult?.ScaleFactor;
        SpeedPerFrame   = result.SpeedResult?.SpeedPerFrame ?? [];
        Calories        = result.CalorieResult?.Calories;
        AverageMet      = result.CalorieResult?.AverageMet;
        DurationMinutes = result.CalorieResult?.DurationMinutes;
        AverageSpeedMph = result.CalorieResult?.AverageSpeedMph;
        RunningFraction = result.CalorieResult?.RunningFraction;
        StepDebugLog    = result.StepDebugLog;
    }
}
