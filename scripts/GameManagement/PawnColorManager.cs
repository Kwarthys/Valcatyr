using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PawnColorManager : Node3D
{
    [Export]
    private int materialID = 0;

    private List<MeshInstance3D> meshes = new();

    private static List<StandardMaterial3D> materialPerPlayerID = new();

    public static void initialize(List<Color> playerColors)
    {
        materialPerPlayerID.Clear();
        foreach (Color c in playerColors)
        {
            StandardMaterial3D playerMaterial = new();
            playerMaterial.AlbedoColor = c;
            materialPerPlayerID.Add(playerMaterial);
        }
    }

    public void setColor(int playerID)
    {
        if (meshes.Count == 0)
        {
            _find3DMeshes();
        }

        meshes.ForEach((mesh) => mesh.SetSurfaceOverrideMaterial(materialID, materialPerPlayerID[playerID]));
    }

    private void _find3DMeshes()
    {
        Queue<Node> toScan = new();
        toScan.Enqueue(this);
        while (toScan.Count > 0)
        {
            Node scanning = toScan.Dequeue();
            if (scanning is MeshInstance3D mesh)
            {
                // Node3D really is a meshInstance
                meshes.Add((MeshInstance3D)scanning);
            }

            foreach (Node child in scanning.GetChildren())
            {
                toScan.Enqueue(child);
            }
        }
    }
}
