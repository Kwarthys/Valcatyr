using Godot;
using System;
using System.Reflection.Metadata.Ecma335;

public partial class OptionsDisplayManager : Control
{
    [Export]
    private Control optionsUIHolder;
    [Export]
    private Curve movementCurve;

    private const float foldedXValue = 150;
    private float displayedXValue;

    private const float duration = 0.5f;

    private bool shown = false;

    private LerpHelper<float> lerper;

    public override void _Ready()
    {
        displayedXValue = optionsUIHolder.Position.X;
        lerper = new(duration, (from, to, time) => Mathf.Lerp(from, to, movementCurve.Sample((float)time)));
        _setPos(foldedXValue);
    }

    public override void _Process(double _dt)
    {
        if (lerper.moving == false)
            return;
        _setPos(lerper.lerp(_dt));

        if (lerper.moving == false && shown == false)
            optionsUIHolder.Visible = false;
    }

    public void onToggleShow()
    {
        float from = optionsUIHolder.Position.X;
        float to = shown ? foldedXValue : displayedXValue;
        lerper.startLerp(from, to);
        shown = !shown;
        optionsUIHolder.Visible = true;
    }

    private void _setPos(float _x)
    {
        Vector2 newPos = optionsUIHolder.Position;
        newPos.X = _x;
        optionsUIHolder.SetPosition(newPos);
    }
}
