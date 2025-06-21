using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PawnColorManager : Node3D
{

    private List<StandardMaterial3D> materials = new();
    [Export]
    private int materialID = 0;

    public override void _Ready()
    {
        Queue<Node> toScan = new();
        toScan.Enqueue(this);
        while (toScan.Count > 0)
        {
            Node scanning = toScan.Dequeue();
            if (scanning is MeshInstance3D mesh)
            {
                // Node3D really is a meshInstance
                StandardMaterial3D material = (StandardMaterial3D)mesh.GetActiveMaterial(materialID);
                if (material != null)
                {
                    materials.Add((StandardMaterial3D)material.Duplicate());
                    mesh.SetSurfaceOverrideMaterial(materialID, materials.Last()); // Make unique to color each one individually                    
                }
            }

            foreach (Node child in scanning.GetChildren())
            {
                toScan.Enqueue(child);
            }
        }
    }

    public void setColor(Color _c)
    {
        materials.ForEach((m) => m.AlbedoColor = _c);
    }
}
