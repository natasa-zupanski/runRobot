using runRobot.Models;

namespace runRobot.Preprocessing;

/// <summary>
/// Central registry for preprocessing steps: maps each <see cref="PoseCorrectorType"/>
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
    public static IReadOnlyList<PoseCorrectorType> Order { get; } =
    [
        PoseCorrectorType.VisibilityInterpolation,
        PoseCorrectorType.TemporalSmoothing,
        PoseCorrectorType.PerspectiveCorrection,
    ];

    private static readonly Dictionary<PoseCorrectorType, string> Labels = new()
    {
        [PoseCorrectorType.VisibilityInterpolation] = "Visibility interpolation",
        [PoseCorrectorType.TemporalSmoothing]       = "Temporal smoothing",
        [PoseCorrectorType.PerspectiveCorrection]   = "Perspective correction",
    };

    // Each factory function receives the current frame list and the aspect ratio.
    // Simple correctors ignore both; PerspectiveDistortionCorrector uses them to calibrate.
    private static readonly Dictionary<PoseCorrectorType, Func<List<PoseFrame>, double, PoseCorrector>> Factories = new()
    {
        [PoseCorrectorType.VisibilityInterpolation] = (_, _)       => new VisibilityInterpolator(),
        [PoseCorrectorType.TemporalSmoothing]       = (_, _)       => new TemporalSmoothingCorrector(),
        [PoseCorrectorType.PerspectiveCorrection]   = (frames, ar) => PerspectiveDistortionCorrector.CalibrateFrom(frames, ar),
    };

    /// <summary>Returns the UI display label for a preprocessing step.</summary>
    public static string GetLabel(PoseCorrectorType step) => Labels[step];

    /// <summary>
    /// Returns a fully configured <see cref="PoseCorrector"/> for the given step.
    /// Steps that require calibration (e.g. perspective correction) use
    /// <paramref name="frames"/> and <paramref name="aspectRatio"/> to do so.
    /// </summary>
    public static PoseCorrector GetCorrector(PoseCorrectorType step, List<PoseFrame> frames, double aspectRatio)
        => Factories[step](frames, aspectRatio);
}
