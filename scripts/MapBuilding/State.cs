using Godot;
using System;
using System.Collections.Generic;
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

    public void setContinentID(int _continentID)
    {
        continentID = _continentID;
        foreach(MapNode n in land)
        {
            n.continentID = _continentID;
        }
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

    public void makeRogueIsland()
    {
        // Too bad, state is told to disapear, mark all MapNode as RogueIsland
        foreach(MapNode n in land)
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

    public void buildShapeTexture(Func<int, Vector3> _getNormalOfNode, Func<int, Vector3> _getVertexOfNode)
    {
        // Find barycenter normal
        Vector3 normalCenter = Vector3.Zero;
        land.ForEach((node) => normalCenter += _getNormalOfNode(node.fullMapIndex));
        normalCenter = normalCenter.Normalized();
        // Build two axis to receive or projections
        Vector3 topAxis = Vector3.Up;
        if(normalCenter.AngleTo(topAxis) < 0.01 || normalCenter.AngleTo(topAxis) > 179.9)
            topAxis = new Vector3(normalCenter.Y, -normalCenter.X, 0.0f).Normalized(); // any perpendicular
        Vector3 sideAxis = topAxis.Cross(normalCenter);
        topAxis = sideAxis.Cross(normalCenter);

        // Now projects coordinates into those axis
        List<Vector2> projectedPositions = new();
        Vector2 xBox = new(); // min , max
        Vector2 yBox = new(); // min , max
        foreach(MapNode n in land)
        {
            Vector3 planetPos = _getVertexOfNode(n.fullMapIndex).Normalized();
            float x = planetPos.Dot(sideAxis);
            float y = planetPos.Dot(topAxis);
            projectedPositions.Add(new(x,y));

            if(x > xBox.Y) xBox.Y = x;
            else if(x < xBox.X) xBox.X = x;
            if(y > yBox.Y) yBox.Y = y;
            else if(y < yBox.X) yBox.X = y;
        }

        // Convert positions in [min; max] in [0; 1]
        float xRange = xBox.Y - xBox.X;
        float yRange = yBox.Y - yBox.X;

        for(int i = 0; i < projectedPositions.Count; ++i)
        {
            Vector2 point = projectedPositions[i];
            point.X = (point.X - xBox.X) / xRange; // Remove min and divide by range to make values in [0;1]
            point.Y = (point.Y - yBox.X) / yRange;
            projectedPositions[i] = point;
        }

        // Build image that will hold building data
        Vector2I imgSize = StateDisplayerManager.stateShapeTextureSize;
        int offset = StateDisplayerManager.stateShapeBorderOffset;
        Image img = Image.CreateEmpty(imgSize.X, imgSize.Y, false, Image.Format.Rgba8);
        img.Fill(Colors.Transparent);
        foreach(Vector2 pos in projectedPositions)
        {
            int x = (int)(pos.X * (imgSize.X - 1 - 2*offset)) + offset;
            int y = (int)(pos.Y * (imgSize.Y - 1 - 2*offset)) + offset;
            img.SetPixel(x,y, Colors.Black);
        }
        // Finally create texture from this image
        stateShapeTexture = ImageTexture.CreateFromImage(img);
    }

}
