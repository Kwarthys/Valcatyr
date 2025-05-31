using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Transactions;

public partial class Planet : MeshInstance3D
{
    [Export]
    private MainController mainController;

    [Export]
    public  BridgeBuilder bridgeBuilder {get; private set;}

    [Export]
    public bool fastBridges = false; // Shortcut bridge over land checks, which take TONS of time and is useless while debugging

    [Export]
    private float planetRotationPeriodSec = 300.0f;

    public const int SIDE_TOP = 0;
    public const int SIDE_BACK = 1;
    public const int SIDE_LEFT = 2;
    public const int SIDE_FRONT = 3;
    public const int SIDE_RIGHT = 4;
    public const int SIDE_BOT = 5;
    public const int SIDE_COUNT = 6;

    public const float PLANET_RADIUS = 2.0f;
    public const int MAP_RESOLUTION = 200;
    public const int FACE_WIDTH = MAP_RESOLUTION - 2;
    public const int FACE_HEIGHT = MAP_RESOLUTION;
    public const int FACE_SIZE = FACE_HEIGHT * FACE_WIDTH;

    public const int CORNER_TOP_RIGHT_BACK = 0;
    public const int CORNER_TOP_BACK_LEFT = 1;
    public const int CORNER_TOP_LEFT_FRONT = 2;
    public const int CORNER_TOP_FRONT_RIGHT = 3;
    public const int CORNER_BOT_BACK_LEFT = 4;
    public const int CORNER_BOT_LEFT_FRONT = 5;
    public const int CORNER_BOT_FRONT_RIGHT = 6;
    public const int CORNER_BOT_RIGHT_BACK = 7;
    public const int CORNERS_COUNT = 8;
    public const int CORNERS_INDEX_START = FACE_WIDTH * FACE_HEIGHT * SIDE_COUNT;

    
    public const float SEA_LEVEL = 0.01f; // 0.005

    List<Vector3> vertices = new();
    List<Vector2> uvs = new();
    List<Vector3> normals = new();
    List<int> indices = new();
    List<Color> colors = new();

    ArrayMesh arrayMesh;
    Godot.Collections.Array surfaceArrays = new();

    Dictionary<int, Vector3> trianglesNormalsPerVertex = new(); // ease normals computation by registering triangles as we go

    public MapManager mapManager;
    public PlanetNodeFinder nodeFinder;

    public const int MAP_SIZE = FACE_WIDTH * FACE_HEIGHT * SIDE_COUNT + CORNERS_COUNT;

    public float[] map = new float[MAP_SIZE];

    public override void _Ready()
    {
        surfaceArrays.Resize((int)Mesh.ArrayType.Max);
        generateMesh();
    }

    private bool rotate = false;

    public override void _Process(double delta)
    {
        if(Input.IsActionJustPressed("Rotate"))
            rotate = !rotate;
        float period = planetRotationPeriodSec;
        if (rotate)
            period = 7.0f;
        Rotate(Vector3.Up, (float)(delta * Math.Tau / period));
    }

    public void setMesh()
    {
        if((refreshFlags & REFRESH_FLAG_VERTICES) != 0)
            surfaceArrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();

        if((refreshFlags & REFRESH_FLAG_UVS) != 0)
            surfaceArrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();

        if((refreshFlags & REFRESH_FLAG_NORMALS) != 0)
            surfaceArrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();

        if((refreshFlags & REFRESH_FLAG_INDICES) != 0)
            surfaceArrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

        if((refreshFlags & REFRESH_FLAG_COLORS) != 0)
            surfaceArrays[(int)Mesh.ArrayType.Color] = colors.ToArray();

        arrayMesh = new();
        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArrays);
        
        Mesh = arrayMesh;
        ShaderMaterial mat = (ShaderMaterial)GetActiveMaterial(0);
        mat.SetShaderParameter("customTexture", mapManager.tex);
        mat.SetShaderParameter("shallowWaterColor", MapManager.shallowSeaColor);

        refreshFlags = REFRESH_FLAG_NONE;
    }

    public void generateMesh()
    {
        vertices.Clear();
        uvs.Clear();
        normals.Clear();
        indices.Clear();
        colors.Clear();

        Vector3[] ups = new Vector3[SIDE_COUNT];
        Vector3[] forwards = new Vector3[SIDE_COUNT];

        ups[SIDE_TOP] = Vector3.Up;
        forwards[SIDE_TOP] = Vector3.Forward;

        ups[SIDE_FRONT] = Vector3.Forward;
        forwards[SIDE_FRONT] = Vector3.Left;

        ups[SIDE_LEFT] = Vector3.Left;
        forwards[SIDE_LEFT] = Vector3.Down;

        ups[SIDE_BACK] = Vector3.Back;
        forwards[SIDE_BACK] = Vector3.Right;

        ups[SIDE_RIGHT] = Vector3.Right;
        forwards[SIDE_RIGHT] = Vector3.Up;

        ups[SIDE_BOT] = Vector3.Down;
        forwards[SIDE_BOT] = Vector3.Back;

        float usecStart = Time.GetTicksUsec();

        for(int sideIndex = 0; sideIndex < SIDE_COUNT; ++sideIndex)
        {
            _appendSurface(ups[sideIndex], forwards[sideIndex], sideIndex);
        }

        _appendCorners(ups, forwards);
        _stichFacesAndCorners();
        _assignNormals();

        GD.Print("Creating Planet took " + ((Time.GetTicksUsec() - usecStart) * 0.000001) + " secs.");

        nodeFinder = new(this);
        mapManager = new(this);
        mapManager.RegisterMap(map);

        refreshFlags = REFRESH_FLAG_ALL;
        
        Callable callable = new(this, MethodName.setMesh);
        callable.Call();

        mainController.notifyPlanetGenerationComplete();
    }

    /// <summary>
    /// Build a bridge from vertex at index _indices.X to vertex at index _indices.Y
    /// </summary>
    public void askBridgeCreation(Vector2I _indices)
    {
        if(_indices.X >= 0 && _indices.Y >= 0 && _indices.X < MAP_SIZE && _indices.Y < MAP_SIZE)
            bridgeBuilder.buildBridge(vertices[_indices.X], vertices[_indices.Y]);
    }

    public int getApproximateVertexAt(int side, Vector2 uv)
    {
        int x = (int)(Mathf.Clamp(uv.X, 0.0f, 1.0f) * (FACE_WIDTH - 1));
        int y = (int)(Mathf.Clamp(uv.Y, 0.0f, 1.0f) * (FACE_HEIGHT - 1));
        return side * FACE_SIZE + x + y * FACE_WIDTH;
    }

    public bool getVertexAndNormal(int _vertexID, out Vector3 _vertex, out Vector3 _normal)
    {
        _vertex = Vector3.Zero;
        _normal = Vector3.Zero;
        if(_vertexID < 0 || _vertexID >= MAP_SIZE)
        {
            GD.PrintErr("Planet.getVertexAndNormal was asked invalid vertex: " + _vertexID);
            return false;
        }
        _vertex = vertices[_vertexID];
        _normal = normals[_vertexID];
        return true;
    }

    public Vector3 getNormal(int _vertexID)
    {
        if(_vertexID < 0 || _vertexID >= MAP_SIZE)
        {
            throw new AccessViolationException("Planet.getVertexAndNormal was asked invalid vertex: " + _vertexID);
        }
        return normals[_vertexID];
    }

    public Vector3 getVertex(int _vertexID)
    {
        if(_vertexID < 0 || _vertexID >= MAP_SIZE)
        {
            GD.PrintErr("Planet.getVertex was asked invalid vertex: " + _vertexID);
            throw new AccessViolationException(); // Should never happen, crash it all to get the call stack
        }
        return vertices[_vertexID];
    }

    public bool tryGetVertex(int _vertexID, out Vector3 _vertex)
    {
        _vertex = new();
        if(_vertexID < 0 || _vertexID >= MAP_SIZE)
            return false;
        _vertex = vertices[_vertexID];
        return true;
    }

    // if terrain is later an issue we might normalize vertex pos on a bool parameter
    /// <summary>
    /// This will return a straight line (throught the planet) squared distance between two vertices, including terrain elevation
    /// </summary>
    public float getSquareDistance(int _vertexIDFrom, int _vertexIDTo)
    {
        if(_vertexIDFrom < vertices.Count
        && _vertexIDTo < vertices.Count
        && _vertexIDFrom >= 0
        && _vertexIDTo >= 0)
        {
            return vertices[_vertexIDFrom].DistanceSquaredTo(vertices[_vertexIDTo]);
        }
        return -1.0f;
    }

    public float getSquareDistance(int _vertexID, Vector3 localPos)
    {
        if(_vertexID < 0 || _vertexID >= MAP_SIZE) return -1.0f;
        return vertices[_vertexID].DistanceSquaredTo(localPos);
    }

    public void setUVYAtIndex(int _index, float _value)
    {
        if(_index >= 0 && _index < uvs.Count)
            uvs[_index] = new(uvs[_index].X, _value);
        else
            GD.PrintErr("Planet.setUVYAtIndex UV out of bounds");

        askUVRefresh();
    }

    private void _appendSurface(Vector3 _localUp, Vector3 _localForward, int side)
    {
        int indexStart = vertices.Count;

        _localUp = _localUp.Normalized();               // Make sure we're working with normalized directions
        _localForward = _localForward.Normalized();
        Vector3 sideAxis = _localUp.Cross(_localForward); // Cross product of normalized vector is already normalized

        for(int j = 0; j < FACE_HEIGHT; ++j)
        {
            for(int i = 0; i < FACE_WIDTH; ++i)
            {
                int index = i + j*FACE_WIDTH;

                float forwardPct = j * 1.0f / (FACE_HEIGHT-1);      // Trailing (-1) to be centered on 0.5 (countings poles and not intervals)
                float sidePct = (i+1) * 1.0f / (FACE_WIDTH+2-1);

                Vector3 vertex = _localUp + _localForward * (forwardPct - 0.5f) * 2.0f + sideAxis * (sidePct - 0.5f) * 2.0f;

                _processAndRegisterVertex(vertex);

                if(i > 0 && j > 0) // Not building triangles for first line and first row, as we build them backwards
                {   // A - B
                    // | \ |
                    // C - D:current
                    int indexA = indexStart + index - 1 - FACE_WIDTH;
                    int indexB = indexStart + index - FACE_WIDTH;
                    int indexC = indexStart + index - 1;
                    int indexD = indexStart + index;

                    _registerTriangle(indexA, indexB, indexD);
                    _registerTriangle(indexA, indexD, indexC);
                }
            }
        }
    }

    private void _appendCorners(Vector3[] _ups, Vector3[] _forwards)
    {
        Func<int, Vector3> getSideAxis = side => _ups[side].Normalized().Cross(_forwards[side].Normalized());
        // MainLoop calculation: 
        // float forwardPct = j * 1.0f / (FACE_HEIGHT-1);      // Trailing (-1) to be centered on 0.5 (countings poles and not intervals)
        // float sidePct = (i+1) * 1.0f / (FACE_WIDTH+2-1);
        // Vector3 samplePos = _localUp + _localForward * (forwardPct - 0.5f) * 2.0f + sideAxis * (sidePct - 0.5f) * 2.0f;

        Vector3 topSideAxis = getSideAxis(SIDE_TOP);
        Vector3 botSideAxis = getSideAxis(SIDE_BOT);

        // Add the 8 missing corners that belong to 3 faces each, and thus belong to no faces
        Vector3 vertex;
        for(int i = 0; i < CORNERS_COUNT; ++i)
        {
            switch(i)
            {
                case CORNER_TOP_RIGHT_BACK:
                {
                    vertex = _ups[SIDE_TOP] + _forwards[SIDE_TOP] * -1.0f + topSideAxis * -1.0f;
                    _processAndRegisterVertex(vertex);
                    break;
                }
                case CORNER_TOP_BACK_LEFT:
                {
                    vertex = _ups[SIDE_TOP] + _forwards[SIDE_TOP] * -1.0f + topSideAxis * 1.0f;
                    _processAndRegisterVertex(vertex);
                    break;
                }
                case CORNER_TOP_LEFT_FRONT:
                {
                    vertex = _ups[SIDE_TOP] + _forwards[SIDE_TOP] * 1.0f + topSideAxis * 1.0f;
                    _processAndRegisterVertex(vertex);
                    break;
                }
                case CORNER_TOP_FRONT_RIGHT:
                {
                    vertex = _ups[SIDE_TOP] + _forwards[SIDE_TOP] * 1.0f + topSideAxis * -1.0f;
                    _processAndRegisterVertex(vertex);
                    break;
                }
                case CORNER_BOT_BACK_LEFT:
                {
                    vertex = _ups[SIDE_BOT] + _forwards[SIDE_BOT] * 1.0f + botSideAxis * 1.0f;
                    _processAndRegisterVertex(vertex);
                    break;
                }
                case CORNER_BOT_LEFT_FRONT:
                {
                    vertex = _ups[SIDE_BOT] + _forwards[SIDE_BOT] * -1.0f + botSideAxis * 1.0f;
                    _processAndRegisterVertex(vertex);
                    break;
                }
                case CORNER_BOT_FRONT_RIGHT:
                {
                    vertex = _ups[SIDE_BOT] + _forwards[SIDE_BOT] * -1.0f + botSideAxis * -1.0f;
                    _processAndRegisterVertex(vertex);
                    break;
                }
                case CORNER_BOT_RIGHT_BACK:
                {
                    vertex = _ups[SIDE_BOT] + _forwards[SIDE_BOT] * 1.0f + botSideAxis * -1.0f;
                    _processAndRegisterVertex(vertex);
                    break;
                }
            }
        }
    }

    struct StitchData
    {
        public StitchData(int _shortSide, int _longSide, bool _reverseLongSide, Func<int, int, int> _shortEdgeBuilder, Func<int, int, int> _longEdgeBuilder)
        {
            shortSide = _shortSide;
            longSide = _longSide;
            reverseLongSide = _reverseLongSide;
            shortEdgeBuilder = _shortEdgeBuilder;
            longEdgeBuilder = _longEdgeBuilder;
        }
        public int shortSide;
        public int longSide;
        public bool reverseLongSide;
        public Func<int, int, int> shortEdgeBuilder;
        public Func<int, int, int> longEdgeBuilder;
    }
    public static int firstRow (int side, int i){ return side * FACE_SIZE + i; }
    public static int lastRow  (int side, int i){ return side * FACE_SIZE + FACE_WIDTH * (FACE_HEIGHT - 1) + i; }
    public static int firstCol (int side, int i){ return side * FACE_SIZE + i * FACE_WIDTH; }
    public static int lastCol  (int side, int i){ return side * FACE_SIZE + (i+1) * FACE_WIDTH - 1; }

    public static Func<int, int, int> getSideGetter(PlanetEdgeManager.Edge _edge)
    {
        switch(_edge)
        {
            case PlanetEdgeManager.Edge.firstRow: return firstRow;
            case PlanetEdgeManager.Edge.lastRow: return lastRow;
            case PlanetEdgeManager.Edge.firstCol: return firstCol;
            case PlanetEdgeManager.Edge.lastCol: return lastCol;
        }
        Debug.Print("reached end of Planet.getSideGetter, should never happen");
        return firstRow;
    }

    private void _stichFacesAndCorners()
    {
        // create remainings triangles between faces and corners
        // StichCorners
        for(int i = 0; i < CORNERS_COUNT; ++i)
        {   //         C
            // B - A
            //     D
            // 3 triangles: BCA, CDA, DBA, in order of neighbors (which is important) with A the corner
            int cornerIndex = CORNERS_INDEX_START + i;

            List<int> nghbs = getNeighbours(cornerIndex);
            int A = cornerIndex;
            int B = nghbs[0];
            int C = nghbs[1];
            int D = nghbs[2];
            
            _registerTriangle(B, C, A);
            _registerTriangle(C, D, A);
            _registerTriangle(D, B, A);
        }
        // Stitch faces
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_TOP, SIDE_BACK), false);
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_TOP, SIDE_LEFT), false);
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_TOP, SIDE_FRONT), true);        
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_TOP, SIDE_RIGHT), true);
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_BACK, SIDE_LEFT), false);
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_FRONT, SIDE_LEFT), true);
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_FRONT, SIDE_RIGHT), false);
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_BACK, SIDE_RIGHT), true);
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_BOT, SIDE_BACK), true);
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_BOT, SIDE_FRONT), false);
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_BOT, SIDE_LEFT), true);
        _stitchEdges(PlanetEdgeManager.getEdge(SIDE_BOT, SIDE_RIGHT), false);
    }

    private void _stitchEdges(PlanetEdgeManager.PlanetEdge _edgeData, bool _reverseTriangles)
    {
        List<int> shortEdge = new();
        List<int> longEdge = new();

        Func<int,int,int> shortBuilder = getSideGetter(_edgeData.shortEdge);
        Func<int,int,int> longBuilder = getSideGetter(_edgeData.longEdge);

        for(int i = 0; i < FACE_WIDTH; ++i)
        {
            shortEdge.Add(shortBuilder(_edgeData.shortSide, i));
            
        }
        for(int i = 0; i < FACE_HEIGHT; ++i)
        {
            longEdge.Add(longBuilder(_edgeData.longSide, _edgeData.reversed ? FACE_HEIGHT - 1 - i : i));
        }
        _doTheStitching(shortEdge, longEdge, _reverseTriangles);
    }

    private void _doTheStitching(List<int> _widthEdge, List<int> _heightEdge, bool _reversed = false)
    {
        // First Triangle
        _registerTriangle(_widthEdge[0], _heightEdge[_reversed ? 1 : 0], _heightEdge[_reversed ? 0 : 1]);
        // Middle Triangles
        for(int i = 1; i < FACE_HEIGHT - 2; ++i)
        {
            // Iterate over HEIGHT
            _registerTriangle(_widthEdge[_reversed ? i-1 : i], _widthEdge[_reversed ? i : i-1], _heightEdge[i]);
            _registerTriangle(_widthEdge[i], _heightEdge[_reversed ? i+1 : i], _heightEdge[_reversed ? i : i+1]);
        }
        //Last Triangle
        _registerTriangle(_widthEdge[FACE_WIDTH-1], _heightEdge[FACE_HEIGHT-(_reversed ? 1 : 2)], _heightEdge[FACE_HEIGHT-(_reversed ? 2 : 1)]);
    }

    public static List<int> getNeighbours(int index)
    {
        List<int> nghbs = new();

        int sideIndex = index / (FACE_WIDTH * FACE_HEIGHT);

        if(sideIndex >= SIDE_COUNT)
        {
            Func<int, int> first = side => side * FACE_SIZE;
            Func<int, int> last = side => (side+1) * FACE_SIZE - 1;
            Func<int, int> lastOfFirstRow = side => side * FACE_SIZE + FACE_WIDTH - 1;
            Func<int, int> firstOfLastRow = side => side * FACE_SIZE + FACE_WIDTH * (FACE_HEIGHT - 1);

            // one of the 8 corners, only 3 neighbours each
            int cornerIndex = index - (FACE_HEIGHT * FACE_WIDTH * 6);
            switch(cornerIndex)
            {
                case CORNER_TOP_RIGHT_BACK:
                {
                    nghbs.Add(first(SIDE_TOP)); // first of top
                    nghbs.Add(last(SIDE_RIGHT)); // last of right
                    nghbs.Add(last(SIDE_BACK)); // last of back
                    break;
                }
                case CORNER_TOP_BACK_LEFT:
                {
                    nghbs.Add(lastOfFirstRow(SIDE_TOP)); // last of first row top
                    nghbs.Add(lastOfFirstRow(SIDE_BACK)); // last of first row back
                    nghbs.Add(lastOfFirstRow(SIDE_LEFT)); // last of first row of left
                    break;
                }
                case CORNER_TOP_LEFT_FRONT:
                {
                    nghbs.Add(last(SIDE_TOP)); // last of top
                    nghbs.Add(first(SIDE_LEFT)); // first of left
                    nghbs.Add(last(SIDE_FRONT)); // last of front
                    break;
                }
                case CORNER_TOP_FRONT_RIGHT:
                {
                    nghbs.Add(firstOfLastRow(SIDE_TOP)); // first of last row of top
                    nghbs.Add(lastOfFirstRow(SIDE_FRONT)); // last of first row of front
                    nghbs.Add(firstOfLastRow(SIDE_RIGHT)); // first of last row of right
                    break;
                }
                case CORNER_BOT_BACK_LEFT:
                {
                    nghbs.Add(last(SIDE_BOT)); // last of bot
                    nghbs.Add(last(SIDE_LEFT)); // last of left
                    nghbs.Add(first(SIDE_BACK)); // first of back
                    break;
                }
                case CORNER_BOT_LEFT_FRONT:
                {
                    nghbs.Add(lastOfFirstRow(SIDE_BOT)); // last of first row of bot
                    nghbs.Add(firstOfLastRow(SIDE_FRONT)); // first of last row of front
                    nghbs.Add(firstOfLastRow(SIDE_LEFT)); // first of last row of left
                    break;
                }
                case CORNER_BOT_FRONT_RIGHT:
                {
                    nghbs.Add(first(SIDE_BOT)); // first of bot
                    nghbs.Add(first(SIDE_RIGHT)); // first of right
                    nghbs.Add(first(SIDE_FRONT)); // first of front
                    break;
                }
                case CORNER_BOT_RIGHT_BACK:
                {
                    nghbs.Add(firstOfLastRow(SIDE_BOT)); // first of last row of bot
                    nghbs.Add(firstOfLastRow(SIDE_BACK)); // first of last row of back
                    nghbs.Add(lastOfFirstRow(SIDE_RIGHT)); // last of first row of right
                    break;
                }
            }
            return nghbs;
        }

        int faceIndex = index % FACE_SIZE;
        int x = faceIndex % FACE_WIDTH;
        int y = faceIndex / FACE_WIDTH;

        // Check below x
        if(x == 0) // first col
        {
            if(y == 0) // first row
            {
                nghbs.Add(_getMatchingCorner(sideIndex, PlanetEdgeManager.Edge.firstRow, PlanetEdgeManager.Edge.firstCol));
            }
            else if(y == MAP_RESOLUTION - 1) // last row
            {
                nghbs.Add(_getMatchingCorner(sideIndex, PlanetEdgeManager.Edge.lastRow, PlanetEdgeManager.Edge.firstCol));
            }
            else
            {
                nghbs.Add(_getMatchingPoint(sideIndex, y, PlanetEdgeManager.Edge.firstCol));
            }

        }
        else
        {
            nghbs.Add(index - 1);
        }

        // check above x
        if(x == FACE_WIDTH - 1) // last col
        {
            if(y == 0) // first row
            {
                nghbs.Add(_getMatchingCorner(sideIndex, PlanetEdgeManager.Edge.firstRow, PlanetEdgeManager.Edge.lastCol));
            }
            else if(y == FACE_HEIGHT - 1) // last row
            {
                nghbs.Add(_getMatchingCorner(sideIndex, PlanetEdgeManager.Edge.lastRow, PlanetEdgeManager.Edge.lastCol));
            }
            else
            {
                nghbs.Add(_getMatchingPoint(sideIndex, y, PlanetEdgeManager.Edge.lastCol));
            }
        }
        else
        {
            nghbs.Add(index + 1);
        }

        // Check below y
        if(y == 0) // first row
        {
            nghbs.Add(_getMatchingPoint(sideIndex, x, PlanetEdgeManager.Edge.firstRow));
        }
        else
        {
            nghbs.Add(index - FACE_WIDTH);
        }

        // Check above y
        if(y == FACE_HEIGHT - 1) // last row
        {
            nghbs.Add(_getMatchingPoint(sideIndex, x, PlanetEdgeManager.Edge.lastRow)); 
        }
        else
        {
            nghbs.Add(index + FACE_WIDTH);
        }

        return nghbs;
    }

    private static int _getMatchingCorner(int _side, PlanetEdgeManager.Edge _edgeA, PlanetEdgeManager.Edge _edgeB)
    {
        Vector3I faces = PlanetEdgeManager.getCornerFaces(_side, _edgeA, _edgeB);
        return CORNERS_INDEX_START + _getCornerIndex(faces.X, faces.Y, faces.Z);
    }

    private static int _getMatchingPoint(int _sideIndex, int _edgeIndex, PlanetEdgeManager.Edge _edge)
    {
        PlanetEdgeManager.PlanetEdge edge = PlanetEdgeManager.getEdge(_sideIndex, _edge);

        int indexToQuery = 0;
        if(edge.shortSide == _sideIndex)
        {
            // we are short side asking for long side neighbor
            if(edge.reversed)
                indexToQuery = FACE_WIDTH - _edgeIndex;
            else
                indexToQuery = _edgeIndex + 1;
            return getSideGetter(edge.longEdge)(edge.longSide, indexToQuery);
        }
        else
        {
            // we are long side asking for short side neighbor
            if(edge.reversed)
                indexToQuery = FACE_WIDTH - _edgeIndex;
            else
                indexToQuery = _edgeIndex - 1;
            return getSideGetter(edge.shortEdge)(edge.shortSide, indexToQuery);
        }
    }

    private static int _getCornerIndex(int sideA, int sideB, int sideC)
    {
        Func<int, int, int, bool> checkFaces = (checkA, checkB,  checkC) =>
        {
            bool containsA = sideA == checkA || sideA == checkB || sideA == checkC;
            if(!containsA) return false;
            bool containsB = sideB == checkA || sideB == checkB || sideB == checkC;
            if(!containsB) return false;
            bool containsC = sideC == checkA || sideC == checkB || sideC == checkC;
            if(!containsC) return false;
            return true;
        };

        if(checkFaces(SIDE_TOP, SIDE_BACK, SIDE_LEFT)) return CORNER_TOP_BACK_LEFT;
        if(checkFaces(SIDE_TOP, SIDE_LEFT, SIDE_FRONT)) return CORNER_TOP_LEFT_FRONT;
        if(checkFaces(SIDE_TOP, SIDE_FRONT, SIDE_RIGHT)) return CORNER_TOP_FRONT_RIGHT;
        if(checkFaces(SIDE_TOP, SIDE_RIGHT, SIDE_BACK)) return CORNER_TOP_RIGHT_BACK;
        if(checkFaces(SIDE_BOT, SIDE_BACK, SIDE_LEFT)) return CORNER_BOT_BACK_LEFT;
        if(checkFaces(SIDE_BOT, SIDE_LEFT, SIDE_FRONT)) return CORNER_BOT_LEFT_FRONT;
        if(checkFaces(SIDE_BOT, SIDE_FRONT, SIDE_RIGHT)) return CORNER_BOT_FRONT_RIGHT;
        if(checkFaces(SIDE_BOT, SIDE_RIGHT, SIDE_BACK)) return CORNER_BOT_RIGHT_BACK;

        Debug.Print("Reached end of _getCornerIndex, should never happen");
        return -1;
    }

    private void _assignNormals()
    {
        for(int i = 0; i < FACE_WIDTH * FACE_HEIGHT * SIDE_COUNT + CORNERS_COUNT; ++i)
        {
            if(!trianglesNormalsPerVertex.ContainsKey(i))
                normals.Add(Vector3.Up); // should not happen
            else
                normals.Add(trianglesNormalsPerVertex[i].Normalized());
        }
    }

    Vector3 seed_offset = new(GD.Randf(), GD.Randf(), GD.Randf());

    private float sampleNoise(Vector3 _pos)
    {
        float value = 0.0f;
        float amplitude = 0.05f;
        float frequency = 1.0f;

        for(int i = 0; i < 3; ++i)
        {
            value += Perlin.Noise((_pos + seed_offset) * frequency) * amplitude;
            amplitude *= 0.5f;
            frequency *= 2.0f;
        }

        return value;
    }

    private Color _assignPrimitiveColor(float height)
    {
        if(height > SEA_LEVEL)
        {
            return new(0.0f, 1.0f, 0.0f);
        }
        else
        {
            float r = Mathf.Clamp(Mathf.Lerp(0.5f, 0.0f, Mathf.Sqrt(SEA_LEVEL-height) * 8.0f), 0.0f, 1.0f);
            float g = Mathf.Clamp(Mathf.Lerp(0.5f, 0.0f, Mathf.Sqrt(SEA_LEVEL-height) * 8.0f), 0.0f, 1.0f);
            float b = 1.0f;
            return new(r,g,b);
        }  
    }

    private void _processAndRegisterVertex(Vector3 _pointOnCube)
    {
            Vector3 pointOnSphere = _pointOnCube.Normalized();
            float noiseValue = sampleNoise(pointOnSphere);
            pointOnSphere *= 1.0f + Mathf.Max(0.0f, noiseValue - SEA_LEVEL);
            pointOnSphere *= PLANET_RADIUS;

            map[vertices.Count] = noiseValue; // Register in complete height map (it contains all the faces) Keep it sync with the other arrays
            uvs.Add(new(noiseValue - SEA_LEVEL, 0)); // storing height on UV x for shader - later in the process, selection state will be in y
            vertices.Add(pointOnSphere);
            colors.Add(_assignPrimitiveColor(noiseValue));
    }

    private void _registerTriangle(int indexA, int indexB, int indexC)
    {
        indices.Add(indexA);
        indices.Add(indexB);
        indices.Add(indexC);

        Vector3 a = vertices[indexB] - vertices[indexA];
        Vector3 b = vertices[indexC] - vertices[indexA];
        Vector3 triangleNormal = b.Cross(a);

        if(!trianglesNormalsPerVertex.ContainsKey(indexA))
        {
            trianglesNormalsPerVertex.Add(indexA, new());
        }
        if(!trianglesNormalsPerVertex.ContainsKey(indexB))
        {
            trianglesNormalsPerVertex.Add(indexB, new());
        }
        if(!trianglesNormalsPerVertex.ContainsKey(indexC))
        {
            trianglesNormalsPerVertex.Add(indexC, new());
        }
        
        trianglesNormalsPerVertex[indexA] += triangleNormal;
        trianglesNormalsPerVertex[indexB] += triangleNormal;
        trianglesNormalsPerVertex[indexC] += triangleNormal;
    }

    private byte refreshFlags = 0;
    private const byte REFRESH_FLAG_VERTICES = 0b1;
    private const byte REFRESH_FLAG_NORMALS = 0b10;
    private const byte REFRESH_FLAG_INDICES = 0b100;
    private const byte REFRESH_FLAG_UVS = 0b1000;
    private const byte REFRESH_FLAG_COLORS = 0b10000;
    private const byte REFRESH_FLAG_ALL = 0b11111;
    private const byte REFRESH_FLAG_NONE = 0;

    public void askVerticesRefresh() { refreshFlags |= REFRESH_FLAG_VERTICES; }
    public void askNormalsRefresh() { refreshFlags |= REFRESH_FLAG_NORMALS; }
    public void askIndicesRefresh() { refreshFlags |= REFRESH_FLAG_INDICES; }
    public void askUVRefresh() { refreshFlags |= REFRESH_FLAG_UVS; }
    public void askColorsRefresh() { refreshFlags |= REFRESH_FLAG_COLORS; }
    public void askFullRefresh() { refreshFlags |= REFRESH_FLAG_ALL; }
}
