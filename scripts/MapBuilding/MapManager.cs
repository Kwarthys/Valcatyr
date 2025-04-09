using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net;
using System.Reflection.Emit;

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
    private const int CONTINENT_MIN_SIZE = 4;
    private const int CONTINENT_MAX_SIZE = 12;

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

        float usecStart = Time.GetTicksUsec();
        _buildStates();
        _buildContinents();
        GD.Print("Creating States and Continents took " + ((Time.GetTicksUsec() - usecStart) * 0.000001) + " secs.");
        _buildTexture();
    }

    public void selectStateOfVertex(int _vertexID)
    {
        if(_vertexID < 0 || _vertexID >= map.Length)
            return;

        MapNode n = map[_vertexID];
        List<int> updatedStates = new();
        if(n.stateID >= 0)
        {
            GD.Print("Selected State_" + n.stateID + " part of Continent_" + n.continentID);

            updatedStates.Add(n.stateID);
            State s = _getStateByStateID(n.stateID);
            _setStateYUV(s, STATE_SELECTED_UV_VALUE);
            foreach(int stateID in s.neighbors)
            {
                updatedStates.Add(stateID);
                State nghb = _getStateByStateID(stateID);
                _setStateYUV(nghb, s.continentID == nghb.continentID ? STATE_ALLY_UV_VALUE : STATE_ENEMY_UV_VALUE);
            }
        }

        states.ForEach(state => 
        {
            if(!updatedStates.Contains(state.id))
            {
                _setStateYUV(state, 0.0f); // reset
            }
        });
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
                        color = statesColor[map[i].continentID%statesColor.Count];
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
                    // Spawn a bridge to link the island(s)
                    ownerPlanet.askBridgeCreation(pointsFullMapIndices);
                    //GD.Print("Island merge between island State_" + currentState.id + " to State_" + mergeState.id);
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
                mergeState = _findStateWithMostSharedBorders(currentState);
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

    private Continent _mergeContinents(Continent _a, Continent _b)
    {
        Continent receiver = _a.id > _b.id ? _b : _a;
        Continent giver = receiver == _a ? _b : _a;

        receiver.absorbContinent(giver, _getStateByStateID);

        foreach(Continent c in continents)
        {
            if(c == receiver || c == giver)
                continue;
            c.swapNeighborIndex(giver.id, receiver.id);
        }

        GD.Print("Merged Continent_" + receiver.id + " to Continent_" + giver.id);

        continents.Remove(giver);
        return receiver;
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

    private Continent _getNewContinent(int _id)
    {
        continents.Add(new(_id));
        return continents[continents.Count - 1];
    }
    private Continent _getContinentByID(int id)
    {
        if(id < 0)
        {
            GD.PrintErr("MapManager._getContinentByID was aksed negative id : Continent_" + id);
            return null;
        }

        foreach(Continent c in continents)
        {
            if(c.id == id) return c;
        }

        GD.PrintErr("MapManager._getContinentByID could not find Continent_" + id);
        return null;
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

    private State _findStateWithMostSharedBorders(State _s, bool _bothWays = true)
    {
        Dictionary<int, int> borderLenghtPerStateID = _computeSharedBoundaries(_s);
        State nghb = null;
        float maxRelativeBoundarySize = 0.0f;
        int ownBorderSize = _s.land.Count;
        foreach(int neighborID in borderLenghtPerStateID.Keys)
        {
            State neighborState = _getStateByStateID(neighborID);
            int sharedBoundaryLength = borderLenghtPerStateID[neighborID];
            float relativeOwnBoundary = sharedBoundaryLength * 1.0f / ownBorderSize;
            if(_bothWays == true)
            {
                // Also check relative boundary size of other state, use the biggest of the two - Preferred for state merging
                float relativeOtherBoundary = sharedBoundaryLength * 1.0f / neighborState.boundaries.Count;
                float relativeCommonBoundary = Math.Max(relativeOwnBoundary, relativeOtherBoundary);
                if(nghb == null || maxRelativeBoundarySize < relativeCommonBoundary)
                {
                    maxRelativeBoundarySize = relativeCommonBoundary;
                    nghb = neighborState;
                }
            }
            else
            {
                // Only care about relative length to State _s - Preferred for continent merging
                if(nghb == null || maxRelativeBoundarySize < relativeOwnBoundary)
                {
                    maxRelativeBoundarySize = relativeOwnBoundary;
                    nghb = neighborState;
                }
            }
        }
        return nghb;
    }

    private Dictionary<int, int> _computeSharedBoundaries(State _s)
    {
        Dictionary<int, int> borderLenghtPerStateID = new();
        foreach(int nodeIndex in _s.boundaries)
        {
            foreach(int stateID in _s.land[nodeIndex].borderingStateIds)
            {
                if(!borderLenghtPerStateID.ContainsKey(stateID))
                    borderLenghtPerStateID.Add(stateID, 0);
                borderLenghtPerStateID[stateID]++;
            }
        }
        return borderLenghtPerStateID;
    }

    private Dictionary<int, int> _computeSharedBoundaries(Continent _c)
    {
        Dictionary<int, int> borderLenghtPerContinentID = new();
        foreach(int stateID in _c.stateIDs)
        {
            State s = _getStateByStateID(stateID);
            Dictionary<int, int> stateBorderLenghtPerStateID = _computeSharedBoundaries(s);
            foreach(int nghbID in stateBorderLenghtPerStateID.Keys)
            {
                if(_c.stateIDs.Contains(nghbID) == false)
                {
                    // Neighboring state is part of another continent
                    State nghb = _getStateByStateID(nghbID);

                    if(borderLenghtPerContinentID.ContainsKey(nghb.continentID) == false)
                        borderLenghtPerContinentID.Add(nghb.continentID, 0);
                    borderLenghtPerContinentID[nghb.continentID] += stateBorderLenghtPerStateID[nghbID];
                }
            }
        }
        return borderLenghtPerContinentID;
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
        _buildContinentsFirstPass(); // This will create continents of mostly 2 states, but more is possible
        _computeContinentsNeighbors();
        _reduceContinentsNumber();
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

            _spreadNewContinentFrom(seed);
        }
    }

    private void _spreadNewContinentFrom(State _seedState)
    {
        State nghb = null;
        if(_seedState.neighbors.Count == 0)
        {
            // State is an island, get closest state by sea, create continent with it or join created continent.
            // Add bridge: No distance limitations
            nghb = _findClosestSeaNeighbor(_seedState, out float distance, out Vector2I points);
            if(nghb == null)
            {
                GD.PrintErr("MapManager._spreadNewContinentFrom failed to find sea connection");
                return;
            }
            nghb.neighbors.Add(_seedState.id);
            _seedState.neighbors.Add(nghb.id);
            ownerPlanet.askBridgeCreation(points);
        }
        else
        {
            // Else seed state has neighbors:
            // Find the on with most shared boundaries and join or create continent
            nghb = _findStateWithMostSharedBorders(_seedState, false);
            if(nghb == null)
            {
                GD.PrintErr("MapManager._spreadNewContinentFrom failed to find most shared border");
                return;
            }
        }

        if(nghb.continentID == -1)
        {
            // Create Continent, add both
            Continent c = _getNewContinent(continents.Count);
            nghb.setContinentID(c.id);
            c.addState(nghb.id);
            _seedState.setContinentID(c.id);
            c.addState(_seedState.id);
        }
        else
        {
            // Join Continent
            Continent c = _getContinentByID(nghb.continentID);
            c.addState(_seedState.id);
            _seedState.setContinentID(c.id);
        }
    }

    private void _computeContinentsNeighbors()
    {
        foreach(Continent c in continents)
        {
            c.computeNeighbors(_getStateByStateID);
        }
    }

    private void _reduceContinentsNumber()
    {
        // Sort continents IDs by number of neighbors
        List<int> orderedContinentIds = new();
        continents.ForEach(c => orderedContinentIds.Add(c.id)); // not yet ordered

        // List is now ordered
        while(orderedContinentIds.Count > 0)
        {
            // Reorder each time as merges can drastically change neibhor counts for many continents
            orderedContinentIds.Sort((idA, idB) => 
            {
                Continent a = _getContinentByID(idA);
                Continent b = _getContinentByID(idB);
                return a.neighborsContinentIDs.Count - b.neighborsContinentIDs.Count;
            });

            // current continent has the lowest (or equal) neighbors
            Continent current = _getContinentByID(orderedContinentIds[0]);
            //GD.Print("Evaluating Continent_" + orderedContinentIds[0]);
            orderedContinentIds.RemoveAt(0);

            if(current.stateIDs.Count >= CONTINENT_MIN_SIZE)
            {
                // Continent has reached nice size, it won't merge on its own
                continue;
            }

            Continent merger = null;
            if(current.neighborsContinentIDs.Count == 0)
            {
                // Island Continent, merge with closest
                State closest = _findClosestSeaNeighbor(current, out float _, out Vector2I points, out State ownState);
                merger = _getContinentByID(closest.continentID);
                ownerPlanet.askBridgeCreation(points);
                closest.neighbors.Add(ownState.id);
                ownState.neighbors.Add(closest.id);
            }
            else
            {
                // Find continent with most shared borders
                Dictionary<int, int > bordersPerContinentsID = _computeSharedBoundaries(current);
                int maxSharedBorder = 0;
                int mergerIndex = -1;
                foreach(int id in bordersPerContinentsID.Keys)
                {
                    if(mergerIndex == -1 || maxSharedBorder < bordersPerContinentsID[id])
                    {
                        mergerIndex = id;
                        maxSharedBorder = bordersPerContinentsID[id];
                    }
                }
                
                if(mergerIndex == -1)
                {
                    GD.PrintErr("Could not merge by borders in MapManager.ReduceContinentsNumber");
                    continue;
                }
                merger = _getContinentByID(mergerIndex);
            }

            Continent newContinent = _mergeContinents(current, merger);
            orderedContinentIds.Remove(merger.id);

            if(newContinent.stateIDs.Count < CONTINENT_MIN_SIZE)
                orderedContinentIds.Add(newContinent.id);
        }

        //GD.Print("Finished continent merging at " + continents.Count + " continents:");
        //foreach(Continent c in continents)
        //{
        //    GD.Print("Continent_" + c.id + "(" + c.stateIDs.Count + ")");
        //}
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

    private State _findClosestSeaNeighbor(Continent _continent, out float _distance, out Vector2I _verticesFullMapIndices, out State _ownState)
    {
        _distance = 0.0f;
        _verticesFullMapIndices = new(-1, -1);
        State closest = null;
        _ownState = null;

        foreach(int stateID in _continent.stateIDs)
        {
            State s = _getStateByStateID(stateID);
            if(s.shores.Count == 0)
                continue;
            
            State candidate = _findClosestSeaNeighbor(s, out float dist, out Vector2I points, _continent.stateIDs);
            
            if(closest == null || _distance > dist)
            {
                closest = candidate;
                _distance = dist;
                _verticesFullMapIndices = points;
                _ownState = s;
            }
        }

        return closest;
    }

    // This will ignore all land direct and indirect neighbors
    private State _findClosestSeaNeighbor(State _state, out float _distance, out Vector2I _verticesFullMapIndices, in List<int> _preBlackListedStateIDs = null)
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
        if(_preBlackListedStateIDs != null)
            blackListIDs.AddRange(_preBlackListedStateIDs); // Don't care about duplicates, as we only use CONTAINS on this

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
