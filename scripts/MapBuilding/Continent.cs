using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

public class Continent
{
    public List<int> stateIDs { get; private set;} = new();
    public List<int> neighborsContinentIDs {get; private set;} = new();
    public int id {get; private set;}
    public int colorID = -1;

    public int score { get; private set; } = 0;

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

    public void setColorID(int _colorID, Func<int, State> _getStateByID)
    {
        colorID = _colorID;
        foreach (int stateID in stateIDs)
        {
            _getStateByID(stateID).setColorID(_colorID);
        }
    }

    public void absorbContinent(Continent toAbsorb, Func<int, State> _getStateByID)
    {
        foreach (int stateID in toAbsorb.stateIDs)
        {
            State s = _getStateByID(stateID);
            s.setContinent(this);
            addState(stateID);
        }

        foreach (int continentID in toAbsorb.neighborsContinentIDs)
        {
            if (continentID != id)
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

    public void computeScore(Func<int, State> _getStateByID)
    {
        // Value of each state: BorderState = 1 else State = 0.34
        float decimalScore = 0.0f;
        foreach(int stateID in stateIDs)
        {
            float stateScore = 0.34f;
            State s = _getStateByID(stateID);
            foreach(int nghbID in s.neighbors)
            {
                State nghb = _getStateByID(nghbID);
                if(nghb.continentID != id)
                {
                    stateScore = 1.0f;
                    break;
                }
            }
            decimalScore += stateScore;
        }
        score = (int)decimalScore;
    }
}
