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
    private const int MIN_STATE_SIZE = 100;
    private const float MAX_SQUARED_DISTANCE_FOR_STATE_MERGE = 1.5f;
    private const int MAX_IMAGE_SIZE = 16380; // Max is 16384 but rounding down for reasons (no)
    private const float STATE_SELECTED_UV_VALUE = 1.0f;
    private const float STATE_ALLY_UV_VALUE = 2.0f;
    private const float STATE_ENEMY_UV_VALUE = -1.0f;
    private const int CONTINENT_MIN_SIZE = 6;
    private const int CONTINENT_MAX_SIZE = 10;

    public static List<Color> statesColor = _generateStatesColors(); // new(){ new(0.0f, 1.0f, 0.0f), new(1.0f,1.0f,0.0f), new(1.0f, 0.5f, 0.5f) };

    public List<State> states;
    public List<Continent> continents;
    public List<List<int>> landMassesStatesIndices = new(); // will store lists of stateIDs. Each list will represent a landmass of states connected by land. This will later be used to define continents.

    public static Color shallowSeaColor = new(0.1f, 0.3f, 0.7f);

    public MapManager(Planet _owner){ ownerPlanet = _owner; }
    public void RegisterMap(float[] planetMap)
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
        _buildContinents();
        _buildTexture();
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
            Color color;
            switch(map[i].nodeType)
            {
                case MapNode.NodeType.Land:
                {
                    if(map[i].stateID == -1)
                        color = Colors.Green;
                    else
                    {
                        color = statesColor[map[i].continentID%STATES_COUNT];
                        if(map[i].isBorder() || map[i].waterBorder)
                            color = color.Lerp(Colors.Black, 0.6f); // make border darker for nice display
                    }
                    break;
                }
                case MapNode.NodeType.RogueIsland: color = new(0.8f, 0.4f, 0.0f); break;
                case MapNode.NodeType.Water:
                default: color = shallowSeaColor; break;
            }
            img.SetPixel(x,y, color);
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
        _computeLandMassesStates();
        Debug.Print("Finished creating tiny states and their boundaries");
        // Then merge most of them until a good amount is reached
        _mergeUntilEnoughStates();
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
            State mergeState = null;

            if(currentState.neighbors.Count == 0) // Island state, merge accross sea if distance to closest is below threshold, or make rogue island (non played land)
            {
                if(currentState.land.Count > MIN_STATE_SIZE) // State island already has an acceptable size, no need to merge
                    continue;

                State nearest = _findClosestSeaNeighbor(currentState, out float distanceSquared, out Vector2I pointsFullMapIndices);
                if(nearest == null)
                {
                    GD.PrintErr("Could not _findClosestSeaNeighbor for State_" + currentState.id);
                    continue;
                }
                if(distanceSquared < MAX_SQUARED_DISTANCE_FOR_STATE_MERGE)
                {
                    mergeState = nearest;
                    _setStateYUV(currentState, STATE_SELECTED_UV_VALUE);
                    _setStateYUV(mergeState, STATE_ALLY_UV_VALUE);

                    // Spawn a bridge to link the island(s)
                    ownerPlanet.askBridgeCreation(pointsFullMapIndices);

                    GD.Print("Island merge between island State_" + currentState.id + " to State_" + mergeState.id);
                }
                else
                {
                    GD.Print("State_" + currentState.id + " set to RogueIsland");
                    _setStateYUV(currentState, STATE_ENEMY_UV_VALUE);
                    currentState.makeRogueIsland();
                    states.Remove(currentState);
                    landMassesStatesIndices[currentState.landMassID].Remove(currentState.id);
                }
            }
            else if(GD.Randf() > 0.0f) // Merging land neighbor can happen: Per boundary size or Per size ? Flip a coin to know
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
                continue;

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

        // Remove giver from landmasses: special cases for island state ? Issues for states on multiple landmasses ?
        // Should be ok to just remove reference to deleted state: some landmasses won't have any states left, keep it in mind but should no be bad
        landMassesStatesIndices[giver.landMassID].Remove(giver.id);

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

    private int _compareStatesNeighbors(int _a, int _b)
    {
        return State.compareStateNeighborsNumber(_getStateByStateID(_a), _getStateByStateID(_b));
    }

    private void _buildContinents()
    {
        _buildContinentsFirstPass(); // This will create continents of size [1:MAX]
        foreach(Continent c in continents)
        {
            string log = "Continent_" + c.index + " (" + c.stateIDs.Count + ")";
            if(c.neighborsContinentIDs.Count > 0)
            {
                log += ": ";
                foreach(int n in c.neighborsContinentIDs)
                {
                    log += "Continent_" + n + " ";
                }
            }
            GD.Print(log);
        }
        // Do merging and exchanges to have continents of sze [MIN:MAX]
    }

    private void _buildContinentsFirstPass()
    {
        continents = new();
        // Find state with less neighbors to seed a continent, sort states by number of neighbors
        List<int> orderedStates = new();
        foreach(State s in states)
            orderedStates.Add(s.id); // not yet ordered
        orderedStates.Sort(_compareStatesNeighbors);

        while(orderedStates.Count > 0)
        {
            // pop state with less neigbhors
            State seed = _getStateByStateID(orderedStates[0]);
            orderedStates.RemoveAt(0);
            if(seed.continentID != -1)
                continue; // already bound to a continent

            _spreadNewContinentFrom(seed.id);
        }
    }

    private void _spreadNewContinentFrom(int seedID)
    {
        int continentID = continents.Count;
        continents.Add(new(continentID));
        List<int> addList = new(){seedID}; // width first graph run

        while(addList.Count > 0 && continents[continentID].stateIDs.Count < CONTINENT_MAX_SIZE )
        {
            // Take state with less neighbors
            State currentState = _getStateByStateID(addList[0]);
            addList.RemoveAt(0);
            if(currentState.continentID != -1)
            {
                // add currentContinent to the neighbors of the one we found
                continents[currentState.continentID].addContinentNeighbor(continentID); // won't add duplicates
                // add the continent we found to the neigbhors of the current
                continents[continentID].addContinentNeighbor(currentState.continentID); // won't add duplicates
                continue; // this one already belongs to another continent
            }
            // Add to continent
            continents[continentID].addState(currentState.id);
            currentState.setContinentID(continentID);
            // Add all its neighbors to list
            if(currentState.neighbors.Count == 0)
            {
                // Island State
                State closest = _findClosestSeaNeighbor(currentState, out float distance, out Vector2I points);
                currentState.neighbors.Add(closest.id);
                closest.neighbors.Add(currentState.id);
                ownerPlanet.askBridgeCreation(points);

                addList.Add(closest.id);
            }
            else
            {
                foreach(int stateID in currentState.neighbors)
                {
                    if(addList.Contains(stateID) || continents[continentID].stateIDs.Contains(stateID))
                        continue;
                    addList.Add(stateID);
                }
            }
        }
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
    private State _findClosestSeaNeighbor(State _state, out float _distance, out Vector2I _verticesFullMapIndices)
    {
        _distance = -1.0f;
        _verticesFullMapIndices = new(-1, -1);

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
                    int ourIndex = _state.land[ourLandID].fullMapIndex;
                    int otherIndex = s.land[otherLandID].fullMapIndex;
                    float distance = ownerPlanet.getSquareDistance(ourIndex, otherIndex);
                    if(closest == null || _distance > distance)
                    {
                        closest = s;
                        _distance = distance;
                        _verticesFullMapIndices = new(ourIndex, otherIndex);
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

    private void _setStateYUV(State _state, float _value)
    {
        foreach(MapNode node in _state.land)
        {
            ownerPlanet.setUVYAtIndex(node.fullMapIndex, _value);
        }
    }
}

public class MapNode // this is not a struct only because i could not bother create a constructor for all those members
{
    public const int INVALID_ID = -1;
    public enum NodeType{Water, Land, RogueIsland};
    public bool isBorder(){return borderingStateIds.Count > 0;}
    public bool waterBorder = false;
    public List<int> borderingStateIds = new();
    public NodeType nodeType = NodeType.Land;
    public int stateID = INVALID_ID;
    public int continentID = INVALID_ID;
    public float height = 0.0f;
    public int fullMapIndex = INVALID_ID;
}
