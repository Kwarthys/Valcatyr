using Godot;
using System;
using System.Collections.Generic;
using System.Threading;

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

    public Player player { get; private set; }

    public ComputerAI(Player _player) { player = _player; }

    private double dtAccumulator = 0.0f;
    private const double ACTION_COOLDOWN = 0.5f; // Seconds per action
    private const double SLOW_ACTION_COOLDOWN = 1.0f;
    private bool slowDown = false; // Used to track when to apply a longer delay, like when changing attack front

    private Queue<GameAction> turnGamePlan;

    private GameAction pooledAction = new();

    public AICharacteristicsData personality = AICharacteristics.neutral;

    // At the start of the game, many countries will be spread and not worth defending, add their continent here to avoid computing them
    private List<int> ignoredContinents = new();

    // Threading synchronization variables
    private volatile bool generatingGamePlan = false;
    private volatile bool gamePlanGenerationDone = false; // These two are kindda locks
    private Thread workerThread;
    private Queue<GameAction> generatedGamePlan;

    public void initializeStrategy()
    {
        foreach (Country c in player.countries)
        {
            if (ignoredContinents.Contains(c.continent.id) == false)
                ignoredContinents.Add(c.continent.id); // At start we ingore ALL of them, then rehabilitates them in the course of the game
        }

        foreach (Continent controlledContinent in getAllControlledContinents())
        {
            _rehabilitateCountriesOf(controlledContinent); // Won't likely happen on first round
        }
        _rehabilitateCountriesOf(_findFirstContinentToFocus());
    }

    public void processTurn(double _dt)
    {
        dtAccumulator += _dt;
        double cooldown = slowDown ? SLOW_ACTION_COOLDOWN : ACTION_COOLDOWN;
        if (dtAccumulator < cooldown)
            return; // skip to let player understand what's happening, MAYBE Option to make instant, or at least quicker/slower
        // Do play
        dtAccumulator -= cooldown;
        switch (GameManager.Instance.gamePhase)
        {
            case GameManager.GamePhase.Init: break; // should not be here
            case GameManager.GamePhase.FirstDeploy: _processFirstDeploy(); break;
            case GameManager.GamePhase.Deploy:
            case GameManager.GamePhase.Attack:
            case GameManager.GamePhase.Reinforce: _processGamePlan(); break;
        }
    }

    private void _processGamePlan()
    {
        if (turnGamePlan == null)
        {
            // Generate a new Plan if we don't have one already cooking
            if (generatingGamePlan == false)
            {
                _generateNewGamePlan();
                // Display cooking widget
                AICookingWidgetManager.showWidget();
            }

            if (gamePlanGenerationDone) // Thread finished cooking us the game plan
            {
                // Retreive computation results
                turnGamePlan = generatedGamePlan;
                // Reset everything
                generatedGamePlan = null;
                workerThread = null;
                generatingGamePlan = false;
                gamePlanGenerationDone = false;
                // Reset cooking widget
                AICookingWidgetManager.hideWidget();
            }
        }

        if (turnGamePlan == null)
        {
            return; // generation most probably not done yet
        }

        if (_isActionValid(pooledAction.type)) // Else it means we need to dequeue a new one
        {
            // execute pooled GameAction
            _executePooledAction();
            slowDown = false;

            if (turnGamePlan != null && pooledAction.type == GameAction.GameActionType.None && turnGamePlan.Count == 0)
            {
                // We finished the execution of the last action of our gameplan, generate a new one next frame by reseting it
                _resetGamePlan();
            }
        }
        else if (pooledAction.type == GameAction.GameActionType.None) // Action has been reset
        {
            GameAction nextAction = new();
            // We may not have actions left, while still being the active player
            if (turnGamePlan.Count == 0)
            {
                if (GameManager.Instance.gamePhase == GameManager.GamePhase.Attack)
                {
                    GameManager.Instance.triggerNextPhase(); // No attacks will further improve our situation, go to last move phase
                    nextAction = GameStateGraph.generateLastMove(player, ignoredContinents);
                    if (nextAction.type == GameAction.GameActionType.None)
                    {
                        // Could not generate a freeMove, skip turn
                        GameManager.Instance.triggerNextPhase();
                        _resetGamePlan();
                        return;
                    }
                }
                else
                    throw new Exception("ComputerAI Turn not in AttackPhase");
            }
            else
                nextAction = turnGamePlan.Dequeue();
            pooledAction = nextAction;
            _updateSelectionForPooledAction(); // display selection for human understanding
            AIVisualMarkerManager.Instance.moveTo(nextAction.to.state.barycenter); // move marker to guide human to the right place (if he wants to follow)
            slowDown = true;
        }
        else
            throw new Exception("Wrong pooledAction type in ProcessPlan " + GameManager.Instance.gamePhase + " -- " + pooledAction.type);
    }

    private void _generateNewGamePlan()
    {
        generatingGamePlan = true;
        pooledAction.reset();

        workerThread = new Thread(new ThreadStart(_blockingGamePlanGeneration));
        workerThread.Start();
    }

    /// <summary>
    /// Should not be called on the mainthread, can take up to dozens of seconds
    /// </summary>
    private void _blockingGamePlanGeneration()
    {
        int reinforcementToCompute = GameManager.Instance.gamePhase == GameManager.GamePhase.Deploy ? GameManager.Instance.reinforcementLeft : 0;
        generatedGamePlan = GameStateGraph.generate(player, reinforcementToCompute, ignoredContinents, 7);

        gamePlanGenerationDone = true;
    }

    private bool _isActionValid(GameAction.GameActionType _type)
    {
        switch (GameManager.Instance.gamePhase)
        {
            case GameManager.GamePhase.Deploy: return _type == GameAction.GameActionType.Deploy;
            case GameManager.GamePhase.Attack:
                return _type == GameAction.GameActionType.Attack
                    || _type == GameAction.GameActionType.Equalize
                    || _type == GameAction.GameActionType.MoveAll;
            case GameManager.GamePhase.Reinforce: return _type == GameAction.GameActionType.Move;
            default:
                return false;
        }
    }

    private void _executePooledAction()
    {
        switch (pooledAction.type)
        {
            case GameAction.GameActionType.Deploy: _executePooledDeployGameAction(); return;
            case GameAction.GameActionType.Attack: _executeAttackGameAction(); return;
            case GameAction.GameActionType.Move:
            case GameAction.GameActionType.MoveAll:
            case GameAction.GameActionType.Equalize: _executeMovementGameAction(); return;
            default:
                return;
        }
    }

    private void _processFirstDeploy()
    {
        // Execute pooled action if already there (was added here in the previous call of this method)
        if (pooledAction.type == GameAction.GameActionType.Deploy)
        {
            _executePooledDeployGameAction();
            pooledAction.type = GameAction.GameActionType.None; // Reset pooled action
            slowDown = false;
            return;
        }
        // Else Generate Deploy action
        Country toReinforce = null;
        float maxThreat = 0.0f;
        foreach (Country c in player.countries)
        {
            if (ignoredContinents.Contains(c.continent.id))
                continue;
            float threat = _computeCountryThreat(c);
            if (toReinforce == null || maxThreat < threat)
            {
                toReinforce = c;
                maxThreat = threat;
            }
        }
        if (toReinforce == null)
        {
            throw new Exception("ProcessFirstDeployment of ComputerAI did not manage to find a country to reinforce");
        }
        pooledAction = new GameAction() { type = GameAction.GameActionType.Deploy, to = toReinforce };

        slowDown = true;
        GameManager.Instance.askSelection(new() { selected = toReinforce });
        AIVisualMarkerManager.Instance.moveTo(toReinforce.state.barycenter);
    }

    private float _computeCountryThreat(Country _c)
    {
        float threat = 0.0f;
        foreach (int stateID in _c.state.neighbors)
        {
            Country country = GameManager.Instance.getCountryByState(stateID);
            if (country.playerID == _c.playerID)
                continue; // Country is friendly, zero threat added
            threat += country.troops - 0.9f; // Lone troop only count as 0.1 as it cannot attack at all (but still differentiate from allies at 0)
        }

        if (_c.troops == 0)
            throw new Exception("Country has zero troops");
        threat /= _c.troops;
        return threat;
    }

    private void _updateSelectionForPooledAction()
    {
        Country from = null;
        if(pooledAction.from != null) // Won't need it if null
            from = GameState.getRealCountryFromAlternativeState(player.countries, pooledAction.from);

        Country to = null;
        if (pooledAction.to != null && pooledAction.type != GameAction.GameActionType.Attack) // Won't need it if null (and Attack use the real country as a target)
            to = GameState.getRealCountryFromAlternativeState(player.countries, pooledAction.to);

        switch (pooledAction.type)
        {
            case GameAction.GameActionType.Deploy:
                GameManager.Instance.askSelection(new() { selected = to });
                break;
            case GameAction.GameActionType.Attack:
                GameManager.Instance.askSelection(new() { selected = from, enemies = new() { pooledAction.to } });
                break;
            case GameAction.GameActionType.Move:
            case GameAction.GameActionType.MoveAll:
            case GameAction.GameActionType.Equalize:
                GameManager.Instance.askSelection(new() { selected = from, allies = new() { to } });
                break;
            default:
                throw new Exception("Wrong pooled action type in Update Selection");
        }
    }

    private void _executePooledDeployGameAction()
    {
        if (pooledAction.type != GameAction.GameActionType.Deploy)
            throw new Exception("Wrong GameAction type given to _executePooledDeployGameAction");

        if (player.countries.Contains(pooledAction.to) == false)
        {
            // In this case target is an alternative state of this country, from AI's thinking, just replace it with the real one
            pooledAction.to = GameState.getRealCountryFromAlternativeState(player.countries, pooledAction.to);
        }

        int amount = 1;
        if (pooledAction.parameter >= 10)
            amount = 10; // Reinforce by 10 when dropping a lot of troops to save a HELL LOT OF TIME

        pooledAction.parameter -= amount;
        if (pooledAction.parameter <= 0)
            pooledAction.reset(); // We finished this Deploy action

        GameManager.Instance.askReinforce(pooledAction.to, amount);
    }

    private void _executeAttackGameAction()
    {
        if (pooledAction.type != GameAction.GameActionType.Attack)
            throw new Exception("Wrong type of action to execute in ExecuteAttack");

        // Attack until conquered or too many losses
        Country actualOrigin = GameState.getRealCountryFromAlternativeState(player.countries, pooledAction.from);
        Country actualDestination = pooledAction.to; // Attacked country is already the right one, as it does not belong to the AI yet
        // Check if we're in a good state to attack. Cannot attack with:
        //  less troops than anticipated                    one                     or less than opponent troops
        if (actualOrigin.troops < pooledAction.parameter || actualOrigin.troops <= 1 || actualOrigin.troops < actualDestination.troops)
        {
            // We took too many losses, abort attack
            _resetGamePlan();
            pooledAction.reset();
            return;
        }
        // Process the attack
        int troopsMovement = CombatManager.Instance.startCombat(actualOrigin, actualDestination);
        if (troopsMovement != 0)
        {
            // Fight is won -> Manage initial troops movement and ownership transfer
            GameManager.Instance.countryConquest(actualOrigin, actualDestination, troopsMovement);
            // End GameAction, as country has been conquered
            pooledAction.reset();
            // If conquested country's continent was ignored, rehabilitates it for stronger offense
            if (ignoredContinents.Contains(actualDestination.continent.id))
                _rehabilitateCountriesOf(actualDestination.continent);
        }
        else // just update the displayed pawns
            GameManager.Instance.updateCountryTroopsDisplay(actualOrigin, actualDestination);
        return;
    }

    private void _executeMovementGameAction()
    {
        if (pooledAction.type != GameAction.GameActionType.MoveAll
        && pooledAction.type != GameAction.GameActionType.Equalize
        && pooledAction.type != GameAction.GameActionType.Move)
            throw new Exception("Wrong type of action to execute in ExecuteMove");
        Country actualOrigin = GameState.getRealCountryFromAlternativeState(player.countries, pooledAction.from);
        Country actualDestination = GameState.getRealCountryFromAlternativeState(player.countries, pooledAction.to);

        int amount = 0;
        switch (pooledAction.type)
        {
            case GameAction.GameActionType.Equalize:
                {
                    // Equalize number of troops by moving half the difference
                    int troopDelta = actualOrigin.troops - actualDestination.troops;
                    if (troopDelta < 1)
                    {
                        // Can't move anything, we already have less or only one more troop than destination
                        pooledAction.reset();
                        return;
                    }
                    amount = troopDelta / 2;
                }
                break;
            case GameAction.GameActionType.MoveAll: amount = actualOrigin.troops - 1; break;
            case GameAction.GameActionType.Move: amount = pooledAction.parameter; _resetGamePlan(); break; // reset game plan for next turn
        }
        GameManager.Instance.askMovement(actualOrigin, actualDestination, amount);
        pooledAction.reset();
    }

    private void _resetGamePlan()
    {
        turnGamePlan.Clear();
        turnGamePlan = null; // This will cause a new computation of best moves
    }

    public struct CountryThreatPair
    {
        public Country country;
        public float threatLevel;
        public static int sort(CountryThreatPair _a, CountryThreatPair _b)
        {
            if (_a.threatLevel > _b.threatLevel) return -1; // descending order
            if (_b.threatLevel > _a.threatLevel) return 1;
            return 0;
        }
    }

    public List<Continent> getAllControlledContinents()
    {
        List<Continent> controlled = new();
        foreach (Continent c in player.stateCountPerContinents.Keys)
        {
            if (c.stateIDs.Count == player.stateCountPerContinents[c])
                controlled.Add(c);
        }
        return controlled;
    }

    private void _rehabilitateCountriesOf(Continent _c)
    {
        _rehabilitateCountriesOf(_c.id);
    }

    private void _rehabilitateCountriesOf(int _cid)
    {
        ignoredContinents.Remove(_cid);
    }

    private int _findFirstContinentToFocus()
    {
        float highestNonFullRatio = 0.0f;
        Continent priority = null;
        foreach (Continent c in player.stateCountPerContinents.Keys)
        {
            if (player.stateCountPerContinents[c] == c.stateIDs.Count)
                continue; // Player has all states of this continent, can ignore
            float ratio = player.stateCountPerContinents[c] * 1.0f / c.stateIDs.Count;
            if (priority == null || ratio > highestNonFullRatio)
            {
                highestNonFullRatio = ratio;
                priority = c;
            }
        }
        return priority.id;
    }
}
