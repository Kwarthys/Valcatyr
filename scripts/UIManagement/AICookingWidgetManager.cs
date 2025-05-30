using Godot;
using System;

public partial class AICookingWidgetManager : Node2D
{
    private static AICookingWidgetManager Instance;
    private Vector2 initialPosition;
    private Vector2 hiddenPosition;
    private const double MOVEMENT_DURATION = 1.0f;
    [Export]
    private Curve movementCurve;
    private double accumulatedDt = 0.0;
    private Vector2 target;
    private Vector2 origin;
    private bool moving = false;

    public override void _Ready()
    {
        Instance = this;
        initialPosition = Position;
        hiddenPosition = new(initialPosition.X, -initialPosition.Y);

        Position = hiddenPosition;
        Visible = false;
    }

    public override void _Process(double _dt)
    {
        if (moving == false)
            return;

        accumulatedDt += _dt;
        float t = (float)(accumulatedDt / MOVEMENT_DURATION);

        if (t > 1.0f)
        {
            Position = target;
            moving = false;

            // hide if offscreen
            if (Position.Y < 0.0f)
            {
                Visible = false;
            }
            return;
        }

        Position = origin.Lerp(target, movementCurve.Sample(t));
    }

    public static void showWidget()
    {
        Instance?._setWidgetVisibility(true);
    }

    public static void hideWidget()
    {
        Instance?._setWidgetVisibility(false);
    }

    private void _setWidgetVisibility(bool _status)
    {
        Visible = true;
        accumulatedDt = 0.0;
        moving = true;
        origin = Position;
        target = _status ? initialPosition : hiddenPosition;
    }



}
