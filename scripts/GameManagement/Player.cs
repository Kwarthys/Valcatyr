using Godot;
using System;
using System.Collections.Generic;

public class Player
{
    public Player(int _id){ id = _id; }
    public bool isHuman = false;
    public int id = -1;
    public List<Country> countries = new();

    public static Color[] playerColors = // colorblindness friendly-ish color palette from Bang Wong 2011 "Points of view: Color blindness" https://www.nature.com/articles/nmeth.1618
    { 
        new(0.90f, 0.63f, 0.0f), // Gold
        new(0.33f, 0.70f, 0.91f), // Cyan-Blue
        new(0.0f, 0.62f, 0.45f), // Green-ish
        new(0.0f, 0.0f, 0.0f), // Black
        new(0.94f, 0.89f, 0.26f), // Yellow
        new(0.80f, 0.47f, 0.66f), // Pink-ish
    };
}