using Godot;
using System;
using System.Collections.Generic;

public partial class BridgeBuilder : Node
{
    [Export]
    public PackedScene planetScene { get; set;}

    private const float BRIDGE_VERTEX_PER_LENGTH = 75.0f;
    private const float BRIDGE_WIDTH = 0.02f;
    private const float BRIDGE_HEIGHT = 0.01f;

    private List<Vector3> vertices;
    private List<Vector3> normals;
    private List<int> triangles;

    //private Planet ownerPlanet = null; i thought i needed this but at least not yet
    //public BridgeBuilder(Planet _owner){ ownerPlanet = _owner; }

    public bool buildBridge(Vector3 _posFrom, Vector3 _posTo)
    {
        vertices = new();
        normals = new();
        triangles = new();

        Vector3 sideDirection = _posFrom.Cross(_posTo).Normalized();

        // Build bridge deck
        int bridgeLength = (int)Mathf.Ceil(_posFrom.DistanceSquaredTo(_posTo) * BRIDGE_VERTEX_PER_LENGTH);
        bridgeLength = Math.Max(4, bridgeLength); // looks bad below 4
        for(int i = 0; i < bridgeLength + 1; ++i) // +1 as we want the last loop where i = length
        {
            float percent = i * 1.0f / bridgeLength;
            Vector3 centerPos = _posFrom.Lerp(_posTo, percent);

            // Elevation
            float elevation = -4 * BRIDGE_HEIGHT * (percent - 0.5f) * (percent - 0.5f) + BRIDGE_HEIGHT; // ax² + c with c = HEIGHT and a = -4c
            centerPos = centerPos.Normalized() * Planet.PLANET_RADIUS * (1.0f + elevation); // bridge starts and end at sea level for convinience and to hide extremities in land

            Vector3 leftVertex = centerPos + BRIDGE_WIDTH * 0.5f * sideDirection;
            Vector3 rightVertex = centerPos - BRIDGE_WIDTH * 0.5f * sideDirection;

            vertices.Add(leftVertex);
            normals.Add(leftVertex.Normalized());
            vertices.Add(rightVertex);
            normals.Add(rightVertex.Normalized());

            if(i > 0)
            {
                // Don't add first triangle
                // LEFT      - RIGHT
                //    |      \    |
                // prev LEFT - prev RIGHT
                int prevLeftIndex = vertices.Count - 4;
                int prevRightIndex = vertices.Count - 3;
                int leftIndex = vertices.Count - 2;
                int rightIndex = vertices.Count - 1;

                triangles.Add(leftIndex);
                triangles.Add(prevRightIndex);
                triangles.Add(prevLeftIndex);

                triangles.Add(leftIndex);
                triangles.Add(rightIndex);
                triangles.Add(prevRightIndex);
            }
        }

        Callable callable = new(this, MethodName.spawnBridge);
        callable.Call();

        return true; // Managed to build the bridge
    }

    public void spawnBridge()
    {
        Godot.Collections.Array surfaceArrays = new();
        surfaceArrays.Resize((int)Mesh.ArrayType.Max);

        surfaceArrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        surfaceArrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        surfaceArrays[(int)Mesh.ArrayType.Index] = triangles.ToArray();

        ArrayMesh arrayMesh = new();
        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, surfaceArrays);
        MeshInstance3D bridge = new() { Mesh = arrayMesh };
        AddChild(bridge);
    }
}
