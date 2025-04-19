using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

public class MapManager
{
    public static MapManager Instance; // registered in constructor

    private Planet planet;

    MapNode[] map = new MapNode[Planet.MAP_SIZE];

    public Texture2D tex {get; private set;}

    private const int STATES_COUNT = 60; // 60 is so cool, can be divided by 2, 3, 4, 5 and 6, and it looks cool on the planet
    private const int MIN_STATE_SIZE = 100;
    private const float MAX_SQUARED_DISTANCE_FOR_STATE_MERGE = 1.5f;
    private const int MAX_IMAGE_SIZE = 16380; // GODOT Max is 16384 but rounding down for reasons (no)
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

    public MapManager(Planet _owner){ planet = _owner; Instance = this; }
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

    public State getStateOfVertex(int _vertexID)
    {
        if(_vertexID < 0 || _vertexID >= map.Length)
            return null;
        int stateID = map[_vertexID].stateID;
        if(stateID < 0) // Exclude Water(-1) and rogue islands(-2)
            return null;
        return getStateByStateID(stateID);
    }

    public void selectStateOfVertex(int _vertexID)
    {
        State toSelect = getStateOfVertex(_vertexID);
        if(toSelect == null) return;

        List<int> updatedStates = new();
        if(toSelect.id >= 0)
        {
            GD.Print(toSelect);
            /*
            updatedStates.Add(toSelect.id);
            _setStateYUV(toSelect, STATE_SELECTED_UV_VALUE);
            foreach(int stateID in toSelect.neighbors)
            {
                updatedStates.Add(stateID);
                State nghb = getStateByStateID(stateID);
                _setStateYUV(nghb, toSelect.continentID == nghb.continentID ? STATE_ALLY_UV_VALUE : STATE_ENEMY_UV_VALUE);
            }*/
        }
/*
        states.ForEach(state => 
        {
            if(!updatedStates.Contains(state.id))
            {
                _setStateYUV(state, 0.0f); // reset
            }
        });*/
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
                        if(map[i].isContinentBorder)
                            color = color.Lerp(Colors.Black, 0.95f); // make continent borders even darker for even nicer display
                        else if(map[i].isBorder() || map[i].waterBorder)
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
        stateIDs.Sort( (int idA, int idB) => { return State.comapreStateSize(getStateByStateID(idA), getStateByStateID(idB)); } );

        while(states.Count > STATES_COUNT)
        {
            // Pop the first element (smallest state) and find him a friend to merge with
            int stateID = stateIDs[0];
            stateIDs.RemoveAt(0);
            State currentState = getStateByStateID(stateID);
            State mergeState = null;

            if(currentState.neighbors.Count == 0) // Island state, merge accross sea if distance to closest is below threshold, or make rogue island (non played land)
            {
                if(currentState.land.Count > MIN_STATE_SIZE) // State island already has an acceptable size, no need to merge
                    continue;

                ClosestSeaNeigbhorData data = _findClosestSeaNeighbor(currentState);
                if(!data)
                {
                    GD.PrintErr("Could not _findClosestSeaNeighbor for State_" + currentState.id);
                    continue;
                }
                if(data.distance < MAX_SQUARED_DISTANCE_FOR_STATE_MERGE)
                {
                    mergeState = data.closestState;
                    // Spawn a bridge to link the island(s)
                    planet.askBridgeCreation(data.points);
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
                    State neighborState = getStateByStateID(nghbID);
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
                if(getStateByStateID(stateIDs[i]).land.Count > newStateSize)
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

        receiver.absorbContinent(giver, getStateByStateID);

        foreach(Continent c in continents)
        {
            if(c == receiver || c == giver)
                continue;
            c.swapNeighborIndex(giver.id, receiver.id);
        }

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
                getStateByStateID(neighborIndex).updateBordersAfterStateMerge(giver.id, receiver.id);
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
    public Continent getContinentByID(int id)
    {
        if(id < 0)
            return null;

        foreach(Continent c in continents)
        {
            if(c.id == id) return c;
        }

        GD.PrintErr("MapManager._getContinentByID could not find Continent_" + id);
        return null;
    }

    public State getStateByStateID(int id)
    {
        if(id < 0)
        {
            GD.PrintErr("MapManager.getStateByStateID was aksed negative id : State_" + id);
            return null;
        }

        foreach(State s in states)
        {
            if(s.id == id) return s;
        }

        GD.PrintErr("MapManager.getStateByStateID could not find State_" + id);
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
            State neighborState = getStateByStateID(neighborID);
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
            State s = getStateByStateID(stateID);
            Dictionary<int, int> stateBorderLenghtPerStateID = _computeSharedBoundaries(s);
            foreach(int nghbID in stateBorderLenghtPerStateID.Keys)
            {
                if(_c.stateIDs.Contains(nghbID) == false)
                {
                    // Neighboring state is part of another continent
                    State nghb = getStateByStateID(nghbID);

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
        return State.compareStateNeighborsNumber(getStateByStateID(_a), getStateByStateID(_b));
    }

    private void _buildContinents()
    {
        _smallIslandConnect();
        continents = new();
        _createContinents();
        _computeContinentsNeighbors();
        _connectContinents();
        _finalConnectivityCheck();
        _computeMapNodesContinentBorders();
        _computeContinentsGameScore();
    }

    /// <summary>
    /// Connect islands too small to be independant continent to other land, until enough states are connected.
    /// Does not create any continents
    /// </summary>
    private void _smallIslandConnect()
    {
        List<State> treated = new();
        foreach(State s in states)
        {
            if(treated.Contains(s)) continue;

            List<State> connecteds = _getAllConnectedStatesFrom(s);
            if(connecteds.Count >= CONTINENT_MIN_SIZE)
            {
                treated.AddRange(connecteds); // Will treat those after all the tiny island have been connected
                continue;
            }

            while(connecteds.Count < CONTINENT_MIN_SIZE)
            {
                List<int> connectedIDs = new();
                connecteds.ForEach((state) => connectedIDs.Add(state.id));
                ClosestSeaNeigbhorData data = _findClosestSeaNeighbor(connectedIDs);
                if(!data)
                {
                    GD.PrintErr("MapManager._findClosestSeaNeighbor in _smallIslandConnect failed");
                    break;
                }
                // Connect them both
                _doBridge(data);
                connecteds = _getAllConnectedStatesFrom(s);
            }
            treated.AddRange(connecteds);
        }
    }

    private void _createContinents()
    {
        foreach(State s in states)
        {
            if(s.continentID != -1) continue;
            List<State> connecteds = _getAllConnectedStatesFrom(s); // starting state is included in result list
            _createContinentsFromStateGroup(connecteds, 0);
        }
    }

    private void _createNewContinentFromStateGroup(List<State> _group)
    {
        // All connected states can join the same continent
        continents.Add(new(continents.Count));
        foreach(State state in _group)
        {
            state.setContinentID(continents.Last().id);
            continents.Last().addState(state.id);
        }
    }

    /// <summary>
    /// Will handle state creation of a Component, meaning all states given in _group must be connected
    /// </summary>
    private void _createContinentsFromStateGroup(List<State> _group, int depth)
    {
        if(depth > 10) // just in case
        {
            GD.PrintErr("MapManager._createContinentsFromStateGroup reached max recursion count. Last group: " + _group.Count);
            _createNewContinentFromStateGroup(_group);
            return;
        }
        if(_group.Count > CONTINENT_MAX_SIZE)
        {
            _splitStatesGroup(_group, depth);
        }
        else
        {
            _createNewContinentFromStateGroup(_group);
        }
    }

    /// <summary>
    /// Use Graph Theory "VertexCut" to create somewhat nice looking continents
    /// </summary>
    private void _splitStatesGroup(List<State> _toSplit, int depth)
    {
        List<VertexCutData> cuts = _tryFindVertexCuts(_toSplit);
        cuts.ForEach((cut) => _attributeBestComponentToIncludeCut(cut));

        // Remove unusable cuts, that leave too small parts
        for(int i = cuts.Count - 1; i >= 0; --i)
        {
            VertexCutData cut = cuts[i];
            string text = cut.ToString();
            int smallestComponent = -1;
            for(int componentIndex = 0; componentIndex < cut.components.Count; ++componentIndex)
            {
                int componentSize = cut.components[componentIndex].Count;
                if(cut.optimalCutComponentIndex == componentIndex)
                    componentSize += cut.statesCut.Count;
                if(smallestComponent == -1 || componentSize < smallestComponent)
                    smallestComponent = componentSize;
            }
            if(smallestComponent < CONTINENT_MIN_SIZE)
                cuts.RemoveAt(i);
        }
        List<State> statesForContinent = new();
        if(_toSplit.Count <= CONTINENT_MAX_SIZE || cuts.Count == 0) // did not find possible cuts -> happens for disc-like continent blobs with high connectivity
        {
            // make continent from what's left
            _createNewContinentFromStateGroup(_toSplit);
            return;
        }
        
        // Find best cut(s)
        int numberOfContinentsToBuild = _toSplit.Count / CONTINENT_MAX_SIZE + 1;
        int meanContinentSize = _toSplit.Count / numberOfContinentsToBuild; // This is very theoretical and will be more of a reference than a decision point

        VertexCutData selectedCut = _findBestCut(cuts, meanContinentSize);
        // Build new groups from cut, and restart process with each subgraph
        for(int i = 0; i < selectedCut.components.Count; ++i)
        {
            List<State> group = selectedCut.components[i];
            if(selectedCut.optimalCutComponentIndex == i)
            {
                group.AddRange(selectedCut.statesCut);
            }
            _createContinentsFromStateGroup(group, depth + 1);
        }
    }

    private VertexCutData _findBestCut(List<VertexCutData> _cuts, int _targetContinentSize)
    {
        // Find the cut that separate a chunk closest to _targetContinentSize
        int cutId = -1;     // best cut index in cuts
        int size = -1;      // Get size closest to _targetContinentSize
        int priority = 0;   // Number of state(s) cutVertices, lower gets priority
        for(int i = 0; i < _cuts.Count; ++i)
        {
            if(_cuts[i].optimalCutComponentIndex == -1)
                GD.PrintErr("Cut in _findBestCut has no optimal cut component");
            int smallestComponentIndex = -1;
            int smallestComponentSize = -1;
            for(int ci = 0; ci < _cuts[i].components.Count; ++ci)
            {
                int ciSize = _cuts[i].components[ci].Count;
                if(_cuts[i].optimalCutComponentIndex == ci)
                    ciSize += _cuts[i].statesCut.Count;
                if(smallestComponentIndex == -1 || smallestComponentSize > ciSize)
                {
                    smallestComponentIndex = ci;
                    smallestComponentSize = ciSize;
                }
            }

            if(cutId == -1                                                                                          // First loop
            || Mathf.Abs(size - _targetContinentSize) > Mathf.Abs(smallestComponentSize - _targetContinentSize)     // or Closer to target
            || (smallestComponentSize == size && priority > _cuts[i].statesCut.Count))                              // or ( SameSize AND priority )
            {
                cutId = i;
                size = smallestComponentSize;
                priority = _cuts[i].statesCut.Count;
            }
        }
        return _cuts[cutId];
    }

    private void _attributeBestComponentToIncludeCut(VertexCutData _cut)
    {
        int[] componentBorderLenght = new int[_cut.components.Count];
        foreach(State s in _cut.statesCut)
        {
            Dictionary<int, int> borderLenghtPerState = _computeSharedBoundaries(s);
            foreach(int stateID in borderLenghtPerState.Keys)
            {
                State nghb = getStateByStateID(stateID);
                int componentIndex = _cut.getComponentIndexOfState(nghb);
                if(componentIndex == -1)
                    continue; // State not in component (most likely a cut)
                componentBorderLenght[componentIndex] += borderLenghtPerState[stateID];
            }
        }
        int largerBoundary = -1;
        int index = -1;
        for(int i = 0; i < componentBorderLenght.Length; ++i)
        {
            if(index == -1 || largerBoundary < componentBorderLenght[i])
            {
                largerBoundary = componentBorderLenght[i];
                index = i;
            }
        }
        _cut.optimalCutComponentIndex = index;
    }

    class VertexCutData
    {
        public List<State> statesCut = new(); // Removing this/these state(s) from graph
        public List<List<State>> components = new(); // results in the graph separated in two or more components
        public int optimalCutComponentIndex = -1; // In order to minimize borders between continents, statesCut should be included in the component of that index
        public override string ToString()
        {
            string s = "Cut:";
            statesCut.ForEach((state) => s += " S_" + state.id);
            s += " spliting graph in ";
            bool first = true;
            for(int i = 0; i < components.Count; ++i)
            {
                if(first == false)
                    s += ", ";
                else
                    first = false;
                s += components[i].Count;
                if(optimalCutComponentIndex == i)
                    s += "(o)";
            }
            if(optimalCutComponentIndex == -1)
                s += "(-o)";
            return s;
        }

        public int getComponentIndexOfState(State _s)
        {
            for(int i = 0; i < components.Count; ++i)
            {
                if(components[i].Contains(_s)) return i;
            }
            return -1;
        }
    }

    private List<VertexCutData> _tryFindVertexCuts(List<State> _graph)
    {
        List<VertexCutData> cuts = new();

        List<List<int>> cuttersToIgnore = new();
        bool testCut(List<int> _cutters)
        {
            // Any vertex added to a vertex cut would cut as well, but it does not bring any information about graph structure
            // So skip when containing an already made full cut
            foreach(List<int> vertexCutters in cuttersToIgnore)
            {
                bool containsAll = true;
                foreach(int vertexCut in vertexCutters)
                {
                    if(_cutters.Contains(vertexCut) == false)
                    {
                        containsAll = false;
                        break;
                    }
                }
                if(containsAll)
                    return false; // _cutters contains an already full set of cutter(s) state(s), skip
            }
            VertexCutData data = new();
            int starterID = 0;
            while(_cutters.Contains(starterID)) starterID++; // Don't start with a cutter, result would be empty -> false posititve
            List<State> testVertexCut = new();
            _cutters.ForEach((i) => testVertexCut.Add(_graph[i]));
            List<State> connecteds = _getAllConnectedStatesFrom(_graph[starterID], testVertexCut, _graph);
            if(connecteds.Count == _graph.Count - testVertexCut.Count) // removing the cut candidates
                return false; // states are not a VertexCut, go next

            // State is vertex cut, it splits _graph in two or more: create a cut data
            cuttersToIgnore.Add(new(_cutters));
            data.statesCut = new(testVertexCut);
            data.components.Add(new(connecteds));
            List<State> added = new(connecteds);
            added.AddRange(testVertexCut);

            foreach(State s in _graph)
            {
                if(added.Contains(s))
                    continue;
                data.components.Add(_getAllConnectedStatesFrom(s, testVertexCut, _graph));
                added.AddRange(data.components.Last());
            }
            
            cuts.Add(data);
            return true;
        }
        // Single Cuts
        for(int i = 0; i < _graph.Count; ++i)
        {
            testCut(new(){i});
        }
        // Double cuts
        for(int j = 0; j < _graph.Count; ++j)
        {
            for(int i = j+1; i < _graph.Count; ++i)
            {
                testCut(new(){j,i});
            }
        }
        // Triple Cuts -> 4 would be overkill
        for(int k = 0; k < _graph.Count; ++k)
        {
            for(int j = k+1; j < _graph.Count; ++j)
            {
                for(int i = j+1; i < _graph.Count; ++i)
                {
                    testCut(new(){k,j,i});
                }
            }
        }
        return cuts;
    }

    /// <summary>
    /// Increase connectivity to ensure every continent is linked (directly or indirectly) to all other continents.
    /// Add other bridges for short hops
    /// </summary>
    private void _connectContinents()
    {
        float forceBridgeDistance = 0.12f;
        float bridgeMaxDistance = 2.0f;

        foreach(Continent c in continents)
        {
            List<ClosestSeaNeigbhorData> bridgeCandidates = new();
            // Custom findClosestSeaNeighbor continent, as we want specific behavior (won't always ignore the same ids)
            foreach(int stateID in c.stateIDs)
            {
                State s = getStateByStateID(stateID);
                if(s.shores.Count == 0)
                    continue;

                List<int> ignored = new();
                ignored.AddRange(c.stateIDs);           // Ignoring all continent states -> normal behavior
                foreach(int nghbId in s.neighbors)
                {
                    if(ignored.Contains(nghbId) == false)
                        ignored.Add(nghbId);            // Ignoring other continent's state that are already connected to this state
                }

                int loops = 0;
                while(loops++ < 5) // wouldn't want an infinite loop
                {
                    ClosestSeaNeigbhorData data = _findClosestSeaNeighbor(s, ignored);
                    if(!data)
                        break;

                    if(data.distance < forceBridgeDistance) // If bridge is smaller than our threshold value
                    {
                        _doBridge(data); // Instantly add it without further logic

                        // Keep looking for bridges until closest one is above force distance
                        ignored.Add(data.closestState.id); // but ignore newly connected state
                        continue;
                    }

                    if(data.distance > bridgeMaxDistance)
                        break; // If closest neigbhor is this far, there's no need to go on

                    bool connectsNewContinent = c.neighborsContinentIDs.Contains(data.closestState.continentID) == false;
                    if(connectsNewContinent)
                    {
                        bridgeCandidates.Add(data);
                        break; // bridge is above force distance, we'll evaluate it against all the others, get outta here
                    }
                    else
                    {
                        // Bridge is still not too long, but continent is already connected -> Ignore all states of this continent
                        ignored.AddRange(getContinentByID(data.closestState.continentID).stateIDs);
                    }
                }
                if(loops == 5)
                {
                    GD.PrintErr("Reached max iterations count of MapManager._connectContinents");
                    setStateSelected(s);
                }
            }
            // We may have created bridges to other continents before adding a short bridge
            for(int i = bridgeCandidates.Count - 1; i >= 0; --i)
            {
                if(c.neighborsContinentIDs.Contains(bridgeCandidates[i].closestState.continentID))
                    bridgeCandidates.RemoveAt(i);
            }
            // Sort bridges from smaller to larger
            bridgeCandidates.Sort((dataA, dataB) => 
            {
                if(dataA.distance > dataB.distance) return 1;
                if(dataA.distance < dataB.distance) return -1;
                return 0;
            });

            while(bridgeCandidates.Count > 0)
            {
                // Build smallest, it will connect a new continent.
                _doBridge(bridgeCandidates[0]);
                int otherID = bridgeCandidates[0].closestState.continentID;
                bridgeCandidates.RemoveAt(0);
                // Remove all candidate bridges connecting to this continent
                for(int i = bridgeCandidates.Count - 1; i >= 0; --i)
                {
                    if(bridgeCandidates[i].closestState.continentID == otherID)
                        bridgeCandidates.RemoveAt(i);
                }
            }
        }

    }

    /// <summary>
    /// Makes sure all continents are direcly or indireclty all connected to each others.
    /// Creates connection to isolated continents if needed
    /// </summary>
    private void _finalConnectivityCheck()
    {
        Queue<Continent> continentsToScan = new();
        List<Continent> scanned = new();
        List<List<Continent>> components = new();

        //Find first
        foreach(Continent c in continents)
        {
            if(scanned.Contains(c))
                continue;

            continentsToScan.Enqueue(c);
            int componentIndex = components.Count;
            components.Add(new());
            while(continentsToScan.Count > 0)
            {
                Continent current = continentsToScan.Dequeue();
                if(scanned.Contains(current))
                    continue;
                scanned.Add(current);
                components.Last().Add(current);
                current.neighborsContinentIDs.ForEach((id) => continentsToScan.Enqueue(getContinentByID(id)));
            }
        }

        if(components.Count == 1)
        {
            GD.Print("All continents are already connected"); // Perfect, will happen most of the time with current brides rules
            return;
        }

        while(components.Count > 1) // Connect the second component to anything, until only one component is left (i.e. all continents are connected)
        {
            List<int> connectedStates = new();
            components[1].ForEach((c) => connectedStates.AddRange(c.stateIDs));
            ClosestSeaNeigbhorData data = new();
            foreach(Continent c in components[1])
            {
                data = _findClosestSeaNeighbor(c.stateIDs, connectedStates);
                if(!data)
                    continue; // a bummer really, should not happen, but if it does, try with another connected continent (hopefully there's one)
                break;
            }

            if(!data)
            {
                // Couldn't bridge to another continent, that's bad, actual real bummer
                GD.PrintErr("Could no bridge continent group to another");
                return; // get out this infinite loop
            }

            _doBridge(data);
            Continent linkedContinent = getContinentByID(data.closestState.continentID);
            int componentIndex = 0;
            for(int i = 0; i < components.Count; ++i)
            {
                if(components[i].Contains(linkedContinent))
                {
                    componentIndex = i;
                    break;
                }
            }

            // Merge Components to linked
            components[componentIndex].AddRange(components[1]);
            components.RemoveAt(1);
            GD.Print("Connected two continent components");
        }
    }

    /// <summary>
    /// Go throught each MapNode of borders to set their ContinentBorder bool if suited
    /// </summary>
    private void _computeMapNodesContinentBorders()
    {
        foreach(Continent c in continents)
        {
            foreach(int stateID in c.stateIDs)
            {
                State s = getStateByStateID(stateID);
                foreach(int landID in s.boundaries)
                {
                    bool isBorder = false;
                    foreach(int otherStateID in s.land[landID].borderingStateIds)
                    {
                        if(getStateByStateID(otherStateID).continentID != c.id)
                        {
                            isBorder = true;
                            break;
                        }
                    }
                    if(isBorder)
                        s.land[landID].isContinentBorder = true;
                }
            }
        }
    }

    private void _computeContinentsGameScore()
    {
        continents.ForEach((c) => c.computeScore(getStateByStateID));
    }

    /// <summary>
    /// Finds all the connected state. When ignoring states, take care not to start from an ignored state, otherwise result will be empty
    /// </summary>
    private List<State> _getAllConnectedStatesFrom(State _s, List<State> _ignoredStates = null, List<State> _whitelist = null)
    {
        List<State> connecteds = new();
        Queue<State> toScan = new();
        toScan.Enqueue(_s);
        while(toScan.Count > 0)
        {
            State currentState = toScan.Dequeue();
            if(connecteds.Contains(currentState) || (_ignoredStates != null && _ignoredStates.Contains(currentState)) || (_whitelist != null && _whitelist.Contains(currentState) == false))
                continue;
            connecteds.Add(currentState);
            foreach(int id in currentState.neighbors)
            {
                toScan.Enqueue(getStateByStateID(id));
            }
        }
        return connecteds;
    }

    private void _computeContinentsNeighbors()
    {
        foreach(Continent c in continents)
        {
            c.computeNeighbors(getStateByStateID);
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
                State state = getStateByStateID(statesToScan[0]);
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

    private void _doBridge(ClosestSeaNeigbhorData _data)
    {
        planet.askBridgeCreation(_data.points);
        if(!_data)
            return; // States might not be a thing at the call's time
        _data.closestState.neighbors.Add(_data.connectorState.id);
        _data.connectorState.neighbors.Add(_data.closestState.id);
        Continent from = getContinentByID(_data.connectorState.continentID);
        Continent to = getContinentByID(_data.closestState.continentID);
        if(from != null && to != null && from.id != to.id) // Continents might not be a thing at the call's time
        {
            from.addContinentNeighbor(to.id);
            to.addContinentNeighbor(from.id);
        }
    }

    struct ClosestSeaNeigbhorData
    {
        public ClosestSeaNeigbhorData(){closestState = null; connectorState = null; distance = 0.0f; points = new(-1,-1);}
        public State closestState;
        public State connectorState;
        public float distance;
        public Vector2I points;

        public static implicit operator bool(ClosestSeaNeigbhorData _data)
        {
            return _data.closestState != null;
        }
    }
    private ClosestSeaNeigbhorData _findClosestSeaNeighbor(Continent _continent)
    {
        return _findClosestSeaNeighbor(_continent.stateIDs);
    }

    private ClosestSeaNeigbhorData _findClosestSeaNeighbor(List<int> _stateIDs, in List<int> _preBlackListedStateIDs = null)
    {
        List<int> totalBlackListed = new();
        totalBlackListed.AddRange(_stateIDs);
        if(_preBlackListedStateIDs != null)
            totalBlackListed.AddRange(_preBlackListedStateIDs);

        ClosestSeaNeigbhorData data = new();
        foreach(int stateID in _stateIDs)
        {
            State s = getStateByStateID(stateID);
            if(s.shores.Count == 0)
                continue;
            
            ClosestSeaNeigbhorData candidate = _findClosestSeaNeighbor(s, totalBlackListed);
            
            if(!data || data.distance > candidate.distance)
            {
                data = candidate;
            }
        }
        return data;
    }

    // This will ignore all land direct and indirect neighbors, and blacklisted
    private ClosestSeaNeigbhorData _findClosestSeaNeighbor(State _state, in List<int> _preBlackListedStateIDs = null)
    {
        //float usecStart = Time.GetTicksUsec();
        // First try to find a bridge without caring for land crossing. Only check final one.
        ClosestSeaNeigbhorData data = _findClosestSeaNeighbor(false, _state, _preBlackListedStateIDs);
        if(!data)
        {
            // Did not find any neigbhors, maybe state has no shores or too many states are excluded
            return data;
        }
        //GD.Print("Bridge finding NO LAND CHECK took " + ((Time.GetTicksUsec() - usecStart) * 0.000001) + " secs.");
        if(planet.fastBridges || _doesPathOnlyCrossWater(data.points[0], data.points[1])) // If final bridge is ok, perfect we saved a hell lot of time
            return data;
        // If not, too bad we'll have to REDO it all AND do pathfinding for EACH try, this little maneuver is gonna cost us 51 years (roughly 1 second instead of 0.005)
        //usecStart = Time.GetTicksUsec();
        data = _findClosestSeaNeighbor(true, _state, _preBlackListedStateIDs);
        //GD.Print("Bridge finding FULL LAND CHECK took " + ((Time.GetTicksUsec() - usecStart) * 0.000001) + " secs.");
        return data;
    }

    private ClosestSeaNeigbhorData _findClosestSeaNeighbor(bool _checkPathOverLand, State _state, in List<int> _preBlackListedStateIDs = null)
    {
        ClosestSeaNeigbhorData data = new();
        data.connectorState = _state;

        if(_state.shores.Count == 0)
        {
            GD.PrintErr("Called _findClosestSeaNeighbor on State_" + _state.id + " which has no shores");
            return data;
        }

        List<int> blackListIDs = landMassesStatesIndices[_state.landMassID];
        if(_preBlackListedStateIDs != null)
            blackListIDs.AddRange(_preBlackListedStateIDs); // Don't care about duplicates, as we only use CONTAINS on this

        foreach(State s in states)
        {
            if(blackListIDs.Contains(s.id)) // this will exclude our starting state as well, which is nice
                continue;
            if(s.shores.Count == 0)
                continue;
            // Evaluated state s has shores and is not on the same landMass as our starting state nor on the ignored list given
            // Compare each of its shores with ours
            foreach(int ourLandID in _state.shores)
            {
                if(_state.boundaries.Contains(ourLandID))
                    continue;

                foreach(int otherLandID in s.shores)
                {
                    if(s.boundaries.Contains(otherLandID))
                        continue;
                    int ourIndex = _state.land[ourLandID].fullMapIndex;
                    int otherIndex = s.land[otherLandID].fullMapIndex;
                    if(_checkPathOverLand && _doesPathOnlyCrossWater(ourIndex, otherIndex) == false)
                        continue; // This as a bridge would cross over water, this is now forbidden
                    float distance = planet.getSquareDistance(ourIndex, otherIndex);
                    if(!data || data.distance > distance)
                    {
                        data.closestState = s;
                        data.distance = distance;
                        data.points = new(ourIndex, otherIndex);
                    }
                }
            }
        }
        return data;
    }

    private bool _doesPathOnlyCrossWater(int _startIndex, int _endIndex, bool debugPrint = false)
    {
        // Sample along straight path and test land or water
        Vector3 start = planet.getVertex(_startIndex);
        Vector3 end = planet.getVertex(_endIndex);
        // Don't use squard dist as that would increase sample number for greater distances. Low sample count to save a bit of time
        int samples = (int)(start.DistanceTo(end) * 2.5f) + 1;
        if(debugPrint)
        {
            planet.setUVYAtIndex(_startIndex, -1.0f);
            planet.setUVYAtIndex(_endIndex, -1.0f);
            GD.Print("Path check uses " + samples + " for a dist of " + start.DistanceTo(end));
        }
        for(int i = 0; i < samples; ++i)
        {
            float percent = (i+1) * 1.0f / (samples+1); // We don't want to start at zero, and don't want to end at 1 (we know these are lands)
            Vector3 pos = start.Lerp(end, percent);
            pos = pos.Normalized() * Planet.PLANET_RADIUS;

            int nodeIndex = planet.nodeFinder.findNodeIndexAtPosition(pos);

            if(debugPrint)
                planet.setUVYAtIndex(nodeIndex, 2.0f);

            if(map[nodeIndex].nodeType == MapNode.NodeType.Land)
                return false; // we found land, we can early out
        }
        return true;
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
            planet.setUVYAtIndex(node.fullMapIndex, _value);
        }
    }

    public void setStateSelected(State _s)
    {
        _setStateYUV(_s, STATE_SELECTED_UV_VALUE);
    }
    public void setStatehighlightAlly(State _s)
    {
        _setStateYUV(_s, STATE_ALLY_UV_VALUE);
    }
    public void setStateHighlightEnemy(State _s)
    {
        _setStateYUV(_s, STATE_ENEMY_UV_VALUE);
    }
    public void resetStateHighlight(State _s)
    {
         _setStateYUV(_s, 0.0f);
    }
}

public class MapNode // this is not a struct only because i could not bother create a constructor for all those members
{
    public const int INVALID_ID = -1;
    public enum NodeType{Water, Land, RogueIsland};
    public bool isBorder(){return borderingStateIds.Count > 0;}
    public bool isContinentBorder = false;
    public bool waterBorder = false;
    public List<int> borderingStateIds = new();
    public NodeType nodeType = NodeType.Land;
    public int stateID = INVALID_ID;
    public int continentID = INVALID_ID;
    public float height = 0.0f;
    public int fullMapIndex = INVALID_ID;
}
