using System.Text.Json.Serialization;

namespace runRobot.Models;

public class PoseFrame
{
    [JsonPropertyName("timestamp")]
    public int Timestamp { get; set; }

    [JsonPropertyName("frame_number")]
    public int FrameNumber { get; set; }

    [JsonPropertyName("landmarks")]
    public List<Landmark> Landmarks { get; set; } = new();

    private static readonly string[] LandmarkNames =
    [
        "Nose", "Left Eye (Inner)", "Left Eye", "Left Eye (Outer)", "Right Eye (Inner)",
        "Right Eye", "Right Eye (Outer)", "Left Ear", "Right Ear", "Mouth (Left)",
        "Mouth (Right)", "Left Shoulder", "Right Shoulder", "Left Elbow", "Right Elbow",
        "Left Wrist", "Right Wrist", "Left Pinky", "Right Pinky", "Left Index",
        "Right Index", "Left Thumb", "Right Thumb", "Left Hip", "Right Hip",
        "Left Knee", "Right Knee", "Left Ankle", "Right Ankle", "Left Heel",
        "Right Heel", "Left Foot Index", "Right Foot Index"
    ];

    public void PrintLandmarks()
    {
        Console.WriteLine($"\n--- Frame {FrameNumber} (Timestamp: {Timestamp}ms) ---");
        for (int i = 0; i < Landmarks.Count && i < LandmarkNames.Length; i++)
        {
            Console.WriteLine($"{i:D2}. {LandmarkNames[i]}: {Landmarks[i]}");
        }
    }

    public Landmark? GetLandmarkByName(string name)
    {
        int index = System.Array.IndexOf(LandmarkNames, name);
        return index >= 0 && index < Landmarks.Count ? Landmarks[index] : null;
    }
}
