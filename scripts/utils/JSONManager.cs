using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class JSONManager
{
    public static T Read<T>(string filePath) where T : ILoadableJSON, new()
    {
        GD.Print(filePath);
        filePath = ProjectSettings.GlobalizePath(filePath);
        GD.Print(filePath);
        string text = File.ReadAllText(filePath);
        Json loader = new();
        loader.Parse(text);
        GD.Print(loader.Data);
        Godot.Collections.Dictionary data = (Godot.Collections.Dictionary)loader.Data;
        GD.Print(data);
        T dataHolder = new();
        dataHolder.tryFill(data);
        return dataHolder;
    }
}

public abstract class ILoadableJSON
{
    public abstract void tryFill(Godot.Collections.Dictionary _data);
}

namespace JSONFormats
{
    public class GameData : ILoadableJSON
    {
        public List<Faction> factions = new();

        public override void tryFill(Godot.Collections.Dictionary _data)
        {
            if (_data.ContainsKey("Factions"))
            {
                Godot.Collections.Array factionArray = (Godot.Collections.Array)_data["Factions"];
                foreach (Godot.Collections.Dictionary faction in factionArray)
                {
                    factions.Add(new());
                    factions.Last().name = (string)faction["name"];
                    factions.Last().level1PawnPath = (string)faction["Level1Pawn"];
                    factions.Last().level2PawnPath = (string)faction["Level2Pawn"];
                }
            }
        }
    }

    public class Faction
    {
        public string name;
        public string level1PawnPath;
        public string level2PawnPath;
    }
}