using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

public class Continent
{
    public List<int> stateIDs { get; private set;} = new();
    public List<int> neighborsContinentIDs {get; private set;} = new();
    public int index {get; private set;}

    public Continent(int _id) { index = _id; }

    public bool addState(int stateID)
    {
        if(stateIDs.Contains(stateID))
            return false; // Did not add, already here
        stateIDs.Add(stateID);
        return true;
    }

    public bool addContinentNeighbor(int continentID)
    {
        if(neighborsContinentIDs.Contains(continentID))
            return false; // Did not add, already here
        neighborsContinentIDs.Add(continentID);
        return true;
    }
}
