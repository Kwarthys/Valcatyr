using Godot;
using System;
using System.Collections.Generic;

public class Player
{
    public Player(int _id){ id = _id; }
    public bool isHuman = false;
    public int id = -1;
    public List<Country> countries {get; private set;} = new();
    public Dictionary<Continent, int> stateCountPerContinents {get; private set;} = new();

    public static Color[] playerColors = // colorblindness friendly-ish color palette from Bang Wong 2011 "Points of view: Color blindness" https://www.nature.com/articles/nmeth.1618
    { 
        new(0.90f, 0.63f, 0.0f), // Gold
        new(0.33f, 0.70f, 0.91f), // Cyan-Blue
        new(0.0f, 0.62f, 0.45f), // Green-ish
        new(0.0f, 0.0f, 0.0f), // Black
        new(0.94f, 0.89f, 0.26f), // Yellow
        new(0.80f, 0.47f, 0.66f), // Pink-ish
    };

    public void addCountry(Country _c)
    {
        if(countries.Contains(_c))
        {
            GD.PrintErr("Tried to add a country to a player who already had it");
            return;
        }
        
        countries.Add(_c);
        if(stateCountPerContinents.ContainsKey(_c.continent) == false)
            stateCountPerContinents.Add(_c.continent, 1);
        else
            stateCountPerContinents[_c.continent] += 1;
    }

    public void removeCountry(Country _c)
    {
        if(countries.Contains(_c) == false)
        {
            GD.PrintErr("Tried to remove a country from a player that did not have it");
            return;
        }
        countries.Remove(_c);
        stateCountPerContinents[_c.continent] -= 1;
    }
}