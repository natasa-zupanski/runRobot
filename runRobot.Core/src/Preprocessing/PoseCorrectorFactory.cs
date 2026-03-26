using runRobot.Models;

namespace runRobot.Preprocessing;

/// <summary>
/// Central registry for preprocessing steps: maps each <see cref="PoseCorrectorStep"/>
/// to its display label and its <see cref="PoseCorrector"/> implementation, and defines
/// the order in which steps must be applied.
/// </summary>
public static class PoseCorrectorFactory
{
    /// <summary>
    /// The order in which active preprocessing steps are applied. Changing this order
    /// changes pipeline behaviour — visibility gaps should be filled before smoothing,
    /// and smoothing should run before perspective correction.
    /// </summary>
    public static IReadOnlyList<PoseCorrectorStep> Order { get; } =
    [
        PoseCorrectorStep.VisibilityInterpolation,
        PoseCorrectorStep.TemporalSmoothing,
        PoseCorrectorStep.PerspectiveCorrection,
    ];

    private static readonly Dictionary<PoseCorrectorStep, string> Labels = new()
    {
        [PoseCorrectorStep.VisibilityInterpolation] = "Visibility interpolation",
        [PoseCorrectorStep.TemporalSmoothing]       = "Temporal smoothing",
        [PoseCorrectorStep.PerspectiveCorrection]   = "Perspective correction",
    };

    // Each factory function receives the current frame list and the aspect ratio.
    // Simple correctors ignore both; PerspectiveDistortionCorrector uses them to calibrate.
    private static readonly Dictionary<PoseCorrectorStep, Func<List<PoseFrame>, double, PoseCorrector>> Factories = new()
    {
        [PoseCorrectorStep.VisibilityInterpolation] = (_, _)       => new VisibilityInterpolator(),
        [PoseCorrectorStep.TemporalSmoothing]       = (_, _)       => new TemporalSmoothingCorrector(),
        [PoseCorrectorStep.PerspectiveCorrection]   = (frames, ar) => PerspectiveDistortionCorrector.CalibrateFrom(frames, ar),
    };

    /// <summary>Returns the UI display label for a preprocessing step.</summary>
    public static string GetLabel(PoseCorrectorStep step) => Labels[step];

    /// <summary>
    /// Returns a fully configured <see cref="PoseCorrector"/> for the given step.
    /// Steps that require calibration (e.g. perspective correction) use
    /// <paramref name="frames"/> and <paramref name="aspectRatio"/> to do so.
    /// </summary>
    public static PoseCorrector GetCorrector(PoseCorrectorStep step, List<PoseFrame> frames, double aspectRatio)
        => Factories[step](frames, aspectRatio);
}
