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
///   1. PoseAnalyzer     — MediaPipe pose extraction (Python subprocess)
///   2. StepEstimator    — zero-crossing step count
///   3. SpeedEstimator   — preprocessing + velocity + stance-phase belt speed
///   4. CalorieEstimator — MET-based calorie burn (only when profile weight is set)
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
        // ── Derive typed values from model fields ──────────────────────────────

        YawCorrectionMethod yawMethod = settings.YawCorrectionMethod switch
        {
            "PerFrame" => YawCorrectionMethod.PerFrame,
            "NoYaw"    => YawCorrectionMethod.NoYaw,
            _          => YawCorrectionMethod.Median,
        };

        double stanceTolerance = settings.StanceTolerance / 100.0;

        double? hipHeightMeters = profile?.HipHeight.HasValue == true
            ? profile.HipHeightUnit == "in"
                ? profile.HipHeight!.Value * 0.0254
                : profile.HipHeight!.Value / 100.0
            : null;

        double? weightKg = profile?.Weight.HasValue == true
            ? profile.WeightUnit == "lbs"
                ? profile.Weight!.Value * 0.453592
                : profile.Weight!.Value
            : null;

        // ── Stage 1: pose extraction ───────────────────────────────────────────

        progress?.Report("Analyzing video…");
        var analyzer   = new PoseAnalyzer(scriptPath, verbose: false);
        var poseFrames = await analyzer.AnalyzeVideoAsync(videoPath, settings.MaxFrames);

        // ── Stage 2: step count ────────────────────────────────────────────────

        progress?.Report("Counting steps…");
        StringWriter? debugWriter = settings.DebugSteps ? new StringWriter() : null;
        int estimatedSteps = new StepEstimator().EstimateSteps(
            poseFrames, debug: settings.DebugSteps, threshold: settings.StepThreshold,
            debugOutput: debugWriter);

        // ── Stage 3: speed estimation ──────────────────────────────────────────

        progress?.Report("Estimating speed…");
        var speedResult = await Task.Run(() =>
            new SpeedEstimator(
                    yawMethod,
                    stanceTolerance,
                    settings.PerspectiveCorrection,
                    settings.TemporalSmoothing,
                    settings.VisibilityInterpolation)
                .Estimate(poseFrames, hipHeightMeters, aspectRatio: aspectRatio));

        // ── Stage 4: calorie estimate (requires weight) ────────────────────────

        CalorieEstimationResult? calorieResult = null;
        if (weightKg.HasValue)
        {
            var calorieEstimator = CalorieEstimator.LoadFromAssets();
            calorieResult = calorieEstimator.Estimate(speedResult, weightKg.Value);
        }

        progress?.Report($"Done — {poseFrames.Count} frames");

        return new AnalysisResult
        {
            PoseFrames     = poseFrames,
            EstimatedSteps = estimatedSteps,
            SpeedResult    = speedResult,
            CalorieResult  = calorieResult,
            StepDebugLog   = debugWriter?.ToString(),
        };
    }
}
