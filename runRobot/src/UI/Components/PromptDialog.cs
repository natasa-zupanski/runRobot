namespace runRobot.UI;

/// <summary>Minimal single-line text input dialog.</summary>
public static class PromptDialog
{
    public static string? Show(string message, Control? owner = null)
    {
        var form = new Form
        {
            Text            = "runRobot",
            Size            = new Size(300, 120),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            MinimizeBox     = false,
            MaximizeBox     = false,
        };
        var lbl = new Label   { Text = message, Location = new Point(10, 10), AutoSize = true };
        var txt = new TextBox { Location = new Point(10, 30), Width = 265 };
        var ok  = new Button  { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(110, 58), Width = 75 };
        form.Controls.AddRange((Control[])[lbl, txt, ok]);
        form.AcceptButton = ok;
        return form.ShowDialog(owner) == DialogResult.OK ? txt.Text.Trim() : null;
    }
}
