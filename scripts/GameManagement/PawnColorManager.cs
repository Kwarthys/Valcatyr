using Godot;
using System;
using System.Collections.Generic;

public partial class PawnColorManager : Node3D
{
    [Export]
    private MeshInstance3D[] meshInstances;
    [Export]
    private int materialID = 0;

    public void setColor(Color _c)
    {
        foreach (MeshInstance3D mesh in meshInstances)
        {
            StandardMaterial3D material = (StandardMaterial3D)mesh.GetActiveMaterial(materialID);
            material.AlbedoColor = _c;
        }
    }
}
