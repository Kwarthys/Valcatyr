using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// A Country is the gameplay representation of a State. Where States are closer to the map generation, Countries are gameplay counterparts
/// </summary>
public class Country
{
    public int playerID = -1;
    public State state = null;
    public Continent continent = null;
    public List<ReferencePoint> referencePoints = new();
    public int troops = 0;
}

public struct ReferencePoint
{
    public ReferencePoint(Vector3 _vertex, Vector3 _normal) { vertex = _vertex; normal = _normal; } // All local to planet
    public Vector3 normal;
    public Vector3 vertex;
}
