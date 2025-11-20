using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

public static class NameGenerator
{
    private static char[] vowels = {'a', 'e', 'i', 'o', 'u', 'y'};
    private static char[] consonants = {'b','c','d','f','g','h','j','k','l','m','n','p','q','r','s','t','v','w','x','z'};

    public static LangageRules getNewLangage()
    {
        LangageRules l = new();
        float totalWeight = 0.0f;
        
        foreach(char vowel in vowels)
        {
            float w = GD.Randf();
            l.vowels.Add(vowel, w);
            totalWeight += w;
        }
        l.totalVowelWeights = totalWeight;

        totalWeight = 0.0f;
        foreach(char consonant in consonants)
        {
            float w = GD.Randf();
            l.consonants.Add(consonant, w);
            totalWeight += w;
        }
        l.totalConsonantWeights = totalWeight;

        l.consonantStreak *= GD.Randf();
        l.doubleConsonants *= GD.Randf();
        l.vowelStreak *= GD.Randf();
        l.doubleVowels *= GD.Randf();

        l.nameLengths.Y = Mathf.CeilToInt(l.nameLengths.X + GD.Randf() * GD.Randf() * l.nameLengths.Y);

        if(GD.Randf() > 0.33f)
        {
            l.usesSuffixes = true;
            // generate suffix
            MayBool vowelStart = MayBool.Any;
            MayBool vowelEnd = MayBool.Any;
            int suffixLength = Mathf.CeilToInt(Mathf.Lerp(2, 6, GD.Randf()));
            l.suffix = generate(l, suffixLength, ref vowelStart, ref vowelEnd, false);
            l.suffixVowelStart = vowelStart == MayBool.Yes;
        }

        return l;
    }

    public static string generateStateName(LangageRules _rules)
    {
        int wordLength = Mathf.RoundToInt(Mathf.Lerp(_rules.nameLengths.X, _rules.nameLengths.Y, GD.Randf()));
        MayBool vowelStart = MayBool.Any;
        MayBool vowelEnd = MayBool.Any;
        if(_rules.usesSuffixes)
        {
            wordLength -= _rules.suffix.Length;
            if(_rules.suffixVowelStart)
                vowelEnd = MayBool.No;
            else
                vowelEnd = MayBool.Yes;
        }
        string word = generate(_rules, wordLength, ref vowelStart, ref vowelEnd);

        if(_rules.usesSuffixes)
            return word + _rules.suffix;
        return word;
    }

    private static string generate(LangageRules _rules, int _desiredLength, ref MayBool _vowelStart, ref MayBool _vowelEnd, bool _makeUpperFirst = true)
    {
        string name = "";
        bool first = _makeUpperFirst;

        bool nextLetterAsVowel = _vowelStart == MayBool.Yes ? true : _vowelStart == MayBool.No ? false : GD.Randf() > 0.5f;
        bool append = true;
        bool addedAVowel;

        while(append)
        {
            addedAVowel = nextLetterAsVowel;

            if(nextLetterAsVowel)
            {
                nextLetterAsVowel = false;
                char letter = _rules.pickVowel();
                name += letter;

                if(first)
                {
                    name = name.ToUpper();
                    first = false;
                    _vowelStart = MayBool.Yes;
                }

                if(_rules.doubleVowels > GD.Randf())
                    name += letter;
                else if(_rules.vowelStreak > GD.Randf())
                    nextLetterAsVowel = true;
            }
            else
            {
                nextLetterAsVowel = true;
                char letter = _rules.pickConsonant();
                name += letter;

                if(first)
                {
                    name = name.ToUpper();
                    first = false;
                    _vowelStart = MayBool.No;
                }

                if(_rules.doubleConsonants > GD.Randf())
                    name += letter;
                else if(_rules.consonantStreak > GD.Randf())
                    nextLetterAsVowel = false;
            }

            if(name.Length >= _desiredLength)
            {
                if(_vowelEnd == MayBool.No && addedAVowel)
                    continue;
                if(_vowelEnd == MayBool.Yes && addedAVowel == false)
                    continue;
                return name;
            }
        }

        GD.PrintErr("Reached end of NameGenerator generate method");
        return "-"; // should never happen
    }
}

public class LangageRules
{
    public Dictionary<char, float> vowels= new();
    public float totalVowelWeights = -1.0f;
    public Dictionary<char, float> consonants = new();
    public float totalConsonantWeights = -1.0f;
    public float consonantStreak = 0.1f;
    public float doubleConsonants = 0.05f;
    public float vowelStreak = 0.1f;
    public float doubleVowels = 0.05f;
    public bool usesSuffixes = false;
    public bool suffixVowelStart = true;
    public string suffix = "";
    public Vector2I nameLengths = new(3, 12);

    public char pickVowel() { return pick(vowels, totalVowelWeights); }
    public char pickConsonant() { return pick(consonants, totalConsonantWeights); }

    private char pick(Dictionary<char, float> list, float totalWeight)
    {
        float pick = totalWeight * GD.Randf();
        float cumulatedWeight = 0.0f;

        foreach(KeyValuePair<char,float> pair in list)
        {
            cumulatedWeight += pair.Value;
            if(cumulatedWeight >= pick)
                return pair.Key;
        }

        GD.PrintErr("Reached end of LangageRules pick method");
        return '-'; // should never happen
    }
}

public enum MayBool{Yes, No, Any};
