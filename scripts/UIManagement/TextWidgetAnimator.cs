using Godot;
using System;

public partial class TextWidgetAnimator : RichTextLabel
{
    [Export] private float timePerPoint;

    [Export] private int maxPoints = 4;

    private string baseText = "";

    private int points = 0;
    private double dtAccumulator = 0.0f;

    public override void _Ready()
    {
        baseText = Text;
    }


    public override void _Process(double _dt)
    {
        if (IsVisibleInTree() == false)
            return;

        dtAccumulator += _dt;

        if (dtAccumulator > timePerPoint)
        {
            dtAccumulator -= timePerPoint;
            points = ++points % (maxPoints + 1);
            updateText();
        }
    }
    
    private void updateText()
    {
        Text = baseText + " ";
        for (int i = 0; i < points; ++i)
            Text += ".";
    }
}
