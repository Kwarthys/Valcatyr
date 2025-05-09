using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Principal;

public class GameState
{
    public GameState(int _playerID, int _depth, GameState _parent, GameAction _action)
    {
        playerID = _playerID;
        depth = _depth;
        actionToThisState = _action;
        parent = _parent;
    }
    
    public float score {get; private set;} = -1;
    public int depth = 0;
    public float minmaxScore = -1; // score getting up from children, not sure how to manage this for now
    public int playerID = -1;
    public Dictionary<Continent, int> countriesPerContinent = new();
    public List<Country> countries = new();
    public GameAction actionToThisState; // What action happened to reach this state from previous state

    public GameState parent = null;
    public List<GameState> children = new();

    public void evaluate()
    {
        // Heart of the stuff, evaluate the strength of the GameState given multiple game aspects and factors
        int continentScore = _evaluateContinentScore(); // How much bonus points granted by Continent occupation
        int countryScore = _evaluateOwnedCountriesScore(); // How much points granted by number of owned states
        float threatScore = _evaluateThreatScore(); // How much our borders are unsafe
        int unusableTroopsScore = _evaluateUnusableTroops(); // How much troops on non bordering states
        if(threatScore < Mathf.Epsilon) // As it cannot be negative, we avoid dividing by zero (can happen for last gamestates where we own all countries, ggs!)
            score = float.MaxValue;
        else
            score = (continentScore + countryScore) / (threatScore + unusableTroopsScore);
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

    public Country getEquivalentCountry(State _s)
    {
        foreach(Country c in countries)
        {
            if(c.state == _s)
                return c;
        }
        throw new Exception("Could not find EquivalentCountry"); //return null
    }

    public Country getEquivalentCountry(Country _c)
    {
        return getEquivalentCountry(_c.state);
    }

    public static Country getRealCountryFromAlternativeState(List<Country> _realCountries, Country _alternate)
    {
        foreach(Country c in _realCountries)
        {
            if(c.state == _alternate.state)
                return c;
        }
        throw new Exception("Could not find real country from alternative"); //return null
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

    /// <summary>
    /// Performs deep and shallow copies as appropriate
    /// </summary>
    public void copyData(GameState _toCopy)
    {
        countries.Clear();
        _toCopy.countries.ForEach((c) => countries.Add(new(c))); // Deep copy
        countriesPerContinent = new(_toCopy.countriesPerContinent); // shallow copy is enough for this one
    }

    public override string ToString()
    {
        return actionToThisState + " -> Score: " + score;
    }


    private int _evaluateContinentScore()
    {
        int continentScore = 0;
        foreach(Continent c in countriesPerContinent.Keys)
        {
            if(countriesPerContinent[c] == c.stateIDs.Count)
                continentScore += c.score;
        }
        return continentScore;
    }

    private int _evaluateOwnedCountriesScore() { return (int)(countries.Count / 3.0f); }

    private float _evaluateThreatScore()
    {
        float threat = 0.0f;
        foreach(Country c in countries)
        {
            float localThreat = 0.0f;
            foreach(int stateID in c.state.neighbors)
            {
                Country realCountry = GameManager.Instance.getCountryByState(stateID);
                if(contains(realCountry))
                    continue; // Real country might not be ours, but in this projected state it is !
                localThreat += realCountry.troops - 0.9f; // Lone troop only count as 0.1 as it cannot attack at all (but still differentiate from allies at 0)
            }
            threat += localThreat / c.state.neighbors.Count;
        }
        return threat;
    }

    private int _evaluateUnusableTroops()
    {
        int maxAmount = 0; // The country with the most unusable troops does not count as we can move it in the move phase
        int unusableTroops = 0;
        foreach(Country c in countries)
        {
            bool nearEnemy = false;
            foreach(int stateID in c.state.neighbors)
            {
                Country realCountry = GameManager.Instance.getCountryByState(stateID);
                if(contains(realCountry) == false)
                {
                    nearEnemy = true;
                    break; // This country has an enemy as neighbor, its troops are useful
                }
            }

            if(nearEnemy || c.troops <= 1)
                continue;

            unusableTroops += c.troops-1;
            if(maxAmount < c.troops-1)
                maxAmount = c.troops-1;
        }
        return unusableTroops - maxAmount;
    }

    /// <summary>
    /// Recursively evaluate all child states, removing all states with a lower minax score.
    /// Called on the root state, this will reduce all tree to only one branch
    /// </summary>
    public void pruneChildren()
    {
        minmaxScore = score;
        if(children.Count == 0)
            return;

        int bestChildIndex = -1;
        for(int i = 0; i < children.Count; ++i)
        {
            children[i].pruneChildren();
            if(children[i].minmaxScore > minmaxScore)
            {
                bestChildIndex = i;
                minmaxScore = children[i].minmaxScore;
            }
        }

        if(bestChildIndex == -1)
        {
            children.Clear(); // Remove all, current state is better than subsequent states
        }
        else
        {
            for(int i = children.Count - 1; i >= 0; --i)
            {
                if(i != bestChildIndex)
                    children.RemoveAt(i); // Remove all except best state sequence
            }
        }
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
        _player.countries.ForEach((c) => rootGameState.countries.Add(new(c))); // Deep copy
        rootGameState.countriesPerContinent = new(_player.stateCountPerContinents); // shallow copy

        rootGameState.evaluate(); // base comparison for next moves. We won't do anything that lowers this score
    }

    public void generate(int maxDepth)
    {
        foreach(Country c in rootGameState.countries)
        {
            _generateCountryActionFromState(rootGameState, c, maxDepth); // start the recursion
        }
    }


    public Queue<GameAction> getBestMoveActions()
    {
        // Perform a kindda minmax, where each game state only keeps the best of its offsprings (or none), until only one branch remains
        rootGameState.pruneChildren();
        if(rootGameState.children.Count == 0)
            return null;
        Queue<GameAction> actions = new();
        GameState nextState = rootGameState.children[0];
        GD.Print("Gameplan:");
        while(true) // scary
        {
            GD.Print(nextState);
            actions.Enqueue(nextState.actionToThisState);
            if(nextState.children.Count == 0)
                break;
            if(nextState.children.Count > 1)
                throw new Exception("GameState has multiple children after pruning");
            nextState = nextState.children[0];
        }

        return actions;
    }

    private void _generateCountryActionFromState(GameState _state, Country _country, int maxDepth)
    {
        int newDepth = _state.depth + 1;
        if(newDepth > maxDepth)
        {
            //GD.Print("Leaving at depth " + newDepth);
            return; // Please no infinite loops
        }

        if(_state.countries.Contains(_country) == false)
            throw new Exception("GameState generation called on a Country not own in the GameState");

        if(_country.troops <= 3)
        {
            //GD.Print("Can't attack from " + _country + ": not enough troops " + _country.troops);
            return; // Can't attack with only one army (less should not happen but let's make sure)
        }

        // First fast loop to scan all neighbors at once, if all are allies or if any enemy has more troops than us, can't attack
        bool allAllied = true;
        foreach(int stateID in _country.state.neighbors)
        {
            Country neighboringCountry = GameManager.Instance.getCountryByState(stateID);
            if(_state.contains(neighboringCountry) == false)
            {
                allAllied = false;
                if(_country.troops < neighboringCountry.troops)
                    return; // We must defend, no attack can be launched from here
            }
        }

        if(allAllied)
        {
            //GD.Print("Can't attack from " + _country + ": no enemies in range");
            return; // Country is surrounded by allies, it cannot attack
        }
        
        // If we reach this, we know we have neighboring enemy.ies, all with less army than us
        foreach(int stateID in _country.state.neighbors)
        {
            Country neighboringCountry = GameManager.Instance.getCountryByState(stateID);
            if(_state.contains(neighboringCountry))
                continue; // Neighboring country already belong to the player in this state

            // ATTACK !
            GameState attackGameState = _generateAttackGameState(_state, _country, neighboringCountry);
            // Create 3 more gamestates for reinforcement: Move all, Move Half, Move None -> We could create as many states as we have troops(-1) but WOW that'd be too much
            // Move None is the same as the attackGameState, so actually create two new state, but start next iteration on those two AND attackState
            Country movementOriginCountry = attackGameState.getEquivalentCountry(_country);
            if(movementOriginCountry.troops > 1)
            {
                Country movementDestinationCountry = attackGameState.getEquivalentCountry(neighboringCountry);
                GameState moveAllState = _generateReinforceGameState(attackGameState, movementOriginCountry, movementDestinationCountry, movementOriginCountry.troops - 1);
                attackGameState.children.Add(moveAllState);
                if(movementOriginCountry.troops > 3) // Below 3, half would make us move nothing
                {
                    // We want to move troops to have approximately the same number of troops in both states.
                    // So the number to move is actually half the difference
                    int troopsToMove = (movementOriginCountry.troops - movementDestinationCountry.troops) / 2;
                    GameState moveHalfState = _generateReinforceGameState(attackGameState, movementOriginCountry, movementDestinationCountry, troopsToMove);
                    attackGameState.children.Add(moveHalfState);
                    // As we move half, both countries have similar amounts of troops, we can attack from both of them
                    _generateCountryActionFromState(moveHalfState, moveHalfState.getEquivalentCountry(movementOriginCountry), maxDepth);
                    _generateCountryActionFromState(moveHalfState, moveHalfState.getEquivalentCountry(movementDestinationCountry), maxDepth);
                }
                //else
                    //GD.Print("Not enough troops to generate MoveHalfState");
                // Move All state can only result in a new attack from the destination of the movement
                _generateCountryActionFromState(moveAllState, moveAllState.getEquivalentCountry(movementDestinationCountry), maxDepth);
            }
            //else
                //GD.Print("Not enough troops to generate MoveAllState");
            // Move None state (attackState) can only lead to an attack from origin, which is already the right one
            _generateCountryActionFromState(attackGameState, movementOriginCountry, maxDepth);
        }
    }

    private GameState _generateAttackGameState(GameState _parent, Country _from, Country _to)
    {
        _parent.children.Add(new(_parent.playerID, _parent.depth + 1, _parent,
            new GameAction()
            {
                from = _from,
                to = _to,
                type = GameAction.GameActionType.Attack
            }
        ));
        GameState attackGameState = _parent.children.Last();
        attackGameState.copyData(_parent);
        Country destCopy = new(_to);
        destCopy.troops = 3;
        attackGameState.add(destCopy);
        Country originCopy = attackGameState.getEquivalentCountry(_from);
        originCopy.troops = (int)Mathf.Floor(originCopy.troops - (_to.troops * 0.5f)); // Approximate to lose 50% of defender troops
        originCopy.troops -= 3; // Remove the 3 that moved to the conquered country

        //string indent = "";
        //for(int i = 0; i < _parent.depth+1; ++i)
        //    indent += "-";
        //GD.Print(indent + " Attack ! " + originCopy + " -> " + destCopy);

        attackGameState.evaluate();
        return attackGameState;
    }

    private GameState _generateReinforceGameState(GameState _parent, Country _from, Country _to, int _amount)
    {
        int troopsMoved = Mathf.Min(_from.troops - 1, _amount);
        _parent.children.Add(new (_parent.playerID, _parent.depth, _parent, // reinforce states don't increase depth, their directly follow attackStates that increments it
            new GameAction()
            {
                from = _from, to = _to,
                type = GameAction.GameActionType.Move,
                parameter = troopsMoved
            }
        ));
        GameState reinforceState = _parent.children.Last();
        reinforceState.copyData(_parent);
        Country fromCopy = reinforceState.getEquivalentCountry(_from);
        fromCopy.troops -= troopsMoved;
        Country destCopy = reinforceState.getEquivalentCountry(_to);
        destCopy.troops += troopsMoved;

        //string indent = "";
        //for(int i = 0; i < _parent.depth; ++i)
        //    indent += "-";
        //GD.Print(indent + " Reinforce ! " + fromCopy + " -> " + troopsMoved + " -> " + destCopy);

        reinforceState.evaluate();
        return reinforceState;
    }
}

public struct GameAction
{
    public GameAction() { type = GameActionType.None; from = null; to = null; parameter = 0; }

    public enum GameActionType{ Attack, Move, Deploy, None };
    public GameActionType type;
    public Country from;
    public Country to;
    public int parameter; // Used by Reinforce to tell how much to move

    public override string ToString()
    {
        string actionString = "";
        switch(type)
        {
            case GameActionType.Attack: actionString = "Attacks"; break;
            case GameActionType.Move: actionString = "Moves+" + parameter + " to"; break;
            case GameActionType.Deploy: actionString = "Deployement in"; break;
            case GameActionType.None: actionString = "RootState"; break;
        }
        actionString += " " + to;
        if(type != GameActionType.Deploy)
            actionString = from + " " + actionString;
        return actionString;
    }
}
