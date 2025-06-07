using Godot;
using System;

public partial class ExplosionFXManager : Node3D
{
    [Export]
    private GpuParticles3D particleSystem;
    [Export]
    private AudioStreamPlayer3D audioPlayer;
    [Export]
    private float pitchRange = 0.5f;

    private int completionReceived = 0;

    public override void _Ready()
    {
        particleSystem.Finished += onElementCompleted;
        audioPlayer.Finished += onElementCompleted;     // Register to both Signals for FX completion

        particleSystem.Restart();

        if (_randomizeSound() == false)
        {
            completionReceived += 1; // we won't play the sound, act as if it was already done to preoperly remove Node
            return;
        }
        audioPlayer.Play();
    }

    private bool _randomizeSound()
    {
        AudioStreamMP3 stream = PreloadManager.getRandomExplosionSound();
        if (stream == null)
            return false; // Can't play the sound
        audioPlayer.Stream = stream;
        audioPlayer.PitchScale = Mathf.Lerp(1.0f - pitchRange, 1.0f + pitchRange, GD.Randf());
        return true;
    }

    public void onElementCompleted()
    {
        if (++completionReceived >= 2)
            QueueFree(); // Mark for deletion at end of frame when sound AND particle System have finished
    }
}
