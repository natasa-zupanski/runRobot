using runRobot.Estimators;
using runRobot.Models;

namespace runRobot.UI;

public class PoseVisualizer
{
    private static readonly (int, int)[] SkeletonConnections =
    [
        // Head
        (0, 1), (1, 2), (2, 3),      // Nose to right eye
        (0, 4), (4, 5), (5, 6),      // Nose to left eye
        (3, 7), (6, 8),              // Eyes to ears

        // Torso
        (9, 10),                       // Mouth
        (11, 12),                      // Shoulders
        (11, 13), (13, 15),           // Left arm
        (12, 14), (14, 16),           // Right arm

        // Hands
        (15, 17), (15, 19), (15, 21), // Left hand fingers
        (16, 18), (16, 20), (16, 22), // Right hand fingers

        // Torso to hips
        (11, 23), (12, 24),           // Shoulders to hips

        // Legs
        (23, 25), (25, 27), (27, 29), (29, 31), // Left leg
        (24, 26), (26, 28), (28, 30), (30, 32), // Right leg
    ];

    public static void VisualizeFrames(List<PoseFrame> frames, int estimatedSteps,
        SpeedEstimationResult? speedResult = null, int? maxFrames = null)
    {
        int numFrames = maxFrames.HasValue ? Math.Min(frames.Count, maxFrames.Value) : frames.Count;
        var bitmaps = new List<Bitmap>();

        for (int i = 0; i < numFrames; i++)
        {
            bitmaps.Add(DrawFrame(frames[i]));
            if ((i + 1) % 10 == 0)
                Console.WriteLine($"Processed {i + 1}/{numFrames} frames");
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new VisualizationForm(bitmaps, estimatedSteps, speedResult));
    }

    public static Bitmap DrawFrame(PoseFrame frame)
    {
        const int width = 800;
        const int height = 600;

        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using (var pen = new Pen(Color.Blue, 2))
        {
            foreach (var (start, end) in SkeletonConnections)
            {
                if (start < frame.Landmarks.Count && end < frame.Landmarks.Count)
                {
                    var lm1 = frame.Landmarks[start];
                    var lm2 = frame.Landmarks[end];

                    if (lm1.Visibility > 0.5 && lm2.Visibility > 0.5)
                    {
                        graphics.DrawLine(pen,
                            (int)(lm1.X * width), (int)(lm1.Y * height),
                            (int)(lm2.X * width), (int)(lm2.Y * height));
                    }
                }
            }
        }

        for (int idx = 0; idx < frame.Landmarks.Count; idx++)
        {
            var landmark = frame.Landmarks[idx];
            if (landmark.Visibility <= 0.5) continue;

            int x = (int)(landmark.X * width);
            int y = (int)(landmark.Y * height);

            Color baseColor = idx == 27 ? Color.Blue : idx == 28 ? Color.Green : Color.Red;
            int alpha = (int)(landmark.Visibility * 255);
            using var brush = new SolidBrush(Color.FromArgb(alpha, baseColor));
            graphics.FillEllipse(brush, x - 3, y - 3, 6, 6);
        }

        using (var font = new Font("Arial", 12))
        using (var brush = new SolidBrush(Color.Black))
            graphics.DrawString($"Frame {frame.FrameNumber}", font, brush, 10, 10);

        return bitmap;
    }
}

public class VisualizationForm : Form
{
    private readonly FrameViewPanel _frameView;

    public VisualizationForm(List<Bitmap> bitmaps, int estimatedSteps, SpeedEstimationResult? speedResult = null)
    {
        Text          = "Pose Visualizations";
        Size          = new Size(900, 700);
        StartPosition = FormStartPosition.CenterScreen;

        _frameView = new FrameViewPanel();
        Controls.Add(_frameView);

        _frameView.LoadFrames(bitmaps, estimatedSteps, speedResult);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        Application.Exit();
    }
}
