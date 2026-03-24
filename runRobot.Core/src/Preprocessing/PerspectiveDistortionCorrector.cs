using runRobot.Models;

namespace runRobot.Preprocessing;

/// <summary>
/// Corrects perspective distortion in a sequence of PoseFrames by calibrating
/// camera parameters (focal length, hip depth) and using them to recover true
/// lateral world-space distances via perspective unprojection.
///
/// Calibration works by finding the focal length and hip depth that minimize
/// variance in the computed 3D distance of symmetric landmark pairs (hips,
/// shoulders) across all frames. Because these segment lengths are physically
/// fixed, high variance means the parameters are wrong.
///
/// Note: this corrects perspective distortion only. It does not rotate the
/// coordinate frame to align with the runner's direction of travel — yaw
/// attenuation (cos θ) on velocity is a separate, unaddressed error.
/// </summary>
public class PerspectiveDistortionCorrector : FramewiseCorrector
{
    public double FocalLength { get; private set; }
    public double HipDepth { get; private set; }

    // Symmetric landmark pairs used for calibration (left index, right index).
    // These body segments have a fixed real-world length that should be constant
    // across all frames regardless of pose.
    private static readonly (int Left, int Right, string Name)[] CalibrationPairs =
    {
        (11, 12, "Shoulders"),
        (23, 24, "Hips"),
    };

    public PerspectiveDistortionCorrector(double focalLength = 1.0, double hipDepth = 3.0)
    {
        FocalLength = focalLength;
        HipDepth    = hipDepth;
    }

    /// <summary>
    /// Estimates optimal camera parameters from a sequence of frames, then
    /// returns a corrector ready to use with those parameters.
    /// </summary>
    public static PerspectiveDistortionCorrector CalibrateFrom(List<PoseFrame> frames, double aspectRatio = 1.0)
    {
        var (focalLength, hipDepth) = Calibrate(frames, aspectRatio);
        return new PerspectiveDistortionCorrector(focalLength, hipDepth);
    }

    /// <summary>
    /// Projects a single frame, returning a new PoseFrame whose landmark X and Y
    /// are replaced with hip-relative world-space coordinates (perspective-unprojected,
    /// aspect-ratio-corrected) and whose landmark Z is unchanged (it is already in
    /// world units — depth relative to the hip midpoint plane).
    ///
    ///   world_x = (screen_x - hip_x) * (HipDepth + Z) / FocalLength   (aspect ratio applied downstream)
    ///   world_y = -(screen_y - hip_y) * (HipDepth + Z) / FocalLength   (Y flipped: up is positive)
    ///   world_z = Z  (passed through)
    /// </summary>
    public override PoseFrame Correct(PoseFrame frame)
    {
        var result = new PoseFrame
        {
            Timestamp   = frame.Timestamp,
            FrameNumber = frame.FrameNumber
        };

        var (hipRefX, hipRefY) = GetHipMidpoint(frame);

        foreach (var lm in frame.Landmarks)
        {
            double depth = HipDepth + lm.Z;
            result.Landmarks.Add(new Landmark
            {
                X          =  (lm.X - hipRefX) * depth / FocalLength,
                Y          = -(lm.Y - hipRefY) * depth / FocalLength,
                Z          = lm.Z,
                Visibility = lm.Visibility
            });
        }

        return result;
    }

    // --- Calibration ---

    private static (double FocalLength, double HipDepth) Calibrate(List<PoseFrame> frames, double aspectRatio)
    {
        // Coarse pass: wide range, low resolution
        var (coarseFL, coarseHD) = GridSearch(
            frames, aspectRatio,
            focalLengthMin: 0.4, focalLengthMax: 2.5, focalLengthSteps: 42,
            hipDepthMin: 1.0,    hipDepthMax: 10.0,   hipDepthSteps: 45
        );

        // Fine pass: narrow range around coarse best, higher resolution
        double flMargin = 0.3;
        double hdMargin = 1.0;
        var (fineFL, fineHD) = GridSearch(
            frames, aspectRatio,
            focalLengthMin: coarseFL - flMargin, focalLengthMax: coarseFL + flMargin, focalLengthSteps: 40,
            hipDepthMin:    coarseHD - hdMargin, hipDepthMax:    coarseHD + hdMargin, hipDepthSteps: 40
        );

        return (fineFL, fineHD);
    }

    private static (double FocalLength, double HipDepth) GridSearch(
        List<PoseFrame> frames, double aspectRatio,
        double focalLengthMin, double focalLengthMax, int focalLengthSteps,
        double hipDepthMin,    double hipDepthMax,    int hipDepthSteps)
    {
        double bestFocalLength = (focalLengthMin + focalLengthMax) / 2;
        double bestHipDepth    = (hipDepthMin + hipDepthMax) / 2;
        double bestCost        = double.MaxValue;

        double flStep = (focalLengthMax - focalLengthMin) / focalLengthSteps;
        double hdStep = (hipDepthMax    - hipDepthMin)    / hipDepthSteps;

        for (int fi = 0; fi <= focalLengthSteps; fi++)
        {
            double focalLength = focalLengthMin + fi * flStep;
            for (int hi = 0; hi <= hipDepthSteps; hi++)
            {
                double hipDepth = hipDepthMin + hi * hdStep;
                double cost = ComputeCost(frames, focalLength, hipDepth, aspectRatio);
                if (cost < bestCost)
                {
                    bestCost        = cost;
                    bestFocalLength = focalLength;
                    bestHipDepth    = hipDepth;
                }
            }
        }

        return (bestFocalLength, bestHipDepth);
    }

    /// <summary>
    /// Cost = sum of normalised variance (coefficient of variation²) for each
    /// calibration pair. Normalising by mean² means hip width and shoulder width
    /// contribute equally regardless of their different absolute lengths.
    /// </summary>
    private static double ComputeCost(List<PoseFrame> frames, double focalLength, double hipDepth, double aspectRatio)
    {
        double totalCost = 0.0;
        int pairsUsed = 0;

        foreach (var (leftIdx, rightIdx, _) in CalibrationPairs)
        {
            var distances = new List<double>();

            foreach (var frame in frames)
            {
                if (frame.Landmarks.Count <= rightIdx) continue;

                var left  = frame.Landmarks[leftIdx];
                var right = frame.Landmarks[rightIdx];

                if (left.Visibility < MinVisibility || right.Visibility < MinVisibility) continue;

                var (hipRefX, hipRefY) = GetHipMidpoint(frame);
                double dist = ComputeWorldDistance(left, right, hipRefX, hipRefY, focalLength, hipDepth, aspectRatio);
                distances.Add(dist);
            }

            if (distances.Count < 2) continue;

            double mean = distances.Average();
            if (mean < 1e-9) continue;

            double variance = distances.Sum(d => (d - mean) * (d - mean)) / distances.Count;
            totalCost += variance / (mean * mean); // normalised variance
            pairsUsed++;
        }

        return pairsUsed > 0 ? totalCost / pairsUsed : double.MaxValue;
    }

    /// <summary>
    /// Full 3D Euclidean distance between two landmarks in world space.
    /// The Z (depth) component is independent of focal length and hip depth,
    /// so it acts as a natural anchor for the calibration.
    /// </summary>
    private static double ComputeWorldDistance(
        Landmark a, Landmark b,
        double hipRefX, double hipRefY,
        double focalLength, double hipDepth, double aspectRatio)
    {
        double depthA = hipDepth + a.Z;
        double depthB = hipDepth + b.Z;

        double wxA = (a.X - hipRefX) * depthA / focalLength * aspectRatio;
        double wyA = (a.Y - hipRefY) * depthA / focalLength;
        double wxB = (b.X - hipRefX) * depthB / focalLength * aspectRatio;
        double wyB = (b.Y - hipRefY) * depthB / focalLength;

        double wz = a.Z - b.Z; // depth separation — independent of parameters
        double dx = wxA - wxB;
        double dy = wyA - wyB;

        return Math.Sqrt(dx * dx + dy * dy + wz * wz);
    }

    private static (double x, double y) GetHipMidpoint(PoseFrame frame)
    {
        if (frame.Landmarks.Count > RightHipIndex)
        {
            var left  = frame.Landmarks[LeftHipIndex];
            var right = frame.Landmarks[RightHipIndex];
            return ((left.X + right.X) / 2.0, (left.Y + right.Y) / 2.0);
        }
        return (0.5, 0.5);
    }
}
