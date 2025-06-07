using Godot;
using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

public partial class OptionSlider : Node
{
    [Export]
    private HSlider slider;
    [Export]
    private SpinBox text;
    [Export]
    private int valueMax = 200;
    [Export]
    private int valueMin = 0;

    public float value { get; private set; } = 0.0f;

    public override void _Ready()
    {
        // Subscribe to LineEdit signal TextSubmitted hidden in the SpinBox
        text.GetLineEdit().TextSubmitted += (_) =>
        {
            // Force lose focus on Enter pressed
            text.GetLineEdit().FocusMode = Control.FocusModeEnum.None;
            text.GetLineEdit().FocusMode = Control.FocusModeEnum.All;
        };
    }

    public void onSliderChange(float _newValue)
    {
        text.SetValueNoSignal(_newValue);
        value = _newValue;
    }

    public void onTextChange(float _newValue)
    {
        slider.SetValueNoSignal(_newValue);
        value = _newValue;
    }
}
