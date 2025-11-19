using Godot;
using System;

public partial class NewGameInterfacer : Control
{
    [Export] private CheckButton regenMap;
    [Export] private CheckButton editPlayers;
    [Export] private Button newGameButton;

    public static NewGameInterfacer Instance;
    public override void _Ready()
    {
        Instance = this;
        disable();
    }

    public static void setEnabled(bool _status)
    {
        Instance?.doSetEnabled(_status);
    }

    public static void enable(){setEnabled(true);}
    public static void disable(){setEnabled(false);}

    public void doSetEnabled(bool _status)
    {
        regenMap.Disabled = !_status;
        editPlayers.Disabled = !_status;
        newGameButton.Disabled = !_status;
    }

    public void onNewGameClic()
    {
        // Send these to GameManager
        disable();
        GameManager.startANewGame(regenMap.ButtonPressed, editPlayers.ButtonPressed);
        TabDisplayManager.closeAllTabs();
    }

}
