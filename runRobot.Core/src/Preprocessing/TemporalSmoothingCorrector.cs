using runRobot.Models;

namespace runRobot.Preprocessing;

/// <summary>
/// Reduces frame-to-frame landmark noise by applying a Gaussian-weighted moving
/// average across time. Each landmark's X, Y, Z, and Visibility are smoothed
/// independently using the same kernel.
///
/// At sequence boundaries the window is truncated to available frames — no
/// padding or reflection is applied. The Gaussian weights are renormalised after
/// truncation, so boundary frames are not biased toward zero.
///
/// Default: windowRadius = 2 (5-frame window), sigma = windowRadius / 2.0.
/// </summary>
public class TemporalSmoothingCorrector(int windowRadius = 2, double? sigma = null) : PoseCorrector
{
    private readonly double _sigma = sigma ?? windowRadius / 2.0;

    public override List<PoseFrame> CorrectAll(List<PoseFrame> frames)
    {
        if (frames.Count == 0) return frames;

        // Precompute unnormalised Gaussian weights for offsets -windowRadius..+windowRadius.
        // Normalisation happens per-frame after boundary truncation.
        double[] kernelWeights = new double[2 * windowRadius + 1];
        for (int k = -windowRadius; k <= windowRadius; k++)
            kernelWeights[k + windowRadius] = Math.Exp(-(k * k) / (2 * _sigma * _sigma));

        var result = new List<PoseFrame>(frames.Count);

        for (int i = 0; i < frames.Count; i++)
        {
            var smoothed = new PoseFrame
            {
                Timestamp   = frames[i].Timestamp,
                FrameNumber = frames[i].FrameNumber
            };

            for (int j = 0; j < frames[i].Landmarks.Count; j++)
            {
                double sumX = 0, sumY = 0, sumZ = 0, sumVis = 0, sumW = 0;

                for (int k = -windowRadius; k <= windowRadius; k++)
                {
                    int fi = i + k;
                    if (fi < 0 || fi >= frames.Count || j >= frames[fi].Landmarks.Count) continue;

                    double w  = kernelWeights[k + windowRadius];
                    var    lm = frames[fi].Landmarks[j];

                    sumX   += w * lm.X;
                    sumY   += w * lm.Y;
                    sumZ   += w * lm.Z;
                    sumVis += w * lm.Visibility;
                    sumW   += w;
                }

                smoothed.Landmarks.Add(sumW > 0
                    ? new Landmark { X = sumX / sumW, Y = sumY / sumW, Z = sumZ / sumW, Visibility = sumVis / sumW }
                    : frames[i].Landmarks[j]);
            }

            result.Add(smoothed);
        }

        return result;
    }
}
