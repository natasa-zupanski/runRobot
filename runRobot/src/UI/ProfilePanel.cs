using runRobot.Models;
using runRobot.Storage;

namespace runRobot.UI;

/// <summary>
/// Sidebar panel that manages user profiles (name + hip height + weight).
/// Exposes HipHeightMeters and WeightKg for MainWindow to pass to the estimators.
/// </summary>
public class ProfilePanel : UserControl
{
    private readonly ComboBox _combo;
    private readonly Button   _saveBtn;
    private readonly Button   _delBtn;
    private readonly TextBox  _hipHeightBox;
    private readonly ComboBox _unitCombo;
    private readonly TextBox  _weightBox;
    private readonly ComboBox _weightUnitCombo;

    /// <summary>Minimum width (px) needed to show all controls without clipping.</summary>
    public int MinimumWidth =>
        90 /* col0 */ +
        _combo.Width + _combo.Margin.Horizontal +
        _saveBtn.PreferredSize.Width + _saveBtn.Margin.Horizontal +
        _delBtn.PreferredSize.Width + _delBtn.Margin.Horizontal;

    private List<UserProfile> _profiles = [];

    /// <summary>Hip height converted to metres, or null if empty/invalid.</summary>
    public double? HipHeightMeters
    {
        get
        {
            if (!double.TryParse(_hipHeightBox.Text, out double h) || h <= 0) return null;
            return _unitCombo.SelectedItem?.ToString() == "in" ? h * 0.0254 : h / 100.0;
        }
    }

    /// <summary>Snapshot of current UI state as a UserProfile (Name left empty).</summary>
    public UserProfile CurrentProfile => new()
    {
        HipHeight     = double.TryParse(_hipHeightBox.Text, out double h) && h > 0 ? h : null,
        HipHeightUnit = _unitCombo.SelectedItem?.ToString() ?? "cm",
        Weight        = double.TryParse(_weightBox.Text, out double w) && w > 0 ? w : null,
        WeightUnit    = _weightUnitCombo.SelectedItem?.ToString() ?? "kg",
    };

    /// <summary>Weight converted to kilograms, or null if empty/invalid.</summary>
    public double? WeightKg
    {
        get
        {
            if (!double.TryParse(_weightBox.Text, out double w) || w <= 0) return null;
            return _weightUnitCombo.SelectedItem?.ToString() == "lbs" ? w * 0.453592 : w;
        }
    }

    public ProfilePanel()
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

        // Row 0: section header
        var header = new Label { Text = "User Profile", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        table.Controls.Add(header, 0, 0);
        table.SetColumnSpan(header, 2);

        // Row 1: profile selector
        table.Controls.Add(SideLabel("Profile"), 0, 1);
        _combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 90, Margin = new Padding(0, 1, 2, 0) };
        _combo.SelectedIndexChanged += OnSelected;
        _saveBtn = new Button { Text = "Save",   AutoSize = true, Margin = new Padding(0, 0, 2, 0) };
        _delBtn  = new Button { Text = "Delete", AutoSize = true, Margin = new Padding(0) };
        _saveBtn.Click += OnSave;
        _delBtn.Click  += OnDelete;
        var row1 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0), Margin = new Padding(0) };
        row1.Controls.AddRange((Control[])[_combo, _saveBtn, _delBtn]);
        table.Controls.Add(row1, 1, 1);

        // Row 2: hip height
        table.Controls.Add(SideLabel("Hip height"), 0, 2);
        _hipHeightBox = new TextBox { Width = 55, Margin = new Padding(0, 1, 2, 0) };
        _unitCombo    = new ComboBox { Width = 48, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 1, 0, 0) };
        _unitCombo.Items.AddRange((string[])["cm", "in"]);
        _unitCombo.SelectedIndex = 0;
        var row2 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0), Margin = new Padding(0) };
        row2.Controls.Add(_hipHeightBox);
        row2.Controls.Add(_unitCombo);
        table.Controls.Add(row2, 1, 2);

        // Row 3: weight
        table.Controls.Add(SideLabel("Weight"), 0, 3);
        _weightBox       = new TextBox { Width = 55, Margin = new Padding(0, 1, 2, 0) };
        _weightUnitCombo = new ComboBox { Width = 48, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 1, 0, 0) };
        _weightUnitCombo.Items.AddRange((string[])["kg", "lbs"]);
        _weightUnitCombo.SelectedIndex = 0;
        var row3 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0), Margin = new Padding(0) };
        row3.Controls.Add(_weightBox);
        row3.Controls.Add(_weightUnitCombo);
        table.Controls.Add(row3, 1, 3);

        Controls.Add(table);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _profiles = AppDataStore.LoadProfiles();
        RefreshCombo();
    }

    private void RefreshCombo()
    {
        _combo.SelectedIndexChanged -= OnSelected;
        _combo.Items.Clear();
        foreach (var p in _profiles) _combo.Items.Add(p.Name);
        _combo.SelectedIndexChanged += OnSelected;
    }

    private void OnSelected(object? sender, EventArgs e)
    {
        if (_combo.SelectedIndex >= 0)
            Apply(_profiles[_combo.SelectedIndex]);
    }

    private void Apply(UserProfile profile)
    {
        _hipHeightBox.Text = profile.HipHeight?.ToString() ?? "";
        int idx = _unitCombo.Items.IndexOf(profile.HipHeightUnit);
        if (idx >= 0) _unitCombo.SelectedIndex = idx;

        _weightBox.Text = profile.Weight?.ToString() ?? "";
        int widx = _weightUnitCombo.Items.IndexOf(profile.WeightUnit);
        if (widx >= 0) _weightUnitCombo.SelectedIndex = widx;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        string name = _combo.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = Prompt("Enter profile name:") ?? "";
            if (string.IsNullOrEmpty(name)) return;
        }

        var profile = new UserProfile
        {
            Name          = name,
            HipHeight     = double.TryParse(_hipHeightBox.Text, out double h) && h > 0 ? h : null,
            HipHeightUnit = _unitCombo.SelectedItem?.ToString() ?? "cm",
            Weight        = double.TryParse(_weightBox.Text, out double w) && w > 0 ? w : null,
            WeightUnit    = _weightUnitCombo.SelectedItem?.ToString() ?? "kg",
        };

        int idx = _profiles.FindIndex(p => p.Name == name);
        if (idx >= 0) _profiles[idx] = profile;
        else          _profiles.Add(profile);

        AppDataStore.SaveProfiles(_profiles);
        RefreshCombo();
        _combo.SelectedItem = name;
    }

    private void OnDelete(object? sender, EventArgs e)
    {
        if (_combo.SelectedIndex < 0) return;
        string name = _combo.SelectedItem!.ToString()!;
        _profiles.RemoveAll(p => p.Name == name);
        AppDataStore.SaveProfiles(_profiles);
        RefreshCombo();
        _combo.Text = "";
    }

    private static Label SideLabel(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 2, 4, 0) };

    private string? Prompt(string message)
    {
        var form = new Form
        {
            Text = "runRobot", Size = new Size(300, 120),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false,
        };
        var lbl = new Label  { Text = message, Location = new Point(10, 10), AutoSize = true };
        var txt = new TextBox { Location = new Point(10, 30), Width = 265 };
        var ok  = new Button  { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(110, 58), Width = 75 };
        form.Controls.AddRange((Control[])[lbl, txt, ok]);
        form.AcceptButton = ok;
        return form.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
