using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public class JSONManager
{
    public static T Read<T>(string filePath)
    {
        string text = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Read).GetAsText();
        return JsonSerializer.Deserialize<T>(text);
    }
}

namespace JSONFormats
{
    public class GameData
    {
        public IList<Faction> Factions { get; set; }
    }

    public class Faction
    {
        public string Name { get; set; }
        public string Level1Pawn { get; set; }
        public string Level2Pawn { get; set; }
    }
}