using Godot;
using System;

public partial class TweakableParametersManager : Node
{
    [Export]
    private OptionSlider fxVolumeOption;
    [Export]
    private OptionSlider cameraShakeOption;
    [Export]
    private OptionSlider planelRotationOption;

    private static TweakableParametersManager Instance;

    public override void _Ready() { Instance = this; }

    public static float getPlanetRotationFactor()
    {
        return Instance.planelRotationOption.getFactor();
    }

    public static float getFxVolumeFactor()
    {
        return Instance.fxVolumeOption.getFactor();
    }

    public static float getCameraShakeFactor()
    {
        return Instance.cameraShakeOption.getFactor();
    }
}
