using Godot;
using System;

public partial class AIVisualMarkerManager : Node
{
    [Export]
    private Node3D marker;

    public static AIVisualMarkerManager Instance;

    public const float MARKER_ALTITUDE = 3.0f;
    public const double MOVEMENT_DURATION = 1.0;
    private double dtAccumulator = 0.0f;
    private bool moving = false;
    private Vector3 movementDestination = new();
    private Vector3 movementOrigin = new();

    public override void _Ready()
    {
        marker.Visible = false;
        marker.Position = new Vector3(GD.Randf(), GD.Randf(), GD.Randf()).Normalized() * MARKER_ALTITUDE;

        Instance = this;
    }

    public override void _Process(double _dt)
    {
        if(moving == false)
            return;

        dtAccumulator += _dt;
        float t = (float)(dtAccumulator / MOVEMENT_DURATION);
        if(t > 1.0f - Mathf.Epsilon)
        {
            moving = false;
            t = 1.0f;
        }

        marker.Position = movementOrigin.Lerp(movementDestination, t).Normalized() * MARKER_ALTITUDE;
        marker.LookAt(Vector3.Zero, Vector3.Up);
    }

    public void setMarkerVisibility(bool _status) { marker.Visible = _status; }
    public void hideMarker(){ setMarkerVisibility(false); }
    public void showMarker(){ setMarkerVisibility(true); }

    public void moveTo(Vector3 _targetPosition)
    {
        dtAccumulator = 0.0f;
        moving = true;
        movementOrigin = marker.Position;
        movementDestination = _targetPosition;
    }

}
