using Godot;
using System;

public partial class StateDisplayerManager : Control
{
    [Export]
    private RichTextLabel stateNameLabel;
    [Export]
    private RichTextLabel playerLabel;
    [Export]
    private RichTextLabel troopsLabel;
    [Export]
    private TextureRect stateShapeDisplay;

    public static Vector2I stateShapeTextureSize = new(50, 50);
    public const int stateShapeBorderOffset = 5;

    [Export]
    private float offsetToHide = -350; // What will be added to X Transform pos to hide panel
    private Vector2 defaultPosition;
    private Vector2 targetPosition;
    private Vector2 originPosition;
    private Vector2 offsetPosition;

    public bool visible {get; private set;} = true;

    private double dtAccumulator = 0.0;
    private const double TIME_RETRACT = 0.2; // takes 1 sec to show/hide
    private bool moving = false;

    public override void _Ready()
    {
        defaultPosition = Position;
        targetPosition = Position;
        offsetPosition = new(offsetToHide, 0.0f);
        setVisible(false);
    }

    public override void _Process(double delta)
    {
        if(moving == false)
            return;
        dtAccumulator += delta;
        double t = dtAccumulator / TIME_RETRACT;
        if(t > 1.0)
        {
            Position = targetPosition;
            moving = false;
        }
        else
        {
            Position = originPosition.Lerp(targetPosition, (float)Mathf.Pow(t, 0.5));
        }
    }

    public void setVisible(bool _isVisible = true)
    {
        if(_isVisible == visible)
            return; // No change of state

        if(_isVisible)
            targetPosition = defaultPosition;
        else
            targetPosition = defaultPosition + offsetPosition;
        // Prepare lerping operations
        originPosition = Position;
        moving = true;
        dtAccumulator = 0.0;
        visible = _isVisible;
    }

    public void setCountryToDisplay(Country _c, bool _show = true)
    {
        stateNameLabel.Text = _c.state.name;
        playerLabel.Text = GameManager.Instance.getPlayerAsString(_c.playerID);
        troopsLabel.Text = _c.troops + " troop" + (_c.troops > 1 ? "s" : "") + ".";
        stateShapeDisplay.Texture = _c.state.stateShapeTexture;

        if(_show)
            setVisible();
    }
}
