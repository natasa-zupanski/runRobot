using runRobot.Models;

namespace runRobot.Estimators;

public class StepEstimator
{
    public int EstimateSteps(List<PoseFrame> frames, bool debug = false, double threshold = 0,
                              TextWriter? debugOutput = null)
    {
        if (frames.Count < 2) return 0;

        // Landmarks: 27 = Left Ankle, 28 = Right Ankle
        const int leftAnkleIndex = 27;
        const int rightAnkleIndex = 28;
        var timestamps = new List<int>();

        int steps = 0;
        var log = debug ? (debugOutput ?? Console.Out) : null;

        log?.WriteLine("\n[StepEstimator DEBUG] deltaX (leftX-rightX) per frame:");

        int startIndex = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            PoseFrame frame = frames[i];
            if (frame.Landmarks.Count > Math.Max(leftAnkleIndex, rightAnkleIndex))
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex >= frames.Count - 2) return 0;

        PoseFrame startFrame = frames[startIndex];
        double prevDiff = startFrame.Landmarks[leftAnkleIndex].X - startFrame.Landmarks[rightAnkleIndex].X;
        int prevSign = Math.Sign(prevDiff);
        timestamps.Add(startFrame.Timestamp);
        log?.WriteLine($"  t={timestamps[timestamps.Count - 1]}ms delta={prevDiff:F4} sign={prevSign}");

        for (int i = startIndex + 1; i < frames.Count; i++)
        {
            PoseFrame frame = frames[i];

            if (frame.Landmarks.Count <= Math.Max(leftAnkleIndex, rightAnkleIndex))
            {
                continue;
            }

            timestamps.Add(frame.Timestamp);

            double currDiff = frame.Landmarks[leftAnkleIndex].X - frame.Landmarks[rightAnkleIndex].X;
            int currSign = Math.Sign(currDiff);

            log?.WriteLine($"  t={timestamps[timestamps.Count - 1]}ms delta={currDiff:F6} sign={currSign}");

            if (currSign != prevSign && Math.Abs(currDiff) > threshold)
            {
                steps++;
                log?.WriteLine($"  → step detected at t={frame.Timestamp}ms (delta={currDiff:F6}) distance={Math.Abs(currDiff-prevDiff):F6}");
            }

            prevDiff = currDiff;
            prevSign = currSign;
        }

        log?.WriteLine($"[StepEstimator DEBUG] Estimated steps: {steps}\n");

        return steps;
    }
}
