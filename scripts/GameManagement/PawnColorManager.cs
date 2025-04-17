using Godot;
using System;
using System.Collections.Generic;

public partial class PawnColorManager : Node3D
{
    [Export]
    private MeshInstance3D[] meshInstances;

    public void setColor(Color _c)
    {
        foreach(MeshInstance3D mesh in meshInstances)
        {
            StandardMaterial3D material = (StandardMaterial3D)mesh.GetActiveMaterial(0);
            material.AlbedoColor = _c;
        }
    }
}
