using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Principal;

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

public class GameState
{
    public GameState(int _playerID, int _depth, GameState _parent, GameAction _action)
    {
        playerID = _playerID;
        depth = _depth;
        actionToThisState = _action;
        parent = _parent;
    }

    public float score { get; private set; } = -1;
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
        if (threatScore < Mathf.Epsilon) // As it cannot be negative, we avoid dividing by zero (can happen for last gamestates where we own all countries, ggs!)
            score = float.MaxValue;
        else
            score = (continentScore + countryScore) / (threatScore + unusableTroopsScore);
    }

    public bool contains(Country _c)
    {
        // We may have another image of this country, check if we have a country pointing to the same state (not GameState, geographic State)
        State stateToFind = _c.state;
        foreach (Country c in countries)
        {
            if (c.state == stateToFind)
                return true;
        }
        return false;
    }

    public Country getEquivalentCountry(State _s)
    {
        foreach (Country c in countries)
        {
            if (c.state == _s)
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
        foreach (Country c in _realCountries)
        {
            if (c.state == _alternate.state)
                return c;
        }
        throw new Exception("Could not find real country from alternative"); //return null
    }

    public void add(Country _c)
    {
        countries.Add(_c);
        // Register in continent logs
        if (countriesPerContinent.ContainsKey(_c.continent) == false)
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
        foreach (Continent c in countriesPerContinent.Keys)
        {
            if (countriesPerContinent[c] == c.stateIDs.Count)
                continentScore += c.score;
        }
        return continentScore;
    }

    private int _evaluateOwnedCountriesScore() { return (int)(countries.Count / 3.0f); }

    private float _evaluateThreatScore()
    {
        float threat = 0.0f;
        foreach (Country c in countries)
        {
            float localThreat = 0.0f;
            foreach (int stateID in c.state.neighbors)
            {
                Country realCountry = GameManager.Instance.getCountryByState(stateID);
                if (contains(realCountry))
                    continue; // Real country might not be ours, but in this projected state it is !
                localThreat += realCountry.troops - 0.9f; // Lone troop only count as 0.1 as it cannot attack at all (but still differentiate from allies at 0)
            }
            threat += localThreat / c.state.neighbors.Count;
        }
        return threat;
    }

    private int _evaluateUnusableTroops()
    {
        int unusableTroops = 0;
        foreach (Country c in countries)
        {
            bool nearEnemy = false;
            foreach (int stateID in c.state.neighbors)
            {
                Country realCountry = GameManager.Instance.getCountryByState(stateID);
                if (contains(realCountry) == false)
                {
                    nearEnemy = true;
                    break; // This country has an enemy as neighbor, its troops are useful
                }
            }
            if (nearEnemy || c.troops <= 1)
                continue;
            unusableTroops += c.troops - 1;
        }
        return unusableTroops;
    }

    /// <summary>
    /// Recursively evaluate all children states, removing all states with a lower minax score.
    /// Called on the root state, this will reduce all tree to only one branch
    /// </summary>
    public void pruneChildren()
    {
        minmaxScore = score;
        if (children.Count == 0)
            return;

        int bestChildIndex = -1;
        for (int i = 0; i < children.Count; ++i)
        {
            children[i].pruneChildren();
            if (children[i].minmaxScore > minmaxScore)
            {
                bestChildIndex = i;
                minmaxScore = children[i].minmaxScore;
            }
        }

        if (bestChildIndex == -1)
        {
            children.Clear(); // Remove all, current state is better than subsequent states
        }
        else
        {
            for (int i = children.Count - 1; i >= 0; --i)
            {
                if (i != bestChildIndex)
                    children.RemoveAt(i); // Remove all except best state sequence
            }
        }
    }

    private CountryThreatPair _computeCountryThreat(Country _c)
    {
        CountryThreatPair threat = new() { country = _c, threatLevel = 0.0f };
        State s = _c.state;
        foreach (int stateID in s.neighbors)
        {
            Country country = GameManager.Instance.getCountryByState(stateID);
            if (contains(country))
                continue; // Country is friendly, zero threat added
            threat.threatLevel += country.troops - 0.9f; // Lone troop only count as 0.1 as it cannot attack at all (but still differentiate from allies at 0)
        }

        if (_c.troops > 0) // if zero because of attacks roundings, consider it to 1
            threat.threatLevel /= _c.troops;

        return threat;
    }

    public List<CountryThreatPair> computeAndSortOwnCountriesThreatLevel()
    {
        List<CountryThreatPair> threats = new();
        countries.ForEach((country) => threats.Add(_computeCountryThreat(country)));
        threats.Sort(CountryThreatPair.sort);
        return threats;
    }

    public List<Country> getAlliedCountriesAccessibleFrom(Country _c)
    {
        List<Country> accessibleCountries = new();
        List<Country> scanned = new();
        Queue<Country> toScan = new();
        toScan.Enqueue(_c);

        while (toScan.Count > 0)
        {
            Country scanning = toScan.Dequeue();
            if (scanned.Contains(scanning))
                continue; // Country already handled
            scanned.Add(scanning);
            if (contains(scanning) == false)
                continue; // Enemy country
            accessibleCountries.Add(getEquivalentCountry(scanning));
            foreach (int stateID in scanning.state.neighbors)
            {
                toScan.Enqueue(GameManager.Instance.getCountryByState(stateID));
            }
        }

        return accessibleCountries;
    }

    public Continent getContinentToFocus()
    {
        float highestNonFullRatio = 0.0f;
        Continent priority = null;
        foreach (Continent c in countriesPerContinent.Keys)
        {
            if (countriesPerContinent[c] == c.stateIDs.Count)
                continue; // Player has all states of this continent, can ignore
            float ratio = countriesPerContinent[c] * 1.0f / c.stateIDs.Count;
            if (priority == null || ratio > highestNonFullRatio)
            {
                highestNonFullRatio = ratio;
                priority = c;
            }
        }

        // Priority is null here if we controll all the continents where we have troops
        // Find new neigboring continent, preferably find less heavily defended
        if (priority == null)
        {
            priority = findNeighborLessDefendedContinent();
        }
        return priority;
    }

    public Continent findNeighborLessDefendedContinent()
    {
        Dictionary<Continent, int> defensesPerContinents = new();
        foreach(Continent c in countriesPerContinent.Keys)
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
                    if (contains(country))
                        continue; // country is friendly
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
}

public struct GameAction
{
    public GameAction() { type = GameActionType.None; from = null; to = null; parameter = 0; }

    public enum GameActionType { Deploy, Attack, Move, FreeMove, None };
    public GameActionType type;
    public Country from;
    public Country to;
    public int parameter; // Used by Reinforce and deploy to tell how much to move
    public override string ToString()
    {
        string actionString = "";
        switch (type)
        {
            case GameActionType.Attack: actionString = "Attacks"; break;
            case GameActionType.FreeMove:
            case GameActionType.Move: actionString = "Moves+" + parameter + " to"; break;
            case GameActionType.Deploy: actionString = parameter + " deploy in"; break;
            case GameActionType.None: actionString = "None"; break;
        }
        actionString += " " + to;
        if (type != GameActionType.Deploy)
            actionString = from + " " + actionString;
        return actionString;
    }
    
    public void reset() { type = GameActionType.None; parameter = 0; }
}
