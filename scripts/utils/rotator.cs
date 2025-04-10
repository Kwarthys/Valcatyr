using Godot;
using System;

public partial class rotator : MeshInstance3D
{
    public override void _Process(double dt)
    {
        Rotate(Vector3.Up, Mathf.Pi * (float)dt);
    }
}
