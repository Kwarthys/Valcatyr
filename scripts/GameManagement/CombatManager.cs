using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;

/// <summary>
/// Combat manager will be responsible for managing combat troops selection, fight outcome and player feedback
/// </summary>
public partial class CombatManager : Node
{
    // Singleton
    public static CombatManager Instance {get; private set;}
    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>
    /// Handles combat, returns the number of troops to move if a change of owner is needed
    /// </summary>
    public int startCombat(Country _attacker, Country _defender)
    {
        if(_attacker.troops <= 1)
        {
            GD.PrintErr("Tried to attack with a country with not enough troops"); // Should not have reached here
            return 0;
        }

        int attackers = Mathf.Min(3, _attacker.troops - 1); // TODO Player selection
        int defenders = Mathf.Min(2, _defender.troops);     // TODO Player selection (IA will always defend with max) (MAYBE setting to always play with max to speed up combat ?)
        CombatOutcome combatOutcome = _resolveCombat(attackers, defenders);
        _attacker.troops -= combatOutcome.attackerLosses;
        _defender.troops -= combatOutcome.defenderLosses;
        if(_defender.troops == 0)
            return attackers - combatOutcome.attackerLosses;
        return 0;
    }

    struct CombatOutcome
    {
        public int attackerLosses;
        public int defenderLosses;
        public override string ToString()
        {
            return "Attacker: -" + attackerLosses + " Defender: -" + defenderLosses;
        }

    }

    /// <summary>
    /// Returns result from attacker perspective: Positive, number of defenders killed. Negative, number of attackers lost
    /// </summary>
    private CombatOutcome _resolveCombat(int _attackers, int _defenders)
    {
        uint[] attackerScores = _throwDice(_attackers, 6u); // Simulate dice roll as we might want to display them in the future (and add modifiers)
        uint[] defenderScores = _throwDice(_defenders, 6u);
        int checkRange = Mathf.Min(_attackers, _defenders);
        CombatOutcome outcome = new();
        for(int i = 0; i < checkRange; ++i)
        {
            if(attackerScores[i] > defenderScores[i])
                outcome.defenderLosses += 1;
            else
                outcome.attackerLosses += 1;
        }
        return outcome;
    }

    private uint[] _throwDice(int _n, uint _faces)
    {
        List<uint> results = new();
        for(int i = 0; i < _n; ++i)
        {
            results.Add(_thowDie(_faces));
        }
        results.Sort(_compareDiceHighToLow);
        return results.ToArray();
    }

    /// <summary>
    /// Result will range from 0 to _faces - 1
    /// </summary>
    private uint _thowDie(uint _faces) { return GD.Randi() % _faces; }

    /// <summary>
    /// Comparer function to sort from highest to lowest dice results
    /// </summary>
    private int _compareDiceHighToLow(uint _a, uint _b)
    {
        if(_a > _b) return -1;
        if(_b > _a ) return 1;
        return 0;
    }
}
