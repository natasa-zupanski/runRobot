using System.Windows.Forms;

namespace runRobot.UI;

public class LaunchDialog : Form
{
    private readonly TextBox _videoPathBox;
    private readonly TextBox _maxFramesBox;
    private readonly TextBox _stepThresholdBox;
    private readonly CheckBox _debugStepsBox;
    private readonly CheckBox _noUiBox;
    private readonly Button _okButton;

    public string[] ResultArgs { get; private set; } = [];

    public LaunchDialog()
    {
        Text = "MediaPipe Pose Analyzer";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new System.Drawing.Size(480, 240);
        Padding = new Padding(12);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 6,
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        // Row 0: Video path
        layout.Controls.Add(Label("Video path *"), 0, 0);
        _videoPathBox = new TextBox { Dock = DockStyle.Fill, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        layout.Controls.Add(_videoPathBox, 1, 0);
        var browseBtn = new Button { Text = "Browse…", Dock = DockStyle.Fill };
        browseBtn.Click += OnBrowse;
        layout.Controls.Add(browseBtn, 2, 0);

        // Row 1: Max frames
        layout.Controls.Add(Label("Max frames"), 0, 1);
        _maxFramesBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_maxFramesBox, 1, 1);
        layout.SetColumnSpan(_maxFramesBox, 2);

        // Row 2: Step threshold
        layout.Controls.Add(Label("Step threshold"), 0, 2);
        _stepThresholdBox = new TextBox { Dock = DockStyle.Fill, Text = "0" };
        layout.Controls.Add(_stepThresholdBox, 1, 2);
        layout.SetColumnSpan(_stepThresholdBox, 2);

        // Row 3: Debug steps
        layout.Controls.Add(Label(""), 0, 3);
        _debugStepsBox = new CheckBox { Text = "--debug-steps", AutoSize = true };
        layout.Controls.Add(_debugStepsBox, 1, 3);
        layout.SetColumnSpan(_debugStepsBox, 2);

        // Row 4: No UI
        layout.Controls.Add(Label(""), 0, 4);
        _noUiBox = new CheckBox { Text = "--no-ui  (skip visualizer)", AutoSize = true };
        layout.Controls.Add(_noUiBox, 1, 4);
        layout.SetColumnSpan(_noUiBox, 2);

        // Row 5: OK / Cancel
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        var cancelBtn = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 75 };
        _okButton = new Button { Text = "Run", DialogResult = DialogResult.OK, Width = 75 };
        _okButton.Click += OnOk;
        AcceptButton = _okButton;
        CancelButton = cancelBtn;
        buttonPanel.Controls.Add(cancelBtn);
        buttonPanel.Controls.Add(_okButton);
        layout.Controls.Add(buttonPanel, 0, 5);
        layout.SetColumnSpan(buttonPanel, 3);

        Controls.Add(layout);
    }

    private static Label Label(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select video file",
            Filter = "Video files|*.mp4;*.avi;*.mov;*.mkv;*.wmv|All files|*.*",
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _videoPathBox.Text = dlg.FileName;
    }

    private void OnOk(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_videoPathBox.Text))
        {
            MessageBox.Show("Please select a video file.", "Missing input",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        var result = new List<string> { _videoPathBox.Text };

        if (int.TryParse(_maxFramesBox.Text, out int mf) && mf > 0)
            result.Add(mf.ToString());

        if (_debugStepsBox.Checked)
            result.Add("--debug-steps");

        if (double.TryParse(_stepThresholdBox.Text, out double th) && th != 0)
        {
            result.Add("--step-threshold");
            result.Add(th.ToString());
        }

        if (_noUiBox.Checked)
            result.Add("--no-ui");

        ResultArgs = [.. result];
    }
}
