namespace runRobot.Models;

public class UserProfile
{
    public string Name { get; set; } = "";
    public double? HipHeight { get; set; }
    public string HipHeightUnit { get; set; } = "cm";
    public double? Weight { get; set; }
    public string WeightUnit { get; set; } = "kg";
}
