namespace runRobot.Models;

public class SettingsPreset
{
    public string Name { get; set; } = "";
    public int? MaxFrames { get; set; }
    public double StepThreshold { get; set; } = 0;
    public double StanceTolerance { get; set; } = 5;  // stored as % (e.g. 5 means 5%)
    public string YawCorrectionMethod { get; set; } = "Median";
    public bool PerspectiveCorrection { get; set; } = true;
    public bool TemporalSmoothing { get; set; } = true;
    public bool VisibilityInterpolation { get; set; } = true;
    public bool DebugSteps { get; set; } = false;
}
