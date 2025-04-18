using Godot;
using System;
using System.Collections.Generic;

public class ComputerAI
{
    public Player player {get; private set;}

    public ComputerAI(Player _player)
    {
        player = _player;
    }

    private double dtAccumulator = 0.0f;
    private double actionCooldown = 1.5f; // Seconds per action

    public void processTurn(double _dt)
    {
        dtAccumulator += _dt;
        if(dtAccumulator < actionCooldown)
            return; // skip to let player understand what's happening, MAYBE Option to make instant, or at least quicker/slower
        // Do play
        dtAccumulator -= actionCooldown;
        switch(GameManager.Instance.gameState)
        {
            case GameManager.GameState.Init: break; // should not be here
            case GameManager.GameState.FirstDeploy: _processFirstDeploy(); break;
            case GameManager.GameState.Deploy: _processDeploy(); break;
            case GameManager.GameState.Attack: _processAttack(); break;
            case GameManager.GameState.Reinforce: _processReinforce(); break;
        }
    }

    private void _processFirstDeploy(){}

    private void _processDeploy()
    {
        // High level thinking right here
        int countryIndex = (int)(GD.Randf() * (player.countries.Count - 1));
        GameManager.Instance.askReinforce(player.countries[countryIndex]);
    }

    private void _processAttack(){ GameManager.Instance.triggerNextPhase(); }
    private void _processReinforce(){ GameManager.Instance.triggerNextPhase(); }

    private Dictionary<Continent, float> computeContinentOccupationRatio()
    {
        Dictionary<Continent, float> ratioPerContinent = new();
        foreach(Continent c in player.stateCountPerContinents.Keys)
        {
            ratioPerContinent.Add(c, player.stateCountPerContinents[c] * 1.0f / c.stateIDs.Count);
        }
        return ratioPerContinent;
    }
}
