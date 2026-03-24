using runRobot.Models;
using runRobot.Preprocessing;
using runRobot.Speed;

namespace runRobot.Estimators;

/// <summary>
/// Holds the full output of a SpeedEstimator run, including intermediate results
/// from each pipeline stage.
/// </summary>
public class SpeedEstimationResult
{
    /// <summary>Estimated speed in projected world-space units per second.</summary>
    public double Speed { get; init; }

    /// <summary>Per-frame speed estimates. Null for frames outside any stance phase.</summary>
    public List<double?> SpeedPerFrame { get; init; } = [];

    /// <summary>Which projection strategy was selected for this sequence.</summary>
    public YawCorrectionMethod YawCorrectionMethod { get; init; }

    /// <summary>Projected 2D frames produced by the projection stage.</summary>
    public List<SideViewFrame> ProjectedFrames { get; init; } = new();

    /// <summary>Per-frame landmark velocities produced by the gait analyzer.</summary>
    public List<FrameVelocity> Velocities { get; init; } = new();

    /// <summary>
    /// Meters per world unit, derived from the provided hip height.
    /// Null when no hip height was supplied.
    /// </summary>
    public double? ScaleFactor { get; init; }
}

/// <summary>
/// End-to-end pipeline that takes raw PoseFrames and returns an estimated speed.
///
/// Stages:
///   1. PreProcessor   — projects 3D landmarks into 2D world-space coordinates
///   2. GaitVelocityAnalyzer — computes per-landmark velocities across the sequence
///   3. TreadmillSpeedEstimator — derives belt speed from foot velocity during stance
///
/// Intermediate results are available on the returned SpeedEstimationResult if
/// further analysis is needed.
/// </summary>
public class SpeedEstimator
{
    private readonly PreProcessor _projector;
    private readonly GaitVelocityAnalyzer _velocityAnalyzer;
    private readonly TreadmillSpeedEstimator _speedEstimator;

    /// <summary>
    /// Creates a SpeedEstimator with default settings for all pipeline stages.
    /// </summary>
    /// <param name="projectionMethod">
    ///     Controls yaw correction strategy. Median (default) estimates a single
    ///     global yaw from all frames. PerFrame estimates yaw independently per
    ///     frame. NoYaw skips yaw correction entirely.
    /// </param>
    public SpeedEstimator(YawCorrectionMethod projectionMethod = YawCorrectionMethod.Median, double stanceHeightTolerance = 0.05, bool usePerspectiveCorrection = true, bool useTemporalSmoothing = true, bool useVisibilityInterpolation = true)
        : this(
            new PreProcessor(projectionMethod, usePerspectiveCorrection, useTemporalSmoothing, useVisibilityInterpolation),
            new GaitVelocityAnalyzer(),
            new TreadmillSpeedEstimator { StanceYTolerance = stanceHeightTolerance })
    { }

    /// <summary>
    /// Creates a SpeedEstimator with pre-configured pipeline components.
    /// Use this constructor to tune individual stage parameters.
    /// </summary>
    public SpeedEstimator(
        PreProcessor projector,
        GaitVelocityAnalyzer velocityAnalyzer,
        TreadmillSpeedEstimator speedEstimator)
    {
        _projector        = projector;
        _velocityAnalyzer = velocityAnalyzer;
        _speedEstimator   = speedEstimator;
    }

    /// <summary>
    /// Runs the full pipeline and returns the estimated speed along with all
    /// intermediate results.
    /// </summary>
    /// <param name="frames">Raw pose frames from MediaPipe.</param>
    /// <param name="hipHeightMeters">
    ///     Real-world hip height in metres. When provided, a <see cref="SpeedEstimationResult.ScaleFactor"/>
    ///     is computed so callers can convert world-unit speeds to m/s or mph.
    /// </param>
    /// <param name="useSegmentLengths">
    ///     When true, the scale factor is derived from the sum of the hip-to-knee
    ///     and knee-to-heel segment lengths rather than the raw vertical hip-to-heel
    ///     distance. This is pose-independent and more robust on videos where the
    ///     leg is never fully extended.
    /// </param>
    public SpeedEstimationResult Estimate(List<PoseFrame> frames, double? hipHeightMeters = null,
        bool useSegmentLengths = true, double aspectRatio = 1.0)
    {
        var projected      = _projector.Project(frames, aspectRatio);
        var velocities     = _velocityAnalyzer.Compute(projected);
        var speed          = _speedEstimator.EstimateSpeed(projected, velocities);
        var speedPerFrame  = _speedEstimator.EstimateSpeedPerFrame(projected, velocities);
        var scaleFactor    = hipHeightMeters.HasValue
                                 ? (useSegmentLengths
                                     ? ComputeScaleFactorFromSegments(projected, hipHeightMeters.Value)
                                     : ComputeScaleFactor(projected, hipHeightMeters.Value))
                                 : null;

        return new SpeedEstimationResult
        {
            Speed            = speed,
            SpeedPerFrame    = speedPerFrame,
            YawCorrectionMethod = _projector.MethodUsed,
            ProjectedFrames  = projected,
            Velocities       = velocities,
            ScaleFactor      = scaleFactor
        };
    }

    // Derives a metres-per-world-unit scale factor from the median hip-to-heel
    // distance across all frames.  In SideViewFrame coordinates the hip is the
    // origin and world Y is positive upward, so heel Y values are negative; we
    // negate them to get the (positive) hip-to-heel distance in world units.
    private static double? ComputeScaleFactor(List<SideViewFrame> projected, double hipHeightMeters)
    {
        const int LeftHeelIndex  = 29;
        const int RightHeelIndex = 30;

        var distances = projected
            .SelectMany(f => new[] { LeftHeelIndex, RightHeelIndex }
                .Where(i => i < f.Landmarks.Count && f.Landmarks[i].Visibility > 0.5)
                .Select(i => -f.Landmarks[i].Y))
            .Where(d => d > 0)
            .OrderBy(d => d)
            .ToList();

        if (distances.Count == 0) return null;

        int    mid    = distances.Count / 2;
        double median = distances.Count % 2 == 0
            ? (distances[mid - 1] + distances[mid]) / 2.0
            : distances[mid];

        return median > 0 ? hipHeightMeters / median : null;
    }

    // Derives a metres-per-world-unit scale factor by summing the hip-to-knee and
    // knee-to-heel segment lengths for each leg in each frame, then taking the median.
    // Because it measures actual skeleton segments rather than a vertical projection,
    // the result is consistent regardless of whether the leg is extended or flexed.
    private static double? ComputeScaleFactorFromSegments(List<SideViewFrame> projected, double hipHeightMeters)
    {
        const int LeftHipIndex   = 23;
        const int RightHipIndex  = 24;
        const int LeftKneeIndex  = 25;
        const int RightKneeIndex = 26;
        const int LeftHeelIndex  = 29;
        const int RightHeelIndex = 30;

        var legLengths = new List<double>();

        foreach (var frame in projected)
        {
            foreach (var (hipIdx, kneeIdx, heelIdx) in new[]
            {
                (LeftHipIndex,  LeftKneeIndex,  LeftHeelIndex),
                (RightHipIndex, RightKneeIndex, RightHeelIndex),
            })
            {
                if (hipIdx  >= frame.Landmarks.Count || frame.Landmarks[hipIdx].Visibility  <= 0.5) continue;
                if (kneeIdx >= frame.Landmarks.Count || frame.Landmarks[kneeIdx].Visibility <= 0.5) continue;
                if (heelIdx >= frame.Landmarks.Count || frame.Landmarks[heelIdx].Visibility <= 0.5) continue;

                var hip  = frame.Landmarks[hipIdx];
                var knee = frame.Landmarks[kneeIdx];
                var heel = frame.Landmarks[heelIdx];

                double hipToKnee  = Math.Sqrt(Math.Pow(knee.X - hip.X,  2) + Math.Pow(knee.Y - hip.Y,  2));
                double kneeToHeel = Math.Sqrt(Math.Pow(heel.X - knee.X, 2) + Math.Pow(heel.Y - knee.Y, 2));
                legLengths.Add(hipToKnee + kneeToHeel);
            }
        }

        if (legLengths.Count == 0) return null;

        legLengths.Sort();
        int    mid    = legLengths.Count / 2;
        double median = legLengths.Count % 2 == 0
            ? (legLengths[mid - 1] + legLengths[mid]) / 2.0
            : legLengths[mid];

        return median > 0 ? hipHeightMeters / median : null;
    }
}
