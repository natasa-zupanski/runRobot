using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace runRobot.Estimators;

/// <summary>
/// Result of a calorie estimation run.
/// </summary>
public record CalorieEstimationResult
{
    /// <summary>Total kilocalories burned over the session.</summary>
    public double Calories { get; init; }

    /// <summary>Average MET value across all frames, weighted by frame duration.</summary>
    public double AverageMet { get; init; }

    /// <summary>Total duration of the session in minutes.</summary>
    public double DurationMinutes { get; init; }

    /// <summary>Average speed in mph across all frames.</summary>
    public double AverageSpeedMph { get; init; }

    /// <summary>Fraction of frames classified as running (speed > 4 mph).</summary>
    public double RunningFraction { get; init; }
}

/// <summary>
/// Estimates kilocalories burned from a treadmill session using MET values from
/// the 2024 Adult Compendium of Physical Activities.
///
/// Formula (per frame): kcal = MET(speed) × weight_kg × frame_duration_hours
///
/// MET is looked up per frame by interpolating a speed-sorted table built from
/// the Compendium JSON files. Speeds above 4 mph use the running table; at or
/// below 4 mph use the walking table.
///
/// Input is taken directly from <see cref="SpeedEstimationResult"/>: per-frame
/// speeds (world units/s) are converted to mph via the result's ScaleFactor.
/// Frames with no stance-phase speed carry forward the last known speed.
/// </summary>
public class CalorieEstimator
{
    private const double RunningThresholdMph = 4.0;

    private readonly List<MetEntry> _walkingTable;
    private readonly List<MetEntry> _runningTable;

    private record MetEntry(double SpeedMph, double Met, string Description);

    private CalorieEstimator(List<MetEntry> walkingTable, List<MetEntry> runningTable)
    {
        _walkingTable = walkingTable;
        _runningTable = runningTable;
    }

    /// <summary>
    /// Loads MET data from the Compendium JSON files in the application's MET/ directory.
    /// </summary>
    /// <param name="assetDirectory">
    ///     Path to the directory containing walking_met.json and running_met.json.
    ///     Defaults to the MET/ subfolder of the application's base directory.
    /// </param>
    public static CalorieEstimator LoadFromAssets(string? assetDirectory = null)
    {
        string dir     = assetDirectory ?? Path.Combine(AppContext.BaseDirectory, "MET");
        var walking    = BuildTable(Path.Combine(dir, "walking_met.json"));
        var running    = BuildTable(Path.Combine(dir, "running_met.json"));
        return new CalorieEstimator(walking, running);
    }

    /// <summary>
    /// Estimates total kilocalories burned using per-frame speed data from the pipeline.
    ///
    /// Per-frame calories: kcal = MET(speed_mph) × weight_kg × frame_duration_hours
    /// Total calories: sum of all per-frame contributions.
    ///
    /// Frames with null speed (outside stance phase) carry forward the most recently
    /// observed speed so the full session duration contributes to the estimate.
    /// </summary>
    /// <param name="speedResult">
    ///     The output of <see cref="SpeedEstimator.Estimate"/>. Provides per-frame
    ///     speeds, projected frames (for timestamps), and the scale factor used to
    ///     convert world-unit speeds to m/s.
    /// </param>
    /// <param name="weightKg">Body mass in kilograms.</param>
    public CalorieEstimationResult Estimate(SpeedEstimationResult speedResult, double weightKg)
    {
        var perFrame   = speedResult.SpeedPerFrame;
        var frames     = speedResult.ProjectedFrames;
        double? scale  = speedResult.ScaleFactor;   // metres per world unit

        if (frames.Count == 0 || perFrame.Count == 0)
            return new CalorieEstimationResult();

        double totalCalories     = 0;
        double totalMetSeconds   = 0;
        double totalSpeedSeconds = 0;
        double runningSeconds    = 0;
        double totalSeconds      = 0;

        double? lastKnownSpeed = null;  // world units/s, carried forward across null frames

        int count = Math.Min(perFrame.Count, frames.Count);

        for (int i = 0; i < count; i++)
        {
            // Frame duration in seconds from adjacent timestamps (ms → s).
            double dtSeconds;
            if (i + 1 < frames.Count)
                dtSeconds = (frames[i + 1].Timestamp - frames[i].Timestamp) / 1000.0;
            else if (i > 0)
                dtSeconds = (frames[i].Timestamp - frames[i - 1].Timestamp) / 1000.0;
            else
                dtSeconds = 0;

            if (dtSeconds <= 0) continue;

            double? worldSpeed = perFrame[i] ?? lastKnownSpeed;
            if (worldSpeed is null) continue;

            lastKnownSpeed = perFrame[i] ?? lastKnownSpeed;

            // Convert world units/s → m/s → mph.
            double mps    = scale.HasValue ? worldSpeed.Value * scale.Value : worldSpeed.Value;
            double mph    = mps * 2.23694;

            double met    = LookupMet(mph);
            double dtHrs  = dtSeconds / 3600.0;

            totalCalories     += met * weightKg * dtHrs;
            totalMetSeconds   += met * dtSeconds;
            totalSpeedSeconds += mph * dtSeconds;
            totalSeconds      += dtSeconds;

            if (mph > RunningThresholdMph)
                runningSeconds += dtSeconds;
        }

        if (totalSeconds == 0)
            return new CalorieEstimationResult();

        return new CalorieEstimationResult
        {
            Calories        = totalCalories,
            AverageMet      = totalMetSeconds   / totalSeconds,
            DurationMinutes = totalSeconds       / 60.0,
            AverageSpeedMph = totalSpeedSeconds  / totalSeconds,
            RunningFraction = runningSeconds     / totalSeconds,
        };
    }

    /// <summary>
    /// Looks up the MET value for a given speed in mph.
    /// Speeds above 4 mph use the running table; at or below use the walking table.
    /// </summary>
    public double LookupMet(double speedMph)
    {
        var table = speedMph > RunningThresholdMph ? _runningTable : _walkingTable;
        double met = Interpolate(table, speedMph);

        // Fallback to the other table if the primary has no coverage.
        if (met == 0)
            met = Interpolate(speedMph > RunningThresholdMph ? _walkingTable : _runningTable, speedMph);

        return met;
    }

    // Linearly interpolates MET from a speed-sorted table.
    // Clamps to the table's min/max speed at the boundaries.
    private static double Interpolate(List<MetEntry> table, double speedMph)
    {
        if (table.Count == 0)        return 0;
        if (speedMph <= table[0].SpeedMph)  return table[0].Met;
        if (speedMph >= table[^1].SpeedMph) return table[^1].Met;

        int hiIdx  = table.FindIndex(e => e.SpeedMph >= speedMph);
        var lo     = table[hiIdx - 1];
        var hi     = table[hiIdx];
        double t   = (speedMph - lo.SpeedMph) / (hi.SpeedMph - lo.SpeedMph);
        return lo.Met + t * (hi.Met - lo.Met);
    }

    // Parses a Compendium JSON file into a speed-sorted MET table.
    // Only entries whose description contains a parseable mph value are included:
    //   "X to Y mph" → midpoint (X+Y)/2
    //   "X mph"      → X
    private static List<MetEntry> BuildTable(string path)
    {
        if (!File.Exists(path)) return [];

        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<MetFileJson>(json);
        if (file is null) return [];

        var rangeRx  = new Regex(@"\b(\d+(?:\.\d+)?)\s+to\s+(\d+(?:\.\d+)?)\s+mph\b",
                                  RegexOptions.IgnoreCase);
        var singleRx = new Regex(@"\b(\d+(?:\.\d+)?)\s+mph\b",
                                  RegexOptions.IgnoreCase);

        var entries = new List<MetEntry>();

        foreach (var activity in file.Activities)
        {
            double speedMph;
            var rangeMatch = rangeRx.Match(activity.Description);

            if (rangeMatch.Success)
            {
                double lo = double.Parse(rangeMatch.Groups[1].Value);
                double hi = double.Parse(rangeMatch.Groups[2].Value);
                speedMph  = (lo + hi) / 2.0;
            }
            else
            {
                var singleMatch = singleRx.Match(activity.Description);
                if (!singleMatch.Success) continue;
                speedMph = double.Parse(singleMatch.Groups[1].Value);
            }

            entries.Add(new MetEntry(speedMph, activity.Met, activity.Description));
        }

        return [.. entries.OrderBy(e => e.SpeedMph)];
    }

    // ── JSON deserialization models ────────────────────────────────────────────

    private sealed class MetFileJson
    {
        [JsonPropertyName("activities")]
        public List<MetActivityJson> Activities { get; init; } = [];
    }

    private sealed class MetActivityJson
    {
        [JsonPropertyName("met")]
        public double Met { get; init; }

        [JsonPropertyName("description")]
        public string Description { get; init; } = "";
    }
}
