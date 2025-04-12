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
        
    }

    private static void _processReinforce(GameManager _gameManager, Country _interactedCountry, Player _player)
    {
        
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
                // Highlight all neighboring enemies
                if(_interactedCountry != null)
                {
                    foreach(int stateID in _interactedCountry.state.neighbors)
                    {
                        State n = _gameManager.planet.mapManager.getStateByStateID(stateID);
                        Country c = _gameManager.getCountryByState(n);
                        if(c.playerID != _player.id)
                            selection.enemies.Add(c);
                    }
                }
                // Highlight ALL allied territories
                foreach(Country c in _player.countries)
                    selection.allies.Add(c);
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
                // gather all connected friendly
                // TODO
                break;
            }
        }
        selection.removeDuplicateSelected();
        return selection;
    }
}
