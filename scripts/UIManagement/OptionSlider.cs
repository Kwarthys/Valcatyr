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

    [Signal]
    public delegate void choiceChangedEventHandler(float _newValue);

    public float value { get; private set; } = 100.0f;

    private void _setValue(float _newValue)
    {
        value = _newValue;
        EmitSignal(SignalName.choiceChanged, _newValue);
    }

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
        _setValue(_newValue);
    }

    public void onTextChange(float _newValue)
    {
        slider.SetValueNoSignal(_newValue);
        _setValue(_newValue);
    }

    public float getFactor() { return value * 0.01f; }
}
