using Godot;
using System;

public partial class AudioVolumeManager : Node
{
    [Export]
    private OptionSlider fxVolumeOption;

    private int fxBusIndex = -1;

    public override void _Ready()
    {
        fxVolumeOption.choiceChanged += fxVolumeChange;
        fxBusIndex = AudioServer.GetBusIndex("VFX");
    }

    private void fxVolumeChange(float _newValue)
    {
        AudioServer.SetBusVolumeDb(fxBusIndex, Mathf.LinearToDb(_newValue * 0.01f));
    }

}
