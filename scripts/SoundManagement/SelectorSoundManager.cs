using Godot;
using System;
using System.Net;

public partial class SelectorSoundManager : AudioStreamPlayer3D
{
    private static SelectorSoundManager Instance;

    [Export]
    private float pitchRange = 0.3f;

    public override void _Ready()
    {
        Instance = this;
    }

    public static void play()
    {
        Instance?._play();
    }

    private void _play()
    {
        PitchScale = Mathf.Lerp(1.0f - pitchRange, 1.0f + pitchRange, GD.Randf());
        Play();
    }
}
