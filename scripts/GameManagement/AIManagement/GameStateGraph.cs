using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

/// <summary>
/// Stores a tree of possible game states by populating a root state
/// </summary>
public class GameStateGraph
{
    private GameState rootGameState = null; // RootGameState will have a null Parent and a None type ActionToThisState

    public void initialize(Player _player)
    {
        rootGameState = new(_player.id, 0, null, new GameAction() { type = GameAction.GameActionType.None });
        _player.countries.ForEach((c) => rootGameState.countries.Add(new(c))); // Deep copy
        rootGameState.countriesPerContinent = new(_player.stateCountPerContinents); // shallow copy

        rootGameState.evaluate(); // base comparison for next moves. We won't do anything that lowers this score
    }

    public void generate(int _deploymentTroops, List<int> _ignoredContinentsIndices, int _maxDepth)
    {
        int statesToEvaluate = rootGameState.getBorderCountries(_ignoredContinentsIndices).Count;
        // Generate all possible cases of reinforcements (ignoring countries surrounded by allies)
        if (_deploymentTroops > 0) // we can skip deployment generation by calling with zero troops
            _generateAllDeployActionsFromState(_deploymentTroops, _ignoredContinentsIndices);
        // Retreive all leaves from graph, to start attacks from each and every one of them (that'll be many)
        List<GameState> deploymentLeaves = _findLeavesFrom(rootGameState);

        foreach (GameState state in deploymentLeaves)
        {
            // Recursively create all attacks and movements available from this state
            _generateAllAttackActionsFromState(state, _maxDepth);
            // Generate free move at the end from each created leaves
            List<GameState> attackPhaseLastStates = _findLeavesFrom(state);
            foreach (GameState gs in attackPhaseLastStates)
            {
                GameAction action = _generateFreeMoveActionFromGameState(gs, _ignoredContinentsIndices);
                if (action.type == GameAction.GameActionType.None)
                    continue; // No movement is possible from this state
                GameState freeMoveGameState = _createCopyOfParentAsChild(gs);
                freeMoveGameState.actionToThisState = action;
                freeMoveGameState.getEquivalentCountry(action.from).troops -= action.parameter;
                freeMoveGameState.getEquivalentCountry(action.to).troops += action.parameter;
                freeMoveGameState.evaluate();
            }
            // We'll try here to prune the tree as we go, to avoid it taking up all the computer space
            // Here we only keep one attack path per possible deployment state (which is still quite a lot)
            // Perform a kindda minmax, where each game state only keeps the best of its offsprings (or none), until only one branch remains
            state.pruneChildren();
        }
    }

    public Queue<GameAction> getBestMoveActions()
    {
        // Special case of the minmax for the deployments move, as we're forced to do all of them
        rootGameState.pruneChildren(true);
        if (rootGameState.children.Count == 0)
        {
            return new();
        }
        Queue<GameAction> actions = new();
        GameState nextState = rootGameState.children[0];
        while (true) // scary
        {
            actions.Enqueue(nextState.actionToThisState);
            if (nextState.children.Count == 0)
                break;
            if (nextState.children.Count > 1)
                throw new Exception("GameState has multiple children after pruning");
            nextState = nextState.children[0];
        }
        return actions;
    }

    private void _generateAllDeployActionsFromState(int _deploymentTroops, List<int> _ignoredContinentsIndices)
    {
        List<Country> countriesToReinforce = _getBorderCountriesForDeployment(rootGameState, _ignoredContinentsIndices);
        GD.Print("N:" + _deploymentTroops + " P:" + countriesToReinforce.Count);
        // Check arbitrary thresholds to use extensive or simplified computation
        float usecStart = Time.GetTicksUsec();
        if (_deploymentTroops > 10 && countriesToReinforce.Count > 5)
        {
            _generateLimitedDeployActionsFromState(rootGameState, _deploymentTroops, countriesToReinforce);
        }
        else
        {
            _generateDeployActionsToCountries(rootGameState, countriesToReinforce, _deploymentTroops); // start complex deploy recursion
        }
        GD.Print("Generating deployment GameStates took " + ((Time.GetTicksUsec() - usecStart) * 0.000001) + " secs.");
    }

    private List<Country> _getBorderCountriesForDeployment(GameState _state, List<int> _ignoredContinentsIndices)
    {
        List<Country> allCountriesToScan = _state.getBorderCountries(_ignoredContinentsIndices);
        if(allCountriesToScan.Count == 0)
        {
            _ignoredContinentsIndices.Remove(_state.getContinentToFocus().id);
            allCountriesToScan = _state.getBorderCountries(_ignoredContinentsIndices);
        }
        return allCountriesToScan;
    }

    /// <summary>
    /// When computation time of exhaustive method gets too high, use this simplified version that does not check all possibilities
    /// Only checks all troops to a single country, and troops distributed in only two countries.
    /// </summary>
    private void _generateLimitedDeployActionsFromState(GameState _state, int _troops, List<Country> _countries)
    {
        for (int j = 0; j < _countries.Count; ++j)
        {
            // All troops to this country
            _createDeployGameState(_state, _countries[j], _troops);
            // Half to this country and half to every other
            int oddDeploymentComplement = _troops % 2 == 0 ? 0 : 1; // Make sure to deploy all troops, even for odd reinforcments
            GameState halfState = _createDeployGameState(_state, _countries[j], _troops / 2 + oddDeploymentComplement);
            for (int i = j + 1; i < _countries.Count; ++i)
            {
                _createDeployGameState(halfState, _countries[i], _troops / 2);
            }
        }
    }

    private void _generateDeployActionsToCountries(GameState _parentState, List<Country> _countries, int _troops)
    {
        if (_countries.Count < 1 || _troops == 0)
            return; // Cannot deploy in no countries, or cannot deploy no troops

        Country toDeployIn = _countries[0];
        if (_countries.Count == 1)
        {
            // If only one left, we're forced to put all in it
            _createDeployGameState(_parentState, toDeployIn, _troops);
            return; // No further deployment possible
        }

        List<Country> nextList = new(_countries);
        nextList.Remove(toDeployIn);
        for (int troopAddition = 0; troopAddition <= _troops; ++troopAddition)
        {
            GameState nextParentState = _parentState;
            if (troopAddition != 0) // Don't create a gamestate if we do nothing
            {
                nextParentState = _createDeployGameState(_parentState, toDeployIn, troopAddition);
            }
            _generateDeployActionsToCountries(nextParentState, nextList, _troops - troopAddition);
        }
    }

    private GameState _createDeployGameState(GameState _parent, Country _deployTo, int _troops)
    {
        GameState newState = _createCopyOfParentAsChild(_parent);
        Country reinforce = newState.getEquivalentCountry(_deployTo);
        newState.actionToThisState = new GameAction()
        {
            type = GameAction.GameActionType.Deploy,
            to = reinforce,
            parameter = _troops
        };
        reinforce.troops += _troops;
        newState.evaluate();
        return newState;
    }

    private void _generateAllAttackActionsFromState(GameState _state, int _maxDepth)
    {
        foreach (Country c in _state.countries)
        {
            _generateCountryAttackActionFromState(_state, c, _maxDepth); // No longer recursive, just does one attack layer and one movement layer
        }

        if (_state.children.Count > 0)
        {
            // Managed to create at least an attack, press on !
            foreach (GameState childState in _state.children)
            {
                _generateAllAttackActionsFromState(childState, _maxDepth);
                // Also generate attack from subsequent movement of this child attack state
                foreach (GameState childOfChild in childState.children)
                {
                    _generateAllAttackActionsFromState(childOfChild, _maxDepth);
                }
            }
        }
    }

    private void _generateCountryAttackActionFromState(GameState _state, Country _country, int maxDepth)
    {
        int newDepth = _state.depth + 1;
        if (newDepth > maxDepth)
        {
            //GD.Print("Leaving at depth " + newDepth);
            return; // Please no infinite loops
        }

        if (_state.countries.Contains(_country) == false)
            throw new Exception("Attack GameState generation called on a Country not owned in the GameState");

        if (_country.troops < 3)
        {
            //GD.Print("Can't attack from " + _country + ": not enough troops " + _country.troops);
            return; // Can't attack with only one army (less should not happen but let's make sure)
        }

        // First fast loop to scan all neighbors at once, if all are allies we can't attack
        bool allAllied = true;
        foreach (int stateID in _country.state.neighbors)
        {
            Country neighboringCountry = GameManager.Instance.getCountryByState(stateID);
            if (_state.contains(neighboringCountry) == false)
            {
                allAllied = false;
                break;
            }
        }

        if (allAllied)
        {
            //GD.Print("Can't attack from " + _country + ": no enemies in range");
            return; // Country is surrounded by allies, it cannot attack
        }

        // If we reach this, we know we have neighboring enemy.ies
        foreach (int stateID in _country.state.neighbors)
        {
            Country neighboringCountry = GameManager.Instance.getCountryByState(stateID);
            if (_state.contains(neighboringCountry))
                continue; // Neighboring country already belong to the player in this state
            if (neighboringCountry.troops > _country.troops)
                continue; // Neighboring country has more troops than us, very risky to attack

            // ATTACK !
            GameState attackGameState = _generateAttackGameState(_state, _country, neighboringCountry);
            // Create 3 more gamestates for reinforcement: Move all, Move Half, Move None -> We could create as many states as we have troops(-1) but WOW that'd be too much
            // Move None is the same as the attackGameState, so actually create two new state, but start next iteration on those two AND attackState
            Country movementOriginCountry = attackGameState.getEquivalentCountry(_country);
            Country movementDestinationCountry = attackGameState.getEquivalentCountry(neighboringCountry);
            if (movementOriginCountry.troops > 1)
            {
                GameState moveAllState = _generateReinforceGameState(attackGameState, movementOriginCountry, movementDestinationCountry, movementOriginCountry.troops - 1);
                attackGameState.children.Add(moveAllState);
                if (movementOriginCountry.troops > 3) // Below 3, half would make us move nothing
                {
                    // We want to move troops to have approximately the same number of troops in both states. (so not technically "move half")
                    // So the number to move is actually half the difference between the two
                    int troopsToMove = (movementOriginCountry.troops - movementDestinationCountry.troops) / 2;
                    GameState moveHalfState = _generateReinforceGameState(attackGameState, movementOriginCountry, movementDestinationCountry, troopsToMove);
                    attackGameState.children.Add(moveHalfState);
                }
            }
        }
    }

    private GameState _generateAttackGameState(GameState _parent, Country _from, Country _to)
    {
        GameState attackGameState = _createCopyOfParentAsChild(_parent);
        attackGameState.actionToThisState = new GameAction()
        {
            from = _from,
            to = _to,
            type = GameAction.GameActionType.Attack
        };
        Country originCopy = attackGameState.getEquivalentCountry(_from);
        originCopy.troops = (int)Mathf.Floor(originCopy.troops - (_to.troops * 0.5f)); // Approximate attacker to lose 50% of defender troops
        originCopy.troops = Mathf.Max(1, originCopy.troops);
        int troopMovement = Mathf.Min(3, originCopy.troops - 1); // cannot empty origin country
        originCopy.troops -= troopMovement; // Remove the troops that moved to the conquered country
        Country destCopy = new(_to);
        destCopy.troops = Mathf.Max(1, troopMovement); // cannot set destination to empty
        attackGameState.add(destCopy);

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
        GameState reinforceState = _createCopyOfParentAsChild(_parent);
        reinforceState.actionToThisState = new GameAction()
        {
            from = _from,
            to = _to,
            type = GameAction.GameActionType.Move,
            parameter = troopsMoved
        };
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

    private GameAction _generateFreeMoveActionFromGameState(GameState _state, List<int> _ignoredContinents)
    {
        List<CountryThreatPair> threats = _state.computeAndSortOwnCountriesThreatLevel();
        for (int i = 0; i < threats.Count; ++i)
        {
            Country toReinforce = threats[i].country;
            if (_ignoredContinents.Contains(toReinforce.continent.id))
                continue; // Country is ignored, it can help but not be reinforced
            List<Country> connectedAllies = _state.getAlliedCountriesAccessibleFrom(toReinforce);
            if (connectedAllies.Count <= 1) // connectedAllies will contain toReinforce
                continue; // Nobody can help them, skip
            // Find country with lowest threat and highest troops that can reinforce
            float threat = -1.0f;
            Country reinforcer = null;
            for (int helperID = threats.Count - 1; helperID > i; --helperID)
            {
                Country candidate = threats[helperID].country;
                if (connectedAllies.Contains(candidate) == false)
                    continue; // Not connected
                if (candidate.troops <= 1)
                    continue; // Does not have available troops
                bool select = true;
                if (reinforcer != null)
                {
                    // In this case we already have an available reinforcer.
                    // if threat is bigger, use the already selected state (it can be equal, but not lower as list is ordered)
                    if (Mathf.Abs(threat - threats[helperID].threatLevel) > Mathf.Epsilon)
                        break;
                    // When threats are similar, use the country with highest troops
                    select = candidate.troops > reinforcer.troops;
                }

                if (select)
                {
                    reinforcer = candidate;
                    threat = threats[helperID].threatLevel;
                }
            }

            if (reinforcer == null)
                continue; // Could not find any country to help this poor one, keep looking one we can help

            int troopMovement = _computeTroopMovementToEqualizeThreats(toReinforce, threats[i].threatLevel, reinforcer, threat);
            if (troopMovement <= 0)
                continue; // threat seems already at equilibrium, try to find another one to help

            // Get outta here after we found one
            return new() { type = GameAction.GameActionType.FreeMove, from = reinforcer, to = toReinforce, parameter = troopMovement };
        }

        return new() { type = GameAction.GameActionType.None };
    }

    private int _computeTroopMovementToEqualizeThreats(Country _toHelp, float _toHelpThreatLevel, Country _helper, float _helperThreatLevel)
    {
        // Threat is SumOfEnemies / Troops. Need to find a troop delta that makes threats equal:
        // SumOfEnemiesOfA / (troopsA - delta) = SumOfEnemiesOfB / (troopsB + delta)
        //
        //          troopsA - troopsB * SumOfEnemiesA/SumOfEnemiesB
        // delta = _________________________________________________
        //                  1 + SumOfEnemiesA/SumOfEnemiesB
        //
        // With A the helper and B the rescued

        double helperSumOfEnemies = _helperThreatLevel * _helper.troops;
        double rescuedSumOfEnemies = _toHelpThreatLevel * _toHelp.troops;
        double enemiesRatio = helperSumOfEnemies / rescuedSumOfEnemies;
        double delta = (_helper.troops - _toHelp.troops * enemiesRatio) / (1 + enemiesRatio);
        return Mathf.Min(Mathf.RoundToInt(delta), _helper.troops - 1);
    }

    /// <summary>
    /// Creates a new state as child of the given parent, with copied data
    /// </summary>
    private GameState _createCopyOfParentAsChild(GameState _parent)
    {
        _parent.children.Add(new(_parent.playerID, _parent.depth + 1, _parent, new()));
        GameState childState = _parent.children.Last();
        childState.copyData(_parent);
        return childState;
    }

    private List<GameState> _findLeavesFrom(GameState _origin)
    {
        List<GameState> leaves = new();
        Queue<GameState> statesToScan = new();
        statesToScan.Enqueue(_origin);
        while (statesToScan.Count > 0) // at least something non-recursive in this hell
        {
            GameState scanning = statesToScan.Dequeue();
            if (scanning.children.Count == 0) // This is a leaf !
            {
                leaves.Add(scanning);
                continue;
            }
            // Not a leaf :(
            scanning.children.ForEach((s) => statesToScan.Enqueue(s));
        }
        return leaves;
    }
}