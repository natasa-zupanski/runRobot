using System.Text.Json.Serialization;

namespace runRobot.Models;

public class Landmark
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("z")]
    public double Z { get; set; }

    [JsonPropertyName("visibility")]
    public double Visibility { get; set; }

    public override string ToString()
    {
        return $"({X:F3}, {Y:F3}, {Z:F3}) Visibility: {Visibility:F3}";
    }
}
