using Godot;
using System;
using System.Collections.Generic;
using System.Timers;

public partial class CombatLog : RichTextLabel
{
    public static CombatLog Instance;

    [Export] private double timeOnScreen = 7.0;

    private List<double> timers = new();
    private List<string> texts = new();

    public override void _Ready()
    {
        Instance = this;
    }

    public static void print(string _txt)
    {
        Instance?.addPrint(_txt);
    }

    private void addPrint(string _txt)
    {
        timers.Add(timeOnScreen);
        texts.Add(_txt);
    }

    public override void _Process(double _dt)
    {
        if(timers.Count == 0)
            return;

        for(int i = 0; i < timers.Count; ++i)
            timers[i] -= _dt;

        string text = "";

        for(int i = timers.Count - 1; i >= 0; --i)
        {
            if(timers[i] > 0.0)
            {
                text += texts[i] + "\n";
            }
            else
            {
                texts.RemoveAt(i);
                timers.RemoveAt(i);
            }
        }

        Text = text;
    }
}
