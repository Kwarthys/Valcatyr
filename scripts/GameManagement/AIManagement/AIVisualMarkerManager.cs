using Godot;
using System;
using System.Collections.Generic;

public partial class AIVisualMarkerManager : Node
{
    [Export]
    private Node3D marker;

    public static AIVisualMarkerManager Instance;

    public const float MARKER_ALTITUDE = 3.0f;
    public const double MOVEMENT_DURATION = 1.0;
    private double dtAccumulator = 0.0f;
    private bool moving = false;
    private Vector3[] movementCheckpoints = new Vector3[5]; // Origin - Quarter - Helf - 3Quarters - Destination

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
            marker.Position = movementCheckpoints[4];
            return;
        }

        marker.Position = _evaluateMovement(t);
        marker.LookAt(Vector3.Zero, Vector3.Up);
    }

    private Vector3 _evaluateMovement(float _t)
    {
        float timeOnLenght = _t * (movementCheckpoints.Length-1);
        int originIndex = (int)timeOnLenght; // Integer part is the index of our origin point
        float evaluateTime = timeOnLenght - originIndex; // decimal part is how much we're advanced between our origin point and the next

        return movementCheckpoints[originIndex].Lerp(movementCheckpoints[originIndex+1], evaluateTime).Normalized() * MARKER_ALTITUDE;
    }

    public void setMarkerVisibility(bool _status) { marker.Visible = _status; }
    public void hideMarker(){ setMarkerVisibility(false); }
    public void showMarker(){ setMarkerVisibility(true); }

    public void moveTo(Vector3 _targetPosition)
    {
        dtAccumulator = 0.0f;
        moving = true;
        // Splitting movement in segments smoothen the normalization and avoid huge boost of speeds when lerp movement goes near planet center
        Vector3 actualTarget = _targetPosition.Normalized() * MARKER_ALTITUDE;
        // First point, our origin
        movementCheckpoints[0] = marker.Position;
        // Last point, destination
        movementCheckpoints[4] = actualTarget;
        // Compute half Point
        movementCheckpoints[2] = movementCheckpoints[0].Lerp(movementCheckpoints[4], 0.5f).Normalized() * MARKER_ALTITUDE;
        // Compute both quarters from half pos
        movementCheckpoints[1] = movementCheckpoints[0].Lerp(movementCheckpoints[2], 0.5f).Normalized() * MARKER_ALTITUDE;
        movementCheckpoints[3] = movementCheckpoints[2].Lerp(movementCheckpoints[4], 0.5f).Normalized() * MARKER_ALTITUDE;
    }
}
