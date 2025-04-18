using Godot;
using System;
using System.Collections.Generic;

public static class HumanPlayerManager
{
    public static void processAction(GameManager _gameManager, Country _interactedCountry, Player _player)
    {
        switch(_gameManager.gameState)
        {
            case GameManager.GameState.Deploy: _processDeploy(_gameManager, _interactedCountry, _player); return;
            case GameManager.GameState.Attack: _processAttack(_gameManager, _interactedCountry, _player); return;
            case GameManager.GameState.Reinforce: _processReinforce(_gameManager, _interactedCountry, _player); return;
        }
    }

    private static void _processDeploy(GameManager _gameManager, Country _interactedCountry, Player _player)
    {
        if(_interactedCountry.playerID == _player.id)
        {
            _gameManager.askReinforce(_interactedCountry);
        }
    }

    private static void _processAttack(GameManager _gameManager, Country _interactedCountry, Player _player)
    {
        if(_interactedCountry == null) return;  // Nothing to attack

        // Attackers checks
        Country attacker = _gameManager.currentSelection.selected;
        if(attacker == null || attacker.playerID != _player.id || attacker == _interactedCountry)
            return; // No attacker selected or attacker does not belong to active player or attacker is trying to attack itself
        if(attacker.troops <= 1) return; // Attacker has not enough troops (cannot attack with only one, as a defeat would leave an empty country)

        if(_interactedCountry.playerID == _player.id) return; // Attacked state is an ally
        bool statesAreNeighbors = attacker.state.neighbors.Contains(_interactedCountry.state.id);
        if(statesAreNeighbors == false) return; // Cannot attack disconnected countries
        // At last i think that's enough, combat can happen

        int troopsMovement = CombatManager.Instance.startCombat(attacker, _interactedCountry);
        if(troopsMovement != 0) // Manage the troops movement and ownership transfer
        {
            _gameManager.countryConquest(attacker, _interactedCountry, troopsMovement);
            if(attacker.troops > 1) // If more troops can move
                FreeMovementManager.Instance.startMovementInteraction(attacker, _interactedCountry); // let player decide what troops to move to new country
        }
        else
            _gameManager.updateCountryTroopsDisplay(attacker, _interactedCountry);
        
    }

    private static void _processReinforce(GameManager _gameManager, Country _interactedCountry, Player _player)
    {
        if(_interactedCountry == null) return;  // Nowhere to move to
        if(_gameManager.currentSelection.selected == null) return; // Nowhere to move from
        Country dest = _interactedCountry;
        Country src = _gameManager.currentSelection.selected;
        bool destIsReachable = _gameManager.currentSelection.allies.Contains(dest);
        if(!destIsReachable) return; // Cannot reach dest from src (or dest is not an ally)

        FreeMovementManager.Instance.startMovementInteraction(src, dest);
    }

    public static GameManager.SelectionData processSelection(GameManager _gameManager, Country _interactedCountry, Player _player)
    {
        // _interactedCountry will be null when player clicked out of state, perform deselection by not re-adding much
        GameManager.SelectionData selection = new();
        selection.selected = _interactedCountry;
        switch(_gameManager.gameState)
        {
            case GameManager.GameState.Deploy:
            {
                // always Highlight ALL allied territories
                selection.allies.AddRange(_player.countries);
                
                if(_interactedCountry == null)
                    break;
                // Highlight all neighboring enemies of selected country
                selection.enemies.AddRange(_gameManager.getNeighboringEnemiesAround(_interactedCountry, _player));
                break;
            }
            case GameManager.GameState.Attack:
            {
                if(_interactedCountry == null)
                    break;
                foreach(int stateID in _interactedCountry.state.neighbors)
                {
                    State n = _gameManager.planet.mapManager.getStateByStateID(stateID);
                    Country c = _gameManager.getCountryByState(n);
                    if(c.playerID == _player.id)
                        selection.allies.Add(c);
                    else
                        selection.enemies.Add(c);
                }
                break;
            }
            case GameManager.GameState.Reinforce:
            {
                if(_interactedCountry == null)
                    break;
                if(_interactedCountry.playerID == _player.id) // Only show possible movements to allies when selected state is allied
                    selection.allies.AddRange(_gameManager.getAlliedCountriesAccessibleFrom(_interactedCountry));
                selection.enemies.AddRange(_gameManager.getNeighboringEnemiesAround(_interactedCountry, _player));
                break;
            }

            case GameManager.GameState.Init:
            case GameManager.GameState.FirstDeploy:
                GD.PrintErr("INIT AND FIRST DEPLOY NYI"); break;
        }
        selection.removeDuplicateSelected(); // we most probably have the selected state also in allies list, remove it if necessary
        return selection;
    }
}
