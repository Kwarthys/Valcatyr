using Godot;
using System;
using System.Collections.Generic;

public partial class TroopDisplayManager : Node3D
{
    [Export]
    private PackedScene level1PawnScene;
    [Export]
    private PackedScene level2PawnScene;

    private Dictionary<int, TroopsData> troopsPerState = new();

    public const int PAWN_FACTORISATION_COUNT = 10; // one level 2 PAWN will be worth this value of level 1 pawns

    public void updateDisplay(Country _c)
    {
        if(troopsPerState.ContainsKey(_c.state.id) == false)
            troopsPerState.Add(_c.state.id, new());

        int troopScore = _c.troops;
        int level1Needed = troopScore % PAWN_FACTORISATION_COUNT;
        int level2Needed = troopScore / PAWN_FACTORISATION_COUNT; // hard coded 2 pawn type system, will do cleaner if more pawn type is needed

        TroopsData troops = troopsPerState[_c.state.id];

        // Manage level 1 Pawns
        if(level1Needed > troops.level1Pawns.Count)
        {
            // Spawn levelones
            _spawnLevelOnes(level1Needed - troops.level1Pawns.Count, _c);
        }
        else if(level1Needed < troops.level1Pawns.Count)
        {
            // Destroy levelones
            _destroyPawnsIn(troops.level1Pawns.Count - level1Needed, troops.level1Pawns);
        }

        // Manage level 2 Pawns
        if(level2Needed > troops.level2Pawns.Count)
        {
            // Spawn leveltwos
            _spawnLevelTwos(level2Needed - troops.level2Pawns.Count, _c);
        }
        else if(level2Needed < troops.level2Pawns.Count)
        {
            // Destroy leveltwos
            _destroyPawnsIn(troops.level2Pawns.Count - level2Needed, troops.level2Pawns);
        }

        troops.troops = troopScore;
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

    private int _spawnPawn(int _n, PackedScene _pawnScene, List<PawnData> _spawnedPawns, Country _c)
    {
        List<ReferencePoint> points = _c.referencePoints;
        List<int> ids = new();
        for(int i = 0; i < points.Count; ++i) ids.Add(i);
        _spawnedPawns.ForEach((data) => ids.Remove(data.referencePointIndex)); // remove already taken reference points

        for(int i = 0; i < _n; ++i)
        {
            int index = ids[(int)(GD.Randf() * ids.Count)];
            ReferencePoint p = points[index];

            Node3D pawn = _pawnScene.Instantiate<Node3D>();
            AddChild(pawn);
            pawn.Position = p.vertex;
            Vector3 localForward = new(p.normal.Y, -p.normal.X, 0.0f); // A specific permutation of normal vector that create a perpendical vector from it
            localForward = localForward.Rotated(p.normal, GD.Randf() * Mathf.Tau); // Rotate forward randomly around normal
            pawn.LookAt(ToGlobal(p.vertex + localForward), ToGlobal(p.normal));

            PawnColorManager helper = (PawnColorManager)pawn;   // Getting the script attached to the root of the pawn scene
            helper.setColor(Player.playerColors[_c.playerID]);  // Apply player color

            PawnData pawnData = new();
            pawnData.instance = pawn;
            pawnData.referencePointIndex = index;
            _spawnedPawns.Add(pawnData);
            ids.Remove(index);

            if(ids.Count == 0)
            {
                GD.PrintErr("TroopDisplayManager._spawnPawn Ran out of ReferncePoints");
                return i+1; // returning how many we could create. If not zero, issue occured
            }
        }
        return _n;
    }

    private void _destroyPawnsIn(int _n, List<PawnData> _list)
    {
        if(_n >= _list.Count)
            _n = _list.Count;
        
        for(int i = 0; i < _n; ++i)
        {
            int index = (int)(GD.Randf() * (_list.Count - 1));
            RemoveChild(_list[index].instance);
            _list.RemoveAt(index);
        }
    }

    struct TroopsData
    {
        public TroopsData() { troops = 0; level1Pawns = new(); level2Pawns = new(); }
        public int troops;
        public List<PawnData> level1Pawns;
        public List<PawnData> level2Pawns;
    }

    struct PawnData
    {
        public Node3D instance;
        public int referencePointIndex;
    }
}
