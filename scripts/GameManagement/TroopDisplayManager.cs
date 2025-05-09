using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public partial class TroopDisplayManager : Node3D
{
    [Export]
    private PackedScene level1PawnScene;
    [Export]
    private PackedScene level2PawnScene;
    [Export]
    private PackedScene explosionFX;
    [Export]
    private Curve pawnMovementSpeedCurve;

    private Dictionary<int, TroopsData> troopsPerState = new();

    public const int PAWN_FACTORISATION_COUNT = 10; // one level 2 PAWN will be worth this value of level 1 pawns
    public const double PAWN_MOVEMENT_DURATION = 1.0;
    public const float PAWN_MOVEMENT_HEIGHT = 0.01f;

    private List<PawnsMovement> pawnsMovements = new();

    public void movePawns(Country _origin, Country _destination, int _amount)
    {
        if(troopsPerState.ContainsKey(_origin.state.id) == false)
            throw new Exception("Cannot move troops from an uknown state");
        if(troopsPerState.ContainsKey(_destination.state.id) == false)
            throw new Exception("Cannot move troops to an uknown state");
        if(troopsPerState[_origin.state.id].troops <= _amount)
            throw new Exception("Origin country has not enough armies to move");

        TroopsData data = troopsPerState[_origin.state.id];

        int l1Moving = _amount % PAWN_FACTORISATION_COUNT;
        int l2Moving = _amount / PAWN_FACTORISATION_COUNT;
        // We may have to despawn level2 pawns to create more level 1. If i have 10 and i want to move 5, i'll have to break this one 10 token
        if(l1Moving > data.level1Pawns.Count)
        {
            // Break a level 2 pawn into multiple level 1 -> It will take more referencepoint than available, so allow doubles
            _destroyPawnsIn(1, data.level2Pawns, false);
            _spawnLevelOnes(PAWN_FACTORISATION_COUNT, _origin);
        }

        // Create Movement data
        pawnsMovements.Add(new(){
            destination = _destination,
            troopsValue = _amount,
            pawns = new()
        });
        
        // Get destination points, preferably not overlapping, but it is allowed as a merge will can occur at arrival
        List<ReferencePoint> l1Targets = _getReferencePoints(l1Moving, _destination, troopsPerState[_destination.state.id].level1Pawns, false);
        List<ReferencePoint> l2Targets = _getReferencePoints(l2Moving, _destination, troopsPerState[_destination.state.id].level2Pawns, false);

        // Retreive pawns that will move, remove them from origin country (and troopsData)
        for(int i = 0; i < l1Moving; ++i)
        {
            // Start from the end, most likely to find overlapping panws there
            int index = data.level1Pawns.Count - 1;
            pawnsMovements.Last().pawns.Add(new(){ pawn = data.level1Pawns[index], destination = l1Targets[i], level = 1});
            // Remove from origin country
            data.level1Pawns.RemoveAt(index);
        }
        for(int i = 0; i < l2Moving; ++i)
        {
            // Start from the end, most likely to find overlapping panws there
            int index = data.level2Pawns.Count - 1;
            pawnsMovements.Last().pawns.Add(new(){ pawn = data.level2Pawns[index], destination = l2Targets[i], level = 2});
            // Remove from origin country
            data.level2Pawns.RemoveAt(index);
        }
        // Instantly update troops, don't wait for movement end
        data.troops -= _amount;
        troopsPerState[_destination.state.id].troops += _amount;
    }

    public void updateDisplay(Country _c)
    {
        if(troopsPerState.ContainsKey(_c.state.id) == false)
            troopsPerState.Add(_c.state.id, new());

        int troopScore = _c.troops;
        int level1Needed = troopScore % PAWN_FACTORISATION_COUNT;
        int level2Needed = troopScore / PAWN_FACTORISATION_COUNT; // hard coded 2 pawn type system, will do cleaner if more pawn type is needed

        TroopsData troops = troopsPerState[_c.state.id];

        bool playExplosions = _c.troops < (troops.level1Pawns.Count + troops.level2Pawns.Count * PAWN_FACTORISATION_COUNT);

        // Manage level 1 Pawns
        if(level1Needed > troops.level1Pawns.Count)
        {
            _spawnLevelOnes(level1Needed - troops.level1Pawns.Count, _c);
        }
        else if(level1Needed < troops.level1Pawns.Count)
        {
            // Destroy levelones
            _destroyPawnsIn(troops.level1Pawns.Count - level1Needed, troops.level1Pawns, playExplosions);
        }

        // Manage level 2 Pawns
        if(level2Needed > troops.level2Pawns.Count)
        {
            _spawnLevelTwos(level2Needed - troops.level2Pawns.Count, _c);
        }
        else if(level2Needed < troops.level2Pawns.Count)
        {
            // Destroy leveltwos
            _destroyPawnsIn(troops.level2Pawns.Count - level2Needed, troops.level2Pawns, playExplosions);
        }

        troops.troops = troopScore;
    }

    public override void _Process(double _dt)
    {
        if(pawnsMovements.Count == 0) return;

        foreach(PawnsMovement movement in pawnsMovements)
        {
            movement.movementTimer += _dt;
            double t = movement.movementTimer / PAWN_MOVEMENT_DURATION;
            if(t > 1.0)
            {
                _manageMovementEnd(movement);
                continue;
            }
            float time = pawnMovementSpeedCurve.Sample((float)t);
            float elevation = -4 * PAWN_MOVEMENT_HEIGHT * (time - 0.5f) * (time - 0.5f) + PAWN_MOVEMENT_HEIGHT; // ax² + c with c = HEIGHT and a = -4c

            foreach(TransitingPawn pawn in movement.pawns)
            {
                float baseElevation = Mathf.Lerp(pawn.pawn.point.vertex.Length(), pawn.destination.vertex.Length(), time);
                Vector3 pos = pawn.pawn.point.vertex.Lerp(pawn.destination.vertex, time);
                pos = pos.Normalized() * (baseElevation + elevation);
                pawn.pawn.instance.Position = pos;
                // Manage rotation
                Vector3 normal = pos.Normalized();
                Vector3 generalDirection = (pawn.destination.vertex - pawn.pawn.point.vertex).Normalized();
                Vector3 side = normal.Cross(generalDirection);
                Vector3 forward = side.Cross(normal);
                pawn.pawn.instance.LookAt(ToGlobal(pos + forward), ToGlobal(normal));
            }
        }

        for(int i = pawnsMovements.Count - 1; i >= 0; --i)
        {
            if(pawnsMovements[i].movementTimer > PAWN_MOVEMENT_DURATION)
            {
                pawnsMovements.RemoveAt(i);
            }
        }
    }

    private void _manageMovementEnd(PawnsMovement _move)
    {
        TroopsData data = troopsPerState[_move.destination.state.id];
        // Add pawns to the country troops
        for(int i = 0; i < _move.pawns.Count; ++i)
        {
            // Pawn destination becomes its actual point
            _move.pawns[i].pawn.point = _move.pawns[i].destination;
            if( _move.pawns[i].level == 1)
            {
                data.level1Pawns.Add(_move.pawns[i].pawn);
            }
            else // level 2
            {
                data.level2Pawns.Add(_move.pawns[i].pawn);
            }
        }

        if(data.level1Pawns.Count >= PAWN_FACTORISATION_COUNT)
        {
            // Too much litle pawns, destroy factorisation count and spawn a level2
            _destroyPawnsIn(PAWN_FACTORISATION_COUNT, data.level1Pawns, false);
            _spawnLevelTwos(1, _move.destination);
        }
    }


    private void _spawnLevelOnes(int _n, Country _c)
    {
        List<PawnData> pawns = troopsPerState[_c.state.id].level1Pawns;
        _spawnPawn(_n, level1PawnScene, pawns, _c);
    }

    private void _spawnLevelTwos(int _n, Country _c)
    {
        List<PawnData> pawns = troopsPerState[_c.state.id].level2Pawns;
        _spawnPawn(_n, level2PawnScene, pawns, _c);
    }

    private List<ReferencePoint> _getReferencePoints(int _n, Country _c, List<PawnData> _spawnedPawns, bool _enforceAvailablePoint = true)
    {
        List<ReferencePoint> availablePoints = new(_c.referencePoints);
        List<ReferencePoint> selectedPoints = new();
        _spawnedPawns.ForEach((data) => availablePoints.Remove(data.point)); // Remove already taken reference points

        for(int i = 0; i < _n; ++i)
        {
            if(availablePoints.Count == 0)
            {
                if(_enforceAvailablePoint)
                {
                    GD.PrintErr("TroopDisplayManager._spawnPawn Ran out of ReferencePoints");
                    break; // stop right here if we cannot provide available point
                }
                else
                {
                    availablePoints = new(_c.referencePoints); // We're allowed to use a taken point, add all points to the available list
                }
            }
            int index = (int)(GD.Randf() * availablePoints.Count);
            selectedPoints.Add(availablePoints[index]);
            availablePoints.RemoveAt(index);
        }
        return selectedPoints;
    }

    private int _spawnPawn(int _n, PackedScene _pawnScene, List<PawnData> _spawnedPawns, Country _c, bool _enforceAvailablePoint = true)
    {
        List<ReferencePoint> availablePoints = _getReferencePoints(_n, _c, _spawnedPawns, _enforceAvailablePoint);
        foreach(ReferencePoint p in availablePoints)
        {
            Node3D pawn = _pawnScene.Instantiate<Node3D>();
            AddChild(pawn);
            pawn.Position = p.vertex;
            Vector3 localForward = new(p.normal.Y, -p.normal.X, 0.0f); // A specific permutation of normal vector that creates a perpendicular vector from it
            localForward = localForward.Rotated(p.normal, GD.Randf() * Mathf.Tau); // Rotate forward randomly around normal
            pawn.LookAt(ToGlobal(p.vertex + localForward), ToGlobal(p.normal));

            PawnColorManager helper = (PawnColorManager)pawn;   // Getting the script attached to the root of the pawn scene
            helper.setColor(Player.playerColors[_c.playerID]);  // Apply player color

            PawnData pawnData = new() { instance = pawn, point = p };
            _spawnedPawns.Add(pawnData);
        }
        return availablePoints.Count;
    }

    private void _destroyPawnsIn(int _n, List<PawnData> _list, bool _playFX)
    {
        if(_n >= _list.Count)
            _n = _list.Count;

        bool tryFindDuplicate(out int duplicateIndex)
        {
            duplicateIndex = -1;
            for(int j = 0; j < _list.Count; ++j) 
            {
                for(int i = j+1; i < _list.Count; ++i)
                {
                    if(_list[j].point.vertex == _list[i].point.vertex)
                    {
                        duplicateIndex = j;
                        return true;
                    }
                }
            }
            return false;
        }
        
        for(int i = 0; i < _n; ++i)
        {
            if (tryFindDuplicate(out int index) == false) // Remove duplicates at first
                index = (int)(GD.Randf() * (_list.Count - 1)); // else remove random

            if (_playFX)
            {
                Node3D fx = explosionFX.Instantiate<Node3D>();
                fx.Position = _list[index].instance.Position;
                fx.Rotation = _list[index].instance.Rotation;
                AddChild(fx); // fx will desotry ifself at the end of its animation
            }

            RemoveChild(_list[index].instance);
            _list.RemoveAt(index);
        }
    }

    public class TroopsData
    {
        public int troops = 0;
        public List<PawnData> level1Pawns = new();
        public List<PawnData> level2Pawns = new();
    }

    public struct PawnData
    {
        public Node3D instance;
        public ReferencePoint point;
    }

    public class TransitingPawn
    {
        public PawnData pawn;
        public ReferencePoint destination; // Origin is stored in the PawnData Reference point field
        public int level;
    }

    public class PawnsMovement
    {
        public Country destination;
        public List<TransitingPawn> pawns;
        public int troopsValue;
        public double movementTimer = 0.0f;
    }
}
