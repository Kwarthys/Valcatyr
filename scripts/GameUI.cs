using Godot;
using System;

public partial class GameUI : Control
{
    public static GameUI Instance{get; private set;}

    [Export]
    RichTextLabel primaryText;
    [Export]
    RichTextLabel secondaryText;
    [Export]
    Button endTurnButton;

    public override void _Ready()
    {
        Instance = this;
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
}
