using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameManager : Node
{
    [Export]
    private TroopDisplayManager troopManager;

    public Planet planet{get; private set;} = null;

    public enum GameState { Init, FirstDeploy, Deploy, Attack, Reinforce };
    public GameState gameState {get; private set;} = GameState.Init;

    private List<Country> countries = new();
    Dictionary<State, int> countryIndexPerState = new();
    private const float REFERENCE_POINTS_MINIMAL_DISTANCE = 0.002f; // Minimal distance that must exist between any two reference points of same state

    private int activePlayer = -1;
    private List<Player> players = new();

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

        for(int i = 0; i < 4; ++i)
        {
            players.Add(new(i));
            if(i == 0)
                players[0].isHuman = true; // only one human for now, need game setup menu for local turn based versus
        }

        _doCountriesRandomAttributions();
        countries.ForEach((c) => {c.troops = 1; troopManager.updateDisplay(c); });
    }

/*
    public override void _Process(double delta)
    {
        if(players[activePlayer].isHuman == false)
        {
            // only treat IA here, as humans play with mouse
        }
    }
*/

    public void askReinforce(Country _c)
    {
        if(gameState != GameState.Deploy)
        {
            GD.PrintErr("GameManager.askReinforce called outisde ouf deployment phase");
            return;
        }
        if(players[activePlayer].id == _c.playerID && reinforcementLeft > 0)
        {
            reinforcementLeft--;
            _c.troops += 1;
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
        movementLeft = 3;
        _setPhaseDisplay();
        _setSecondaryDisplay();
        _updatePhaseDisplay();
    }

    private void _startNextPlayerTurn()
    {
        // End of reinforcement phase by button for player, call by AI
        movementLeft = 0; // Not forced to use all
        // Start deploy phase
        gameState = GameState.Deploy;
        activePlayer = (activePlayer + 1) % players.Count;
        reinforcementLeft = (int)(players[activePlayer].countries.Count * 0.3f); // should also depend on controlled continents scores
        _updatePhaseDisplay();
    }

    private void _triggerNextPhase()
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
        Player active = players[activePlayer];
        string s = active.isHuman ? "Player " : "Bot ";
        return s + (activePlayer+1);
    }

    private void _updatePhaseDisplay()
    {
        _setPhaseDisplay();
        _setSecondaryDisplay();
        SelectionData selection = HumanPlayerManager.processSelection(this, null, players[activePlayer]);
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
        _triggerNextPhase();
    }

    public enum PlanetInteraction { Primary, Secondary }
    public void onPlanetInteraction(PlanetInteraction _type, int _vertexClicked)
    {
        State s = planet.mapManager.getStateOfVertex(_vertexClicked);
        if(s == null)
        {
            // Clicked off any state
            // Reset selection
            if(currentSelection.selected != null)
            {
                SelectionData selection = HumanPlayerManager.processSelection(this, null, players[activePlayer]);
                _applySelection(selection);
            }
            return;
        }
        else
        {
            Country c = getCountryByState(s);
            if(_type == PlanetInteraction.Secondary)
            {
                if(players[activePlayer].isHuman)
                {
                    HumanPlayerManager.processAction(this, c, players[activePlayer]);
                }
            }
            else
            {
                SelectionData selection = HumanPlayerManager.processSelection(this, c, players[activePlayer]);
                _applySelection(selection);
            }
        }
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
            players[playerToGive].countries.Add(countries[countryIndex]);

            playerToGive = (playerToGive + 1) % players.Count;
            countriesIndices.Remove(countryIndex);
        }
    }

    public Country getCountryByState(State _s)
    {
        return countries[countryIndexPerState[_s]];
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

    private void _initializeCountries()
    {
        foreach(State s in planet.mapManager.states)
        {
            countries.Add(new());
            Country c = countries.Last();
            c.state = s;

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
