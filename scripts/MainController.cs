using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class MainController : Node3D
{
    [Export]
    private Planet planet;

    private GameManager gameManager;
    public void notifyPlanetGenerationComplete()
    {
        gameManager = new(planet);
    }
}
