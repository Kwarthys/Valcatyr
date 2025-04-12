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

public partial class MapManager : Node
{
    private Planet planet;

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

    public MapManager(Planet _owner){ planet = _owner; }
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
                    planet.askBridgeCreation(pointsFullMapIndices);
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
                State closest = _findClosestSeaNeighbor(connectedIDs, out float distance, out Vector2I points, out State closestConnected);
                if(closest == null)
                {
                    GD.PrintErr("MapManager._findClosestSeaNeighbor in _smallIslandConnect failed");
                    break;
                }
                // Connect them both
                planet.askBridgeCreation(points);
                closest.neighbors.Add(closestConnected.id);
                closestConnected.neighbors.Add(closest.id);
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

            if(connecteds.Count > CONTINENT_MAX_SIZE)
            {
                _splitStatesIntoContinents(connecteds);
            }
            else
            {
                // All connected states can join the same continent
                continents.Add(new(continents.Count));
                foreach(State state in connecteds)
                {
                    state.setContinentID(continents.Last().id);
                    continents.Last().addState(state.id);
                }
            }
        }
    }

    /// <summary>
    /// Use Graph Theory "VertexCut" to create somewhat nice looking continents
    /// </summary>
    private void _splitStatesIntoContinents(List<State> _toSplit)
    {
        List<VertexCutData> cuts = _tryFindVertexCuts(_toSplit);
        int originalCount = cuts.Count();

        bool last = false;
        while(_toSplit.Count > 0)
        {
            // Remove unusable cuts, that leave too small parts
            for(int i = cuts.Count - 1; i >= 0; --i)
            {
                VertexCutData cut = cuts[i];
                string text = cut.ToString();
                int smallestSide = Mathf.Min(cut.componentA.Count, cut.componentB.Count);
                if(smallestSide + cut.statesCut.Count < CONTINENT_MIN_SIZE)
                    cuts.RemoveAt(i);
            }
            List<State> statesForContinent = new();
            if(_toSplit.Count <= CONTINENT_MAX_SIZE || cuts.Count == 0) // cut enough times or did not find possible cuts -> happens for sphere-like continent shapes with high connectivity
            {
                // make continent from what's left
                statesForContinent.AddRange(_toSplit);
                last = true;
            }
            else
            {
                // Now find best cut(s)
                int numberOfContinentsToBuild = _toSplit.Count / CONTINENT_MAX_SIZE + 1;
                int meanContinentSize = _toSplit.Count / numberOfContinentsToBuild; // This is very theoretical and will be more of a referene than a decision point

                VertexCutData selectedCut = _findBestCut(cuts, meanContinentSize, out bool includeCuts, out bool useSideA);
                // Do Cut
                if(useSideA)
                    statesForContinent.AddRange(selectedCut.componentA);
                else
                    statesForContinent.AddRange(selectedCut.componentB);
                if(includeCuts)
                    statesForContinent.AddRange(selectedCut.statesCut);
            }

            continents.Add(new(continents.Count));
            foreach(State s in statesForContinent)
            {
                s.setContinentID(continents.Last().id);
                continents.Last().addState(s.id);
                _toSplit.Remove(s);
            }

            if(last)
                return; // no need to clean, we're outta here

            // Remove cuts about the part we just cut, to be reusable next round
            for(int i = cuts.Count - 1; i >= 0; --i)
            {
                bool removed = false;
                VertexCutData cut = cuts[i];
                foreach(State cutState in cut.statesCut)
                {
                    if(statesForContinent.Contains(cutState))
                    {
                        cuts.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }
                if(!removed) // no need to do that if we removed it
                {
                    foreach(State s in statesForContinent)
                    {
                        if(cut.componentA.Contains(s))
                            cut.componentA.Remove(s);
                        if(cut.componentB.Contains(s))
                            cut.componentB.Remove(s);
                    }
                }
            }
        }
    }

    private VertexCutData _findBestCut(List<VertexCutData> _cuts, int _targetContinentSize, out bool _includeCut, out bool _sideA)
    {
        // Find the cut that separate a chunk closest to _targetContinentSize
        int cutId = -1;
        int size = -1;
        int priority = 0; // 1 -> single Cut - 2 -> Double cut : Simple cuts have priority
        _includeCut = false;
        _sideA = false;
        for(int i = 0; i < _cuts.Count; ++i)
        {
            int cutSize = _cuts[i].statesCut.Count;
            int sizeA = _cuts[i].componentA.Count;   // Use one of the 4, the one closest to _targetContinentSize and below CONTINENT MAX SIZE
            int sizeAWithCut = sizeA + cutSize;
            int sizeB = _cuts[i].componentB.Count;
            int sizeBWithCut = sizeB + cutSize;
            bool sideAHasCuts = false;
            bool sideBHasCuts = false;

            if(Mathf.Abs(sizeA - _targetContinentSize) > Mathf.Abs(sizeAWithCut - _targetContinentSize) && sizeAWithCut < CONTINENT_MAX_SIZE)
            {
                sizeA = sizeAWithCut;
                sideAHasCuts = true;
            }

            if(Mathf.Abs(sizeB - _targetContinentSize) > Mathf.Abs(sizeBWithCut - _targetContinentSize) && sizeBWithCut < CONTINENT_MAX_SIZE)
            {
                sizeB = sizeBWithCut;
                sideBHasCuts = true;
            }

            int candidateSize = 0;
            bool candidateUseCuts = false;
            bool candidateUsesA = false;
            if(Mathf.Abs(sizeA - _targetContinentSize) < Mathf.Abs(sizeB - _targetContinentSize) && sizeA < CONTINENT_MAX_SIZE)
            {
                candidateSize = sizeA;
                candidateUseCuts = sideAHasCuts;
                candidateUsesA = true;
            }
            else
            {
                candidateSize = sizeB;
                candidateUseCuts = sideBHasCuts;
            }

            bool priorityWins = priority > cutSize;
            if(cutId == -1                                                                                // If first loop
            || Mathf.Abs(candidateSize - _targetContinentSize) < Mathf.Abs(size - _targetContinentSize)   // Or if size is better
            || (candidateSize == size && priorityWins)                                                    // Or if size is same but we have a smaller cut
            || size > CONTINENT_MAX_SIZE                                                                  // Or current selected size is too large
            )
            {
                size = candidateSize;
                _includeCut = candidateUseCuts;
                priority = cutSize;
                cutId = i;
                _sideA = candidateUsesA;
            }
        }
        return _cuts[cutId];
    }

    struct VertexCutData
    {
        public List<State> statesCut; // Removing this/these state(s) from graph
        public List<State> componentA; // results in the graph separated in two components
        public List<State> componentB; // A and B, which are other states, without the cuts
        public override string ToString()
        {
            string s = "Cut:";
            foreach(State state in statesCut)
                s += " " + state.id;
            s += " spliting graph in " + componentA.Count + " and " + componentB.Count;
            return s;
        }
    }

    private List<VertexCutData> _tryFindVertexCuts(List<State> _graph)
    {
        List<VertexCutData> cuts = new();

        List<State> getMissingIn(List<State> _source)
        {
            List<State> l = new();
            foreach(State s in _graph)
            {
                if(_source.Contains(s) == false)
                    l.Add(s);
            }
            return l;
        }

        List<int> singleCuttersToIgnoreInDoubles = new();

        // Single Cuts
        for(int i = 0; i < _graph.Count; ++i)
        {
            State cutCandidate = _graph[i];
            // do the connectivity search, pretending cutCandidate doesn't exist
            List<State> connecteds = _getAllConnectedStatesFrom(_graph[i != 0 ? 0 : 1], new(){cutCandidate});
            if(connecteds.Count == _graph.Count - 1) // removing the cut candidate
                continue; // state is not a VertexCut, go next

            // State is vertex cut, it splits _graph in two: create a cut data
            VertexCutData data = new();
            data.statesCut = new(){cutCandidate};
            data.componentA = new(connecteds);
            data.componentB = getMissingIn(connecteds);
            data.componentB.Remove(cutCandidate);
            cuts.Add(data);
            singleCuttersToIgnoreInDoubles.Add(i);
        }

        // Double cuts -> could do triple but not sure its useful given the size of our continents
        for(int j = 0; j < _graph.Count; ++j)
        {
            for(int i = j+1; i < _graph.Count; ++i)
            {
                if(singleCuttersToIgnoreInDoubles.Contains(j) || singleCuttersToIgnoreInDoubles.Contains(i))
                    continue;

                int starterID = 0;
                while(starterID == j || starterID == i) starterID++;
                // Pretend states at j and i don't exist
                List<State> connecteds = _getAllConnectedStatesFrom(_graph[starterID], new(){_graph[j], _graph[i]});
                if(connecteds.Count == _graph.Count - 2) // removing the cut candidates
                    continue; // states are not a VertexCut, go next

                // State is vertex cut, it splits _graph in two: create a cut data
                VertexCutData data = new();
                data.statesCut = new(){_graph[j], _graph[i]};
                data.componentA = new(connecteds);
                data.componentB = getMissingIn(connecteds);
                data.componentB.Remove(_graph[j]);
                data.componentB.Remove(_graph[i]);
                cuts.Add(data);
            }
        }

        return cuts;
    }

    /// <summary>
    /// Finds all the connected state. When ignoring states, take care not to start from an ignored state, otherwise result will be empty
    /// </summary>
    private List<State> _getAllConnectedStatesFrom(State _s, List<State> _ignoredStates = null)
    {
        List<State> connecteds = new();
        Queue<State> toScan = new();
        toScan.Enqueue(_s);
        while(toScan.Count > 0)
        {
            State currentState = toScan.Dequeue();
            if(connecteds.Contains(currentState) || (_ignoredStates != null && _ignoredStates.Contains(currentState)))
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

    private State _findClosestSeaNeighbor(Continent _continent, out float _distance, out Vector2I _verticesFullMapIndices, out State _ownState)
    {
        return _findClosestSeaNeighbor(_continent.stateIDs, out _distance, out _verticesFullMapIndices, out _ownState);
    }

    private State _findClosestSeaNeighbor(List<int> _stateIDs, out float _distance, out Vector2I _verticesFullMapIndices, out State _ownState)
    {
        _distance = 0.0f;
        _verticesFullMapIndices = new(-1, -1);
        State closest = null;
        _ownState = null;

        foreach(int stateID in _stateIDs)
        {
            State s = getStateByStateID(stateID);
            if(s.shores.Count == 0)
                continue;
            
            State candidate = _findClosestSeaNeighbor(s, out float dist, out Vector2I points, _stateIDs);
            
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

        if(planet == null)
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
                    float distance = planet.getSquareDistance(ourIndex, otherIndex);
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
    public bool waterBorder = false;
    public List<int> borderingStateIds = new();
    public NodeType nodeType = NodeType.Land;
    public int stateID = INVALID_ID;
    public int continentID = INVALID_ID;
    public float height = 0.0f;
    public int fullMapIndex = INVALID_ID;
}
