namespace runRobot.Models;

/// <summary>
/// A landmark in 2D world space, with coordinates relative to the hip midpoint.
/// Units match MediaPipe's normalized scale unless a ScaleFactor is applied.
/// </summary>
public class Landmark2D
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Visibility { get; set; }

    public override string ToString() => $"({X:F3}, {Y:F3}) Visibility: {Visibility:F3}";
}

/// <summary>
/// Pose landmarks projected into 2D world-space coordinates via perspective unprojection.
/// Coordinates are relative to the hip midpoint. Positive X = right in frame,
/// positive Y = up (screen Y is flipped).
/// </summary>
public class SideViewFrame
{
    public int Timestamp { get; set; }
    public int FrameNumber { get; set; }
    public List<Landmark2D> Landmarks { get; set; } = new();
}
