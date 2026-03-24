using runRobot.Models;

namespace runRobot.Speed;

/// <summary>
/// Estimates treadmill belt speed from projected landmark velocities.
///
/// On a treadmill the hip stays roughly fixed in screen space, so body velocity
/// cannot be derived from hip position. However, during stance phase the foot is
/// in contact with the belt and moves backward relative to the hip at exactly the
/// belt speed. Since SideViewFrame coordinates are already hip-relative, the foot's
/// velocity magnitude during stance directly gives the belt speed.
///
///   belt_speed ≈ foot.Speed during stance phase
///
/// Using the full speed magnitude (rather than just Vx) correctly handles inclined
/// treadmills, where the belt moves along the slope and the foot has both horizontal
/// and vertical velocity components during stance.
///
/// Stance phase is identified as frames where the foot Y position is near its
/// minimum (i.e. close to the ground).
/// </summary>
public class TreadmillSpeedEstimator
{
    // A foot is considered in stance when its Y is within this fraction of the
    // full Y range above the minimum. Lower = stricter (only deepest contact frames).
    public double StanceYTolerance { get; set; } = 0.05;

// Landmark indices
    private const int LeftHeelIndex   = 29;
    private const int RightHeelIndex  = 30;
    private const int LeftAnkleIndex  = 27;
    private const int RightAnkleIndex = 28;

    /// <summary>
    /// Estimates treadmill speed from the full sequence.
    /// Returns speed in the same units as the projected coordinates (world-space
    /// normalized units per second). Multiply by a known scale factor to get m/s.
    /// </summary>
    public double EstimateSpeed(List<SideViewFrame> frames, List<FrameVelocity> velocities)
    {
        var leftStanceVelocities  = GetStanceVelocities(frames, velocities, leftFoot: true);
        var rightStanceVelocities = GetStanceVelocities(frames, velocities, leftFoot: false);

        var allStanceVelocities = leftStanceVelocities.Concat(rightStanceVelocities).ToList();

        if (allStanceVelocities.Count == 0) return 0;

        // Use median rather than mean to suppress outliers at stance transitions
        return Median(allStanceVelocities);
    }

    /// <summary>
    /// Returns a per-frame stance flag for one foot. True = foot is in stance phase.
    /// Useful for visualisation or downstream gait event detection.
    /// </summary>
    public List<bool> GetStancePhase(List<SideViewFrame> frames, bool leftFoot)
    {
        int heelIndex = leftFoot ? LeftHeelIndex : RightHeelIndex;

        var footYValues = frames
            .Select(frame => heelIndex < frame.Landmarks.Count
                ? frame.Landmarks[heelIndex].Y
                : double.MaxValue)
            .ToList();

        double minY      = footYValues.Min();
        double maxY      = footYValues.Max();
        double threshold = minY + StanceYTolerance * (maxY - minY);

        return footYValues.Select(y => y <= threshold).ToList();
    }

    /// <summary>
    /// Returns the start/end frame indices of every contiguous stance episode for one foot.
    /// Each segment begins and ends at a non-stance boundary, so the frames immediately
    /// before and after each range are flight-phase frames.
    ///
    /// Use this to pre-split a video sequence into per-stride groups before calling
    /// <see cref="EstimateSpeedPerStride"/>, or to drive any per-step analysis.
    /// </summary>
    public List<(int Start, int End)> GetStanceSegments(List<SideViewFrame> frames, bool leftFoot)
    {
        var stanceFlags = GetStancePhase(frames, leftFoot);
        var segments    = new List<(int, int)>();
        int? segStart   = null;

        for (int i = 0; i < stanceFlags.Count; i++)
        {
            if (stanceFlags[i] && segStart == null)
                segStart = i;
            else if (!stanceFlags[i] && segStart != null)
            {
                segments.Add((segStart.Value, i - 1));
                segStart = null;
            }
        }

        // Close a segment that runs to the last frame
        if (segStart != null)
            segments.Add((segStart.Value, stanceFlags.Count - 1));

        return segments;
    }

    /// <summary>
    /// Estimates treadmill speed once per foot-contact episode for one foot.
    /// Returns one speed value per stance segment in chronological order, using the
    /// median of foot velocities within that segment.
    ///
    /// This is the per-stride counterpart of <see cref="EstimateSpeed"/>: instead of
    /// a single median across the whole clip, you get one estimate each time the foot
    /// touches down — useful for detecting speed changes within a run.
    /// </summary>
    public List<(int Start, int End, double Speed)> EstimateSpeedPerStride(
        List<SideViewFrame> frames,
        List<FrameVelocity> velocities,
        bool leftFoot)
    {
        return [.. GetStanceSegments(frames, leftFoot)
            .Select(seg => (seg.Start, seg.End, Speeds: FootSpeedsInRange(velocities, leftFoot, seg.Start, seg.End)))
            .Where(x => x.Speeds.Count > 0)
            .Select(x => (x.Start, x.End, Median(x.Speeds)))];
    }

    /// <summary>
    /// Returns a per-frame speed estimate derived from <see cref="EstimateSpeedPerStride"/>.
    /// Frames within a stance segment are assigned that stride's speed. Frames covered
    /// by both feet simultaneously (walking double-support) are assigned the average of
    /// the two strides' speeds. Frames outside any stance segment are null.
    /// </summary>
    public List<double?> EstimateSpeedPerFrame(
        List<SideViewFrame> frames,
        List<FrameVelocity> velocities)
    {
        var speedByFrame = new double?[frames.Count];

        foreach (var (start, end, speed) in EstimateSpeedPerStride(frames, velocities, leftFoot: true)
                                    .Concat(EstimateSpeedPerStride(frames, velocities, leftFoot: false)))
        {
            for (int i = start; i <= end && i < frames.Count; i++)
                speedByFrame[i] = speedByFrame[i] is { } existing
                    ? (existing + speed) / 2.0
                    : speed;
        }

        return [.. speedByFrame];
    }

    // --- Private helpers ---

    // Returns the flat list of all stance-frame speeds across the whole clip.
    // GetStanceVelocities and EstimateSpeedPerStride both reduce to grouping these
    // values differently: the former flattens all segments, the latter mediates each.
    private List<double> GetStanceVelocities(
        List<SideViewFrame> frames,
        List<FrameVelocity> velocities,
        bool leftFoot)
    {
        return [.. GetStanceSegments(frames, leftFoot)
            .SelectMany(seg => FootSpeedsInRange(velocities, leftFoot, seg.Start, seg.End))];
    }

    // Collects the average heel+ankle speed for each frame in [start, end].
    // Speed magnitude is used (not just Vx) so inclined treadmills are handled
    // correctly — the belt moves along the slope, contributing both horizontal and
    // vertical foot velocity during stance. Frames with no usable landmarks are skipped.
    private static List<double> FootSpeedsInRange(
        List<FrameVelocity> velocities,
        bool leftFoot,
        int start,
        int end)
    {
        int heelIndex  = leftFoot ? LeftHeelIndex  : RightHeelIndex;
        int ankleIndex = leftFoot ? LeftAnkleIndex : RightAnkleIndex;

        List<double> speeds = [];

        for (int i = start; i <= end && i < velocities.Count; i++)
        {
            var    frame        = velocities[i];
            double speed        = 0;
            int    contributors = 0;

            if (heelIndex  < frame.Landmarks.Count) { speed += frame.Landmarks[heelIndex].Speed;  contributors++; }
            if (ankleIndex < frame.Landmarks.Count) { speed += frame.Landmarks[ankleIndex].Speed; contributors++; }

            if (contributors > 0) speeds.Add(speed / contributors);
        }

        return speeds;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int mid    = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }
}
