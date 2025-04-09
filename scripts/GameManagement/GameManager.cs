using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameManager : Node
{
    [Export]
    private TroopDisplayManager troopManager;

    private Planet planet = null;

    private List<Country> countries = new();
    private const float REFERENCE_POINTS_MINIMAL_DISTANCE = 0.002f; // Minimal distance that must exist between any two reference points of same state

    public void initialize(Planet _planet)
    {
        planet = _planet;
        _initializeCountries();
        countries.ForEach((c) => {c.troops = (int)(GD.Randf() * 50.0 + 1); troopManager.updateDisplay(ref c, c.troops); });
    }

    private void _initializeCountries()
    {
        foreach(State s in planet.mapManager.states)
        {
            countries.Add(new());
            Country c = countries.Last();
            c.stateID = s.id;

            // Find reference points, that will later be used to spawn Pawns
            List<int> pointIndices = new();
            List<int> blackListed = new();
            bool leave = false;
            bool pointsAreGood = false;
            int loops = 0;
            while(pointsAreGood == false)
            {
                loops++;
                float minDist = REFERENCE_POINTS_MINIMAL_DISTANCE * (1.0f - (loops / 20) * 0.1f); // reduce minimal distance over time to avoid infinite loops
                if(loops%20 == 0)
                {
                    //GD.Print("points dist reduced to " + minDist);
                    blackListed.Clear();
                }

                // Always keep list full, either for first loop or to replace removed point
                int intLoops = 0;
                while(pointIndices.Count < TroopDisplayManager.PAWN_FACTORISATION_COUNT + 2) // more reference point to add diversity
                {
                    intLoops++;
                    float localMinDist = minDist * (1.0f - (intLoops / 20) * 0.1f); // reduce minimal distance AGAIN over time to avoid infinite loops
                    if(intLoops%20 == 0)
                    {
                        //GD.Print("ShoreDist reduced to " + localMinDist);
                        blackListed.Clear();
                    }
                    int indexCandidate = s.land[(int)(GD.Randf() * (s.land.Count-1))].fullMapIndex;
                    if(blackListed.Contains(indexCandidate) || s.boundaries.Contains(indexCandidate) || s.shores.Contains(indexCandidate))
                        continue;

                    if(pointIndices.Contains(indexCandidate) == false)
                    {
                        Vector3 point = planet.getVertex(indexCandidate);

                        bool checkDistances(List<int> indices)
                        {
                            foreach (int borderIndex in indices)
                            {
                                float dist = point.DistanceSquaredTo(planet.getVertex(s.land[borderIndex].fullMapIndex));
                                if (dist < localMinDist)
                                    return false;
                            }
                            return true;
                        }

                        // Check distance to shores/borders
                        if(checkDistances(s.boundaries) && checkDistances(s.shores))
                            pointIndices.Add(indexCandidate);
                        else
                            blackListed.Add(indexCandidate); // shores and borders won't change, we can defintely forget about this one

                    }
                }

                // Check all the points
                leave = false;
                for(int j = 0; j < pointIndices.Count && leave == false; ++j)
                {
                    for(int i = j+1; i < pointIndices.Count && leave == false; ++i)
                    {
                        int indexJ = pointIndices[j];
                        int indexI = pointIndices[i];
                        float dist = planet.getVertex(indexJ).DistanceSquaredTo(planet.getVertex(indexI));

                        if(dist < minDist)
                        {
                            // redraw one vertex at random
                            int indexToRedraw = GD.Randf() > 0.5f ? i : j;
                            pointIndices.RemoveAt(indexToRedraw);
                            leave = true;
                        }
                    }
                }

                if(leave)
                    continue;

                pointsAreGood = true; // All distances were above min \o/
                foreach(int index in pointIndices)
                {
                    planet.getVertexAndNormal(index, out Vector3 vertex, out Vector3 normal);
                    c.referencePoints.Add(new(vertex, normal));
                }
            }
        }
    }
}
