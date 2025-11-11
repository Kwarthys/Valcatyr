using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;

public partial class TabDisplayManager : TabBar
{
    [Export]
    private Godot.Collections.Array<Control> uiHolders;
    [Export]
    private Curve movementCurve;

    private const float foldedXValue = 150;
    private float displayedXValue;

    [Export]
    private float duration = 0.5f;

    private int shownIndex = -1;
    private int movingIndex = -1;

    private LerpHelper<float> lerper;

    public override void _Ready()
    {
        foreach (Control holder in uiHolders)
        {
            displayedXValue = holder.Position.X;
            _setPos(holder, foldedXValue);
        }
        lerper = new(duration, (from, to, time) => Mathf.Lerp(from, to, movementCurve.Sample((float)time)));
        CurrentTab = -1;
    }

    public override void _Process(double _dt)
    {
        if (lerper.moving == false)
            return;

        _setPos(movingIndex, lerper.lerp(_dt));

        if (lerper.moving == false && CurrentTab != movingIndex) // Movement just finished
        {
            // We retracted another tab that the one we want to show
            uiHolders[movingIndex].Visible = false; // Hide retracted
            shownIndex = -1;

            if (CurrentTab != -1) // A tab is commanded to be shown
            {
                toggleTab(CurrentTab); // Start deployment
                shownIndex = CurrentTab;
            }
        }
    }

    public void onTabClic(int clicedIndex)
    {
        manageTabChange();
    }
    
    private void manageTabChange()
    {
        if (CurrentTab == shownIndex)
            return;

        if (shownIndex == -1)
            toggleTab(CurrentTab);
        else
            toggleTab(shownIndex);
    }

    public void toggleTab(int index)
    {
        movingIndex = index;
        float from = uiHolders[index].Position.X;
        float to = CurrentTab == index ? displayedXValue : foldedXValue;
        lerper.startLerp(from, to);
        uiHolders[index].Visible = true;
        shownIndex = CurrentTab;
    }

    private void _setPos(int index, float _x)
    {
        _setPos(uiHolders[index], _x);
    }

    private void _setPos(Control item, float _x)
    {
        Vector2 newPos = item.Position;
        newPos.X = _x;
        item.SetPosition(newPos);
    }
}
