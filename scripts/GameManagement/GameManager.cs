using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameManager : Node
{
    [Export]
    private TroopDisplayManager troopManager;

#if DEBUG
    [Export]
    private int startingTroops = 1; // Used for debug
#endif
    [Export]
    private StateDisplayerManager stateDisplayer;

    [Export]
    private float airPawnsHeightRatio = 1.15f;

    // Singleton
    public static GameManager Instance;
    public override void _Ready()
    {
        Instance = this;
    }

    public Planet planet{get; private set;} = null;

    public enum GamePhase { Init, FirstDeploy, Deploy, Attack, Reinforce, End };
    public GamePhase gamePhase {get; private set;} = GamePhase.Init;

    private List<Country> countries = new();
    Dictionary<State, int> countryIndexPerState = new();
    private const float REFERENCE_POINTS_MINIMAL_DISTANCE = 0.002f; // Minimal distance that must exist between any two reference points of same state

    private int activePlayerIndex = -1;
    private List<Player> players = new();
    private List<PlayerData> playersConfigData;
    private Dictionary<Player, ComputerAI> aiPerPlayer = new();

    public int reinforcementLeft {get; private set;} = 0;
    public int movementLeft {get; private set;} = 0;

    public bool waitingForMovement = false;
    
    public struct SelectionData
    {
        public SelectionData() { enemies = new(); allies = new(); selected = null; }
        public void removeDuplicateSelected()
        {
            if (allies.Contains(selected))
                allies.Remove(selected);
            if (enemies.Contains(selected))
                enemies.Remove(selected);
        }
        public List<Country> enemies;
        public List<Country> allies;
        public Country selected;
    }

    public SelectionData currentSelection {get; private set;} = new();

    private int readyReceived = 0;

    public void onGameSetupReady(List<PlayerData> _playersData)
    {
        playersConfigData = _playersData;
        playersConfigData.ForEach((data) =>
        {
            // Sanitizing if player forgot to enter a name
            if (data.isHuman && data.playerName == "")
                data.playerName = "Player " + (playersConfigData.IndexOf(data) + 1);
        });

        // Initialize Pawn colors
        List<Color> playerColors = new();
        playersConfigData.ForEach((data) =>
        {
            playerColors.Add(Parameters.colors[data.colorID]);
        });
        PawnColorManager.initialize(playerColors);

        if (++readyReceived == 2)
            initialize();
        else
            WorldBuildingWidgetManager.show(); // warn user(s) that they're waiting for world building to complete
    }

    public void onPlanetGenerationReady(Planet _planet)
    {
        planet = _planet;
        if (++readyReceived == 2)
        {
            initialize();
            WorldBuildingWidgetManager.hide();
        }
    }

    public void initialize()
    {
        CustomLogger.print("Starting GameManager initialization");
        _initializeCountries();

        foreach (PlayerData playerData in playersConfigData)
        {
            players.Add(new(players.Count, playerData.colorID, playerData.factionID));
            if (playerData.isHuman)
                players.Last().isHuman = true;
            else
                aiPerPlayer.Add(players.Last(), new(players.Last())); // instantiate AI
        }

        _doCountriesRandomAttributions();
        // Init AIs focus
        foreach (Player p in players)
        {
            if (p.isHuman) continue;
            aiPerPlayer[p].initializeStrategy();
        }

        countries.ForEach((c) =>
        {
#if DEBUG
            c.troops = Mathf.Clamp(startingTroops, 1, 99); // Arbitrary limits
#else
            c.troops = 1;
#endif
            troopManager.updateDisplay(c);
        });

        CustomLogger.print("GameManager initialized");
        _updatePhaseDisplay();
        triggerNextPhase();
    }

    private void _reset()
    {
        // Making sure a new call to initialize would put us in a good start state
        gamePhase = GamePhase.Init;
        players.Clear();
        aiPerPlayer.Clear();
        planet = null;
        troopManager.reset();
        countries.Clear();
        readyReceived = 1; // Not possible to change game setup yet

        _updatePhaseDisplay();
    }

    public void startANewGame()
    {
        Planet p = planet; // Saving the planet as our reset will clear our reference to it
        _reset();
        p.generate(); // Start new generation
    }


    public override void _Process(double _dt)
    {
        if (gamePhase == GamePhase.Init)
            return; // Game has not started yet
        // only treat IA here, as humans play with mouse
        if (players[activePlayerIndex].isHuman == false)
        {
            ComputerAI ai = aiPerPlayer[players[activePlayerIndex]];
            ai.processTurn(_dt);
        }
    }

    public void askReinforce(Country _c, int _amount = 1)
    {
        if(players[activePlayerIndex].id != _c.playerID)
            return;

        switch(gamePhase)
        {
            default:
                GD.PrintErr("GameManager.askReinforce called outisde of first deployment or deployment phase");
                return;
            case GamePhase.Deploy:
            {
                if (reinforcementLeft <= 0)
                    return;
                // Manage end of Deploy phase
                _amount = Mathf.Min(_amount, reinforcementLeft);
                reinforcementLeft -= _amount;
                if (reinforcementLeft == 0)
                        _startAttackPhase();
                else
                    _setSecondaryDisplay();
            } break;
            case GamePhase.FirstDeploy:
            {
                activePlayerIndex = (activePlayerIndex + 1) % players.Count; // One by one turn by turn, does not change phase but change active player
                // Manage end of First Deploy phase
                if(activePlayerIndex == 0) // each time all players have placed a pawn
                {
                    if(--reinforcementLeft <= 0)
                        _startDeploymentPhase(); // End of phase
                }
                AIVisualMarkerManager.Instance.setMarkerVisibility(players[activePlayerIndex].isHuman == false); // Display AI Marker if next player is AI
                AIVisualMarkerManager.Instance.setMarkerColor(activePlayerIndex);
                _updatePhaseDisplay();
                _amount = 1; // Cannot deploy more in FirstDeploy
            }break;
        }
        _c.troops += _amount;
        troopManager.updateDisplay(_c);
        _notifyCountryTroopUpdate(_c);
        ReinforcementSoundManager.play(_c.state.barycenter);
    }

    public void askMovement(Country _from, Country _to, int _amount)
    {
        _amount = Mathf.Min(_from.troops - 1, _amount); // some sanitizing, should already be taken into account
        _from.troops -= _amount;
        _to.troops += _amount;
        troopManager.movePawns(_from, _to, _amount);

        _notifyCountryTroopUpdate(_from, _to);

        movementLeft -= 1;
        if(movementLeft == 0)
            _startNextPlayerTurn();
    }

    public void updateCountryTroopsDisplay(Country _a, Country _b)
    {
        troopManager.updateDisplay(_a);
        troopManager.updateDisplay(_b);
        _notifyCountryTroopUpdate(_a,_b);
    }

    public void countryConquest(Country _attacker, Country _defender, int _attackingTroops)
    {
        if(_attacker.playerID == _defender.playerID)
            GD.PrintErr(_attacker + " attacks " + _defender + " AND THEY ARE ALLIED"); // don't return as we'd stuck the AI in the infinite loop

        _attackingTroops = Mathf.Min(_attacker.troops - 1, _attackingTroops); // some sanitizing, should already be taken into account

        _defender.troops = 0;
        troopManager.updateDisplay(_defender); // Boom no more troops
        _defender.troops = _attackingTroops; // Joking, we display 0 while moving attackers troops but we already put'em there

        if(players[_defender.playerID].countries.Count == 1)
        {
            // This player lost its final country, he lost
            _eliminatePlayerFromGame(players[_defender.playerID]);
        }

        players[_defender.playerID].removeCountry(_defender);
        _defender.playerID = _attacker.playerID;
        players[_attacker.playerID].addCountry(_defender);

        troopManager.movePawns(_attacker, _defender, _attackingTroops);
        _attacker.troops -= _attackingTroops;

        _notifyCountryTroopUpdate(_attacker,_defender);

        if(gamePhase == GamePhase.Attack) // Don't apply selection is game is over with this conquest
            _applySelection(HumanPlayerManager.processSelection(_attacker, players[activePlayerIndex]));
    }

    public void _notifyCountryTroopUpdate(Country _a, Country _b = null)
    {
        if(currentSelection.selected == null)
            return;
        if(currentSelection.selected == _a || ( _b !=null && currentSelection.selected == _b ))
            stateDisplayer.setCountryToDisplay(currentSelection.selected); // Refresh display with new troops
    }

    public int getColorIDOfPlayer(int _playerID)
    {
        return players[_playerID].colorID;
    }

    public int getFactionIDOfPlayer(int _playerID)
    {
        return players[_playerID].factionID;
    }

    public AICharacteristicsData getAIPersonalityByPlayerID(int _id)
    {
        if (_id < 0 || _id >= players.Count)
            throw new Exception("Invalid player id " + _id);
        if (players[_id].isHuman)
            throw new Exception("Trying to get AI Data of a human player");
        return aiPerPlayer[players[_id]].personality * AICharacteristics.baseline;
    }

    private void _eliminatePlayerFromGame(Player _p)
    {
        _p.hasLostTheGame = true;
        Player alivePlayerMemory = null;
        foreach (Player p in players)
        {
            if (p.hasLostTheGame == false)
            {
                if (alivePlayerMemory != null)
                    return; // At least two players are here, continue the game
                alivePlayerMemory = p;
            }
        }

        // Game is done, only one player remaining
        gamePhase = GamePhase.End;
        activePlayerIndex = players.IndexOf(alivePlayerMemory);
        resetSelection();
        _updatePhaseDisplay();
    }

    private void _startDeploymentPhase()
    {
        gamePhase = GamePhase.Deploy;
        reinforcementLeft = _computePlayerNewArmies(players[activePlayerIndex]);
        _updatePhaseDisplay();
    }

    private void _startAttackPhase()
    {
        // triggered when all troops deployed in deploy phase
        gamePhase = GamePhase.Attack;

        if(players[activePlayerIndex].isHuman)
            ArmySlider.show();

        _updatePhaseDisplay();
    }

    private void _startReinforcePhase()
    {
        gamePhase = GamePhase.Reinforce;
        movementLeft = 1;
        _updatePhaseDisplay();
    }

    private void _startNextPlayerTurn()
    {
        // End of last movement phase by button for player, call by AI
        movementLeft = 0; // Not forced to use all
        // Find next player still in game
        do
        {
            activePlayerIndex = (activePlayerIndex + 1) % players.Count;
        } while (players[activePlayerIndex].hasLostTheGame);

        _startDeploymentPhase();
        // Only show marker when AIs are playing
        AIVisualMarkerManager.Instance.setMarkerVisibility(players[activePlayerIndex].isHuman == false);
        AIVisualMarkerManager.Instance.setMarkerColor(activePlayerIndex);
    }

    private void _startFirstDeployment()
    {
        activePlayerIndex = 0; // First player go !
        gamePhase = GamePhase.FirstDeploy;
#if DEBUG
        reinforcementLeft = 1; // speed things up while debuging
#else
        reinforcementLeft = 40 / players.Count; // this can take quite the time, board game setup eh :: This could be a game setup parameter
#endif
        _updatePhaseDisplay();
    }

    public void triggerNextPhase()
    {
        switch (gamePhase)
        {
            case GamePhase.Init: _startFirstDeployment(); return;
            case GamePhase.Attack: _startReinforcePhase(); ArmySlider.hide(); return;
            case GamePhase.Reinforce: _startNextPlayerTurn(); return; // Go Back to Deploy phase

            case GamePhase.FirstDeploy:
            case GamePhase.Deploy:
                GD.PrintErr("Should not be in _triggerNextPhase in " + getGameStateAsString() + ", transition is automatic"); return;
            case GamePhase.End: return; // Game is over
        }

        throw new Exception(); // should never be in another state
    }

    public string getGameStateAsString()
    {
        switch(gamePhase)
        {
            case GamePhase.Init: return "Initialisation Phase"; // should not be displayed
            case GamePhase.FirstDeploy: return "Initial Deployment Phase";
            case GamePhase.Deploy: return "Deployment Phase";
            case GamePhase.Attack: return "Attack Phase";
            case GamePhase.Reinforce: return "Reinforcement Phase";
            case GamePhase.End: return "VICTORY";
        }
        return "<invalid>";
    }

    public string getActivePlayerAsString()
    {
        return players[activePlayerIndex].isHuman ? playersConfigData[activePlayerIndex].playerName : "Bot " + (activePlayerIndex+1);
    }

    private void _updatePhaseDisplay()
    {
        if (gamePhase != GamePhase.Init)
        {
            _setPhaseDisplay();
            _setSecondaryDisplay();
            _applySelection(HumanPlayerManager.processSelection(null, players[activePlayerIndex]));
        }
        else
        {
            GameUI.setPrimary("");
            GameUI.setSecondary("");
        }

        GameUI.setPhaseButtonVisibility(_shouldDisplayEndPhaseButton());
        GameUI.setNewGameButtonVisibility(_shouldDisplayNewGameButton());
    }

    private bool _shouldDisplayEndPhaseButton()
    {
        if(activePlayerIndex < 0 || activePlayerIndex >= players.Count)
            return false;
        return players[activePlayerIndex].isHuman && (gamePhase == GamePhase.Attack || gamePhase == GamePhase.Reinforce);
    }

    private bool _shouldDisplayStartGameButton()
    {
        return planet != null && gamePhase == GamePhase.Init;
    }

    private bool _shouldDisplayNewGameButton()
    {
        return gamePhase == GamePhase.End;
    }

    private void _setPhaseDisplay()
    {
        GameUI.setPrimary(GameUI.makeBold(getActivePlayerAsString()) + ": " + getGameStateAsString());
    }

    private void _setSecondaryDisplay()
    {
        string s = "";
        switch(gamePhase)
        {
            case GamePhase.FirstDeploy: // same
            case GamePhase.Deploy: s = reinforcementLeft + " reinforcement(s) left to place."; break;
            case GamePhase.Attack: break;
            case GamePhase.Reinforce: s = movementLeft + " troop movement left."; break;
            case GamePhase.End: s = "GG"; break;
        }
        GameUI.setSecondary(s);
    }

    public void onEndTurnButtonPressed()
    {
        triggerNextPhase();
    }

    public enum PlanetInteraction { Primary, Secondary }
    public void onPlanetInteraction(PlanetInteraction _type, int _vertexClicked)
    {
        if(gamePhase == GamePhase.Init || gamePhase == GamePhase.End || players[activePlayerIndex].isHuman == false || waitingForMovement)
            return; // Don't do anything here while game has not started, or in AI's turn, or Human is moving troops

        State s = planet.mapManager.getStateOfVertex(_vertexClicked);
        if(s == null)
        {
            // Clicked off any state
            // Reset selection
            if(currentSelection.selected != null)
            {
                SelectionData selection = HumanPlayerManager.processSelection(null, players[activePlayerIndex]);
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
                    HumanPlayerManager.processAction(c, players[activePlayerIndex]);
                }
            }
            else
            {
                SelectionData selection = HumanPlayerManager.processSelection(c, players[activePlayerIndex]);
                _applySelection(selection);
            }
        }
    }

    public void askSelection(SelectionData _selection) { _applySelection(_selection); }
    public void resetSelection() { _applySelection(new()); }

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

        currentSelection = _selection;

        if(currentSelection.selected != null)
        {
            planet.mapManager.setStateSelected(currentSelection.selected.state);
            stateDisplayer.setCountryToDisplay(currentSelection.selected);
        }
        else
            stateDisplayer.setVisible(false);

        planet.setMesh(); // Apply selection visuals

        SelectorSoundManager.play();
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
        return Mathf.Max(3, (int)Mathf.Ceil(_player.countries.Count / 3.0f) + continentScore);
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
                    c.airReferencePoints.Add(new(vertex * airPawnsHeightRatio, normal));
                }
            }
        }
    }
}
