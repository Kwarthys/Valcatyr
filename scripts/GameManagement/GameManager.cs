using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameManager : Node
{
    [Export]
    private TroopDisplayManager troopManager;

    [Export]
    private StateDisplayerManager stateDisplayer;

    // Singleton
    public static GameManager Instance;
    public override void _Ready()
    {
        Instance = this;
    }

    public Planet planet{get; private set;} = null;

    public enum GameState { Init, FirstDeploy, Deploy, Attack, Reinforce };
    public GameState gameState {get; private set;} = GameState.Init;

    private List<Country> countries = new();
    Dictionary<State, int> countryIndexPerState = new();
    private const float REFERENCE_POINTS_MINIMAL_DISTANCE = 0.002f; // Minimal distance that must exist between any two reference points of same state

    private int activePlayerIndex = -1;
    private List<Player> players = new();
    private Dictionary<Player, ComputerAI> aiPerPlayer = new();

    public int reinforcementLeft {get; private set;} = 0;
    public int movementLeft {get; private set;} = 0;
    
    public struct SelectionData
    {
        public SelectionData(){enemies = new(); allies = new(); selected = null;}
        public void removeDuplicateSelected()
        {
            if(allies.Contains(selected))
                allies.Remove(selected);
            if(enemies.Contains(selected))
                enemies.Remove(selected);
        }
        public List<Country> enemies;
        public List<Country> allies;
        public Country selected;
    }

    public SelectionData currentSelection {get; private set;} = new();

    public void initialize(Planet _planet)
    {
        planet = _planet;
        _initializeCountries();

        List<int> hoomanIndices = new(){0}; // only one human for now, TODO: need game setup menu for local turn based versus

        for(int i = 0; i < 2; ++i) // TODO Adjust number of player 3-6
        {
            players.Add(new(i));
            if(hoomanIndices.Contains(i))
                players[i].isHuman = true; // set player as human
            else
                aiPerPlayer.Add(players[i], new(players[i])); // instantiate AI
        }

        _doCountriesRandomAttributions();
        countries.ForEach((c) => {c.troops = 1; troopManager.updateDisplay(c); });
    }


    public override void _Process(double _dt)
    {
        if(gameState == GameState.Init)
            return; // Game has not started yet
        // only treat IA here, as humans play with mouse
        if(players[activePlayerIndex].isHuman == false)
        {
            ComputerAI ai = aiPerPlayer[players[activePlayerIndex]];
            ai.processTurn(_dt);
        }
    }

    public void askReinforce(Country _c)
    {
        if(gameState != GameState.Deploy)
        {
            GD.PrintErr("GameManager.askReinforce called outisde ouf deployment phase");
            return;
        }
        if(players[activePlayerIndex].id == _c.playerID && reinforcementLeft > 0)
        {
            reinforcementLeft--;
            _c.troops += 8;//1; TODO REMOVE CHEAT
            troopManager.updateDisplay(_c);

            if(reinforcementLeft == 0)
            {
                _startAttackPhase();
            }
            else
            {
                _setSecondaryDisplay();
            }
        }
    }

    public void askMovement(Country _from, Country _to, int _amount)
    {
        _amount = Mathf.Min(_from.troops - 1, _amount); // some sanitizing, should already be taken into account
        _from.troops -= _amount;
        _to.troops += _amount;
        troopManager.updateDisplay(_from);
        troopManager.updateDisplay(_to);

        movementLeft -= 1;
        if(movementLeft == 0)
            _startNextPlayerTurn();
    }

    public void updateCountryTroopsDisplay(Country _a, Country _b)
    {
        troopManager.updateDisplay(_a);
        troopManager.updateDisplay(_b);
    }

    public void countryConquest(Country _attacker, Country _defender, int _attackingTroops)
    {
        if(_attacker.playerID == _defender.playerID)
            GD.PrintErr(_attacker + " attacks " + _defender + " AND THEY ARE ALLIED"); // don't return as we'd stuck the AI in the infinite loop

        _attackingTroops = Mathf.Min(_attacker.troops - 1, _attackingTroops); // some sanitizing, should already be taken into account
        _attacker.troops -= _attackingTroops;
        _defender.troops = _attackingTroops;
        players[_defender.playerID].removeCountry(_defender);
        _defender.playerID = _attacker.playerID;
        players[_attacker.playerID].addCountry(_defender);

        troopManager.updateDisplay(_attacker);
        troopManager.updateDisplay(_defender, true); // change colors of pre-existing troops
        SelectionData selection = HumanPlayerManager.processSelection(this, _defender, players[_attacker.playerID]); // select newly acquired country
        _applySelection(selection);
    }

    private void _startDeploymentPhase()
    {
        gameState = GameState.Deploy;
        activePlayerIndex = (activePlayerIndex + 1) % players.Count;
        reinforcementLeft = _computePlayerNewArmies(players[activePlayerIndex]);
        _updatePhaseDisplay();
    }

    private void _startAttackPhase()
    {
        // triggered by button for player, and call by AI
        gameState = GameState.Attack;
        _setPhaseDisplay();
        _setSecondaryDisplay();
        _updatePhaseDisplay();
    }

    private void _startReinforcePhase()
    {
        gameState = GameState.Reinforce;
        movementLeft = 1;
        _setPhaseDisplay();
        _setSecondaryDisplay();
        _updatePhaseDisplay();
    }

    private void _startNextPlayerTurn()
    {
        // End of reinforcement phase by button for player, call by AI
        movementLeft = 0; // Not forced to use all
        _startDeploymentPhase();
    }

    public void triggerNextPhase()
    {
        switch(gameState)
        {
            case GameState.Attack: _startReinforcePhase(); return;

            case GameState.Init:
            case GameState.FirstDeploy: // first deployment not implemented yet (each player place a single troop turn by turn)
            case GameState.Reinforce: _startNextPlayerTurn(); return;

            case GameState.Deploy:
                GD.PrintErr("Should not be in _triggerNextPhase in " + getGameStateAsString() + ", transition is automatic"); return;
        }

        throw new Exception(); // should never be in another state
    }

    public string getGameStateAsString()
    {
        switch(gameState)
        {
            case GameState.Init: return "Initialisation Phase"; // should not be displayed
            case GameState.FirstDeploy: return "Initial Deployment Phase";
            case GameState.Deploy: return "Deployment Phase";
            case GameState.Attack: return "Attack Phase";
            case GameState.Reinforce: return "Reinforcement Phase";
        }
        return "<invalid>";
    }

    public string getActivePlayerAsString()
    {
        Player active = players[activePlayerIndex];
        string s = active.isHuman ? "Player " : "Bot ";
        return s + (activePlayerIndex+1);
    }

    private void _updatePhaseDisplay()
    {
        _setPhaseDisplay();
        _setSecondaryDisplay();
        SelectionData selection = HumanPlayerManager.processSelection(this, null, players[activePlayerIndex]);
        _applySelection(selection);
    }

    private void _setPhaseDisplay()
    {
        GameUI.Instance?.setPrimary(GameUI.makeBold(getActivePlayerAsString()) + ": " + getGameStateAsString());
    }

    private void _setSecondaryDisplay()
    {
        string s = "";
        switch(gameState)
        {
            case GameState.Deploy: s = reinforcementLeft + " reinforcement left to place."; break;
            case GameState.Attack: break;
            case GameState.Reinforce: s = movementLeft + " troop movement left."; break;
        }
        GameUI.Instance?.setSecondary(s);
    }

    public void onEndTurnButtonPressed()
    {
        triggerNextPhase();
    }

    public enum PlanetInteraction { Primary, Secondary }
    public void onPlanetInteraction(PlanetInteraction _type, int _vertexClicked)
    {
        if(gameState == GameState.Init || players[activePlayerIndex].isHuman == false)
            return; // Don't do anything here while game has not started, or in AI's turn

        State s = planet.mapManager.getStateOfVertex(_vertexClicked);
        if(s == null)
        {
            // Clicked off any state
            // Reset selection
            if(currentSelection.selected != null)
            {
                SelectionData selection = HumanPlayerManager.processSelection(this, null, players[activePlayerIndex]);
                _applySelection(selection);
            }
            return;
        }
        else
        {
            Country c = getCountryByState(s);
            if(_type == PlanetInteraction.Secondary)
            {
                if(players[activePlayerIndex].isHuman)
                {
                    HumanPlayerManager.processAction(this, c, players[activePlayerIndex]);
                }
            }
            else
            {
                SelectionData selection = HumanPlayerManager.processSelection(this, c, players[activePlayerIndex]);
                _applySelection(selection);
            }
        }
    }

    public void askSelection(SelectionData _selection)
    {
        _applySelection(_selection);
    }

    private void _applySelection(SelectionData _selection)
    {
        // Remove what is already selection and no longer (or not at the same place) in the new selection
        List<Country> updateNotNeeded = new(); // keep track of already right highlighted countries
        foreach(Country c in currentSelection.allies)
        {
            if(_selection.allies.Contains(c))
                updateNotNeeded.Add(c);
            else
                planet.mapManager.resetStateHighlight(c.state);
        }
        foreach(Country c in currentSelection.enemies)
        {
            if(_selection.enemies.Contains(c))
                updateNotNeeded.Add(c);
            else
                planet.mapManager.resetStateHighlight(c.state);
        }

        if(currentSelection.selected != null)
        {
            planet.mapManager.resetStateHighlight(currentSelection.selected.state);
        }

        // Select new states
        foreach(Country c in _selection.allies)
        {
            if(updateNotNeeded.Contains(c) == false)
                planet.mapManager.setStatehighlightAlly(c.state);
        }
        foreach(Country c in _selection.enemies)
        {
            if(updateNotNeeded.Contains(c) == false)
                planet.mapManager.setStateHighlightEnemy(c.state);
        }

        if(_selection.selected != null)
            planet.mapManager.setStateSelected(_selection.selected.state);

        currentSelection = _selection;
        planet.setMesh();

        if(currentSelection.selected == null)
            stateDisplayer.setVisible(false);
        else
            stateDisplayer.setCountryToDisplay(currentSelection.selected);
    }

    private void _doCountriesRandomAttributions()
    {
        List<int> countriesIndices = new();
        for(int i = 0; i < countries.Count; ++i)
        {
            countriesIndices.Add(i);
        }

        int playerToGive = 0;
        while(countriesIndices.Count > 0)
        {
            int countryIndex = countriesIndices[(int)(GD.Randf() * (countriesIndices.Count - 1))];
            countries[countryIndex].playerID = playerToGive;
            players[playerToGive].addCountry(countries[countryIndex]);

            playerToGive = (playerToGive + 1) % players.Count;
            countriesIndices.Remove(countryIndex);
        }
    }

    public Country getCountryByState(State _s)
    {
        return countries[countryIndexPerState[_s]];
    }

    public Country getCountryByState(int _stateID)
    {
        State s = MapManager.Instance.getStateByStateID(_stateID);
        if(s == null) return null;
        return countries[countryIndexPerState[s]];
    }

    public List<Country> getNeighboringEnemiesAround(Country _c, Player _player = null)
    {
        int playerID = _player == null ? _c.playerID : _player.id;
        List<Country> enemyCountries = new();
        foreach(int stateID in _c.state.neighbors)
        {
            State state = planet.mapManager.getStateByStateID(stateID);
            Country neigbhorCountry = getCountryByState(state);
            if(neigbhorCountry.playerID != playerID)
                enemyCountries.Add(neigbhorCountry);
        }
        return enemyCountries;
    }

    public List<Country> getAlliedCountriesAccessibleFrom(Country _c)
    {
        List<Country> accessibleCountries = new();
        List<State> scannedStates = new();
        Queue<State> statesToScan = new();
        statesToScan.Enqueue(_c.state);

        while(statesToScan.Count > 0)
        {
            State currentState = statesToScan.Dequeue();
            if(scannedStates.Contains(currentState))
                continue; // Country already handled
            scannedStates.Add(currentState);
            Country currentCountry = getCountryByState(currentState);
            if(currentCountry.playerID != _c.playerID)
                continue; // Enemy country
            accessibleCountries.Add(currentCountry);
            foreach(int stateID in currentState.neighbors)
            {
                statesToScan.Enqueue(planet.mapManager.getStateByStateID(stateID));
            }
        }

        return accessibleCountries;
    }

    private int _computePlayerNewArmies(Player _player)
    {
        // Compute continent bonus
        int continentScore = 0;
        foreach(Continent c in _player.stateCountPerContinents.Keys)
        {
            if(c.stateIDs.Count == _player.stateCountPerContinents[c]) // Does player have all the countries of this continent ?
                continentScore += c.score; // HUUUUGE bonus
        }
        // Combine state count and continent
        return (int)Mathf.Ceil(_player.countries.Count / 3.0f) + continentScore;
    }

    private void _initializeCountries()
    {
        foreach(State s in planet.mapManager.states)
        {
            countries.Add(new());
            Country c = countries.Last();
            c.state = s;
            c.continent = planet.mapManager.getContinentByID(s.continentID);

            countryIndexPerState.Add(s, countries.Count - 1);

            // Find reference points, that will later be used to spawn Pawns
            List<int> pointIndices = new();
            List<int> blackListed = new();
            bool leave = false;
            bool pointsAreGood = false;
            int loops = 0;
            while(pointsAreGood == false)
            {
                loops++;
                float minDist = REFERENCE_POINTS_MINIMAL_DISTANCE * (1.0f - (loops / 20) * 0.1f); // reduce minimal distance over time to avoid infinite loops
                if(loops%20 == 0)
                {
                    //GD.Print("points dist reduced to " + minDist);
                    blackListed.Clear();
                }

                // Always keep list full, either for first loop or to replace removed point
                int intLoops = 0;
                while(pointIndices.Count < TroopDisplayManager.PAWN_FACTORISATION_COUNT + 2) // more reference point to add diversity
                {
                    intLoops++;
                    float localMinDist = minDist * (1.0f - (intLoops / 20) * 0.1f); // reduce minimal distance AGAIN over time to avoid infinite loops
                    if(intLoops%20 == 0)
                    {
                        //GD.Print("ShoreDist reduced to " + localMinDist);
                        blackListed.Clear();
                    }
                    int indexCandidate = s.land[(int)(GD.Randf() * (s.land.Count-1))].fullMapIndex;
                    if(blackListed.Contains(indexCandidate) || s.boundaries.Contains(indexCandidate) || s.shores.Contains(indexCandidate))
                        continue;

                    if(pointIndices.Contains(indexCandidate) == false)
                    {
                        Vector3 point = planet.getVertex(indexCandidate);

                        bool checkDistances(List<int> indices)
                        {
                            foreach (int borderIndex in indices)
                            {
                                float dist = point.DistanceSquaredTo(planet.getVertex(s.land[borderIndex].fullMapIndex));
                                if (dist < localMinDist)
                                    return false;
                            }
                            return true;
                        }

                        // Check distance to shores/borders
                        if(checkDistances(s.boundaries) && checkDistances(s.shores))
                            pointIndices.Add(indexCandidate);
                        else
                            blackListed.Add(indexCandidate); // shores and borders won't change, we can defintely forget about this one

                    }
                }

                // Check all the points
                leave = false;
                for(int j = 0; j < pointIndices.Count && leave == false; ++j)
                {
                    for(int i = j+1; i < pointIndices.Count && leave == false; ++i)
                    {
                        int indexJ = pointIndices[j];
                        int indexI = pointIndices[i];
                        float dist = planet.getVertex(indexJ).DistanceSquaredTo(planet.getVertex(indexI));

                        if(dist < minDist)
                        {
                            // redraw one vertex at random
                            int indexToRedraw = GD.Randf() > 0.5f ? i : j;
                            pointIndices.RemoveAt(indexToRedraw);
                            leave = true;
                        }
                    }
                }

                if(leave)
                    continue;

                pointsAreGood = true; // All distances were above min \o/
                foreach(int index in pointIndices)
                {
                    planet.getVertexAndNormal(index, out Vector3 vertex, out Vector3 normal);
                    c.referencePoints.Add(new(vertex, normal));
                }
            }
        }
    }
}
