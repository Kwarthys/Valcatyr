using Godot;
using System;
using System.Collections.Generic;

public partial class WidgetsManager : Control
{
    [Export] private Godot.Collections.Array<string> labels;
    [Export] private Godot.Collections.Array<Control> widgets;

    private Dictionary<string, int> widgetIndexPerLabel = new();

    public static WidgetsManager Instance;

    public override void _Ready()
    {
        Instance = this;
        for(int i = 0; i < widgets.Count; ++i)
        {
            widgets[i].Hide();
            widgetIndexPerLabel.Add(labels[i], i);
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
    }

    private void doShow(string _label)
    {
        widgets[widgetIndexPerLabel[_label]].Show();
    }

}
