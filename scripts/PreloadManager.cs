using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;

public partial class PreloadManager : Node
{
    [Export]
    private string[] explosionSoundPaths;
    [Export]
    private string gameDataPath;
    private AudioStreamMP3[] explosionsSoundsStreams;

    public static PreloadManager Instance;
    [Export]
    private string pawnsRootFolder;

    private PackedScene[] pawnScenes;
    private string[] pawnPaths;

    public override void _Ready()
    {
        Instance = this;
        explosionsSoundsStreams = new AudioStreamMP3[explosionSoundPaths.Length];
        foreach (string path in explosionSoundPaths)
        {
            ResourceLoader.LoadThreadedRequest(path, "", false, ResourceLoader.CacheMode.Ignore);
        }

        _loadFactionsData();
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

    private void _loadFactionsData()
    {
        JSONFormats.GameData data = JSONManager.Read<JSONFormats.GameData>(gameDataPath);
        pawnScenes = new PackedScene[data.Factions.Count * 2]; // 2 Pawns per faction
        pawnPaths = new string[data.Factions.Count * 2];
        string[] factionNames = new string[data.Factions.Count];
        int pawnIndex = 0;
        int factionIndex = 0;
        foreach (JSONFormats.Faction f in data.Factions)
        {
            pawnPaths[pawnIndex] = _assemblePawnPath(f.Level1Pawn);
            pawnScenes[pawnIndex] = null;
            ResourceLoader.LoadThreadedRequest(pawnPaths[pawnIndex++], "", false, ResourceLoader.CacheMode.Ignore);
            pawnPaths[pawnIndex] = _assemblePawnPath(f.Level2Pawn);
            pawnScenes[pawnIndex] = null;
            ResourceLoader.LoadThreadedRequest(pawnPaths[pawnIndex++], "", false, ResourceLoader.CacheMode.Ignore);

            factionNames[factionIndex++] = f.Name;
        }

        Parameters.setFactionNames(factionNames);
    }

    public static PackedScene getPawnScene(int _factionID, int _pawnLevel)
    {
        if (Instance == null) return null;
        return Instance._getPawnScene(_factionID, _pawnLevel);
    }

    private PackedScene _getPawnScene(int _factionID, int _pawnLevel)
    {
        if (_pawnLevel != 1 && _pawnLevel != 2)
        {
            throw new Exception("Requested Pawn level invalid: " + _pawnLevel);
        }
        int pawnIndex = _factionID * 2 + _pawnLevel - 1;

        if (pawnScenes[pawnIndex] == null)
        {
            string pawnPath = pawnPaths[pawnIndex];
            if (ResourceLoader.LoadThreadedGetStatus(pawnPath) == ResourceLoader.ThreadLoadStatus.Loaded)
                pawnScenes[pawnIndex] = (PackedScene)ResourceLoader.LoadThreadedGet(pawnPath);
            else
                return null;
        }

        return pawnScenes[pawnIndex];
    }

    private string _assemblePawnPath(string _pawnEndPath) { return pawnsRootFolder + _pawnEndPath; }
}
