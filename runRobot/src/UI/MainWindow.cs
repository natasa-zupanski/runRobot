namespace runRobot.UI;

public class MainWindow : Form
{
    private readonly TextBox        _videoPathBox;
    private readonly ProfilePanel   _profilePanel;
    private readonly PresetPanel    _presetPanel;
    private readonly Button         _runButton;
    private readonly Label          _statusLabel;
    private readonly FrameViewPanel _frameView;
    private readonly SplitContainer _split;
    private readonly TableLayoutPanel _sidebar;

    private double _videoAspectRatio = 1.0;

    public MainWindow()
    {
        Text = "MediaPipe Pose Analyzer";
        Size = new Size(1100, 750);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        _split = new SplitContainer
        {
            Dock      = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
        };
        Controls.Add(_split);

        // --- Sidebar ---
        // Wrap in a scrollable panel so PresetPanel is always reachable even on small screens.
        var sidebarScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _split.Panel1.Controls.Add(sidebarScroll);

        _sidebar = new TableLayoutPanel
        {
            AutoSize     = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock         = DockStyle.Top,
            ColumnCount  = 2,
            Padding      = new Padding(8),
        };
        _sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        _sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebarScroll.Controls.Add(_sidebar);

        // Row 0: Video path
        _sidebar.Controls.Add(new SideLabel("Video path", topMargin: 6), 0, 0);
        _videoPathBox = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
        _sidebar.Controls.Add(_videoPathBox, 1, 0);

        // Row 1: Browse
        var browseBtn = new Button { Text = "Browse…", Dock = DockStyle.Fill };
        browseBtn.Click += OnBrowse;
        _sidebar.Controls.Add(browseBtn, 0, 1);
        _sidebar.SetColumnSpan(browseBtn, 2);

        // Row 2: Profile panel
        _profilePanel = new ProfilePanel { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
        _sidebar.Controls.Add(_profilePanel, 0, 2);
        _sidebar.SetColumnSpan(_profilePanel, 2);

        // Row 3: Preset panel
        _presetPanel = new PresetPanel { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
        _sidebar.Controls.Add(_presetPanel, 0, 3);
        _sidebar.SetColumnSpan(_presetPanel, 2);

        // Row 4: Run button
        _runButton = new Button { Text = "Run", Dock = DockStyle.Fill, Height = 35, Margin = new Padding(0, 8, 0, 0) };
        _runButton.Click += OnRun;
        _sidebar.Controls.Add(_runButton, 0, 4);
        _sidebar.SetColumnSpan(_runButton, 2);

        // Row 5: Status label
        _statusLabel = new Label { Dock = DockStyle.Fill, AutoSize = false, Margin = new Padding(0, 6, 0, 0) };
        _sidebar.Controls.Add(_statusLabel, 0, 5);
        _sidebar.SetColumnSpan(_statusLabel, 2);

        // --- Frame view ---
        _frameView = new FrameViewPanel();
        _split.Panel2.Controls.Add(_frameView);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        int preferred = Math.Max(_profilePanel.MinimumWidth, _presetPanel.MinimumWidth)
                        + _sidebar.Padding.Horizontal;
        _split.SplitterDistance = preferred;
        _split.Panel1MinSize    = preferred;
    }

    private async void OnBrowse(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Select video file",
            Filter = "Video files|*.mp4;*.avi;*.mov;*.mkv;*.wmv|All files|*.*",
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _videoPathBox.Text = dlg.FileName;
            double? detectedRatio = await TryGetVideoAspectRatioAsync(dlg.FileName);
            if (detectedRatio is null)
                Console.WriteLine("Warning: could not detect video aspect ratio — defaulting to 1:1 (no correction).");
            else
                Console.WriteLine($"Detected video aspect ratio: {detectedRatio.Value:F4}");
            _videoAspectRatio = detectedRatio ?? 1.0;
        }
    }

    private static async Task<double?> TryGetVideoAspectRatioAsync(string videoPath)
    {
        try
        {
            var file  = await Windows.Storage.StorageFile.GetFileFromPathAsync(videoPath);
            var props = await file.Properties.GetVideoPropertiesAsync();
            uint w = props.Width, h = props.Height;
            if (w > 0 && h > 0) return (double)w / h;
        }
        catch { }
        return null;
    }

    private async void OnRun(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_videoPathBox.Text))
        {
            MessageBox.Show("Please select a video file.", "Missing input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _runButton.Enabled = false;
        _frameView.ClearFrames();

        try
        {
            string scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "pose_analyzer.py");
            var pipeline = new AnalysisPipeline(scriptPath);
            var progress = new Progress<string>(SetStatus);

            var result = await pipeline.RunAsync(
                videoPath:   _videoPathBox.Text,
                aspectRatio: _videoAspectRatio,
                settings:    _presetPanel.CurrentSettings,
                profile:     _profilePanel.CurrentProfile,
                progress:    progress);

            if (result.StepDebugLog is not null)
                Console.Write(result.StepDebugLog);

            // Bitmap rendering is a UI concern; Core pipeline returns raw PoseFrames.
            var bitmaps = await Task.Run(() =>
                result.PoseFrames.Select(PoseVisualizer.DrawFrame).ToList());

            _frameView.LoadFrames(bitmaps, result.EstimatedSteps, result.SpeedResult, result.CalorieResult);
            SetStatus($"Done — {bitmaps.Count} frames");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            _runButton.Enabled = true;
        }
    }

    private void SetStatus(string text) => _statusLabel.Text = text;
}
