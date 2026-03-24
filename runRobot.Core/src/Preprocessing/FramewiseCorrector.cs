using runRobot.Models;

namespace runRobot.Preprocessing;

/// <summary>
/// Base class for corrections that transform each PoseFrame independently.
/// Provides a default <see cref="CorrectAll"/> that maps <see cref="Correct"/>
/// over every frame, and exposes shared landmark constants used by frame-level
/// correctors.
/// </summary>
public abstract class FramewiseCorrector : PoseCorrector
{
    protected const int    LeftHipIndex  = 23;
    protected const int    RightHipIndex = 24;
    protected const double MinVisibility = 0.5;

    /// <summary>Applies this correction to a single frame.</summary>
    public abstract PoseFrame Correct(PoseFrame frame);

    /// <inheritdoc/>
    public override List<PoseFrame> CorrectAll(List<PoseFrame> frames) =>
        [.. frames.Select(Correct)];
}
