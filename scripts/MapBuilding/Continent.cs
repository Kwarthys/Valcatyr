using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

public class Continent
{
    public List<int> stateIDs { get; private set;} = new();
    public List<int> neighborsContinentIDs {get; private set;} = new();
    public int id {get; private set;}

    public Continent(int _id) { id = _id; }

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

    public void computeNeighbors(Func<int, State> _getStateByID)
    {
        foreach(int stateID in stateIDs)
        {
            State s = _getStateByID(stateID);
            foreach(int nghbStateID in s.neighbors)
            {
                State nghbState = _getStateByID(nghbStateID);
                if(nghbState.continentID == id)
                    continue; // Don't care about neighbors inside continent
                addContinentNeighbor(nghbState.continentID); // won't allow duplicates
            }
        }
    }

    public void absorbContinent(Continent toAbsorb, Func<int, State> _getStateByID)
    {
        foreach(int stateID in toAbsorb.stateIDs)
        {
            State s = _getStateByID(stateID);
            s.setContinentID(id);
            addState(stateID);
        }

        foreach(int continentID in toAbsorb.neighborsContinentIDs)
        {
            if(continentID != id)
                addContinentNeighbor(continentID); // won't allow duplicates
        }
    }

    public void swapNeighborIndex(int _from, int _to)
    {
        if(neighborsContinentIDs.Contains(_from))
        {
            neighborsContinentIDs.Remove(_from);
            addContinentNeighbor(_to); // won't allow duplicates
        }
    }
}
