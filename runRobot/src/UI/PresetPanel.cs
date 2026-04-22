using runRobot.Models;
using runRobot.Preprocessing;
using runRobot.Storage;

namespace runRobot.UI;

/// <summary>
/// Sidebar panel that manages settings presets and exposes all pipeline
/// configuration values for MainWindow to read when running.
/// </summary>
public class PresetPanel : UserControl
{
    private readonly ComboBox _combo;
    private readonly Button   _saveBtn;
    private readonly Button   _delBtn;

    /// <summary>Minimum width (px) needed to show all controls without clipping.</summary>
    public int MinimumWidth =>
        90 /* col0 */ +
        _combo.Width + _combo.Margin.Horizontal +
        _saveBtn.PreferredSize.Width + _saveBtn.Margin.Horizontal +
        _delBtn.PreferredSize.Width + _delBtn.Margin.Horizontal;
    private readonly TextBox  _maxFramesBox;
    private readonly TextBox  _stepThresholdBox;
    private readonly TextBox  _stanceToleranceBox;
    private readonly CheckBox _debugStepsBox;
    private readonly ComboBox _yawMethodCombo;
    private readonly Dictionary<PoseCorrectorType, CheckBox> _correctorBoxes = [];

    private List<SettingsPreset> _presets = [];

    // --- Properties used by CurrentSettings ---

    private int?   MaxFrames      => int.TryParse(_maxFramesBox.Text, out int mf) && mf > 0 ? mf : null;
    private double StepThreshold  => double.TryParse(_stepThresholdBox.Text,  out double th) ? th : 0;
    private double StanceTolerance => (double.TryParse(_stanceToleranceBox.Text, out double st) ? st : 5.0) / 100.0;
    private YawCorrectionMethod YawMethod => Enum.GetValues<YawCorrectionMethod>()[_yawMethodCombo.SelectedIndex];

    /// <summary>Snapshot of current UI state as a SettingsPreset (Name left empty).</summary>
    public SettingsPreset CurrentSettings => new()
    {
        MaxFrames           = MaxFrames,
        StepThreshold       = StepThreshold,
        StanceTolerance     = StanceTolerance * 100,
        YawCorrectionMethod = YawMethod,
        PoseCorrectorSteps  = _correctorBoxes
            .Where(kv => kv.Value.Checked)
            .Select(kv => kv.Key)
            .ToList(),
        DebugSteps          = _debugStepsBox.Checked,
    };

    public PresetPanel()
    {
        AutoSize     = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var table = new TableLayoutPanel
        {
            AutoSize     = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock         = DockStyle.Top,
            ColumnCount  = 2,
            Padding      = new Padding(0),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Row 0: separator line
        var separator = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.ControlDark, Margin = new Padding(0, 8, 0, 6), Height = 1 };
        table.Controls.Add(separator, 0, 0);
        table.SetColumnSpan(separator, 2);

        // Row 1: section header
        var header = new Label { Text = "Settings", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        table.Controls.Add(header, 0, 1);
        table.SetColumnSpan(header, 2);

        // Row 2: preset selector
        table.Controls.Add(new SideLabel("Preset"), 0, 2);
        _combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 90, Margin = new Padding(0, 1, 2, 0) };
        _combo.SelectedIndexChanged += OnSelected;
        _saveBtn = new Button { Text = "Save",   AutoSize = true, Margin = new Padding(0, 0, 2, 0) };
        _delBtn  = new Button { Text = "Delete", AutoSize = true, Margin = new Padding(0) };
        _saveBtn.Click += OnSave;
        _delBtn.Click  += OnDelete;
        var row2 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0), Margin = new Padding(0) };
        row2.Controls.AddRange((Control[])[_combo, _saveBtn, _delBtn]);
        table.Controls.Add(row2, 1, 2);

        // Row 3: max frames
        table.Controls.Add(new SideLabel("Max frames"), 0, 3);
        _maxFramesBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 1, 0, 0) };
        table.Controls.Add(_maxFramesBox, 1, 3);

        // Row 4: step threshold
        table.Controls.Add(new SideLabel("Step threshold"), 0, 4);
        _stepThresholdBox = new TextBox { Dock = DockStyle.Fill, Text = "0", Margin = new Padding(0, 1, 0, 0) };
        table.Controls.Add(_stepThresholdBox, 1, 4);

        // Row 5: stance tolerance
        table.Controls.Add(new SideLabel("Stance tolerance"), 0, 5);
        _stanceToleranceBox = new TextBox { Dock = DockStyle.Fill, Text = "5", Margin = new Padding(0, 1, 0, 0) };
        table.Controls.Add(_stanceToleranceBox, 1, 5);

        // Row 6: debug steps (full width)
        _debugStepsBox = new CheckBox { Text = "Debug steps", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        table.Controls.Add(_debugStepsBox, 0, 6);
        table.SetColumnSpan(_debugStepsBox, 2);

        // Rows 7+: one checkbox per PoseCorrectorType, in pipeline application order
        int stepRow = 7;
        foreach (var step in PoseCorrectorFactory.Order)
        {
            var box = new CheckBox { Text = PoseCorrectorFactory.GetLabel(step), AutoSize = true, Checked = true, Margin = new Padding(0, 2, 0, 0) };
            _correctorBoxes[step] = box;
            table.Controls.Add(box, 0, stepRow);
            table.SetColumnSpan(box, 2);
            stepRow++;
            if (step == PoseCorrectorType.YawCorrection)
            {
                table.Controls.Add(new SideLabel("Yaw correction"), 0, stepRow);
                _yawMethodCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 1, 0, 0) };
                _yawMethodCombo.Items.AddRange(Enum.GetValues<YawCorrectionMethod>().Select(v => v.ToString()).ToArray());
                _yawMethodCombo.SelectedIndex = 0;
                table.Controls.Add(_yawMethodCombo, 1, stepRow);
                stepRow++;
            }
        }

        Controls.Add(table);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _presets = AppDataStore.LoadPresets();
        RefreshCombo();
    }

    private void RefreshCombo()
    {
        _combo.SelectedIndexChanged -= OnSelected;
        _combo.Items.Clear();
        foreach (var p in _presets) _combo.Items.Add(p.Name);
        _combo.SelectedIndexChanged += OnSelected;
    }

    private void OnSelected(object? sender, EventArgs e)
    {
        if (_combo.SelectedIndex >= 0)
            Apply(_presets[_combo.SelectedIndex]);
    }

    private void Apply(SettingsPreset preset)
    {
        _maxFramesBox.Text       = preset.MaxFrames?.ToString() ?? "";
        _stepThresholdBox.Text   = preset.StepThreshold.ToString();
        _stanceToleranceBox.Text = preset.StanceTolerance.ToString();
        _yawMethodCombo.SelectedIndex = preset.YawCorrectionMethod switch
        {
            YawCorrectionMethod.PerFrame => 1,
            YawCorrectionMethod.NoYaw    => 2,
            _                            => 0,
        };
        foreach (var (step, box) in _correctorBoxes)
            box.Checked = preset.PoseCorrectorSteps.Contains(step);
        _debugStepsBox.Checked              = preset.DebugSteps;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        string name = _combo.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = PromptDialog.Show("Enter preset name:", this) ?? "";
            if (string.IsNullOrEmpty(name)) return;
        }

        var preset = CurrentSettings;
        preset.Name = name;

        int idx = _presets.FindIndex(p => p.Name == name);
        if (idx >= 0) _presets[idx] = preset;
        else          _presets.Add(preset);

        AppDataStore.SavePresets(_presets);
        RefreshCombo();
        _combo.SelectedItem = name;
    }

    private void OnDelete(object? sender, EventArgs e)
    {
        if (_combo.SelectedIndex < 0) return;
        string name = _combo.SelectedItem!.ToString()!;
        _presets.RemoveAll(p => p.Name == name);
        AppDataStore.SavePresets(_presets);
        RefreshCombo();
        _combo.Text = "";
    }

}
