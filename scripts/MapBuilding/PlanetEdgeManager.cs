using Godot;
using System;
using System.Reflection.Metadata.Ecma335;

public static class PlanetEdgeManager
{
    public enum Edge{ firstCol, lastCol, firstRow, lastRow };
    public struct PlanetEdge
    {
        public PlanetEdge(int _shortSide, int _longSide, Edge _shortEdge,  Edge _longEdge, bool _reversed = false)
        {
            reversed = _reversed;
            shortEdge = _shortEdge;
            longEdge = _longEdge;
            shortSide = _shortSide;
            longSide = _longSide;
        }
        public bool reversed;
        public Edge shortEdge;
        public Edge longEdge;
        public int shortSide;
        public int longSide;
    }

    public static PlanetEdge[] edgesData {get; private set;} = _buildEdges();

    private static PlanetEdge[] _buildEdges()
    {
        PlanetEdge[] edges = new PlanetEdge[EDGES_COUNT];
        edges[EDGE_TOP_BACK] = new(Planet.SIDE_TOP, Planet.SIDE_BACK, Edge.firstRow, Edge.lastCol, true);
        edges[EDGE_TOP_LEFT] = new(Planet.SIDE_LEFT, Planet.SIDE_TOP, Edge.firstRow, Edge.lastCol, true);
        edges[EDGE_TOP_FRONT] = new(Planet.SIDE_TOP, Planet.SIDE_FRONT, Edge.lastRow, Edge.lastCol, false);
        edges[EDGE_TOP_RIGHT] = new(Planet.SIDE_RIGHT, Planet.SIDE_TOP, Edge.lastRow, Edge.firstCol, true);
        edges[EDGE_BACK_LEFT] = new(Planet.SIDE_BACK, Planet.SIDE_LEFT, Edge.firstRow, Edge.lastCol, true);
        edges[EDGE_FRONT_LEFT] = new(Planet.SIDE_FRONT, Planet.SIDE_LEFT, Edge.lastRow, Edge.firstCol, true);
        edges[EDGE_FRONT_RIGHT] = new(Planet.SIDE_FRONT, Planet.SIDE_RIGHT, Edge.firstRow, Edge.firstCol, false);
        edges[EDGE_BACK_RIGHT] = new(Planet.SIDE_BACK, Planet.SIDE_RIGHT, Edge.lastRow, Edge.lastCol, false);
        edges[EDGE_BOT_BACK] = new(Planet.SIDE_BOT, Planet.SIDE_BACK, Edge.lastRow, Edge.firstCol, true);
        edges[EDGE_BOT_LEFT] = new(Planet.SIDE_LEFT, Planet.SIDE_BOT, Edge.lastRow, Edge.lastCol, false);
        edges[EDGE_BOT_FRONT] = new(Planet.SIDE_BOT, Planet.SIDE_FRONT, Edge.firstRow, Edge.firstCol, false);
        edges[EDGE_BOT_RIGHT]  = new(Planet.SIDE_RIGHT, Planet.SIDE_BOT, Edge.firstRow, Edge.firstCol, false);
        edges[EDGE_INVALID]  = new(-1,-1, Edge.firstCol, Edge.firstCol, false);
        return edges;
    }

    public static PlanetEdge getEdge(int side, Edge edge)
    {
        foreach(PlanetEdge pe in edgesData)
        {
            if((pe.shortSide == side && pe.shortEdge == edge) || (pe.longSide == side && pe.longEdge == edge))
                return pe;
        }
        return edgesData[EDGE_INVALID];
    }

    public static int getCorner(int _side, Edge _edgeA, Edge _edgeB)
    {
        // Sanitizing input
        if((_edgeA == Edge.firstCol || _edgeA == Edge.lastCol) && (_edgeB == Edge.firstCol || _edgeB == Edge.lastCol))
        {
            GD.PrintErr("PlanetEdgeManager.getCorner received two same sided edges");
            return -1;
        }
        if((_edgeA == Edge.firstRow || _edgeA == Edge.lastRow) && (_edgeB == Edge.firstRow || _edgeB == Edge.lastRow))
        {
            GD.PrintErr("PlanetEdgeManager.getCorner received two same sided edges");
            return -1;
        }

        Edge shortEdge = _edgeA;
        Edge longEdge = _edgeB;
        if(_edgeA == Edge.firstCol || _edgeA == Edge.lastCol)
        {
            longEdge = _edgeA;
            shortEdge = _edgeB;
        }

        // this sucks but i don't see how to do without aweful switches
        if(shortEdge == Edge.firstRow)
        {
            if(longEdge == Edge.firstCol)
            {
                switch(_side)
                {
                    case Planet.SIDE_TOP: return Planet.CORNER_TOP_RIGHT_BACK;
                    case Planet.SIDE_BACK: return Planet.CORNER_BOT_BACK_LEFT;
                    case Planet.SIDE_LEFT: return Planet.CORNER_TOP_LEFT_FRONT;
                    case Planet.SIDE_FRONT: return Planet.CORNER_BOT_FRONT_RIGHT;
                    case Planet.SIDE_RIGHT: return Planet.CORNER_BOT_FRONT_RIGHT;
                    case Planet.SIDE_BOT: return Planet.CORNER_BOT_FRONT_RIGHT;
                }
            }
            else // lastCol
            {
                switch(_side)
                {
                    case Planet.SIDE_TOP: return Planet.CORNER_TOP_BACK_LEFT;
                    case Planet.SIDE_BACK: return Planet.CORNER_TOP_BACK_LEFT;
                    case Planet.SIDE_LEFT: return Planet.CORNER_TOP_BACK_LEFT;
                    case Planet.SIDE_FRONT: return Planet.CORNER_TOP_FRONT_RIGHT;
                    case Planet.SIDE_RIGHT: return Planet.CORNER_BOT_RIGHT_BACK;
                    case Planet.SIDE_BOT: return Planet.CORNER_BOT_LEFT_FRONT;
                }
            }
        }
        else // lastRow
        {
            if(longEdge == Edge.firstCol)
            {
                switch(_side)
                {
                    case Planet.SIDE_TOP: return Planet.CORNER_TOP_FRONT_RIGHT;
                    case Planet.SIDE_BACK: return Planet.CORNER_BOT_RIGHT_BACK;
                    case Planet.SIDE_LEFT: return Planet.CORNER_BOT_LEFT_FRONT;
                    case Planet.SIDE_FRONT: return Planet.CORNER_BOT_LEFT_FRONT;
                    case Planet.SIDE_RIGHT: return Planet.CORNER_TOP_FRONT_RIGHT;
                    case Planet.SIDE_BOT: return Planet.CORNER_BOT_RIGHT_BACK;
                }
            }
            else // lastCol
            {
                switch(_side)
                {
                    case Planet.SIDE_TOP: return Planet.CORNER_TOP_LEFT_FRONT;
                    case Planet.SIDE_BACK: return Planet.CORNER_TOP_RIGHT_BACK;
                    case Planet.SIDE_LEFT: return Planet.CORNER_BOT_BACK_LEFT;
                    case Planet.SIDE_FRONT: return Planet.CORNER_TOP_LEFT_FRONT;
                    case Planet.SIDE_RIGHT: return Planet.CORNER_TOP_RIGHT_BACK;
                    case Planet.SIDE_BOT: return Planet.CORNER_BOT_BACK_LEFT;
                }
            }
        }
        
        GD.PrintErr("Reached end of PlanetEdgeManager.getCorner");
        return -1;
    }

    public static Vector3I getCornerFaces(int _side, Edge _edgeA, Edge _edgeB)
    {
        PlanetEdge peA = getEdge(_side, _edgeA);
        PlanetEdge peB = getEdge(_side, _edgeB);
        int faceA = _side == peA.shortSide ? peA.longSide : peA.shortSide;
        int faceB = _side == peB.shortSide ? peB.longSide : peB.shortSide;
        return new(_side, faceA, faceB);
    }

    public static PlanetEdge getEdge(int sideA, int sideB)
    {
        Func<int, bool> checkFace = (toCheck) => (sideA == toCheck || sideB == toCheck);

        if(checkFace(Planet.SIDE_TOP))
        {
            if(checkFace(Planet.SIDE_BACK))
                return edgesData[EDGE_TOP_BACK];
            if(checkFace(Planet.SIDE_LEFT))
                return edgesData[EDGE_TOP_LEFT];
            if(checkFace(Planet.SIDE_FRONT))
                return edgesData[EDGE_TOP_FRONT];
            if(checkFace(Planet.SIDE_RIGHT))
                return edgesData[EDGE_TOP_RIGHT];
        }

        if(checkFace(Planet.SIDE_BOT))
        {
            if(checkFace(Planet.SIDE_BACK))
                return edgesData[EDGE_BOT_BACK];
            if(checkFace(Planet.SIDE_FRONT))
                return edgesData[EDGE_BOT_FRONT];
            if(checkFace(Planet.SIDE_LEFT))
                return edgesData[EDGE_BOT_LEFT];
            if(checkFace(Planet.SIDE_RIGHT))
                return edgesData[EDGE_BOT_RIGHT];
        }

        if(checkFace(Planet.SIDE_LEFT))
        {
            if(checkFace(Planet.SIDE_BACK))
                return edgesData[EDGE_BACK_LEFT];
            if(checkFace(Planet.SIDE_FRONT))
                return edgesData[EDGE_FRONT_LEFT];
        }

        if(checkFace(Planet.SIDE_RIGHT))
        {
            if(checkFace(Planet.SIDE_FRONT))
                return edgesData[EDGE_FRONT_RIGHT];
            if(checkFace(Planet.SIDE_BACK))
                return edgesData[EDGE_BACK_RIGHT];
        }

        return edgesData[EDGE_INVALID];
    }

    public static int getFaceFromCorners(int cornerA, int cornerB, int cornerC)
    {
        Func<int, int> check = index => (index == cornerA || index == cornerB || index == cornerC) ? 1 : 0;
        int hasCorner_TOP_RIGHT_BACK = check(Planet.CORNER_TOP_RIGHT_BACK);
        int hasCorner_TOP_BACK_LEFT = check(Planet.CORNER_TOP_BACK_LEFT);
        int hasCorner_TOP_LEFT_FRONT = check(Planet.CORNER_TOP_LEFT_FRONT);
        int hasCorner_TOP_FRONT_RIGHT = check(Planet.CORNER_TOP_FRONT_RIGHT);
        int hasCorner_BOT_BACK_LEFT = check(Planet.CORNER_BOT_BACK_LEFT);
        int hasCorner_BOT_LEFT_FRONT = check(Planet.CORNER_BOT_LEFT_FRONT);
        int hasCorner_BOT_FRONT_RIGHT = check(Planet.CORNER_BOT_FRONT_RIGHT);
        int hasCorner_BOT_RIGHT_BACK = check(Planet.CORNER_BOT_RIGHT_BACK);

        if(hasCorner_TOP_RIGHT_BACK + hasCorner_TOP_BACK_LEFT + hasCorner_TOP_LEFT_FRONT + hasCorner_TOP_FRONT_RIGHT == 3) return Planet.SIDE_TOP;
        if(hasCorner_BOT_BACK_LEFT + hasCorner_BOT_LEFT_FRONT + hasCorner_BOT_FRONT_RIGHT + hasCorner_BOT_RIGHT_BACK == 3) return Planet.SIDE_BOT;
        if(hasCorner_TOP_RIGHT_BACK + hasCorner_TOP_BACK_LEFT + hasCorner_BOT_BACK_LEFT + hasCorner_BOT_RIGHT_BACK == 3) return Planet.SIDE_BACK;
        if(hasCorner_TOP_BACK_LEFT + hasCorner_TOP_LEFT_FRONT + hasCorner_BOT_BACK_LEFT + hasCorner_BOT_LEFT_FRONT == 3) return Planet.SIDE_LEFT;
        if(hasCorner_TOP_LEFT_FRONT + hasCorner_TOP_FRONT_RIGHT + hasCorner_BOT_LEFT_FRONT + hasCorner_BOT_FRONT_RIGHT == 3) return Planet.SIDE_FRONT;
        if(hasCorner_TOP_RIGHT_BACK + hasCorner_TOP_FRONT_RIGHT + hasCorner_BOT_FRONT_RIGHT + hasCorner_BOT_RIGHT_BACK == 3) return Planet.SIDE_RIGHT;
        GD.PrintErr("Reached end of PlanetEdgeManager.getFaceFromCorners");
        return -1;
    }

    public static string cornerIndexToString(int _cornerIndex)
    {
        switch(_cornerIndex)
        {
            case Planet.CORNER_TOP_RIGHT_BACK: return "CORNER_TOP_RIGHT_BACK";
            case Planet.CORNER_TOP_BACK_LEFT: return "CORNER_TOP_BACK_LEFT";
            case Planet.CORNER_TOP_LEFT_FRONT: return "CORNER_TOP_LEFT_FRONT";
            case Planet.CORNER_TOP_FRONT_RIGHT: return "CORNER_TOP_FRONT_RIGHT";
            case Planet.CORNER_BOT_BACK_LEFT: return "CORNER_BOT_BACK_LEFT";
            case Planet.CORNER_BOT_LEFT_FRONT: return "CORNER_BOT_LEFT_FRONT";
            case Planet.CORNER_BOT_FRONT_RIGHT: return "CORNER_BOT_FRONT_RIGHT";
            case Planet.CORNER_BOT_RIGHT_BACK: return "CORNER_BOT_RIGHT_BACK";
            default: return "invalid";
        }
    }

    public static string sideIndexToString(int _side)
    {
        switch(_side)
        {
            case Planet.SIDE_TOP: return "SIDE_TOP";
            case Planet.SIDE_BACK: return "SIDE_BACK";
            case Planet.SIDE_LEFT: return "SIDE_LEFT";
            case Planet.SIDE_FRONT: return "SIDE_FRONT";
            case Planet.SIDE_RIGHT: return "SIDE_RIGHT";
            case Planet.SIDE_BOT: return "SIDE_BOT";
            default: return "invalid";
        }
    }

    private const int EDGE_TOP_BACK = 0;
    private const int EDGE_TOP_LEFT = 1;
    private const int EDGE_TOP_FRONT = 2;
    private const int EDGE_TOP_RIGHT = 3;
    private const int EDGE_BACK_LEFT = 4;
    private const int EDGE_FRONT_LEFT = 5;
    private const int EDGE_BACK_RIGHT = 6;
    private const int EDGE_FRONT_RIGHT = 7;
    private const int EDGE_BOT_BACK = 8;
    private const int EDGE_BOT_LEFT = 9;
    private const int EDGE_BOT_FRONT = 10;
    private const int EDGE_BOT_RIGHT = 11;
    private const int EDGE_INVALID = 12;
    private const int EDGES_COUNT = 13;
}
