using Godot;
using System;

public partial class rotator : Node3D
{
    [Export]
    private float speed = 1.0f;
    public override void _Process(double dt)
    {
        Rotate(Vector3.Up, Mathf.Tau * speed * (float)dt);
    }
}
