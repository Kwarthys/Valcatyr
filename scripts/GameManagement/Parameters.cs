using Godot;
using System;
using System.Linq;

public class Parameters
{
    public static Color[] colors = // colorblindness friendly-ish color palette from Bang Wong 2011 "Points of view: Color blindness" https://www.nature.com/articles/nmeth.1618
    {
        new(0.90f, 0.63f, 0.0f), // Gold
        new(0.33f, 0.70f, 0.91f), // Cyan-Blue
        new(0.0f, 0.62f, 0.45f), // Green-ish
        new(0.80f, 0.47f, 0.66f), // Pink-ish
        new(0.98f, 0.94f, 0.90f), // Cream
        new(0.94f, 0.89f, 0.26f), // Yellow
    };

    public static string[] colorNames =
    {
        "Gold",
        "Cyan",
        "Green",
        "Pink",
        "Cream",
        "Yellow"
    };

    public static Color rogueIslandColor = new(0.89f, 0.90f, 0.66f);

    public static int getRandomColorID()
    {
        return (int)(GD.Randf() * (colors.Length - 1));
    }

    public static string[] factionNames { get; private set; }

    public static void setFactionNames(string[] _names)
    {
        factionNames = _names;
        OnFactionNamesReceived();
    }

    public static event EventHandler factionNamesReceived; // Use an event in case loading takes too much time or happens after others that need it

    protected static void OnFactionNamesReceived()
    {
        factionNamesReceived?.Invoke(typeof(Parameters), EventArgs.Empty);
    }
}
