using runRobot.Models;

namespace runRobot.Preprocessing;

/// <summary>
/// Applies a configurable sequence of preprocessing corrections to raw PoseFrames,
/// then projects them into 2D world-space SideViewFrames.
///
/// The active steps are controlled by the <see cref="PoseCorrectorStep"/> values
/// passed to the constructor. Steps always execute in this fixed order when enabled:
///
///   1. VisibilityInterpolation — fill low-visibility landmark gaps
///   2. TemporalSmoothing       — Gaussian moving average over time
///   3. PerspectiveCorrection   — calibrated unprojection to world coordinates
///   4. Aspect ratio scaling    — always applied when aspectRatio != 1.0
///   5. Yaw correction          — NoYaw / PerFrame / Median
/// </summary>
public class PoseCorrectorPipeline
{
    private readonly YawCorrectionMethod _method;
    private readonly HashSet<PoseCorrectorStep> _steps;

    public YawCorrectionMethod MethodUsed { get; private set; }

    public PoseCorrectorPipeline(YawCorrectionMethod method = YawCorrectionMethod.Median,
                        IEnumerable<PoseCorrectorStep>? steps = null)
    {
        _method = method;
        _steps  = steps is not null
            ? new HashSet<PoseCorrectorStep>(steps)
            : [PoseCorrectorStep.VisibilityInterpolation, PoseCorrectorStep.TemporalSmoothing, PoseCorrectorStep.PerspectiveCorrection];
    }

    /// <summary>
    /// Applies the configured preprocessing steps, then projects to 2D by dropping Z.
    /// Call <see cref="MethodUsed"/> afterwards to confirm the yaw strategy used.
    /// </summary>
    public List<SideViewFrame> Project(List<PoseFrame> frames, double aspectRatio = 1.0)
    {
        var worldFrames = frames;
        foreach (var step in PoseCorrectorFactory.Order.Where(_steps.Contains))
            worldFrames = PoseCorrectorFactory.GetCorrector(step, worldFrames, aspectRatio).CorrectAll(worldFrames);

        if (aspectRatio != 1.0)
            worldFrames = [.. worldFrames.Select(frame =>
            {
                var scaled = new PoseFrame { Timestamp = frame.Timestamp, FrameNumber = frame.FrameNumber };
                scaled.Landmarks.AddRange(frame.Landmarks.Select(lm => new Landmark
                {
                    X = lm.X * aspectRatio, Y = lm.Y, Z = lm.Z, Visibility = lm.Visibility
                }));
                return scaled;
            })];

        MethodUsed = _method;
        var corrected = _method switch
        {
            YawCorrectionMethod.NoYaw    => worldFrames,
            YawCorrectionMethod.PerFrame => new YawCorrector().CorrectAllPerFrame(worldFrames),
            _                            => YawCorrector.EstimateFrom(worldFrames).CorrectAll(worldFrames),
        };

        return [.. corrected.Select(frame =>
        {
            var sv = new SideViewFrame { Timestamp = frame.Timestamp, FrameNumber = frame.FrameNumber };
            sv.Landmarks.AddRange(frame.Landmarks.Select(lm => new Landmark2D { X = lm.X, Y = lm.Y, Visibility = lm.Visibility }));
            return sv;
        })];
    }
}
