using Godot;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;


public class State
{
    public List<MapNode> land = new();
    public List<int> boundaries = new(); // boundaries array contains index to MapNode in land
    public List<int> neighbors = new(); // StateIds of neighboring states
    public List<int> shores = new(); // Indices of nodes in Land with water as neighbor
    public int id = -1;
    public int continentID {get; private set;} = -1;
    public int landMassID = -1; // Continents may contain multiple islands, land mass only cares about direct land neighbors
    
    public Vector3 barycenter = new();

    public Texture2D stateShapeTexture {get; private set;}

    public static int comapreStateSize(State _a, State _b)
    {
        int sizeA = _a.land.Count;
        int sizeB = _b.land.Count;
        if(sizeA == sizeB)
            return 0;
        return sizeA > sizeB ? 1 : -1;
    }

    public static int compareStateNeighborsNumber(State _a, State _b)
    {
            int countA = _a.neighbors.Count;
            int countB = _b.neighbors.Count;
            if(countA > countB) return 1;
            if(countA < countB) return -1;
            return 0;
    }

    public void setContinent(Continent _continent)
    {
        continentID = _continent.id;
        foreach(MapNode n in land)
        {
            n.continentID = _continent.id;
            n.colorID = _continent.colorID;
        }
    }

    public void setColorID(int _id)
    {
        foreach(MapNode n in land)
        {
            n.colorID = _id;
        }
    }

    public void absordState(State _giver)
    {
        if (!verifyBoundaryIntegrity())
            GD.Print("Error on receiver State_" + id + " at the start of _mergeStates");
        if (!_giver.verifyBoundaryIntegrity())
            GD.Print("Error on giver State_" + _giver.id + " at the start of _mergeStates");

        int borderOffset = land.Count;
        int colorID = land[0].colorID;

        foreach (MapNode node in _giver.land)
        {
            node.stateID = id;
            node.colorID = colorID;
            land.Add(node);
        }

        // Offset giver references to its own land by the amounf of land previously in receiver,
        // as giver's land will be shifted by that amount -> this counts for shores and boundaries
        foreach (int shoreID in _giver.shores)
        {
            shores.Add(shoreID + borderOffset);
        }

        removeBoundaryToState(_giver.id); // Receiver state removes all reference to giver state in its neighboring data

        foreach (int borderIndex in _giver.boundaries)
        {
            if (_giver.land[borderIndex].borderingStateIds.Count == 0)
            {
                GD.PrintErr("State_" + _giver.id + " boundary has no borders");
                continue;
            }

            if (_giver.land[borderIndex].borderingStateIds.Contains(id))
            {
                _giver.land[borderIndex].borderingStateIds.Remove(id);
            }
            if (_giver.land[borderIndex].borderingStateIds.Count > 0)
            {
                boundaries.Add(borderIndex + borderOffset); // Only add border if it still have neighbors after removing receiving state
            }
        }

        foreach (int neighborID in _giver.neighbors)
        {
            if (neighborID != id && neighbors.Contains(neighborID) == false)
                neighbors.Add(neighborID);
        }

        if (!verifyBoundaryIntegrity())
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

    public void makeRogueIsland()
    {
        // Too bad, state is told to disapear, mark all MapNode as RogueIsland
        foreach (MapNode n in land)
        {
            n.stateID = -1;
            n.nodeType = MapNode.NodeType.RogueIsland;
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

    public override string ToString()
    {
        string text = "State_" + id + " in Continent_" + continentID + ":";
        foreach(int nghb in neighbors)
            text += " S_" + nghb;
        return text;
    }

    public void buildShapeTexture(Func<int, Vector3> _getNormalOfNode, Func<int, Vector3> _getVertexOfNode, Func<int, MapNode> _getMapNode)
    {
        // Find barycenter normal
        Vector3 normalCenter = Vector3.Zero;
        land.ForEach((node) => normalCenter += _getNormalOfNode(node.fullMapIndex));
        normalCenter = normalCenter.Normalized();
        barycenter = normalCenter; // Store for later use for camera and marker movements
        // Build two axis to receive or projections
        Vector3 topAxis = Vector3.Up;
        if(normalCenter.AngleTo(topAxis) < 0.01 || normalCenter.AngleTo(topAxis) > 179.9)
            topAxis = new Vector3(normalCenter.Y, -normalCenter.X, 0.0f).Normalized(); // any perpendicular
        Vector3 sideAxis = topAxis.Cross(normalCenter);
        topAxis = sideAxis.Cross(normalCenter);

        // Now projects coordinates into those axis
        Dictionary<int, Vector2> projectedPositionsPerIndex = new();
        Vector2 xBox = new(); // min , max
        Vector2 yBox = new(); // min , max
        Dictionary<int, List<int>> borderIndexConnections = new(); // Build ordered borders, to draw the exterior shape of the sate
        void registerProjectionsAndConnections(List<int> _nodes)
        {
            foreach(int index in _nodes)
            {
                int fullMapIndex = land[index].fullMapIndex;
                if(projectedPositionsPerIndex.ContainsKey(fullMapIndex))
                    continue; // Can happen when a border is also in shores

                Vector3 planetPos = _getVertexOfNode(fullMapIndex).Normalized();
                float x = planetPos.Dot(sideAxis);
                float y = planetPos.Dot(topAxis);
                projectedPositionsPerIndex.Add(fullMapIndex, new(x,y));

                if(x > xBox.Y) xBox.Y = x;
                else if(x < xBox.X) xBox.X = x;
                if(y > yBox.Y) yBox.Y = y;
                else if(y < yBox.X) yBox.X = y;

                List<int> nghbs = new();
                Dictionary<int, int> extendedNeighborsConnections = new(); // Find corner neighbors by counting those which are referenced twice in our direct neighbors
                foreach(int nghb in Planet.getNeighbours(fullMapIndex))
                {
                    MapNode node = _getMapNode(nghb);
                    if(node.stateID == id && (node.isBorder() || node.waterBorder))
                    {
                        nghbs.Add(nghb);
                    }
                    foreach(int extendedNeighborIndex in Planet.getNeighbours(nghb))
                    {
                        if(extendedNeighborIndex == fullMapIndex)
                            continue; // Don't reference ourself
                        MapNode extendedNode = _getMapNode(extendedNeighborIndex);
                        if(extendedNode.stateID == id && (extendedNode.isBorder() || extendedNode.waterBorder))
                        {
                            if(extendedNeighborsConnections.ContainsKey(extendedNeighborIndex) == false)
                                extendedNeighborsConnections.Add(extendedNeighborIndex, 1);
                            else
                                extendedNeighborsConnections[extendedNeighborIndex] += 1;
                        }
                    }
                }
                // First register direct neighbors borders
                borderIndexConnections.Add(fullMapIndex, nghbs);
                // Then register extended neibhbors borders
                // Each time our extended neighbors dictionnary has a node referenced twice, it's a border neighbor diagonal to us, add it to list
                foreach(int extendedIndex in extendedNeighborsConnections.Keys)
                {
                    if(extendedNeighborsConnections[extendedIndex] == 2)
                        borderIndexConnections[fullMapIndex].Add(extendedIndex);
                }
                if(borderIndexConnections.Count == 0)
                    GD.Print("oops");
            }
        }
        registerProjectionsAndConnections(boundaries);
        registerProjectionsAndConnections(shores);

        // remap positions from [min; max] to [0+offset; SIZE-offset] now that we know the full range of our points
        Vector2I imgSize = StateDisplayerManager.stateShapeTextureSize;
        int offset = StateDisplayerManager.stateShapeBorderOffset;
        float xRange = xBox.Y - xBox.X;
        float yRange = yBox.Y - yBox.X;
        foreach(int i in projectedPositionsPerIndex.Keys)
        {
            Vector2 point = projectedPositionsPerIndex[i];
            float x = (point.X - xBox.X) / xRange; // Remove min and divide by range to make values in [0;1]
            float y = (point.Y - yBox.X) / yRange;
            x = x * (imgSize.X - 1 - 2*offset) + offset; // From [0;1] to [0+offset; size-offset]
            y = y * (imgSize.Y - 1 - 2*offset) + offset;
            projectedPositionsPerIndex[i] = new(x,y);
        }

        // Build image that will hold building data
        Image img = Image.CreateEmpty(imgSize.X, imgSize.Y, false, Image.Format.Rgba8);
        img.Fill(Colors.Transparent);

        // Draw lines between connected borders
        List<int> drawnIndices = new();
        foreach(int index in projectedPositionsPerIndex.Keys)
        {
            Vector2 point = projectedPositionsPerIndex[index];
            foreach(int otherIndex in borderIndexConnections[index])
            {
                if(drawnIndices.Contains(otherIndex))
                    continue;
                Vector2 otherPoint = projectedPositionsPerIndex[otherIndex];
                Vector2I from = new((int)point.X, (int)point.Y);
                Vector2I to = new((int)otherPoint.X, (int)otherPoint.Y);
                _drawLine(img, from, to, Colors.Black);
            }
            drawnIndices.Add(index);
        }

        // Flood fill from exterior to register all exterior pixels
        List<int> exteriorPixels = new();
        Queue<int> indicesToCheck = new();
        indicesToCheck.Enqueue(0);
        int imgFullSize = imgSize.X * imgSize.Y;
        while(indicesToCheck.Count > 0)
        {
            int index = indicesToCheck.Dequeue();
            if(exteriorPixels.Contains(index))
                continue;
            int x = index % imgSize.X;
            int y = index / imgSize.X;
            if(img.GetPixel(x,y).A < 0.5f)
            {
                exteriorPixels.Add(index);
                checkAndAdd(index + 1);
                checkAndAdd(index - 1);
                checkAndAdd(index + imgSize.X);
                checkAndAdd(index - imgSize.X);
            }
        }
        // Flood fill helper
        void checkAndAdd(int _index)
        {
            if(_index < 0 || _index >= imgFullSize)
                return;
            indicesToCheck.Enqueue(_index);
        }

        // Color all transparent pixels not reached by flood fill
        for(int j = 0; j < imgSize.Y; ++j)
        {
            for(int i = 0; i < imgSize.X; ++i)
            {
                if(img.GetPixel(i,j).A > 0.5f)
                    continue; // pixel is not transparent
                int index = j * imgSize.X + i;
                if(exteriorPixels.Contains(index))
                    continue;
                img.SetPixel(i,j, Colors.White);
            }
        }

        // Finally create texture from this image
        stateShapeTexture = ImageTexture.CreateFromImage(img);
    }

    private void _drawLine(Image _img, Vector2I _from, Vector2I _to, Color _color)
    {
        float length = (_from - _to).Length();
        int loops = (int)Mathf.Ceil(length);
        float dx = (_to.X - _from.X) / length;
        float dy = (_to.Y - _from.Y) / length;

        float x = _from.X;
        float y = _from.Y;
        int loop = 0;
        while(loop++ < loops)
        {
            _img.SetPixel((int)(x+0.5f), (int)(y+0.5f), _color);
            x += dx;
            y += dy;
        }
        _img.SetPixel(_to.X, _to.Y, _color);
    }
}
