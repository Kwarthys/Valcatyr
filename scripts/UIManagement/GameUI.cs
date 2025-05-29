using Godot;
using System;

public partial class GameUI : Control
{
    public static GameUI Instance { get; private set; }

    [Export]
    RichTextLabel primaryText;
    [Export]
    RichTextLabel secondaryText;
    [Export]
    Button endTurnButton;
    [Export]
    Button startGameButton;

    public override void _Ready()
    {
        Instance = this;
        // Initialize states
        setPhaseButtonVisibility(false);
        setGameButtonVisibility(true);
        setPrimary("");
        setSecondary("");
    }

    public void setPrimary(string _text)
    {
        primaryText.Text = _text;
    }

    public void setSecondary(string _text)
    {
        secondaryText.Text = _text;
    }

    public static string makeBold(string _text)
    {
        return "[b]" + _text + "[/b]";
    }

    public void setPhaseButtonVisibility(bool _status)
    {
        endTurnButton.Visible = _status;
    }

    public void setGameButtonVisibility(bool _status)
    {
        startGameButton.Visible = _status;
    }
}
