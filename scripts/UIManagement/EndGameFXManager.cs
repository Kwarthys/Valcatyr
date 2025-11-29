using Godot;
using System;
using System.Collections.Generic;

public partial class EndGameFXManager : Node
{
    [Export] private PackedScene fx;
    public static EndGameFXManager Instance;

    public override void _Ready()
    {
        Instance = this;
    }

    public static void goNuts(List<Vector3> _positions, List<Vector3> _rotations)
    {
        Instance?.spawnFXs(_positions, _rotations);
    }

    private void spawnFXs(List<Vector3> _positions, List<Vector3> _rotations)
    {
        for(int i = 0; i < _positions.Count; ++i)
        {
            Node3D node = fx.Instantiate<Node3D>();
            node.Position = _positions[i];
            node.Rotation = _rotations[i];
            AddChild(node);

            if(i == 0)
            {
                // When the first one finishes, destroy all FX
                GpuParticles3D particles = (GpuParticles3D)node.GetChild(0);
                particles.Finished += destroyEveryFX;
            }
        }
    }

    public void destroyEveryFX()
    {
        foreach(Node n in GetChildren())
        {
            n.QueueFree();
        }
    }
}
