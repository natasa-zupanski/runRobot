using runRobot.Estimators;
using runRobot.Models;
using runRobot.Preprocessing;

namespace runRobot;

/// <summary>
/// Output of a completed analysis pass.
/// </summary>
public record AnalysisResult
{
    public List<PoseFrame>          PoseFrames     { get; init; } = [];
    public int                      EstimatedSteps { get; init; }
    public SpeedEstimationResult?   SpeedResult    { get; init; }
    public CalorieEstimationResult? CalorieResult  { get; init; }
    public string?                  StepDebugLog   { get; init; }
}

/// <summary>
/// Runs the full analysis pipeline end-to-end, reporting progress via
/// IProgress&lt;string&gt; so the caller can update a status label without any
/// coupling to a specific UI type.
///
/// Stages:
///   1. PoseAnalyzer          — MediaPipe pose extraction (Python subprocess)
///   2. PoseCorrectorPipeline — visibility interpolation, smoothing, perspective correction
///   3. StepEstimator         — zero-crossing step count (on corrected frames)
///   4. SpeedEstimator        — 2D projection + velocity + stance-phase belt speed
///   5. CalorieEstimator      — MET-based calorie burn (only when profile weight is set)
/// </summary>
public class AnalysisPipeline(string scriptPath)
{
    /// <summary>
    /// Runs the pipeline using a <see cref="SettingsPreset"/> and an optional
    /// <see cref="UserProfile"/> for body metrics.
    /// </summary>
    public async Task<AnalysisResult> RunAsync(
        string             videoPath,
        double             aspectRatio,
        SettingsPreset     settings,
        UserProfile?       profile  = null,
        IProgress<string>? progress = null)
    {
        var (hipHeightMeters, weightKg) = ResolveProfile(profile);

        progress?.Report("Analyzing video…");
        var poseFrames = await ExtractPosesAsync(videoPath, settings.MaxFrames);

        progress?.Report("Correcting pose…");
        var (correctedFrames, pipeline) = await CorrectPosesAsync(poseFrames, settings, aspectRatio);

        progress?.Report("Counting steps…");
        var (estimatedSteps, stepDebugLog) = CountSteps(correctedFrames, settings);

        progress?.Report("Estimating speed…");
        var speedResult = await EstimateSpeedAsync(pipeline, correctedFrames, hipHeightMeters, settings.StanceTolerance / 100.0);

        var calorieResult = EstimateCalories(speedResult, weightKg);

        progress?.Report($"Done — {correctedFrames.Count} frames");

        return new AnalysisResult
        {
            PoseFrames     = poseFrames,
            EstimatedSteps = estimatedSteps,
            SpeedResult    = speedResult,
            CalorieResult  = calorieResult,
            StepDebugLog   = stepDebugLog,
        };
    }

    private async Task<List<PoseFrame>> ExtractPosesAsync(string videoPath, int? maxFrames)
    {
        var analyzer = new PoseAnalyzer(scriptPath, verbose: false);
        return await analyzer.AnalyzeVideoAsync(videoPath, maxFrames);
    }

    private static async Task<(List<PoseFrame> Frames, PoseCorrectorPipeline Pipeline)> CorrectPosesAsync(
        List<PoseFrame> frames, SettingsPreset settings, double aspectRatio)
    {
        var pipeline  = new PoseCorrectorPipeline(settings);
        var corrected = await Task.Run(() => pipeline.Correct(frames, aspectRatio));
        return (corrected, pipeline);
    }

    private static (int Steps, string? DebugLog) CountSteps(List<PoseFrame> frames, SettingsPreset settings)
    {
        StringWriter? debugWriter = settings.DebugSteps ? new StringWriter() : null;
        int steps = new StepEstimator().EstimateSteps(
            frames, debug: settings.DebugSteps, threshold: settings.StepThreshold,
            debugOutput: debugWriter);
        return (steps, debugWriter?.ToString());
    }

    private static async Task<SpeedEstimationResult> EstimateSpeedAsync(
        PoseCorrectorPipeline pipeline, List<PoseFrame> corrected,
        double? hipHeightMeters, double stanceTolerance)
    {
        return await Task.Run(() =>
        {
            var projected = PoseCorrectorPipeline.Project(corrected);
            return new SpeedEstimator(stanceTolerance)
                .Estimate(projected, hipHeightMeters, methodUsed: pipeline.MethodUsed);
        });
    }

    private static CalorieEstimationResult? EstimateCalories(SpeedEstimationResult speedResult, double? weightKg)
    {
        if (!weightKg.HasValue) return null;
        return CalorieEstimator.LoadFromAssets().Estimate(speedResult, weightKg.Value);
    }

    private static (double? HipHeightMeters, double? WeightKg) ResolveProfile(UserProfile? profile)
    {
        double? hipHeightMeters = profile?.HipHeight.HasValue == true
            ? profile.HipHeightUnit == "in"
                ? profile.HipHeight.Value * 0.0254
                : profile.HipHeight.Value / 100.0
            : null;

        double? weightKg = profile?.Weight.HasValue == true
            ? profile.WeightUnit == "lbs"
                ? profile.Weight.Value * 0.453592
                : profile.Weight.Value
            : null;

        return (hipHeightMeters, weightKg);
    }
}
