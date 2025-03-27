using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;

public partial class MapManager : Node
{
    private Planet ownerPlanet;

    MapNode[] map = new MapNode[Planet.MAP_SIZE];

    public Texture2D tex {get; private set;}

    private const int STATES_COUNT = 60;
    private const int MIN_STATE_SIZE = 10;
    private const int MAX_IMAGE_SIZE = 16380; // Max is 16384 but rounding down for reasons (no)
    private const float STATE_SELECTED_UV_VALUE = 1.0f;
    private const float STATE_ALLY_UV_VALUE = 2.0f;
    private const float STATE_ENEMY_UV_VALUE = -1.0f;

    public static List<Color> statesColor = _generateStatesColors(); // new(){ new(0.0f, 1.0f, 0.0f), new(1.0f,1.0f,0.0f), new(1.0f, 0.5f, 0.5f) };

    public List<State> states;
    public List<List<int>> landMassesStatesIndices = new(); // will store lists of stateIDs. Each list will represent a landmass of states connected by land. This will later be used to define continents.

    public static Color shallowSeaColor = new(0.1f, 0.3f, 0.7f);

    public MapManager(Planet _owner){ ownerPlanet = _owner; }
    public void RegisterMap(float[] planetMap, ref List<Vector2> uvs)
    {
        for(int mapIndex = 0; mapIndex < planetMap.Length; ++mapIndex)
        {
            map[mapIndex] = new MapNode();
            map[mapIndex].height = planetMap[mapIndex];
            map[mapIndex].fullMapIndex = mapIndex;
            if(planetMap[mapIndex] <= Planet.SEA_LEVEL)
                map[mapIndex].nodeType = MapNode.NodeType.Water;
        }

        _buildStates();
        _buildTexture();

        // last addition test(s)
        foreach(State s in states)
        {
            if(s.shores.Count == 0)
                continue;
            State nearest = _findClosestSeaNeighbor(s, out float dist);
            if(nearest != null)
                GD.Print("SquaredDist from State_" + s.id + " to State_" + nearest.id + ": " + dist);

            _setStateYUV(s, STATE_SELECTED_UV_VALUE, ref uvs); // display result on Valcatyr
            _setStateYUV(nearest, STATE_ALLY_UV_VALUE, ref uvs);

            break;
        }
    }

    private void _buildTexture()
    {
        int imgHeight = (map.Length / MAX_IMAGE_SIZE) + 1;
        int imgWidth = Math.Min(map.Length, MAX_IMAGE_SIZE);

        if(imgHeight * imgWidth > MAX_IMAGE_SIZE * MAX_IMAGE_SIZE)
            Debug.Print("Image reached max capacity");

        Image img = Image.CreateEmpty(imgWidth, imgHeight, false, Image.Format.Rgb8);
        img.Fill(new(1.0f,0.0f,1.0f));
        Debug.Print("ImgSize: " + img.GetSize());
        for(int i = 0; i < map.Length; ++i)
        {
            //float c = i * 1.0f / map.Length;
            //img.SetPixel(i,0, new(c,0,0));
            int x = i % MAX_IMAGE_SIZE;
            int y = i / MAX_IMAGE_SIZE;
            if(map[i].nodeType == MapNode.NodeType.Land)
            {
                if(map[i].stateID == -1)
                    img.SetPixel(x, y, Colors.Green);
                else
                {
                    Color c = statesColor[map[i].stateID%STATES_COUNT];
                    if(map[i].isBorder() || map[i].waterBorder)
                        c = c.Lerp(Colors.Black, 0.6f); // make border darker for nice display
                    img.SetPixel(x, y, c);
                }
            }
            else
            {
                img.SetPixel(x,y, shallowSeaColor);
            }
        }
        tex = ImageTexture.CreateFromImage(img);
        //img.SavePng("img.png"); // funny to look at it "flat"
    }

    private void _buildStates()
    {
        states = new List<State>();

        // Start by creating a lot of tiny states
        _buildTinyStates();
        _computeStatesBoundaries();
        Debug.Print("Finished creating tiny states and their boundaries");
        // Then merge most of them until a good amount is reached
        _mergeUntilEnoughStates();
        _computeLandMassesStates();
    }

    private void _mergeUntilEnoughStates()
    {
        List<int> stateIDs = new();
        for(int i = 0; i < states.Count; ++i)
        {
            if(states[i].id < 0)
            {
                Debug.Print("State at index " + i + " has invalid ID: " + states[i].id);
            }
            stateIDs.Add(states[i].id);
        }
        stateIDs.Sort( (int idA, int idB) => { return State.comapreStateSize(_getStateByStateID(idA), _getStateByStateID(idB)); } );

        while(states.Count >= STATES_COUNT)
        {
            // Pop the first element (smallest state) and find him a friend to merge with
            int stateID = stateIDs[0];
            stateIDs.RemoveAt(0);
            State currentState = _getStateByStateID(stateID);

            if(currentState.neighbors.Count == 0) // No merging possible TODO: MERGE ACCROSS SEE IF DISTANCE BELOW THRESHOLD
                continue;
            
            State mergeState = null;
            // Marging can happen: Per boundary size or Per size ? Flip a coin to know
            if(GD.Randf() > 0.0f)
            {
                // Merge with neighbor with most common boundary
                Dictionary<int, int> borderLenghtPerStateID = _computeSharedBoundaries(currentState);
                float maxRelativeBoundarySize = 0.0f;
                int ownBorderSize = currentState.land.Count;
                foreach(int neighborID in borderLenghtPerStateID.Keys)
                {
                    State neighborState = _getStateByStateID(neighborID);
                    int sharedBoundaryLength = borderLenghtPerStateID[neighborID];
                    float relativeOwnBoundary = sharedBoundaryLength * 1.0f / ownBorderSize;
                    float relativeOtherBoundary = sharedBoundaryLength * 1.0f / neighborState.boundaries.Count;
                    float relativeCommonBoundary = Math.Max(relativeOwnBoundary, relativeOtherBoundary);

                    if(mergeState == null || maxRelativeBoundarySize < relativeCommonBoundary)
                    {
                        maxRelativeBoundarySize = relativeCommonBoundary;
                        mergeState = neighborState;
                    }
                }
            }
            else
            {
                // Merge with smallest neighbor
                int smallestNeighborSize = 0;
                foreach(int nghbID in currentState.neighbors)
                {
                    State neighborState = _getStateByStateID(nghbID);
                    if(mergeState == null || smallestNeighborSize > neighborState.land.Count)
                    {
                        smallestNeighborSize = neighborState.land.Count;
                        mergeState = neighborState;
                    }
                }
            }

            if(mergeState == null)
            {
                GD.PrintErr("Ooops");
                continue;
            }
            
            stateIDs.Remove(mergeState.id); // we removed both state merging from stateIndices
            State newState = _mergeStates(currentState, mergeState);

            // Insert it back at the right place, in ascending land size
            bool inserted = false;
            int newStateSize = newState.land.Count;
            for(int i = 0; i < stateIDs.Count; ++i)
            {
                if(_getStateByStateID(stateIDs[i]).land.Count > newStateSize)
                {
                    stateIDs.Insert(i, newState.id);
                    inserted = true;
                    break;
                }
            }
            if(!inserted)
                stateIDs.Add(newState.id);
        }
    }

    private State _mergeStates(State _a, State _b)
    {
        // Chose receiving State, lowest ID
        State receiver = _a.id > _b.id ? _b : _a;
        State giver = receiver == _a ? _b : _a;

       receiver.absordState(giver);

        // Update other neighbors to reference new state and no longer the one that no longer exists
        foreach(int neighborIndex in giver.neighbors)
        {
            if(neighborIndex != receiver.id)
            {
                _getStateByStateID(neighborIndex).updateBordersAfterStateMerge(giver.id, receiver.id);
            }
        }

        states.Remove(giver);

        if(!receiver.verifyBoundaryIntegrity())
            GD.Print("Error on State_" + receiver.id + " after completing its merge with State_" + giver.id);

        // Return new state
        return receiver;
    }

    private State _getStateByStateID(int id)
    {
        if(id < 0)
        {
            GD.PrintErr("MapManager._getStateByStateID was aksed negative id : State_" + id);
            return null;
        }

        foreach(State s in states)
        {
            if(s.id == id) return s;
        }

        GD.PrintErr("MapManager._getStateByStateID could not find State_" + id);
        return null;
    }

    private Dictionary<int, int> _computeSharedBoundaries(State s)
    {
        Dictionary<int, int> borderLenghtPerStateID = new();
        foreach(int nodeIndex in s.boundaries)
        {
            foreach(int stateID in s.land[nodeIndex].borderingStateIds)
            {
                if(!borderLenghtPerStateID.ContainsKey(stateID))
                    borderLenghtPerStateID.Add(stateID, 0);
                borderLenghtPerStateID[stateID]++;
            }
        }
        return borderLenghtPerStateID;
    }

    private void _computeStatesBoundaries() // Used to compute boundaries and states neigbhors for the first time (deletes prior data)
    {
        foreach(State s in states)
        {
            s.boundaries.Clear();
            s.neighbors.Clear();
            foreach(MapNode node in s.land)
            {
                node.borderingStateIds.Clear();
                foreach(int neighborIndex in Planet.getNeighbours(node.fullMapIndex))
                {
                    int neighborStateID = map[neighborIndex].stateID;
                    if(neighborStateID == -1)
                    {
                        node.waterBorder = true;
                    }
                    else if(neighborStateID >= 0 && neighborStateID != s.id) // StateIDs below zero is sea or rogue island
                    {
                        if(!s.neighbors.Contains(neighborStateID))
                            s.neighbors.Add(neighborStateID);

                        if(!node.borderingStateIds.Contains(neighborStateID))
                            node.borderingStateIds.Add(neighborStateID);
                    }
                }

                if(node.isBorder())
                    s.boundaries.Add(s.land.IndexOf(node));
                if(node.waterBorder)
                    s.shores.Add(s.land.IndexOf(node));
            }
            bool fine = s.verifyBoundaryIntegrity();
            if(fine == false)
                GD.Print("Error on State_" + s.id + " right after computing its boundaries");
        }
    }

    private void _buildTinyStates()
    {
        int stateIndex = 0;
        for(int i = 0; i < map.Length; ++i)
        {
            MapNode node = map[i];
            if(node.nodeType == MapNode.NodeType.Land && node.stateID == MapNode.INVALID_ID)
            {
                State state = new();
                state.id = stateIndex++;
                List<int> lands = _spreadState(i);
                foreach(int landIndex in lands)
                {
                    map[landIndex].stateID = state.id;
                    state.land.Add(map[landIndex]);
                }
                states.Add(state);
            }
        }
        Debug.Print("Created " + states.Count + " states");
    }

    private List<int> _spreadState(int starter, int maxSpread = 150)
    {
        List<int> result = new();
        List<int> indicesToCheck = new(){ starter };

        while(indicesToCheck.Count > 0)
        {
            int index = indicesToCheck[0];
            indicesToCheck.RemoveAt(0);
            if(map[index].nodeType == MapNode.NodeType.Land && map[index].stateID == MapNode.INVALID_ID)
            {
                if(!result.Contains(index))
                {
                    result.Add(index);
                    if(result.Count >= maxSpread)
                        return result;
                }

                foreach(int neighbor in Planet.getNeighbours(index))
                {
                    if(!indicesToCheck.Contains(neighbor) && !result.Contains(neighbor))
                    {
                        if(GD.Randf() > 0.3f)
                            indicesToCheck.Add(neighbor);   // Wide search: Square states
                        else
                            indicesToCheck.Insert(0, neighbor); // Depth search: spaghetti states
                    }
                }
            }
        }

        return result;
    }

    private void _computeLandMassesStates()
    {
        landMassesStatesIndices = new();
        List<int> statesToScan = new();
        List<int> scannedIDs = new();

        for(int i = 0; i < states.Count; ++i)
        {
            // If already scanned, continue
            if(scannedIDs.Contains(states[i].id))
                continue;

            // Start graph search
            statesToScan.Clear();
            statesToScan.Add(states[i].id);

            int landMassIndex = landMassesStatesIndices.Count;
            landMassesStatesIndices.Add(new());

            // Scan all states in the scan list, where we add land neigbhors
            while(statesToScan.Count > 0)
            {
                State state = _getStateByStateID(statesToScan[0]);
                statesToScan.RemoveAt(0);
                scannedIDs.Add(state.id);
                state.landMassID = landMassIndex;
                landMassesStatesIndices[landMassIndex].Add(state.id);
                //GD.Print("Adding State_" + state.id + " to landMass_" + landMassIndex + "(" + landMassesStatesIndices[landMassIndex].Count + ")");

                foreach(int nghbID in state.neighbors)
                {
                    if( !statesToScan.Contains(nghbID) // if not already registered to be scanned
                    && !landMassesStatesIndices[landMassIndex].Contains(nghbID) ) // and not already in landMass
                    {
                        // Register to scan
                        statesToScan.Add(nghbID);
                    }
                }
                //GD.Print("LandMass_" + landMassIndex + " registered " + landMassesStatesIndices[landMassIndex].Count + " states.");
            }
            //GD.Print("Registered " + landMassesStatesIndices.Count + " land masses");
        }
    }


    // This will ignore all land direct and indirect neighbors
    private State _findClosestSeaNeighbor(State _state, out float _distance)
    {
        _distance = -1.0f;

        if(ownerPlanet == null)
            return null;

        if(_state.shores.Count == 0)
        {
            GD.PrintErr("Called _findClosestSeaNeighbor on State_" + _state.id + " which has no shores");
            return null;
        }

        if(_state.landMassID >= landMassesStatesIndices.Count)
            return null;

        List<int> blackListIDs = landMassesStatesIndices[_state.landMassID];

        State closest = null;

        foreach(State s in states)
        {
            if(blackListIDs.Contains(s.id)) // this will exclude our starting state as well, which is nice
                continue;
            if(s.shores.Count == 0)
                continue;
            // Evaluated state s has shores and is not on the same landMass as our starting state
            // Compare each of its shores with ours
            foreach(int ourLandID in _state.shores)
            {
                foreach(int otherLandID in s.shores)
                {
                    float distance = ownerPlanet.getSquareDistance(_state.land[ourLandID].fullMapIndex, s.land[otherLandID].fullMapIndex);
                    if(closest == null || _distance > distance)
                    {
                        closest = s;
                        _distance = distance;
                    }
                }
            }
        }
        return closest;
    }

    private static List<Color> _generateStatesColors()
    {
        List<Color> colors = new();
        for(int i = 0; i < STATES_COUNT; ++i)
        {
            colors.Add(new(GD.Randf(), GD.Randf(), GD.Randf()));
        }
        return colors;
    }

    private void _setStateYUV(State _state, float _value, ref List<Vector2> _uvs)
    {
        foreach(MapNode node in _state.land)
        {
            _uvs[node.fullMapIndex] = new(_uvs[node.fullMapIndex].X, _value);
        }
    }
}

public class MapNode
{
    public const int INVALID_ID = -1;
    public enum NodeType{Water, Land, RogueIsland};
    public bool isBorder(){return borderingStateIds.Count > 0;}
    public bool waterBorder = false;
    public List<int> borderingStateIds = new();
    public NodeType nodeType = NodeType.Land;
    public int playerID = INVALID_ID;
    public int stateID = INVALID_ID;
    public int continentID = INVALID_ID;
    public float height = 0.0f;
    public int fullMapIndex = INVALID_ID;
}

public class State
{
    public List<MapNode> land = new();
    public List<int> boundaries = new(); // boundaries array contains index to MapNode in land
    public List<int> neighbors = new(); // StateIds of neighboring states
    public List<int> shores = new(); // Indices of nodes in Land with water as neighbor
    public int id = -1;
    public int continentID = -1;
    public int landMassID = -1; // Continents may contain multiple islands, land mass only cares about direct land neighbors

    public static int comapreStateSize(State _a, State _b)
    {
        int sizeA = _a.land.Count;
        int sizeB = _b.land.Count;
        if(sizeA == sizeB)
            return 0;
        return sizeA > sizeB ? 1 : -1;
    }

    public void absordState(State _giver)
    {
        if(!verifyBoundaryIntegrity())
            GD.Print("Error on receiver State_" + id + " at the start of _mergeStates");
        if(!_giver.verifyBoundaryIntegrity())
            GD.Print("Error on giver State_" + _giver.id + " at the start of _mergeStates");

        int borderOffset = land.Count;

        foreach(MapNode node in _giver.land)
        {
            node.stateID = id;
            land.Add(node);
        }

        // Offset giver references to its own land by the amounf of land previously in receiver,
        // as giver's land will be shifted by that amount -> this counts for shores and boundaries
        foreach(int shoreID in _giver.shores)
        {
            shores.Add(shoreID + borderOffset);
        }

        removeBoundaryToState(_giver.id); // Receiver state removes all reference to giver state in its neighboring data

        foreach(int borderIndex in _giver.boundaries)
        {
            if(_giver.land[borderIndex].borderingStateIds.Count == 0)
            {
                GD.PrintErr("State_" + _giver.id + " boundary has no borders");
                continue;
            }

            if(_giver.land[borderIndex].borderingStateIds.Contains(id))
            {
                _giver.land[borderIndex].borderingStateIds.Remove(id);
            }
            if(_giver.land[borderIndex].borderingStateIds.Count > 0)
            {
                boundaries.Add(borderIndex + borderOffset); // Only add border if it still have neighbors after removing receiving state
            }
        }

        foreach(int neighborID in _giver.neighbors)
        {
            if(neighborID != id && neighbors.Contains(neighborID) == false)
                neighbors.Add(neighborID);
        }

        if(!verifyBoundaryIntegrity())
            GD.Print("Error on State_" + id + " after absorbing State_" + _giver.id);
    }

    public void removeBoundaryToState(int stateToRemove)
    {
        if(neighbors.Contains(stateToRemove))
            neighbors.Remove(stateToRemove);

        foreach(int i in boundaries)
        {
            if(land[i].borderingStateIds.Contains(stateToRemove))
                land[i].borderingStateIds.Remove(stateToRemove);
        }

        for(int i = boundaries.Count - 1; i >= 0; --i)
        {
            if(land[boundaries[i]].isBorder() == false)
                boundaries.RemoveAt(i);
        }

        if(!verifyBoundaryIntegrity())
            GD.Print("Issue after removeBoundaryToState_" + stateToRemove + " on State_" + id);
    }

    public void updateBordersAfterStateMerge(int stateIDFrom, int stateIDTo)
    {
        if(neighbors.Contains(stateIDFrom))
        {
            neighbors.Remove(stateIDFrom); // Remove ref to the old state if we had it
            if(!neighbors.Contains(stateIDTo))
                neighbors.Add(stateIDTo); // Add ref to the new state if we did not have it already
        }

        foreach(int i in boundaries)
        {
            if(land[i].borderingStateIds.Contains(stateIDFrom))
            {
                land[i].borderingStateIds.Remove(stateIDFrom); // Remove ref to the old state if we had it
                if(!land[i].borderingStateIds.Contains(stateIDTo))
                    land[i].borderingStateIds.Add(stateIDTo); // Add ref to the new state if we did not have it already
            }
        }
    }

    public bool verifyBoundaryIntegrity() // unit test like function to help debugging
    {
        foreach(int i in boundaries)
        {
            if(i < 0 || i >= land.Count)
            {
                GD.PrintErr("Boundary error: Index is negative or geater than landSize: " + i + " / " + land.Count);
                return false;
            }

            if(land[i].borderingStateIds.Count == 0)
            {
                GD.PrintErr("Boundary error: Node is in boundary list of State_" + id + " while not having any borders");
                return false;
            }
        }
        foreach(int i in neighbors)
        {
            if(i == id)
            {
                GD.PrintErr("Boundary error: State_" + id + " is its own neigbhour");
                return false;
            }
        }

        return true; // Everything is fine
    }
}
