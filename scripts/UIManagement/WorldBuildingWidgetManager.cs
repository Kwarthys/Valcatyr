using Godot;
using System;

public partial class WorldBuildingWidgetManager : Control
{
    public static WorldBuildingWidgetManager Instance;

    public override void _Ready()
    {
        Instance = this;
        hide();
    }

    public static void hide()
    {
        Instance?.Hide();
    }

    public static void show()
    {
        Instance?.Show();
    }

}
