using runRobot.Models;

namespace runRobot.Preprocessing;

/// <summary>
/// Base class for all preprocessing corrections applied to a sequence of PoseFrames.
/// Sequence-aware correctors (e.g. temporal smoothing, outlier rejection) derive
/// directly from this class and implement <see cref="CorrectAll"/> only.
/// Frame-by-frame correctors should derive from <see cref="FramewiseCorrector"/> instead.
/// </summary>
public abstract class PoseCorrector
{
    /// <summary>Applies this correction to a sequence of frames.</summary>
    public abstract List<PoseFrame> CorrectAll(List<PoseFrame> frames);
}
