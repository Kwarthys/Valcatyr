using Godot;
using System;
using System.ComponentModel.DataAnnotations;

public partial class ArmySlider : Control
{
    [Export] private RichTextLabel subtitle;
    [Export] private Slider slider;
    public static ArmySlider Instance;
    private int maxArmy = 3;

    private Vector2 restPos = new(10000.0f, 10000.0f);
    private Vector2 activePos;

    public override void _Ready()
    {
        Instance = this;
        activePos = Position;
        hide();
    }

    public static void hide() { Instance?.updateShow(false); }
    public static void show() { Instance?.updateShow(true); }

    private void updateShow(bool _status)
    {
        Visible = _status;
        Position = _status ? activePos : restPos;

        if(_status)
            onValueChanged((float)slider.Value);
    }

    public static void setMaxArmy(int _max, bool setValueToMax = false)
    {
        Instance?.doSetMaxArmy(_max, setValueToMax);
    }

    private void doSetMaxArmy(int _max, bool setValueToMax)
    {
        maxArmy = _max;

        if(setValueToMax)
            slider.Value = _max;

        onValueChanged((float)slider.Value);
    }

    public static double getValue()
    {
        if(Instance == null || Instance.IsVisibleInTree() == false)
            return -1.0;
        return Instance.slider.Value;
    }

    public void onValueChanged(float newValue)
    {
        if(IsVisibleInTree() == false)
            return;
        if(maxArmy == 0)
            slider.Value = 0.0;
        else
            slider.Value = Mathf.Clamp(newValue, 1.0, maxArmy);

        subtitle.Text = "Attack with " + Mathf.RoundToInt(slider.Value) + " troops";
    }
}
