using Godot;
using System;
using System.Collections.Generic;

public partial class PlanetInputManager : Area3D
{
    [Export]
    public Planet planet {get; set;}
    [Export]
    public Camera3D camera {get; set;}

    public override void _PhysicsProcess(double _dt)
    {
        if(Input.IsActionJustPressed("Primary"))
        {
            Vector2 mousePos = GetViewport().GetMousePosition();
            Vector3 from = camera.ProjectRayOrigin(mousePos);
            Vector3 to = from + camera.ProjectRayNormal(mousePos) * 5.0f;
            // this types are aweful so let's use some "var"
            var spaceState = GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.CollideWithAreas = true;
            var result = spaceState.IntersectRay(query);

            if(result.Count == 0)
                return;

            Vector3 worldHitPos = (Vector3)result["position"];
            Vector3 planetLocalHitPos = planet.ToLocal(worldHitPos);

            int nearClicIndex = _tryNarrowVertexClicked(planetLocalHitPos);
            nearClicIndex = _findClosestIndexViaNeighbors(nearClicIndex, planetLocalHitPos); // this is the one clicked !

            planet.setUVYAtIndex(nearClicIndex, 20.0f);
            planet.setMesh(); // Reseting the full mesh seems brutally overkill only to apply UVs (i.e. display selection), but that'll do for now
        }
    }

    private int _tryNarrowVertexClicked(Vector3 sphereLocalHit)
    {
        // Start from 6 faces centers
        Vector2 uvCenter = new(0.5f, 0.5f);
        List<int> samples = new();
        for(int i = 0; i < Planet.SIDE_COUNT; ++i)
        {
            samples.Add(planet.getApproximateVertexAt(i, uvCenter));
        }

        int closestIndex = _findClosest(samples, sphereLocalHit, out float dist);
        // Preparing the dichotomy loop
        int faceIndex = samples.IndexOf(closestIndex);
        GD.Print("Clic detected on " + PlanetEdgeManager.sideIndexToString(faceIndex));
        Vector2 sampleCenter = uvCenter;
        Vector2[] samplePoss = new Vector2[4];
        float sampleRange = 0.25f; // Space between sample Center and samples. We start with center at 0.5,0.5, so first samples are 0.25 apart

        while(sampleRange > 0.005f)
        {
            samples.Clear();
            samplePoss[0] = new(sampleCenter.X + sampleRange, sampleCenter.Y + sampleRange);
            samplePoss[1] = new(sampleCenter.X + sampleRange, sampleCenter.Y - sampleRange);
            samplePoss[2] = new(sampleCenter.X - sampleRange, sampleCenter.Y + sampleRange);
            samplePoss[3] = new(sampleCenter.X - sampleRange, sampleCenter.Y - sampleRange);
            samples.Add(planet.getApproximateVertexAt(faceIndex, samplePoss[0]));
            samples.Add(planet.getApproximateVertexAt(faceIndex, samplePoss[1]));
            samples.Add(planet.getApproximateVertexAt(faceIndex, samplePoss[2]));
            samples.Add(planet.getApproximateVertexAt(faceIndex, samplePoss[3]));
            closestIndex = _findClosest(samples, sphereLocalHit, out dist); // might use dist later for early out below a threshold
            sampleCenter = samplePoss[samples.IndexOf(closestIndex)];
            sampleRange *= 0.5f;
        }
        return closestIndex; // Can start search by neighbors from this one
    }

    private int _findClosestIndexViaNeighbors(int startingIndex, Vector3 localToSphere)
    {
        int index = startingIndex;
        float refDist = planet.getSquareDistance(index, localToSphere);
        int iteration = 0;
        while(iteration++ < 1000) // wouldn't want an infinite loop
        {
            List<int> nghbs = Planet.getNeighbours(index);
            int closest = _findClosest(nghbs, localToSphere, out float dist);
            if(dist > refDist)
            {
                // All neihbors are further than the point, we found the one
                return index;
            }
            index = closest;
            refDist = dist;
        }

        GD.PrintErr("Reach max iteration of PlanetInputManger._findClosestIndexViaNeighbors");
        return index;
    }

    private int _findClosest(List<int> vertices, Vector3 localPos, out float _distance)
    {
        _distance = -1.0f;
        int closest = -1;
        foreach(int i in vertices)
        {
            if(planet.tryGetVertex(i, out Vector3 vertex))
            {
                float dist = vertex.DistanceSquaredTo(localPos);
                if(closest == -1 || dist < _distance)
                {
                    closest = i;
                    _distance = dist;
                }
            }
        }
        return closest;
    }
}
