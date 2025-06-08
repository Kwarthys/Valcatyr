using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

public partial class ReinforcementSoundManager : AudioStreamPlayer3D
{
    private static ReinforcementSoundManager Instance;

    public override void _Ready()
    {
        Instance = this;
    }

    [Export]
    private float pitchRange = 0.1f;

    public static void play(Vector3 _planetPos)
    {
        Instance?._play(_planetPos);
    }

    private void _play(Vector3 _planetPos)
    {
        Position = _planetPos;
        PitchScale = Mathf.Lerp(1.0f - pitchRange, 1.0f + pitchRange, GD.Randf());
        Play();
    }

}
