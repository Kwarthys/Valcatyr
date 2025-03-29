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

    public void absorbContinent(Continent toAbsorb, Func<int, State> _getStateByID)
    {
        foreach(int stateID in toAbsorb.stateIDs)
        {
            State s = _getStateByID(stateID);
            s.setContinentID(index);
            addState(stateID);
        }

        foreach(int continentID in toAbsorb.neighborsContinentIDs)
        {
            if(continentID != index)
                addContinentNeighbor(continentID); // won't allow duplicates
        }
    }

    public void swapNeighborIndex(int from, int to)
    {
        if(neighborsContinentIDs.Contains(from))
        {
            neighborsContinentIDs.Remove(from);
            addContinentNeighbor(to); // won't allow duplicates
        }
    }
}
