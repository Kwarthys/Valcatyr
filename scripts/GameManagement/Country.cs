using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// A Country is the gameplay representation of a State. Where States are closer to the map generation, Countries are gameplay counterparts
/// </summary>
public class Country
{
    public int playerID = -1;
    public int stateID = -1;
    public List<ReferencePoint> referencePoints = new();
}

public struct ReferencePoint
{
    public ReferencePoint(Vector3 _vertex, Vector3 _normal) { worldPos = _vertex; normal = _normal; }
    public Vector3 normal;
    public Vector3 worldPos;
}
