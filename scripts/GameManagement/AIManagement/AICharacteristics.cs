using Godot;
using System;

public class AICharacteristics
{
    /// <summary>
    /// This one is the baseline for all AIs, that try to emulate a balanced gameplay
    /// AIs will then factor those with their own to tune their behavior
    /// </summary>
    public static AICharacteristicsData baseline { get; private set; } = new()
    {
        threatFactor = 1.5f,
        countryFactor = 0.5f,
        continentFactor = 2.0f,
        unusableTroopsFactor = 0.1f
    };

    public static AICharacteristicsData neutral { get; private set; } = new()
    {
        threatFactor = 1.0f,
        countryFactor = 1.0f,
        continentFactor = 1.0f,
        unusableTroopsFactor = 1.0f
    };
}

public struct AICharacteristicsData
{
    public float threatFactor;
    public float countryFactor;
    public float continentFactor;
    public float unusableTroopsFactor;

    public static AICharacteristicsData operator *(AICharacteristicsData _a, AICharacteristicsData _b)
    {
        _a.threatFactor *= _b.threatFactor;
        _a.countryFactor *= _b.countryFactor;
        _a.continentFactor *= _b.continentFactor;
        _a.unusableTroopsFactor *= _b.unusableTroopsFactor;
        return _a;
    }
    public override string ToString()
    {
        return threatFactor + ", " + countryFactor + ", " + continentFactor + ", " + unusableTroopsFactor;
    }

}
