using Godot;
using System;

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
}
