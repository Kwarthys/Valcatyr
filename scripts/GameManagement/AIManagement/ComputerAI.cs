using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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
    private const double ACTION_COOLDOWN = 0.5f; // Seconds per action
    private const double SLOW_ACTION_COOLDOWN = 3.0f;
    private bool slowDown = false; // Used to track when to apply a longer delay, like when changing attack front

    private Continent focusedContinent = null;
    private List<CountryThreatPair> threats;

    private Queue<GameAction> attackGamePlan = new();

    private GameAction pooledAction = new();


    public void processTurn(double _dt)
    {
        dtAccumulator += _dt;
        double cooldown = slowDown ? SLOW_ACTION_COOLDOWN : ACTION_COOLDOWN;
        if(dtAccumulator < cooldown)
            return; // skip to let player understand what's happening, MAYBE Option to make instant, or at least quicker/slower
        // Do play
        dtAccumulator -= cooldown;
        switch(GameManager.Instance.gameState)
        {
            case GameManager.GameState.Init: break; // should not be here
            case GameManager.GameState.FirstDeploy: _processFirstDeploy(); break;
            case GameManager.GameState.Deploy: _processDeploy(); break;
            case GameManager.GameState.Attack: _processAttack(); break;
            case GameManager.GameState.Reinforce: _processReinforce(); break;
        }
    }

    private void _processFirstDeploy()
    {
        if(pooledAction.type == GameAction.GameActionType.Deploy)
        {
            _executePooledDeployGameAction(false);
            pooledAction.type = GameAction.GameActionType.None; // Reset pooled action
            slowDown = false;
            return;
        }
        
        // Else Generate Deploy action
        focusedContinent = _getFocusedContinent();
        Country toReinforce = null;
        float maxThreat = 0.0f;
        foreach(Country c in player.countries)
        {
            if(c.continent.id != focusedContinent.id)
                continue;
            float threat = computeCountryThreat(c).threatLevel;
            if(toReinforce == null || maxThreat < threat)
            {
                toReinforce = c;
                maxThreat = threat;
            }
        }
        if(toReinforce == null)
        {
            throw new Exception("ProcessFirstDeployment of ComputerAI did not manage to find a country to reinforce");
        }
        pooledAction = new GameAction(){ type = GameAction.GameActionType.Deploy, to = toReinforce };

        slowDown = true;
        GameManager.Instance.askSelection( new(){ selected = toReinforce } );
        AIVisualMarkerManager.Instance.moveTo(toReinforce.state.barycenter);

    }

    private void _processDeploy()
    {
        if(pooledAction.type == GameAction.GameActionType.Deploy) // Else it means it's the first time we deploy, generate a GameAction without executing it
        {
            // execute pooled GameAction
            _executePooledDeployGameAction();
        }

        if(GameManager.Instance.gameState != GameManager.GameState.Deploy)
        {
            // We dropped our last reinforcement, gamemanager switched phase
            pooledAction.type = GameAction.GameActionType.None;
            GameManager.Instance.resetSelection();
            slowDown = false; // slow down will happen of first loop of attack phase
            return; // Don't generate another deployment order as we're done here
        }

        GameAction newAction = _generateDeployGameAction();
        if(pooledAction.type != GameAction.GameActionType.Deploy || pooledAction.to != newAction.to)
        {
            // Only reset bigger timer and update selection if deployment target changes
            slowDown = true;
            GameManager.Instance.askSelection(new(){ selected = newAction.to }); // display selection for human understanding
            AIVisualMarkerManager.Instance.moveTo(newAction.to.state.barycenter); // move marker to guide human to the right place (if he wants to follow)
        }
        else
            slowDown = false; // go fast to next deployement

        pooledAction = newAction;
    }

    private GameAction _generateDeployGameAction()
    {
        if(threats == null)
            threats = _computeAndSortOwnCountriesThreatLevel(); // Only compute it the first time we go in here per turn (made null at the end of movement)
        return new(){ to = threats[0].country, parameter = 1, type = GameAction.GameActionType.Deploy };
    }

    private void _executePooledDeployGameAction(bool _updateThreats = true)
    {
        if(pooledAction.type != GameAction.GameActionType.Deploy)
            throw new Exception("Wrong GameAction type given to _executePooledDeployGameAction");
        GameManager.Instance.askReinforce(pooledAction.to);

        if(_updateThreats == false)
            return;

        for(int i = 0; i < threats.Count; ++i)
        {
            if(threats[i].country == pooledAction.to)
            {
                _updateAndInsertThreat(threats, threats[i]);
                return;
            }
        }
    }

    private void _processAttack()
    {
        bool newPlan = false;
        if(attackGamePlan == null || attackGamePlan.Count == 0)
        {
            GameStateGraph graph = new();
            graph.initialize(player);
            graph.generate(10);
            attackGamePlan = graph.getBestMoveActions();
            newPlan = true;
        }
        
        if(attackGamePlan == null || attackGamePlan.Count == 0) // If still null after tree evaluation, nothing we can do will further improve our situtation, end attack phase
        {
            GameManager.Instance.triggerNextPhase();
            return;
        }

        bool planUpdated = false;
        if(newPlan == false)
            planUpdated = _executeAttackPlan(); // Don't execute new plan instantly to let player reach sector as will

        // Manage visual feedback for human understanding of WTF is happening
        if(attackGamePlan.Count > 0)
        {
            if(planUpdated || newPlan)
            {
                GameManager.SelectionData selection = new();
                GameAction nextMove = attackGamePlan.Peek();
                if(nextMove.type == GameAction.GameActionType.Move)
                {
                    planUpdated = false; // Don't wait for post-combat free move
                    selection.allies = new(){ GameState.getRealCountryFromAlternativeState(player.countries, nextMove.to) };
                }
                else if(nextMove.type == GameAction.GameActionType.Attack)
                {
                    AIVisualMarkerManager.Instance.moveTo(nextMove.to.state.barycenter);
                    selection.enemies = new(){ nextMove.to };
                }
                selection.selected = GameState.getRealCountryFromAlternativeState(player.countries, nextMove.from);
                GameManager.Instance.askSelection(selection); // If empty, it will clear selection
            }
        }
        else
        {
            planUpdated = false; // don't wait here for last move, wait in first action of next plan or first pass of next phase
            GameManager.Instance.resetSelection();
        }

        slowDown = planUpdated || newPlan; // Wait if plan is new or if new attack action is selected
    }

    private bool _executeAttackPlan()
    {
        bool planUpdate = false;
        if(attackGamePlan.Count == 0)
            throw new Exception("Trying to execute an empty attack game plan");
        // If we do have a valid attack plan, execute it step by step
        GameAction action = attackGamePlan.Peek(); // attacks take time, we won't dequeue each time
        // Interpret action
        GD.Print("IA Interpreting action: " + action);
        Country actualOrigin = null;
        Country actualDestination = null;
        switch(action.type)
        {
            case GameAction.GameActionType.None: throw new Exception("Game action of type None ended up in the attack plan");
            case GameAction.GameActionType.Deploy: throw new Exception("Game action of type Reinforce ended up in the attack plan");
            case GameAction.GameActionType.Attack:
            {
                // Attack until conquered or too many losses
                actualOrigin = GameState.getRealCountryFromAlternativeState(player.countries, action.from);
                actualDestination = action.to; // Attacked country is already the right one, as it does not belong to the AI yet
                // Check if we're in a good state to attack
                if(actualOrigin.troops < actualDestination.troops * 1.1f) // Cannot attack with less than 10% more troops
                {
                    // We took too many losses, abort attack
                    attackGamePlan.Clear();
                    break;
                }
                // Process the attack
                int troopsMovement = CombatManager.Instance.startCombat(actualOrigin, actualDestination);
                if(troopsMovement != 0)
                {
                     // Fight is won -> Manage initial troops movement and ownership transfer
                    GameManager.Instance.countryConquest(actualOrigin, actualDestination, troopsMovement);
                    // End GameAction, as country has been conquered
                    attackGamePlan.Dequeue();
                    planUpdate = true;
                }
                else
                    GameManager.Instance.updateCountryTroopsDisplay(actualOrigin, actualDestination);
                break;
            }
            case GameAction.GameActionType.Move:
            {
                actualOrigin = GameState.getRealCountryFromAlternativeState(player.countries, action.from);
                actualDestination = GameState.getRealCountryFromAlternativeState(player.countries, action.to);
                GameManager.Instance.askMovement(actualOrigin, actualDestination, action.parameter);
                attackGamePlan.Dequeue();
                planUpdate = true;
                break;
            }
        }
        return planUpdate;
    }

    private void _processReinforce()
    {
        if(pooledAction.type != GameAction.GameActionType.Move)
        {
            // First time here, generate action, update selection and move AI Marker
            GameAction freeMoveAction = _generateFreeMoveAction();
            if(freeMoveAction.type == GameAction.GameActionType.None)
            {
                // Could not reinforce, skip
                pooledAction.type = GameAction.GameActionType.None;
                GameManager.Instance.triggerNextPhase(); // Skip turn if we could not reinforce anyone, gameManager skips atomaticaly when last move is asked
            }
            slowDown = true;
            pooledAction = freeMoveAction;
            AIVisualMarkerManager.Instance.moveTo(freeMoveAction.from.state.barycenter);
            GameManager.Instance.askSelection(new(){selected = freeMoveAction.from, allies = new(){freeMoveAction.to}});
        }
        else
        {
            // It's the second time we go here, we can execute the movement
            GameManager.Instance.askMovement(pooledAction.from, pooledAction.to, pooledAction.parameter);
            threats = null; // reset all threats, as they will likely change a lot with all other players turns
            AIVisualMarkerManager.Instance.moveTo(pooledAction.to.state.barycenter);
            pooledAction.type = GameAction.GameActionType.None;
            GameManager.Instance.resetSelection();
            slowDown = false;
        }
    }

    private GameAction _generateFreeMoveAction()
    {
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

            // Get outta here after we found one
            return new(){ type = GameAction.GameActionType.Move, from = reinforcer, to = toReinforce, parameter = troopMovement };
        }

        return new(){ type = GameAction.GameActionType.None };
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
