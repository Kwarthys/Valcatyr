using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class MainController : Node
{
    [Export]
    private Planet planet;

    [Export]
    private GameManager gameManager;
    
    public void notifyPlanetGenerationComplete()
    {
        gameManager.initialize(planet);
    }
}
