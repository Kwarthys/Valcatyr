using Godot;
using System;

public partial class GameUI : Control
{
    private static GameUI Instance;

    [Export]
    RichTextLabel primaryText;
    [Export]
    RichTextLabel secondaryText;
    [Export]
    Button endTurnButton;
    [Export]
    Button newGameButton;

    public override void _Ready()
    {
        Instance = this;
        // Initialize states
        setPhaseButtonVisibility(false);
        setNewGameButtonVisibility(false);
        setPrimary("");
        setSecondary("");
    }

    public static void setPrimary(string _text)
    {
        if(Instance != null)
            Instance.primaryText.Text = _text;
    }

    public static void setSecondary(string _text)
    {
        if(Instance != null)
            Instance.secondaryText.Text = _text;
    }

    public static string makeBold(string _text)
    {
        return "[b]" + _text + "[/b]";
    }

    public static void setPhaseButtonVisibility(bool _status)
    {
        if(Instance != null)
            Instance.endTurnButton.Visible = _status;
    }

    public static void setNewGameButtonVisibility(bool _status)
    {
        if(Instance != null)
            Instance.newGameButton.Visible = _status;
    }
}
