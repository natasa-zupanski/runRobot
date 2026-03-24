using runRobot.Models;

namespace runRobot.Preprocessing;

public enum YawCorrectionMethod
{
    /// <summary>No yaw correction — perspective distortion corrected only.</summary>
    NoYaw,

    /// <summary>Per-frame yaw estimation and correction.</summary>
    PerFrame,

    /// <summary>Median yaw estimation across all frames.</summary>
    Median
}

/// <summary>
/// Second stage of the projection pipeline. Takes perspective-corrected world-space
/// PoseFrames (output of PerspectiveDistortionCorrector) and applies a full XZ yaw
/// rotation to align the coordinate frame with the runner's direction of travel.
///
/// Without yaw correction, a camera placed slightly off-axis attenuates the foot's
/// X velocity by cos(θ), underestimating belt speed. Yaw θ is estimated from the
/// median hip-pair orientation across all frames and applied as a rotation around Y:
///
///   correctedX = worldX · cos(θ) + worldZ · sin(θ)
///   correctedZ = −worldX · sin(θ) + worldZ · cos(θ)
///
/// After correction, X is aligned with the runner's travel direction and Z is the
/// perpendicular-to-travel depth component.
/// </summary>
public class YawCorrector(double yawAngleRadians = 0.0) : FramewiseCorrector
{
    /// <summary>
    /// Estimated yaw offset in radians between the camera X axis and the runner's
    /// direction of travel. Zero = perfectly side-on. Positive = runner travels
    /// slightly toward the camera as they move in the +X direction.
    /// </summary>
    public double YawAngleRadians { get; private set; } = yawAngleRadians;

    /// <summary>
    /// Estimates yaw from a sequence of perspective-corrected world-coord frames
    /// and returns a corrector ready to use.
    /// </summary>
    public static YawCorrector EstimateFrom(List<PoseFrame> worldFrames) =>
        new(EstimateYaw(worldFrames));

    /// <summary>
    /// Applies yaw rotation to a single world-coord frame, returning a new PoseFrame
    /// whose X is aligned with the runner's travel direction and Z is the
    /// perpendicular-to-travel depth component.
    /// </summary>
    public override PoseFrame Correct(PoseFrame worldFrame)
    {
        double cosYaw = Math.Cos(YawAngleRadians);
        double sinYaw = Math.Sin(YawAngleRadians);

        var result = new PoseFrame
        {
            Timestamp   = worldFrame.Timestamp,
            FrameNumber = worldFrame.FrameNumber
        };

        result.Landmarks.AddRange(worldFrame.Landmarks.Select(lm => new Landmark
        {
            X          =  lm.X * cosYaw + lm.Z * sinYaw,
            Y          =  lm.Y,
            Z          = -lm.X * sinYaw + lm.Z * cosYaw,
            Visibility =  lm.Visibility
        }));

        return result;
    }

    /// <summary>
    /// Applies a per-frame yaw estimate to each frame independently rather than a
    /// global median. Frames where hip visibility is insufficient fall back to
    /// the instance's <see cref="YawAngleRadians"/>.
    /// Captures gradual camera drift or genuine stride-to-stride trunk rotation,
    /// but is noisier than <see cref="CorrectAll"/> for a fixed camera.
    /// </summary>
    public List<PoseFrame> CorrectAllPerFrame(List<PoseFrame> worldFrames) =>
        [.. worldFrames.Select(frame =>
        {
            double yaw = YawForFrame(frame) ?? YawAngleRadians;
            return new YawCorrector(yaw).Correct(frame);
        })];

    /// <summary>
    /// Returns the estimated yaw angle in radians for each frame individually.
    /// Null when hip landmarks are not visible enough to compute yaw for that frame.
    /// Useful for visualising per-stride trunk rotation or diagnosing camera placement.
    /// </summary>
    public static List<double?> EstimateYawPerFrame(List<PoseFrame> worldFrames) =>
        [.. worldFrames.Select(YawForFrame)];

    // Estimates a single global yaw as the median of all per-frame estimates.
    private static double EstimateYaw(List<PoseFrame> worldFrames)
    {
        var yawAngles = worldFrames
            .Select(YawForFrame)
            .Where(y => y.HasValue)
            .Select(y => y!.Value)
            .ToList();

        if (yawAngles.Count == 0) return 0.0;

        yawAngles.Sort();
        int mid = yawAngles.Count / 2;
        return yawAngles.Count % 2 == 0
            ? (yawAngles[mid - 1] + yawAngles[mid]) / 2.0
            : yawAngles[mid];
    }

    // Computes yaw for a single frame from the hip-pair orientation in world space.
    //
    // For the hip pair (left minus right):
    //   dx = hipL.X - hipR.X  ≈ -hip_width · sin(θ)
    //   dz = hipL.Z - hipR.Z  ≈  hip_width · cos(θ)
    //
    // The hip vector makes angle phi = atan2(dz, dx) with the X axis. Travel
    // direction is perpendicular: theta = phi - pi/2. Normalised to [-pi/2, pi/2].
    private static double? YawForFrame(PoseFrame frame)
    {
        if (frame.Landmarks.Count <= RightHipIndex) return null;

        var hipL = frame.Landmarks[LeftHipIndex];
        var hipR = frame.Landmarks[RightHipIndex];

        if (hipL.Visibility < MinVisibility || hipR.Visibility < MinVisibility) return null;

        double theta = Math.Atan2(hipL.Z - hipR.Z, hipL.X - hipR.X) - Math.PI / 2;

        while (theta >  Math.PI / 2) theta -= Math.PI;
        while (theta < -Math.PI / 2) theta += Math.PI;

        return theta;
    }
}
