using System.Diagnostics;
using System.Text.Json;
using runRobot.Models;

namespace runRobot;

public class PoseAnalyzer
{
    private readonly string _pythonScriptPath;
    private readonly bool _verbose;

    public PoseAnalyzer(string pythonScriptPath, bool verbose = false)
    {
        _pythonScriptPath = pythonScriptPath;
        _verbose = verbose;
    }

    public async Task<List<PoseFrame>> AnalyzeVideoAsync(string videoPath, int? maxFrames = null)
    {
        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException($"Video file not found: {videoPath}");
        }

        if (!File.Exists(_pythonScriptPath))
        {
            throw new FileNotFoundException($"Python script not found: {_pythonScriptPath}");
        }

        // Derive model path from the script location: scripts/ → ../ → MLModels/
        string modelPath = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(_pythonScriptPath)!, "..", "MLModels", "pose_landmarker_heavy.task"));

        var args = $"\"{_pythonScriptPath}\" \"{videoPath}\"";
        if (maxFrames.HasValue)
        {
            args += $" {maxFrames}";
        }
        args += $" --model-path \"{modelPath}\"";

        try
        {
            var process = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(process))
            {
                if (proc == null)
                    throw new InvalidOperationException("Failed to start Python process");

                var output = await proc.StandardOutput.ReadToEndAsync();
                var error = await proc.StandardError.ReadToEndAsync();

                await proc.WaitForExitAsync();

                if (proc.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Python script failed: {error}");
                }

                if (_verbose)
                {
                    Console.WriteLine($"[DEBUG] Python output length: {output.Length} bytes");
                }

                return JsonSerializer.Deserialize<List<PoseFrame>>(output)
                    ?? throw new InvalidOperationException("Failed to deserialize pose data");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error running pose analysis: {ex.Message}", ex);
        }
    }

    public void PrintPoseSummary(List<PoseFrame> frames)
    {
        Console.WriteLine($"\n=== Pose Analysis Summary ===");
        Console.WriteLine($"Total frames analyzed: {frames.Count}");

        if (frames.Count > 0)
        {
            Console.WriteLine($"First frame landmarks: {frames[0].Landmarks.Count}");
            var allVis = frames.SelectMany(f => f.Landmarks.Select(l => l.Visibility));
            if (allVis.Any())
            {
                Console.WriteLine($"Average landmark visibility: {allVis.Average():F3}");
            }
            else
            {
                Console.WriteLine("Average landmark visibility: N/A (no landmarks detected)");
            }
        }
    }
}
