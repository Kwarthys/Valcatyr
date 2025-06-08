using Godot;
using System;

public partial class PawnSlideSoundManager : AudioStreamPlayer3D
{
    private Node3D target = null; // Sound will follow this fellow (and stop if it gets destroyed)

    [Export]
    private float pitchRange = 0.1f;

    public override void _Ready()
    {
        Finished += () => QueueFree(); // Destroy node on sound completion
        PitchScale = Mathf.Lerp(1.0f - pitchRange, 1.0f + pitchRange, GD.Randf());
        Play();
    }

    public override void _Process(double _dt)
    {
        if(target == null)
            return;
        Position = target.Position;
    }

    public void follow(Node3D _target)
    {
        target = _target;
        Position = _target.Position;
    }
}
