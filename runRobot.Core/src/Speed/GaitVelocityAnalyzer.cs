using runRobot.Models;

namespace runRobot.Speed;

/// <summary>
/// Velocity of a single landmark between frames, in projected world-space units per second.
/// Coordinates are relative to the hip midpoint (same reference as SideViewFrame).
/// </summary>
public class LandmarkVelocity
{
    /// <summary>Horizontal velocity — positive = forward in frame.</summary>
    public double Vx { get; set; }

    /// <summary>Vertical velocity — positive = upward.</summary>
    public double Vy { get; set; }

    /// <summary>Magnitude of the velocity vector.</summary>
    public double Speed => Math.Sqrt(Vx * Vx + Vy * Vy);

    public override string ToString() => $"Vx={Vx:F3} Vy={Vy:F3} Speed={Speed:F3}";
}

/// <summary>
/// Per-frame velocities for all landmarks, with named accessors for common gait landmarks.
/// </summary>
public class FrameVelocity
{
    public int FrameNumber { get; set; }
    public double TimestampSeconds { get; set; }
    public List<LandmarkVelocity> Landmarks { get; set; } = new();

    // Named accessors using MediaPipe landmark indices
    public LandmarkVelocity? LeftShoulder   => GetLandmark(11);
    public LandmarkVelocity? RightShoulder  => GetLandmark(12);
    public LandmarkVelocity? LeftHip        => GetLandmark(23);
    public LandmarkVelocity? RightHip       => GetLandmark(24);
    public LandmarkVelocity? LeftKnee       => GetLandmark(25);
    public LandmarkVelocity? RightKnee      => GetLandmark(26);
    public LandmarkVelocity? LeftAnkle      => GetLandmark(27);
    public LandmarkVelocity? RightAnkle     => GetLandmark(28);
    public LandmarkVelocity? LeftHeel       => GetLandmark(29);
    public LandmarkVelocity? RightHeel      => GetLandmark(30);
    public LandmarkVelocity? LeftFootIndex  => GetLandmark(31);
    public LandmarkVelocity? RightFootIndex => GetLandmark(32);

    private LandmarkVelocity? GetLandmark(int index) =>
        index < Landmarks.Count ? Landmarks[index] : null;
}

/// <summary>
/// Computes per-landmark velocities across a sequence of projected SideViewFrames.
///
/// All velocities are in projected world-space units per second, relative to the
/// hip midpoint. This means foot velocity represents how fast the foot is moving
/// relative to the body — a key gait metric. At heel strike, foot speed drops
/// toward zero; peak speed occurs mid-swing.
///
/// Interior frames use central differences (more accurate). Edge frames fall back
/// to one-sided differences.
/// </summary>
public class GaitVelocityAnalyzer
{
    /// <summary>
    /// Computes velocities for every landmark in every frame.
    /// Returns one FrameVelocity per input frame, in the same order.
    /// </summary>
    public List<FrameVelocity> Compute(List<SideViewFrame> frames)
    {
        if (frames.Count == 0) return new List<FrameVelocity>();
        if (frames.Count == 1) return new List<FrameVelocity> { ZeroVelocityFrame(frames[0]) };

        var result = new List<FrameVelocity>(frames.Count);

        for (int i = 0; i < frames.Count; i++)
        {
            // Central difference for interior frames, one-sided at edges
            int prev = Math.Max(0, i - 1);
            int next = Math.Min(frames.Count - 1, i + 1);

            double dt = TimeDeltaSeconds(frames[prev], frames[next]);
            var frameVelocity = new FrameVelocity
            {
                FrameNumber      = frames[i].FrameNumber,
                TimestampSeconds = frames[i].Timestamp / 1000.0
            };

            int landmarkCount = Math.Min(frames[prev].Landmarks.Count, frames[next].Landmarks.Count);
            for (int j = 0; j < landmarkCount; j++)
            {
                var landmarkPrev = frames[prev].Landmarks[j];
                var landmarkNext = frames[next].Landmarks[j];

                frameVelocity.Landmarks.Add(dt > 0
                    ? new LandmarkVelocity
                    {
                        Vx = (landmarkNext.X - landmarkPrev.X) / dt,
                        Vy = (landmarkNext.Y - landmarkPrev.Y) / dt
                    }
                    : new LandmarkVelocity { Vx = 0, Vy = 0 });
            }

            result.Add(frameVelocity);
        }

        return result;
    }

    /// <summary>
    /// Returns just the speed (magnitude) of a specific landmark across all frames.
    /// Useful for plotting a single landmark's motion over time.
    /// </summary>
    public List<double> GetLandmarkSpeed(List<SideViewFrame> frames, int landmarkIndex)
    {
        return Compute(frames)
            .Select(frame => landmarkIndex < frame.Landmarks.Count
                ? frame.Landmarks[landmarkIndex].Speed
                : 0.0)
            .ToList();
    }

    /// <summary>
    /// Returns the foot speed (average of heel and ankle) for one foot across all frames.
    /// Near-zero values indicate the foot is on the ground (heel strike / stance phase).
    /// </summary>
    public List<double> GetFootSpeed(List<SideViewFrame> frames, bool leftFoot)
    {
        int heelIndex  = leftFoot ? 29 : 30;
        int ankleIndex = leftFoot ? 27 : 28;

        return Compute(frames).Select(frame =>
        {
            double heelSpeed  = heelIndex  < frame.Landmarks.Count ? frame.Landmarks[heelIndex].Speed  : 0;
            double ankleSpeed = ankleIndex < frame.Landmarks.Count ? frame.Landmarks[ankleIndex].Speed : 0;
            return (heelSpeed + ankleSpeed) / 2.0;
        }).ToList();
    }

    private static double TimeDeltaSeconds(SideViewFrame a, SideViewFrame b) =>
        (b.Timestamp - a.Timestamp) / 1000.0;

    private static FrameVelocity ZeroVelocityFrame(SideViewFrame frame) =>
        new FrameVelocity
        {
            FrameNumber      = frame.FrameNumber,
            TimestampSeconds = frame.Timestamp / 1000.0,
            Landmarks        = frame.Landmarks.Select(_ => new LandmarkVelocity()).ToList()
        };
}
