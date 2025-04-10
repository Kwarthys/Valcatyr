using Godot;
using System;
using System.Collections.Generic;

public partial class PlanetInputManager : Area3D
{
    [Export]
    private Planet planet;

    [Export]
    public GameManager gameManager;
    
    [Export]
    public Camera3D camera {get; set;}

    public override void _PhysicsProcess(double _dt)
    {
        bool primary = Input.IsActionJustPressed("Primary");
        bool secondary = Input.IsActionJustPressed("Secondary");
        if(primary || secondary)
        {
            GameManager.PlanetInteraction interaction = primary ? GameManager.PlanetInteraction.Primary : GameManager.PlanetInteraction.Secondary;

            Vector2 mousePos = GetViewport().GetMousePosition();
            Vector3 from = camera.ProjectRayOrigin(mousePos);
            Vector3 to = from + camera.ProjectRayNormal(mousePos) * 5.0f;
            // this types are aweful so let's use some "var"
            var spaceState = GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.CollideWithAreas = true;
            var result = spaceState.IntersectRay(query);

            if(result.Count == 0)
            {
                gameManager.onPlanetInteraction(interaction, -1);
                return;
            }

            Vector3 worldHitPos = (Vector3)result["position"];
            Vector3 planetLocalHitPos = planet.ToLocal(worldHitPos);

            int nearClicIndex = _dichotomyNarrowSearch(planetLocalHitPos); // narrow down vertex index via dichotomy. Output will be near actual vertex clicked
            nearClicIndex = _findClosestIndexViaNeighbors(nearClicIndex, planetLocalHitPos); // this is the one clicked ! (i.e. closest to clic position)

            gameManager.onPlanetInteraction(interaction, nearClicIndex);
        }
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
