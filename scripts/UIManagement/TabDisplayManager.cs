using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Reflection.Metadata.Ecma335;

public partial class TabDisplayManager : TabBar
{
    public static TabDisplayManager Instance;
    [Export] private Godot.Collections.Array<Control> uiHolders;
    [Export] private Curve movementCurve;
    [Export] private float foldedXValue = 500.0f;
    private Dictionary<Control, float> displayedXValuePerControl = new();

    [Export]
    private float duration = 0.5f;

    private int shownIndex = -1;
    private int movingIndex = -1;

    private LerpHelper<float> lerper;

    public override void _Ready()
    {
        Instance = this;

        foreach (Control holder in uiHolders)
        {
            displayedXValuePerControl.Add(holder, holder.Position.X);
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

    public static void closeAllTabs()
    {
        if(Instance == null) return;

        Instance.CurrentTab = -1;
        Instance.manageTabChange();
    }

    public enum Tab { Help, Options, Game};

    public static void selectTab(Tab _tab)
    {
        if(Instance == null) return;
        int tabIndex = -1;
        switch(_tab)
        {
            case Tab.Help: tabIndex = 0; break;
            case Tab.Options: tabIndex = 1; break;
            case Tab.Game: tabIndex = 2; break;
        }
        Instance.CurrentTab = tabIndex;
        Instance.manageTabChange();
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
        float to = CurrentTab == index ? displayedXValuePerControl[uiHolders[index]] : foldedXValue;
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
