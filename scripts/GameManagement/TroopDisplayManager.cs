using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public partial class TroopDisplayManager : Node3D
{
    [Export]
    private PackedScene explosionFX;
    [Export]
    private PackedScene movementSoundFX;
    [Export]
    private Curve pawnMovementSpeedCurve;

    private Dictionary<int, TroopsData> troopsPerState = new();

    public const int PAWN_FACTORISATION_COUNT = 10; // one level 2 PAWN will be worth this value of level 1 pawns
    public const double PAWN_MOVEMENT_DURATION = 1.0;
    public const float PAWN_MOVEMENT_HEIGHT = 0.01f;

    private List<PawnsMovement> pawnsMovements = new();

    public void reset()
    {
        // Destroy all pawns
        foreach (TroopsData data in troopsPerState.Values)
        {
            _destroyPawnsIn(data.level1Pawns.Count, data.level1Pawns, GD.Randf() > 0.95f);
            _destroyPawnsIn(data.level2Pawns.Count, data.level2Pawns, GD.Randf() > 0.8f); // Play some FX for extra fun
        }
        troopsPerState.Clear();
        pawnsMovements.Clear();
    }

    public Vector3[] getPosRotOfAPawn(Country _c)
    {
        Vector3[] values = new Vector3[2];
        Node3D pawn = null;
        TroopsData data = troopsPerState[_c.state.id];
        if(data.level2Pawns.Count > 0)
        {
            pawn = data.level2Pawns[0].instance;
        }
        else
        {
            pawn = data.level1Pawns[0].instance;
        }

        values[0] = pawn.Position;
        values[1] = pawn.Rotation;
        return values;
    }

    public void movePawns(Country _origin, Country _destination, int _amount)
    {
        if (troopsPerState.ContainsKey(_origin.state.id) == false)
            throw new Exception("Cannot move troops from an uknown state");
        if (troopsPerState.ContainsKey(_destination.state.id) == false)
            throw new Exception("Cannot move troops to an uknown state");
        if (troopsPerState[_origin.state.id].troops <= _amount)
            throw new Exception("Origin country has not enough armies to move");

        TroopsData originData = troopsPerState[_origin.state.id];

        int l1Moving = _amount % PAWN_FACTORISATION_COUNT;
        int l2Moving = _amount / PAWN_FACTORISATION_COUNT;
        // We may have to despawn level2 pawns to create more level 1. If i have 10 and i want to move 5, i'll have to break this one 10 token
        if (l1Moving > originData.level1Pawns.Count)
        {
            // Break a level 2 pawn into multiple level 1 -> It will take more referencepoint than available, so allow doubles
            _destroyPawnsIn(1, originData.level2Pawns, false);
            _spawnLevelOnes(PAWN_FACTORISATION_COUNT, _origin);
        }

        // Create Movement data
        pawnsMovements.Add(new()
        {
            destination = _destination,
            troopsValue = _amount,
            pawns = new()
        });

        // Get destination points, preferably not overlapping, but it is allowed as a merge will can occur at arrival
        List<ReferencePoint> l1Targets = _getGroundReferencePoints(l1Moving, _destination, troopsPerState[_destination.state.id].level1Pawns, false);
        List<ReferencePoint> l2Targets = _getAirReferencePoints(l2Moving, _destination, troopsPerState[_destination.state.id].level2Pawns, false);

        TroopsData destinationData = troopsPerState[_destination.state.id];

        // Retreive pawns that will move, remove them from origin country (and troopsData)
        for (int i = 0; i < l1Moving; ++i)
        {
            // Start from the end, most likely to find overlapping panws there
            int index = originData.level1Pawns.Count - 1;
            pawnsMovements.Last().pawns.Add(new() { pawn = originData.level1Pawns[index], destination = l1Targets[i] });
            // Remove from origin country
            originData.level1Pawns.RemoveAt(index);
            // Add to destination data
            destinationData.level1Pawns.Add(pawnsMovements.Last().pawns.Last().pawn);
        }
        for (int i = 0; i < l2Moving; ++i)
        {
            // Start from the end, most likely to find overlapping panws there
            int index = originData.level2Pawns.Count - 1;
            pawnsMovements.Last().pawns.Add(new() { pawn = originData.level2Pawns[index], destination = l2Targets[i] });
            // Remove from origin country
            originData.level2Pawns.RemoveAt(index);
            // Add to destination data
            destinationData.level2Pawns.Add(pawnsMovements.Last().pawns.Last().pawn);
        }
        // Instantly update troops, don't wait for movement end
        originData.troops -= _amount;
        troopsPerState[_destination.state.id].troops += _amount;

        // Create sound effect for this movement
        PawnSlideSoundManager slideFX = movementSoundFX.Instantiate<PawnSlideSoundManager>();
        // Retreive a pawn to attach the sound FX to
        Node3D pawn = pawnsMovements.Last().pawns[0].pawn.instance;
        slideFX.follow(pawn);
        AddChild(slideFX);
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
                if(pawn.pawn.instance.IsInsideTree() == false)
                {
                    // Pawn must have been destroyed while moving, happens when player attacks next country without waiting movement end
                    // Simply skip it, don't bother delete, it will happen at the end of the movement anyway
                    continue; 
                }
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
        int factionID = GameManager.Instance.getFactionIDOfPlayer(_c.playerID);
        PackedScene pawnScene = PreloadManager.getPawnScene(factionID, 1);
        if(pawnScene != null)
            _spawnPawn(_n, pawnScene, pawns, false, _c, false);
    }

    private void _spawnLevelTwos(int _n, Country _c)
    {
        List<PawnData> pawns = troopsPerState[_c.state.id].level2Pawns;
        int factionID = GameManager.Instance.getFactionIDOfPlayer(_c.playerID);
        PackedScene pawnScene = PreloadManager.getPawnScene(factionID, 2);
        if(pawnScene != null)
            _spawnPawn(_n, pawnScene, pawns, true, _c, false);
    }

    private List<ReferencePoint> _getAirReferencePoints(int _n, Country _c, List<PawnData> _spawnedPawns, bool _enforceAvailablePoint = true)
    {
        return _getReferencePoints(_n, _spawnedPawns, _enforceAvailablePoint, _c.airReferencePoints);
    }

    private List<ReferencePoint> _getGroundReferencePoints(int _n, Country _c, List<PawnData> _spawnedPawns, bool _enforceAvailablePoint = true)
    {
        return _getReferencePoints(_n, _spawnedPawns, _enforceAvailablePoint, _c.referencePoints);
    }

    private List<ReferencePoint> _getReferencePoints(int _n, List<PawnData> _spawnedPawns, bool _enforceAvailablePoint, List<ReferencePoint> _pointList)
    {
        List<ReferencePoint> availablePoints = new(_pointList);
        List<ReferencePoint> selectedPoints = new();
        _spawnedPawns.ForEach((data) => availablePoints.Remove(data.point)); // Remove already taken reference points

        for (int i = 0; i < _n; ++i)
        {
            if (availablePoints.Count == 0)
            {
                if (_enforceAvailablePoint)
                {
                    GD.PrintErr("TroopDisplayManager._spawnPawn Ran out of ReferencePoints");
                    break; // stop right here if we cannot provide available point
                }
                else
                {
                    availablePoints = new(_pointList); // We're allowed to use a taken point, add all points to the available list
                }
            }
            int index = (int)(GD.Randf() * availablePoints.Count);
            selectedPoints.Add(availablePoints[index]);
            availablePoints.RemoveAt(index);
        }
        return selectedPoints;
    }

    private int _spawnPawn(int _n, PackedScene _pawnScene, List<PawnData> _spawnedPawns, bool _isAir, Country _c, bool _enforceAvailablePoint = true)
    {
        List<ReferencePoint> availablePoints;
        if(_isAir)
            availablePoints = _getAirReferencePoints(_n, _c, _spawnedPawns, _enforceAvailablePoint);
        else
            availablePoints = _getGroundReferencePoints(_n, _c, _spawnedPawns, _enforceAvailablePoint);

        foreach (ReferencePoint p in availablePoints)
        {
            Node3D pawn = _pawnScene.Instantiate<Node3D>();
            AddChild(pawn);
            pawn.Position = p.vertex;
            Vector3 localForward = new(p.normal.Y, -p.normal.X, 0.0f); // A specific permutation of normal vector that creates a perpendicular vector from it
            localForward = localForward.Rotated(p.normal, GD.Randf() * Mathf.Tau); // Rotate forward randomly around normal
            pawn.LookAt(ToGlobal(p.vertex + localForward), ToGlobal(p.normal));

            PawnColorManager helper = (PawnColorManager)pawn;   // Getting the script attached to the root of the pawn scene
            helper.setColor(_c.playerID);    // Apply player color

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
                CameraShaker.shake();
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

    public class PawnData
    {
        public Node3D instance;
        public ReferencePoint point;
    }

    public class TransitingPawn
    {
        public PawnData pawn;
        public ReferencePoint destination; // Origin is stored in the PawnData Reference point field
    }

    public class PawnsMovement
    {
        public Country destination;
        public List<TransitingPawn> pawns;
        public int troopsValue;
        public double movementTimer = 0.0f;
    }
}
