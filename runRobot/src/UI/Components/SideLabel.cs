namespace runRobot.UI;

/// <summary>Standard sidebar label: auto-sized, left-anchored, for use in TableLayoutPanel column 0.</summary>
public class SideLabel : Label
{
    public SideLabel(string text, int topMargin = 2)
    {
        Text     = text;
        AutoSize = true;
        Anchor   = AnchorStyles.Left | AnchorStyles.Top;
        Margin   = new Padding(0, topMargin, 4, 0);
    }
}
