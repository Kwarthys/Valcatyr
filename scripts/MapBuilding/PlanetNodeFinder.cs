using Godot;
using System;
using System.Collections.Generic;

public class PlanetNodeFinder
{
    private Planet planet;

    public PlanetNodeFinder(Planet _planet) { planet = _planet; }

    /// <summary>
    /// Finds the index of the node closest to given coordinates, in planet local space
    /// </summary>
    public int findNodeIndexAtPosition(Vector3 _sphereLocalPosition)
    {
        int nearClicIndex = _dichotomyNarrowSearch(_sphereLocalPosition); // narrow down vertex index via dichotomy. Output will be near actual vertex clicked
        nearClicIndex = _findClosestIndexViaNeighbors(nearClicIndex, _sphereLocalPosition); // this is the one clicked ! (i.e. closest to clic position)
        return nearClicIndex;
    }

    private int _dichotomyNarrowSearch(Vector3 sphereLocalHit)
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
        Vector2 sampleCenter = uvCenter;
        Vector2[] samplePoss = new Vector2[4];
        float sampleRange = 0.25f; // Space between sample Center and samples. We start with center at 0.5,0.5, so first samples are 0.25 apart

        int iterations = 0;
        while(iterations++ < 6) // 6 seems enough for a sphere resolution of 200
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
        while(iteration++ < 50) // wouldn't want an infinite loop
        {
            List<int> nghbs = Planet.getNeighbours(index);
            int closest = _findClosest(nghbs, localToSphere, out float dist);
            if(dist > refDist)
            {
                // All neihbors are further than the point, we found the one
                //GD.Print("Found clicked vertex in " + iteration + " iterations.");
                return index;
            }
            //GD.Print("NghbFind: ids " + index + "(" + refDist + ") -> " + closest + "(" + dist + ")");
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
