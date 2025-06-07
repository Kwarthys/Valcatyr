using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;

public partial class PreloadManager : Node
{
    [Export]
    private string[] explosionSoundPaths;
    private AudioStreamMP3[] explosionsSoundsStreams;

    public static PreloadManager Instance;

    public override void _Ready()
    {
        Instance = this;
        explosionsSoundsStreams = new AudioStreamMP3[explosionSoundPaths.Length];
        foreach (string path in explosionSoundPaths)
        {
            ResourceLoader.LoadThreadedRequest(path, "", false, ResourceLoader.CacheMode.Ignore);
        }
    }

    public static AudioStreamMP3 getRandomExplosionSound()
    {
        if (Instance == null) return null;
        return Instance._getRandomExplosionSound();
    }

    private AudioStreamMP3 _getRandomExplosionSound()
    {
        int randIndex = (int)(GD.Randf() * (explosionSoundPaths.Length - 1));
        if (explosionsSoundsStreams[randIndex] == null)
        {
            if (ResourceLoader.LoadThreadedGetStatus(explosionSoundPaths[randIndex]) == ResourceLoader.ThreadLoadStatus.Loaded)
                explosionsSoundsStreams[randIndex] = (AudioStreamMP3)ResourceLoader.LoadThreadedGet(explosionSoundPaths[randIndex]);
            else
                return null;
        }
        return explosionsSoundsStreams[randIndex];
    }
}
