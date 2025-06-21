using Godot;
using System;
using System.Collections.Generic;

public partial class PawnColorManager : Node3D
{
    [Export]
    private MeshInstance3D[] meshInstances;
    [Export]
    private int materialID = 0;

    public override void _Ready()
    {
        foreach (MeshInstance3D mesh in meshInstances)
        {
            StandardMaterial3D material = (StandardMaterial3D)mesh.GetActiveMaterial(materialID);
            StandardMaterial3D materialCopy = (StandardMaterial3D)material.Duplicate(); // Make unique to color each one individually
            mesh.SetSurfaceOverrideMaterial(0, materialCopy);
        }
    }


    public void setColor(Color _c)
    {
        foreach (MeshInstance3D mesh in meshInstances)
        {
            StandardMaterial3D material = (StandardMaterial3D)mesh.GetActiveMaterial(materialID);
            material.AlbedoColor = _c;
        }
    }
}
