using Godot;
using System;
using System.Collections.Generic;

public class ComputerAI
{
    // Computer AI has to defend its territories and conquer more
    // Defense: 
    // - Add REINFORCEMENT to border countries, until a safe amount compared to threatening country is reached
    // - CONQUER countries to minimise number of border states
    // - REGROUP armies from safe countries to risky ones
    // Offense:
    // - Only ATTACK while worth
    // - Mass armies and Attack weak countries
    // - Focus on the conquest of continents to boost troops
    // Most likely have to add exceptions in the first turn as countries are scattered, and we don't want the AI to split its efforts
    //   -> Sacrifice countries to conquer continents

    public Player player {get; private set;}

    public ComputerAI(Player _player)
    {
        player = _player;
    }

    private double dtAccumulator = 0.0f;
    private double actionCooldown = 1.5f; // Seconds per action

    private Continent focusedContinent = null;
    private List<CountryThreatPair> threats;

    private Queue<GameAction> attackGamePlan = new();

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

    private void _processFirstDeploy(){} // NYI

    private void _processDeploy()
    {
        if(threats == null)
            threats = _computeAndSortOwnCountriesThreatLevel(); // Only compute it the first time we go in here per turn (made null at the end of movement)
        Country toReinforce = threats[0].country;
        GameManager.Instance.askReinforce(toReinforce);
        _updateAndInsertThreat(threats, threats[0]);
    }

    private void _processAttack()
    {
        if(attackGamePlan == null || attackGamePlan.Count == 0)
        {
            GameStateGraph graph = new();
            graph.initialize(player);
            graph.generate(10);
            attackGamePlan = graph.getBestMoveActions();
        }
        
        if(attackGamePlan == null || attackGamePlan.Count == 0) // If still null after tree evaluation, nothing we can do will further improve our situtation, end attack phase
        {
            GameManager.Instance.triggerNextPhase();
            return;
        }

        // If we do have a valid attack plan, execute it step by step
        GameAction action = attackGamePlan.Peek(); // attacks take time, we won't dequeue each time
        // Interpret action
        GD.Print("IA Interpreting action: " + action);
        switch(action.type)
        {
            case GameAction.GameActionType.None: throw new Exception("Game action of type None ended up in the attack plan");
            case GameAction.GameActionType.Attack:
            {
                // Attack until conquered or too many losses
                Country actualOrigin = GameState.getRealCountryFromAlternativeState(player.countries, action.from);
                Country actualDestination = action.to; // Attacked country is already the right one, as it does not belong to the AI yet
                // Check if we're in a good state to attack
                if(actualOrigin.troops < actualDestination.troops * 1.1f) // Cannot attack with less than 10% more troops
                {
                    // We took too many losses, abort attack
                    attackGamePlan = null;
                    return;
                }
                // Process the attack
                int troopsMovement = CombatManager.Instance.startCombat(actualOrigin, actualDestination);
                if(troopsMovement != 0)
                {
                     // Fight is won -> Manage initial troops movement and ownership transfer
                    GameManager.Instance.countryConquest(actualOrigin, actualDestination, troopsMovement);
                    // End GameAction, as country has been conquered
                    attackGamePlan.Dequeue();
                }
                else
                    GameManager.Instance.updateCountryTroopsDisplay(actualOrigin, actualDestination);
                break;
            }
            case GameAction.GameActionType.Reinforce:
            {
                Country actualOrigin = GameState.getRealCountryFromAlternativeState(player.countries, action.from);
                Country actualDestination = GameState.getRealCountryFromAlternativeState(player.countries, action.to);
                GameManager.Instance.askMovement(actualOrigin, actualDestination, action.parameter);
                attackGamePlan.Dequeue();
                break;
            }
        }
    }

    private void _processReinforce()
    {
        bool didReinforce = false;
        threats = _computeAndSortOwnCountriesThreatLevel(); // Recompute all threats after our turn, as a lot might have changed
        for(int i = 0; i < threats.Count; ++i)
        {
            Country toReinforce = threats[i].country;
            List<Country> connectedAllies = GameManager.Instance.getAlliedCountriesAccessibleFrom(toReinforce);
            if(connectedAllies.Count <= 1) // connectedAllies will contain toReinforce
                continue; // Nobody can help them, skip
            //Find country with lowest threat and highest troops that can reinforce
            float threat = -1.0f;
            Country reinforcer = null;
            for(int helperID = threats.Count - 1; helperID > i; --helperID)
            {
                Country candidate = threats[helperID].country;
                if(connectedAllies.Contains(candidate) == false)
                    continue; // Not connected
                if(candidate.troops <= 1)
                    continue; // Does not have available troops
                bool select = true;
                if(reinforcer != null)
                {
                    // In this case we already have an available reinforcer.
                    // if threat is bigger, use the already selected state (it can be equal, but not lower as list is ordered)
                    if(Mathf.Abs(threat - threats[helperID].threatLevel) > Mathf.Epsilon)
                        break;
                    // When threats are similar, use the country with highest troops
                    select = candidate.troops > reinforcer.troops;
                }

                if(select)
                {
                    reinforcer = candidate;
                    threat = threats[helperID].threatLevel;
                }
            }

            if(reinforcer == null)
                continue; // Could not find any country to help this poor one, keep looking one we can help

            int troopMovement = _computeTroopMovementToEqualizeThreats(toReinforce, threats[i].threatLevel, reinforcer, threat);
            GD.Print(reinforcer + " sending " + troopMovement + " to " + toReinforce);
            if(troopMovement <= 0)
                continue; // threat seems already at equilibrium, try to find another one to help

            GameManager.Instance.askMovement(reinforcer, toReinforce, troopMovement);
            didReinforce = true;
            break; // We only have one reinforcement, get outta here after it's done
        }

        threats = null; // reset all threats, as they will likely change a lot with all other players turns
        if(!didReinforce)
            GameManager.Instance.triggerNextPhase(); // Skip turn if we could not reinforce anyone, gameManager skips atomaticaly when last move is asked
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

        double helperSumOfEnemies = _helperThreatLevel / _helper.troops;
        double rescuedSumOfEnemies = _toHelpThreatLevel / _toHelp.troops;
        double enemiesRatio = helperSumOfEnemies / rescuedSumOfEnemies;
        double delta = (_helper.troops - _toHelp.troops * enemiesRatio) / (1+enemiesRatio);
        return Mathf.Min(Mathf.RoundToInt(delta), _helper.troops - 1);
    }

    private Continent _getFocusedContinent()
    {
        float highestNonFullRatio = 0.0f;
        Continent priority = null;
        foreach(Continent c in player.stateCountPerContinents.Keys)
        {
            if(player.stateCountPerContinents[c] == c.stateIDs.Count)
                continue; // Player has all states of this continent, can ignore
            float ratio = player.stateCountPerContinents[c] * 1.0f / c.stateIDs.Count;
            if(priority == null || ratio > highestNonFullRatio)
            {
                highestNonFullRatio = ratio;
                priority = c;
            }
        }

        // Find new neigboring continent, preferably find less heavily defended
        priority ??= _findNeighborLessDefendedContinent();
        // This does the same thing at the commented if below, not sure if i like it
        //if(priority == null)
        //{
        //    priority = _findNeighborLessDefendedContinent();
        //}
        return priority;
    }

    private Continent _findNeighborLessDefendedContinent()
    {
        Dictionary<Continent, int> defensesPerContinents = new();
        foreach(Continent c in player.stateCountPerContinents.Keys)
        {
            foreach(int continentID in c.neighborsContinentIDs)
            {
                Continent otherContinent = MapManager.Instance.getContinentByID(continentID);
                if(defensesPerContinents.ContainsKey(otherContinent))
                    continue; // already treated
                // Sum up defenses
                defensesPerContinents.Add(otherContinent, 0);
                foreach(int stateID in otherContinent.stateIDs)
                {
                    Country country = GameManager.Instance.getCountryByState(stateID);
                    defensesPerContinents[otherContinent] += country.troops;
                }
            }
        }
        // Find less defended
        Continent priority = null;
        int lowestDefense = -1;
        foreach(Continent c in defensesPerContinents.Keys)
        {
            if(priority == null || lowestDefense > defensesPerContinents[c])
            {
                priority = c;
                lowestDefense = defensesPerContinents[c];
            }
        }
        return priority;
    }

    public struct CountryThreatPair
    {
        public Country country;
        public float threatLevel;
        public static int sort(CountryThreatPair _a, CountryThreatPair _b)
        {
            if(_a.threatLevel > _b.threatLevel) return -1; // descending order
            if(_b.threatLevel > _a.threatLevel) return 1;
            return 0;
        }
    }

    /// <summary>
    /// When we know only one threat needs updating in an already sorted list, just remove old reinsert the new one in order
    /// </summary>
    private void _updateAndInsertThreat(List<CountryThreatPair> _threats, CountryThreatPair _toUpdate)
    {
        // Remove related entry
        _threats.Remove(_toUpdate);
        // compute new value
        CountryThreatPair newThreat = computeCountryThreat(_toUpdate.country);
        // Reinsert it while respecting the order (greatest to smallest threatLevels)
        for(int i = 0; i < _threats.Count; ++i)
        {
            if(_threats[i].threatLevel < newThreat.threatLevel)
            {
                _threats.Insert(i, newThreat);
                return;
            }
        }
        // If we didn't insert just add it to the end
        _threats.Add(newThreat);
    }

    private List<CountryThreatPair> _computeAndSortOwnCountriesThreatLevel()
    {
        List<CountryThreatPair> threats = new();
        player.countries.ForEach((country) => threats.Add(computeCountryThreat(country)));
        threats.Sort(CountryThreatPair.sort);
        return threats;
    }

    public static CountryThreatPair computeCountryThreat(Country _c)
    {
        CountryThreatPair threat = new(){ country = _c, threatLevel = 0.0f };
        State s = _c.state;
        foreach(int stateID in s.neighbors)
        {
            Country country = GameManager.Instance.getCountryByState(stateID);
            if(country.playerID == _c.playerID)
                continue; // Country is friendly, zero threat added
            threat.threatLevel += country.troops;
        }
        
        if(_c.troops == 0)
            throw new Exception("Country has zero troops");
        threat.threatLevel /= _c.troops;

        return threat;
    }
}
