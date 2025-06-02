using Godot;
using System;

/// <summary>
/// FreeMovementManager is responsible for UI Display for the free movement phase, and player feedback
/// </summary>
public partial class FreeMovementManager : Node
{
    [Export]
    private CanvasItem uiContainer;
    [Export]
    private Slider slider;
    [Export]
    private RichTextLabel originLabel;
    [Export]
    private RichTextLabel destinationLabel;

    // Singleton
    public static FreeMovementManager Instance {get; private set;}
    public override void _Ready()
    {
        Instance = this;
        uiContainer.Visible = false;
    }
    private Country originCountry = null;
    private Country destinationCountry = null;
    
    public bool isInteractionOn() { return uiContainer.Visible; }

    public void startMovementInteraction(Country _from, Country _to)
    {
        originCountry = _from;
        destinationCountry = _to;
        // Do fancy UI stuff
        slider.MinValue = 0.0;
        slider.MaxValue = _from.troops - 1;
        slider.Value = 0.0;
        slider.TickCount = _from.troops;
        uiContainer.Visible = true;
        onSliderUpdate(0.0f);

        GameManager.Instance.waitingForMovement = true; // Freezing human interactions
    }

    public void onMovementValidation()
    {
        _executeMove((int)slider.Value);
    }

    public void onMoveAll()
    {
        _executeMove((int)slider.MaxValue);
    }

    public void onMoveNone()
    {
        _executeMove(0);
    }

    private void _executeMove(int _amount)
    {
        if(_amount != 0)
            GameManager.Instance.askMovement(originCountry, destinationCountry, _amount);
        GameManager.Instance.waitingForMovement = false;
        originCountry = null;
        destinationCountry = null;
        uiContainer.Visible = false;
    }

    public void onSliderUpdate(float _value)
    {
        originLabel.Text = originCountry.troops + " -> " + (originCountry.troops - (int)_value);
        destinationLabel.Text = destinationCountry.troops + " -> " + (destinationCountry.troops + (int)_value).ToString();
    }

}
