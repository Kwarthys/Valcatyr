using Godot;
using System;
using System.Collections.Generic;

public class Player
{
    public Player(int _id){ id = _id; }
    public bool isHuman = false;
    public int id = -1;
    public List<Country> countries = new();
}