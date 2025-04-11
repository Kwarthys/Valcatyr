using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameManager : Node
{
    [Export]
    private TroopDisplayManager troopManager;

    private Planet planet = null;

    public enum GameState { Deploy, Attack, Reinforce };
    public GameState gameState {get; private set;} = GameState.Deploy;

    private List<Country> countries = new();
    private const float REFERENCE_POINTS_MINIMAL_DISTANCE = 0.002f; // Minimal distance that must exist between any two reference points of same state

    private int activePlayer = 0;
    private List<Player> players = new();

    public int reinforcementLeft {get; private set;} = 0;
    public int movementLeft {get; private set;} = 0;

    public void initialize(Planet _planet)
    {
        planet = _planet;
        _initializeCountries();
        countries.ForEach((c) => {c.troops = 1; troopManager.updateDisplay(c); });

        for(int i = 0; i < 4; ++i)
        {
            players.Add(new(i));
            if(i == 0)
                players[0].isHuman = true; // only one human for now, need game setup menu for local turn based versus
        }

        _doCountriesRandomAttributions();

        activePlayer = -1; // to start at 0
        _startNextPlayerTurn();
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
    }

    private void _startReinforcePhase()
    {
        gameState = GameState.Reinforce;
        movementLeft = 3;
        _setPhaseDisplay();
        _setSecondaryDisplay();

        foreach(Country c in players[activePlayer].countries)
        {
            planet.mapManager.setStatehighlightAlly(c.state);
        }
        planet.setMesh();
    }

    private void _startNextPlayerTurn()
    {
        // End of reinforcement phase by button for player, call by AI
        movementLeft = 0; // Not forced to use all

        gameState = GameState.Deploy;
        activePlayer = (activePlayer + 1) % players.Count;
        reinforcementLeft = 8; // should depend on controlled states and continent count
        _setPhaseDisplay();
        _setSecondaryDisplay();
    }

    public string getGameStateAsString()
    {
        switch(gameState)
        {
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
        GD.Print("CLIC");
    }

    public enum PlanetInteraction { Primary, Secondary }
    public void onPlanetInteraction(PlanetInteraction _type, int _vertexClicked)
    {
        State s = planet.mapManager.getStateOfVertex(_vertexClicked);
        if(s == null)
        {
            // Clicked off any state
            GD.Print("ClickedOUT");

            // TODO: hacky for now as i don't have a proper start signal
            _setPhaseDisplay();
            _setSecondaryDisplay();
            return;
        }
        else
        {
            Country c = _getCountryByState(s);
            if(players[activePlayer].isHuman)
            {
                HumanPlayerManager.processAction(this, c, players[activePlayer]);
            }
            // TODO else display data about state (or right clic ?)
        }
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

        foreach(Player p in players)
        {
            string s = p.id + ":";
            foreach(Country c in p.countries)
            {
                s += " S_" + c.state.id;
            }
            GD.Print(s);
        }
    }

    private Country _getCountryByState(State _s)
    {
        foreach(Country c in countries)
        {
            if(c.state == _s) return c;
        }
        return null;
    }

    private void _initializeCountries()
    {
        foreach(State s in planet.mapManager.states)
        {
            countries.Add(new());
            Country c = countries.Last();
            c.state = s;

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
