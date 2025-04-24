using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class GameState
{
    public GameState(int _playerID, int _depth, GameState _parent, GameAction _action)
    {
        playerID = _playerID;
        depth = _depth;
        actionToThisState = _action;
        parent = _parent;
    }
    
    public int score {get; private set;} = -1;
    public int depth = 0;
    public int minmaxScore = -1; // score getting up from children, not sure how to manage this for now
    public int playerID = -1;
    public Dictionary<Continent, int> countriesPerContinent = new();
    public List<Country> countries = new();
    public GameAction actionToThisState; // What action happened to reach this state from previous state

    public GameState parent = null;
    public List<GameState> children = new();

    public void evaluate()
    {
        // Heart of the stuff, evaluate the strength of the GameState given multiple game aspects and factors
        score = 42;
    }

    public bool contains(Country _c)
    {
        // We may have another image of this country, check if we have a country pointing to the same state (not GameState, geographic State)
        State stateToFind = _c.state;
        foreach(Country c in countries)
        {
            if(c.state == stateToFind)
                return true;
        }
        return false;
    }

    public void add(Country _c)
    {
        countries.Add(_c);
        // Register in continent logs
        if(countriesPerContinent.ContainsKey(_c.continent) == false)
        {
            countriesPerContinent.Add(_c.continent, 1);
        }
        else
            countriesPerContinent[_c.continent] += 1;
    }
}

/// <summary>
/// Stores a tree of possible game states by populating a root state
/// </summary>
public class GameStateGraph
{
    private GameState rootGameState = null; // RootGameState will have a null Parent and a None type ActionToThisState

    public void initialize(Player _player)
    {
        rootGameState = new(_player.id, 0, null, new GameAction(){ type = GameAction.GameActionType.None });
        rootGameState.countries = new(_player.countries);
        rootGameState.countriesPerContinent = new(_player.stateCountPerContinents);
    }

    public void generate(int maxDepth)
    {
        foreach(Country c in rootGameState.countries)
        {
            _generateCountryActionFromState(rootGameState, c, maxDepth); // start the recursion
        }
    }

    private void _generateCountryActionFromState(GameState _state, Country _country, int maxDepth)
    {
        int newDepth = _state.depth + 1;
        if(newDepth > maxDepth)
            return; // Please no infinite loops
        if(_state.countries.Contains(_country) == false)
            throw new Exception("GameState generation called on a Country not own in the GameState");
        
        foreach(int stateID in _country.state.neighbors)
        {
            Country neighboringCountry = GameManager.Instance.getCountryByState(stateID);
            if(_state.contains(neighboringCountry))
                continue; // Neighboring country already belong to the player in this state
            // Can we attack ?
            if(_country.troops * 0.8f > neighboringCountry.troops) // Need 20% more troops to even try
            {
                // ATTACK !
                GameState attackGameState = _generateAttackGameState(_state, _country, neighboringCountry);
                // Create 3 gamestates for reinforcement: Move all, Move None, Move Half -> We could create as many states as we have troops(-1) but WOW that'd be too much
            }
        }
    }

    private GameState _generateAttackGameState(GameState _parent, Country _from, Country _to)
    {
        _parent.children.Add(new(_parent.playerID, _parent.depth + 1, null, 
            new GameAction()
            {
                from = _from,
                to = _to,
                type = GameAction.GameActionType.Attack
            }
        ));
        GameState attackGameState = _parent.children.Last();
        attackGameState.countries = new(_parent.countries);
        Country destCopy = new(_to);
        destCopy.troops = 3;
        attackGameState.add(destCopy);
        attackGameState.countries.Remove(_from);
        Country originCopy = new Country(_from);
        originCopy.troops = (int)Mathf.Floor(originCopy.troops - (_to.troops * 0.5f)); // Approximate to lose 50% of defender troops
        attackGameState.countries.Add(originCopy);

        return attackGameState;
    }
}

public struct GameAction
{
    public enum GameActionType{ Attack, Reinforce, None };
    public GameActionType type;
    public Country from;
    public Country to;
    public int parameter; // Used by Reinforce to tell how much to move
}
