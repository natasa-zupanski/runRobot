using runRobot.Estimators;

namespace runRobot.UI;

/// <summary>
/// UserControl that owns frame playback: PictureBox, navigation buttons, playback
/// timer, FPS field, frame/steps/speed labels, and speed-unit conversion.
/// </summary>
public class FrameViewPanel : UserControl
{
    private readonly PictureBox _pictureBox;
    private readonly Button     _prevButton;
    private readonly Button     _nextButton;
    private readonly Button     _playButton;
    private readonly Button     _stopButton;
    private readonly TextBox    _fpsTextBox;
    private readonly Label      _frameLabel;
    private readonly Label      _stepsLabel;
    private readonly Label      _medianSpeedLabel;
    private readonly Label      _frameSpeedLabel;
    private readonly Label      _caloriesLabel;
    private readonly ComboBox   _speedUnitCombo;

    private readonly System.Windows.Forms.Timer _timer;
    private List<Bitmap> _bitmaps = [];
    private SpeedEstimationResult? _speedResult;
    private double? _lastKnownSpeed;
    private int _currentIndex = 0;

    public FrameViewPanel()
    {
        _timer = new System.Windows.Forms.Timer();
        _timer.Tick += Timer_Tick;

        var vizPanel = new Panel { Dock = DockStyle.Fill };

        _pictureBox = new PictureBox
        {
            Dock      = DockStyle.Fill,
            SizeMode  = PictureBoxSizeMode.Zoom,
            BackColor = Color.DimGray,
        };
        vizPanel.Controls.Add(_pictureBox);

        var bar = new FlowLayoutPanel
        {
            AutoSize      = true,
            AutoSizeMode  = AutoSizeMode.GrowAndShrink,
            WrapContents  = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding       = new Padding(4, 4, 4, 0),
        };
        var barScroll = new Panel
        {
            Dock        = DockStyle.Bottom,
            Height      = 52,
            AutoScroll  = true,
        };

        _prevButton = BarButton("◀ Prev", PrevButton_Click);
        _nextButton = BarButton("Next ▶", NextButton_Click);
        _playButton = BarButton("▶ Play", PlayButton_Click);
        _stopButton = BarButton("■ Stop", StopButton_Click);
        _stopButton.Enabled = false;

        bar.Controls.Add(_prevButton);
        bar.Controls.Add(_nextButton);
        bar.Controls.Add(_playButton);
        bar.Controls.Add(_stopButton);
        bar.Controls.Add(new Label { Text = "FPS:", AutoSize = true, Margin = new Padding(8, 6, 0, 0) });
        _fpsTextBox = new TextBox { Text = "10", Width = 40 };
        bar.Controls.Add(_fpsTextBox);
        _frameLabel       = new Label { Text = "No frames loaded", AutoSize = true, Margin = new Padding(8, 6, 0, 0) };
        _stepsLabel       = new Label { Text = "", AutoSize = true, Margin = new Padding(16, 6, 0, 0) };
        _medianSpeedLabel = new Label { Text = "", AutoSize = true, Margin = new Padding(16, 6, 0, 0) };
        _frameSpeedLabel  = new Label { Text = "", AutoSize = true, Margin = new Padding(16, 6, 0, 0) };
        _caloriesLabel    = new Label { Text = "", AutoSize = true, Margin = new Padding(16, 6, 0, 0) };
        bar.Controls.Add(_frameLabel);
        bar.Controls.Add(_stepsLabel);
        bar.Controls.Add(_medianSpeedLabel);
        bar.Controls.Add(_frameSpeedLabel);
        bar.Controls.Add(_caloriesLabel);
        bar.Controls.Add(new Label { Text = "Speed unit:", AutoSize = true, Margin = new Padding(16, 6, 0, 0) });
        _speedUnitCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width         = 72,
            Margin        = new Padding(2, 3, 0, 0),
        };
        _speedUnitCombo.Items.AddRange((string[])["units/s", "m/s", "mph"]);
        _speedUnitCombo.SelectedIndex = 0;
        _speedUnitCombo.SelectedIndexChanged += (_, _) => RefreshSpeedLabels();
        bar.Controls.Add(_speedUnitCombo);

        barScroll.Controls.Add(bar);
        vizPanel.Controls.Add(barScroll);

        Dock = DockStyle.Fill;
        Controls.Add(vizPanel);

        UpdateNavButtons();
    }

    private static Button BarButton(string text, EventHandler handler)
    {
        var btn = new Button { Text = text, AutoSize = true };
        btn.Click += handler;
        return btn;
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public void ClearFrames()
    {
        _timer.Stop();
        _pictureBox.Image = null;
        foreach (var bmp in _bitmaps) bmp.Dispose();
        _bitmaps = [];
        _currentIndex = 0;
        _frameLabel.Text = "No frames loaded";
        _stepsLabel.Text = "";
        _medianSpeedLabel.Text = "";
        _frameSpeedLabel.Text = "";
        _caloriesLabel.Text = "";
        _speedResult = null;
        _lastKnownSpeed = null;
        UpdateNavButtons();
    }

    public void LoadFrames(List<Bitmap> bitmaps, int estimatedSteps, SpeedEstimationResult? speedResult,
        CalorieEstimationResult? calorieResult = null)
    {
        _bitmaps = bitmaps;
        _speedResult = speedResult;
        _lastKnownSpeed = null;
        _stepsLabel.Text = $"Estimated steps: {estimatedSteps}";
        _medianSpeedLabel.Text = speedResult is not null
            ? $"Median Speed: {FormatSpeed(speedResult.Speed)}"
            : "";
        _caloriesLabel.Text = calorieResult is not null
            ? $"Calories: {calorieResult.Calories:F1} kcal"
            : "Calories: N/A (no weight)";
        ShowFrame(0);
    }

    // ── Internal logic ───────────────────────────────────────────────────────

    private void ShowFrame(int index)
    {
        if (index < 0 || index >= _bitmaps.Count) return;
        _currentIndex = index;
        _pictureBox.Image = _bitmaps[index];
        _frameLabel.Text = $"Frame {_currentIndex + 1} of {_bitmaps.Count}";

        if (_speedResult is not null && _currentIndex < _speedResult.SpeedPerFrame.Count)
        {
            double? frameSpeed = _speedResult.SpeedPerFrame[_currentIndex];
            if (frameSpeed.HasValue) _lastKnownSpeed = frameSpeed;
            else                     frameSpeed = _lastKnownSpeed;
            _frameSpeedLabel.Text = frameSpeed.HasValue
                ? $"Frame Speed: {FormatSpeed(frameSpeed)}"
                : "Frame Speed: N/A";
        }

        UpdateNavButtons();
    }

    private void RefreshSpeedLabels()
    {
        _medianSpeedLabel.Text = _speedResult is not null
            ? $"Median Speed: {FormatSpeed(_speedResult.Speed)}"
            : "";
        if (_bitmaps.Count > 0) ShowFrame(_currentIndex);
    }

    private string FormatSpeed(double? worldSpeed)
    {
        if (!worldSpeed.HasValue) return "N/A";

        string unit = _speedUnitCombo.SelectedItem?.ToString() ?? "units/s";
        double? scaleFactor = _speedResult?.ScaleFactor;

        if (unit != "units/s" && scaleFactor is null)
            return "N/A (no hip height)";

        (double display, string fmt) = unit switch
        {
            "m/s" => (worldSpeed.Value * scaleFactor!.Value,           "F2"),
            "mph" => (worldSpeed.Value * scaleFactor!.Value * 2.23694, "F2"),
            _     => (worldSpeed.Value,                                  "F3"),
        };

        return $"{display.ToString(fmt)} {unit}";
    }

    private void UpdateNavButtons()
    {
        bool playing  = _timer.Enabled;
        bool hasFrames = _bitmaps.Count > 0;
        _prevButton.Enabled = !playing && _currentIndex > 0;
        _nextButton.Enabled = !playing && _currentIndex < _bitmaps.Count - 1;
        _playButton.Enabled = !playing && hasFrames;
        _stopButton.Enabled = playing;
    }

    private void PrevButton_Click(object? sender, EventArgs e) => ShowFrame(_currentIndex - 1);
    private void NextButton_Click(object? sender, EventArgs e) => ShowFrame(_currentIndex + 1);

    private void PlayButton_Click(object? sender, EventArgs e)
    {
        if (!int.TryParse(_fpsTextBox.Text, out int fps) || fps <= 0)
        {
            MessageBox.Show("Please enter a valid FPS (positive integer).");
            return;
        }
        _timer.Interval = 1000 / fps;
        _timer.Start();
        UpdateNavButtons();
    }

    private void StopButton_Click(object? sender, EventArgs e)
    {
        _timer.Stop();
        UpdateNavButtons();
    }

    private void Timer_Tick(object? sender, EventArgs e) =>
        ShowFrame((_currentIndex + 1) % _bitmaps.Count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            foreach (var bmp in _bitmaps) bmp.Dispose();
        }
        base.Dispose(disposing);
    }
}
