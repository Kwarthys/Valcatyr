using Godot;
using System;
using System.Collections.Generic;

public class Player
{
    public Player(int _id){ id = _id; }
    public bool isHuman = false;
    public bool hasLostTheGame = false; // hehe
    public int id = -1;
    public List<Country> countries {get; private set;} = new();
    public Dictionary<Continent, int> stateCountPerContinents {get; private set;} = new();

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