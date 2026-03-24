using runRobot.Models;

namespace runRobot.Api.Models;

/// <summary>
/// Body for POST /analyze.
/// </summary>
public record AnalyzeRequest
{
    /// <summary>Absolute path to the video file on the server.</summary>
    public string VideoPath { get; init; } = "";

    /// <summary>
    /// Video width / height. Defaults to 1.0 (square) if not supplied.
    /// Pass the correct ratio to avoid under-estimating horizontal speed.
    /// </summary>
    public double AspectRatio { get; init; } = 1.0;

    /// <summary>Pipeline settings. Defaults are used when null.</summary>
    public SettingsPreset? Settings { get; init; }

    /// <summary>Runner body metrics used for speed scaling and calorie estimation.</summary>
    public UserProfile? Profile { get; init; }
}
