using Godot;
using System;
using System.Collections.Generic;

public static class HumanPlayerManager
{
    public static void processAction(Country _interactedCountry, Player _player)
    {
        switch(GameManager.Instance.gamePhase)
        {
            case GameManager.GamePhase.FirstDeploy: // Same as Deploy
            case GameManager.GamePhase.Deploy: _processDeploy(_interactedCountry, _player); return;
            case GameManager.GamePhase.Attack: _processAttack(_interactedCountry, _player); return;
            case GameManager.GamePhase.Reinforce: _processReinforce(_interactedCountry, _player); return;
        }
    }

    private static void _processDeploy(Country _interactedCountry, Player _player)
    {
        if(_interactedCountry.playerID == _player.id)
        {
            int amount = 1;
            if (Input.IsActionPressed("ModifierShift"))
                amount = GameManager.Instance.reinforcementLeft;
            else if (Input.IsActionPressed("ModifierCtrl"))
                amount = 5;
            GameManager.Instance.askReinforce(_interactedCountry, amount);
        }
    }

    private static void _processAttack(Country _interactedCountry, Player _player)
    {
        if(_interactedCountry == null) return;  // Nothing to attack

        // Attackers checks
        Country attacker = GameManager.Instance.currentSelection.selected;
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
            GameManager.Instance.countryConquest(attacker, _interactedCountry, troopsMovement);
            if(attacker.troops > 1) // If more troops can move
                FreeMovementManager.Instance.startMovementInteraction(attacker, _interactedCountry); // let player decide what troops to move to new country
            processSelection(_interactedCountry, _player); // Select newly acquired country
        }
        else
            GameManager.Instance.updateCountryTroopsDisplay(attacker, _interactedCountry);
        
    }

    private static void _processReinforce(Country _interactedCountry, Player _player)
    {
        if(_interactedCountry == null) return;  // Nowhere to move to
        if(GameManager.Instance.currentSelection.selected == null) return; // Nowhere to move from
        Country dest = _interactedCountry;
        Country src = GameManager.Instance.currentSelection.selected;
        bool destIsReachable = GameManager.Instance.currentSelection.allies.Contains(dest);
        if(!destIsReachable) return; // Cannot reach dest from src (or dest is not an ally)

        FreeMovementManager.Instance.startMovementInteraction(src, dest);
    }

    public static GameManager.SelectionData processSelection(Country _interactedCountry, Player _player)
    {
        // _interactedCountry will be null when player clicked out of state, perform deselection by not re-adding much
        GameManager.SelectionData selection = new();
        selection.selected = _interactedCountry;
        switch(GameManager.Instance.gamePhase)
        {
            case GameManager.GamePhase.FirstDeploy: // Same as Deploy, fallthrough
            case GameManager.GamePhase.Deploy:
            {
                // always Highlight ALL allied territories
                selection.allies.AddRange(_player.countries);
                
                if(_interactedCountry == null)
                    break;
                // Highlight all neighboring enemies of selected country
                selection.enemies.AddRange(GameManager.Instance.getNeighboringEnemiesAround(_interactedCountry, _player));
                break;
            }
            case GameManager.GamePhase.Attack:
            {
                if(_interactedCountry == null)
                    break;
                foreach(int stateID in _interactedCountry.state.neighbors)
                {
                    State n = GameManager.Instance.planet.mapManager.getStateByStateID(stateID);
                    Country c = GameManager.Instance.getCountryByState(n);
                    if(c.playerID == _player.id)
                        selection.allies.Add(c);
                    else
                        selection.enemies.Add(c);
                }
                break;
            }
            case GameManager.GamePhase.Reinforce:
            {
                if(_interactedCountry == null)
                    break;
                if(_interactedCountry.playerID == _player.id) // Only show possible movements to allies when selected state is allied
                    selection.allies.AddRange(GameManager.Instance.getAlliedCountriesAccessibleFrom(_interactedCountry));
                selection.enemies.AddRange(GameManager.Instance.getNeighboringEnemiesAround(_interactedCountry, _player));
                break;
            }
            case GameManager.GamePhase.End: break; // Always empty selection
            case GameManager.GamePhase.Init:
                GD.PrintErr("Should not be here in INIT mode");
                break;
        }
        selection.removeDuplicateSelected(); // we most probably have the selected state also in allies list, remove it if necessary
        return selection;
    }
}
