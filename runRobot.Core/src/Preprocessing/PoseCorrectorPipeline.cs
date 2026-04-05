using runRobot.Models;

namespace runRobot.Preprocessing;

/// <summary>
/// Applies a configurable sequence of preprocessing corrections to raw PoseFrames,
/// then projects them into 2D world-space SideViewFrames.
///
/// The active steps are controlled by the <see cref="PoseCorrectorType"/> values
/// passed to the constructor. Steps always execute in this fixed order when enabled:
///
///   1. VisibilityInterpolation — fill low-visibility landmark gaps
///   2. TemporalSmoothing       — Gaussian moving average over time
///   3. PerspectiveCorrection   — calibrated unprojection to world coordinates
///   4. Aspect ratio scaling    — always applied when aspectRatio != 1.0 (between PDC and yaw)
///   5. YawCorrection           — NoYaw / PerFrame / Median (optional step)
/// </summary>
public class PoseCorrectorPipeline
{
    private readonly YawCorrectionMethod _method;
    private readonly HashSet<PoseCorrectorType> _steps;

    public YawCorrectionMethod MethodUsed { get; private set; }

    public PoseCorrectorPipeline(SettingsPreset settings)
    {
        _method = settings.YawCorrectionMethod;
        _steps  = [.. settings.PoseCorrectorSteps];
    }

    /// <summary>
    /// Applies the configured corrector steps to raw pose frames, including AR scaling
    /// and optional yaw correction. The returned frames are in AR-scaled, yaw-aligned
    /// world-space coordinates when <see cref="PoseCorrectorType.PerspectiveCorrection"/>
    /// and <see cref="PoseCorrectorType.YawCorrection"/> are both active.
    /// Pass the result to <see cref="Project"/> to obtain 2D side-view frames.
    /// </summary>
    public List<PoseFrame> Correct(List<PoseFrame> frames, double aspectRatio = 1.0)
    {
        // Non-yaw correction steps first (VisibilityInterpolation, TemporalSmoothing, PerspectiveCorrection).
        var result = frames;
        foreach (var step in PoseCorrectorFactory.Order.Where(s => s != PoseCorrectorType.YawCorrection && _steps.Contains(s)))
            result = PoseCorrectorFactory.GetCorrector(step, result, aspectRatio, _method).CorrectAll(result);

        // AR scaling must precede yaw so world-X distances are accurate before rotation.
        if (aspectRatio != 1.0)
            result = [.. result.Select(frame =>
            {
                var scaled = new PoseFrame { Timestamp = frame.Timestamp, FrameNumber = frame.FrameNumber };
                scaled.Landmarks.AddRange(frame.Landmarks.Select(lm => new Landmark
                {
                    X = lm.X * aspectRatio, Y = lm.Y, Z = lm.Z, Visibility = lm.Visibility
                }));
                return scaled;
            })];

        // Yaw correction runs last (optional).
        MethodUsed = _method;
        if (_steps.Contains(PoseCorrectorType.YawCorrection))
            result = PoseCorrectorFactory.GetCorrector(PoseCorrectorType.YawCorrection, result, aspectRatio, _method).CorrectAll(result);

        return result;
    }

    /// <summary>
    /// Projects corrected pose frames into 2D <see cref="SideViewFrame"/>s by stripping Z.
    /// AR scaling and yaw correction are applied upstream in <see cref="Correct"/>.
    /// </summary>
    public static List<SideViewFrame> Project(List<PoseFrame> corrected)
    {
        return [.. corrected.Select(frame =>
        {
            var sv = new SideViewFrame { Timestamp = frame.Timestamp, FrameNumber = frame.FrameNumber };
            sv.Landmarks.AddRange(frame.Landmarks.Select(lm => new Landmark2D { X = lm.X, Y = lm.Y, Visibility = lm.Visibility }));
            return sv;
        })];
    }
}
