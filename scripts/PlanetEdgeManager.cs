using Godot;
using System;

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
