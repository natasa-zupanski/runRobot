using runRobot.Models;

namespace runRobot.Preprocessing;

/// <summary>
/// Applies perspective distortion correction followed by an optional yaw correction
/// strategy, converting raw PoseFrames into 2D world-space SideViewFrames.
///
/// PerspectiveDistortionCorrector always runs first to recover true world-space
/// coordinates. The world frames are then passed to YawCorrector with the
/// chosen yaw strategy:
///
///   NoYaw    — no yaw correction (yaw = 0)
///   PerFrame — yaw estimated and applied independently per frame
///   Median   — a single global yaw estimated as the median across all frames
/// </summary>
public class PreProcessor
{
    private readonly YawCorrectionMethod _method;
    private readonly bool _usePerspectiveCorrection;
    private readonly bool _useTemporalSmoothing;
    private readonly bool _useVisibilityInterpolation;

    public YawCorrectionMethod MethodUsed { get; private set; }

    public PreProcessor(YawCorrectionMethod method = YawCorrectionMethod.Median,
                        bool usePerspectiveCorrection    = true,
                        bool useTemporalSmoothing        = true,
                        bool useVisibilityInterpolation  = true)
    {
        _method                    = method;
        _usePerspectiveCorrection  = usePerspectiveCorrection;
        _useTemporalSmoothing      = useTemporalSmoothing;
        _useVisibilityInterpolation = useVisibilityInterpolation;
    }

    /// <summary>
    /// Applies perspective distortion correction, yaw correction, then projects to 2D
    /// by dropping Z. Call <see cref="MethodUsed"/> afterwards to confirm the strategy.
    /// </summary>
    public List<SideViewFrame> Project(List<PoseFrame> frames, double aspectRatio = 1.0)
    {
        var interpolatedFrames = _useVisibilityInterpolation
            ? new VisibilityInterpolator().CorrectAll(frames)
            : frames;

        var smoothedFrames = _useTemporalSmoothing
            ? new TemporalSmoothingCorrector().CorrectAll(interpolatedFrames)
            : interpolatedFrames;

        var worldFrames = _usePerspectiveCorrection
            ? PerspectiveDistortionCorrector.CalibrateFrom(smoothedFrames, aspectRatio).CorrectAll(smoothedFrames)
            : smoothedFrames;

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
