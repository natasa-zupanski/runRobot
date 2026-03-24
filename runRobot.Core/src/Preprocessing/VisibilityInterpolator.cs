using runRobot.Models;

namespace runRobot.Preprocessing;

/// <summary>
/// Fills low-visibility landmark gaps with linearly interpolated positions.
/// For each landmark, spans of frames where visibility falls below
/// <paramref name="minVisibility"/> are identified and bridged by lerping
/// between the last visible frame before the gap and the first visible frame
/// after it. Gaps at the start or end of the sequence (no valid endpoint on
/// one side) are left unchanged rather than extrapolated.
///
/// Should run before temporal smoothing so the smoother operates on complete
/// trajectories rather than spreading bad low-visibility estimates.
/// </summary>
public class VisibilityInterpolator(double minVisibility = 0.5) : PoseCorrector
{
    public override List<PoseFrame> CorrectAll(List<PoseFrame> frames)
    {
        if (frames.Count == 0) return frames;

        // Deep-copy all frames so input is not mutated.
        List<PoseFrame> result = [.. frames.Select(f =>
        {
            var copy = new PoseFrame { Timestamp = f.Timestamp, FrameNumber = f.FrameNumber };
            copy.Landmarks.AddRange(f.Landmarks.Select(lm =>
                new Landmark { X = lm.X, Y = lm.Y, Z = lm.Z, Visibility = lm.Visibility }));
            return copy;
        })];

        int landmarkCount = result.Max(f => f.Landmarks.Count);

        for (int j = 0; j < landmarkCount; j++)
        {
            int i = 0;
            while (i < result.Count)
            {
                // Advance past visible frames.
                if (j >= result[i].Landmarks.Count || result[i].Landmarks[j].Visibility >= minVisibility)
                {
                    i++;
                    continue;
                }

                // Found the start of a low-visibility gap.
                int gapStart = i;
                while (i < result.Count &&
                       (j >= result[i].Landmarks.Count || result[i].Landmarks[j].Visibility < minVisibility))
                    i++;
                int gapEnd = i; // index of first visible frame after the gap (or Count if none)

                // Skip gaps with no valid endpoint on either side — don't extrapolate.
                bool hasBefore = gapStart > 0 && j < result[gapStart - 1].Landmarks.Count;
                bool hasAfter  = gapEnd < result.Count && j < result[gapEnd].Landmarks.Count;
                if (!hasBefore || !hasAfter) continue;

                var    before     = result[gapStart - 1].Landmarks[j];
                var    after      = result[gapEnd].Landmarks[j];
                double spanLength = gapEnd - gapStart + 1; // distance between the two valid endpoints

                for (int fi = gapStart; fi < gapEnd; fi++)
                {
                    if (j >= result[fi].Landmarks.Count) break;
                    double t = (fi - gapStart + 1) / spanLength;
                    result[fi].Landmarks[j] = new Landmark
                    {
                        X          = before.X          + t * (after.X          - before.X),
                        Y          = before.Y          + t * (after.Y          - before.Y),
                        Z          = before.Z          + t * (after.Z          - before.Z),
                        Visibility = before.Visibility + t * (after.Visibility - before.Visibility),
                    };
                }
            }
        }

        return result;
    }
}
