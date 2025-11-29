using Godot;
using System;
using System.Collections.Generic;

public partial class WidgetsManager : Control
{
    [Export] private Godot.Collections.Array<string> labels;
    [Export] private Godot.Collections.Array<Control> widgets;
    [Export] private float aiWidgetDelayBeforeShow;

    private Dictionary<string, int> widgetIndexPerLabel = new();

    public static WidgetsManager Instance;

    private Timer aiWidgetTimer = new();

    public override void _Ready()
    {
        Instance = this;
        for(int i = 0; i < widgets.Count; ++i)
        {
            widgets[i].Hide();
            widgetIndexPerLabel.Add(labels[i], i);
        }
    }

    public override void _Process(double _dt)
    {
        if(aiWidgetTimer.counting == false)
            return;
        if(aiWidgetTimer.update(_dt))
        {
            // Timer has finished !
            widgets[widgetIndexPerLabel["AIThinking"]].Show();
        }
    }

    public static void hide(string _label)
    {
        Instance?.doHide(_label);
    }

    public static void show(string _label)
    {
        Instance?.doShow(_label);
    }

    private void doHide(string _label)
    {
        widgets[widgetIndexPerLabel[_label]].Hide();
        aiWidgetTimer.interrupt();
    }

    private void doShow(string _label)
    {
        if(_label == "AIThinking")
        {
            aiWidgetTimer.start(aiWidgetDelayBeforeShow);
        }
        else
            widgets[widgetIndexPerLabel[_label]].Show();
    }

}

public class Timer
{
    public bool counting {get; private set;} = false;
    private double timeAccumulator = 0.0;
    private double targetTime = 1.0;

    public void start(double _targetTime)
    {
        targetTime = _targetTime;
        timeAccumulator = 0.0;
        counting = true;
    }

    public bool update(double _dt)
    {
        timeAccumulator += _dt;

        if(timeAccumulator > targetTime)
        {
            counting = false;
            return true;
        }
        return false;
    }

    public void interrupt()
    {
        counting = false;
    }

}
