using Godot;
using System;
using System.ComponentModel.DataAnnotations;

public partial class ArmySlider : VSlider
{
    [Export] private RichTextLabel subtitle;
    public static ArmySlider Instance;
    private int maxArmy = 3;

    public override void _Ready()
    {
        Instance = this;
        hide();
    }

    public static void show()
    {
        Instance?.doShow();
    }

    private void doShow()
    {
        Show();
        subtitle.Show();
        onValueChanged((float)Value);
    }

    public static void setMaxArmy(int _max, bool setValueToMax = false)
    {
        Instance?.doSetMaxArmy(_max, setValueToMax);
    }

    private void doSetMaxArmy(int _max, bool setValueToMax)
    {
        maxArmy = _max;

        if(setValueToMax)
            Value = _max;

        onValueChanged((float)Value);
    }

    public static void hide()
    {
        Instance?.Hide();
        Instance?.subtitle.Hide();
    }

    public static double getValue()
    {
        if(Instance == null || Instance.IsVisibleInTree() == false)
            return -1.0;
        return Instance.Value;
    }

    public void onValueChanged(float newValue)
    {
        if(IsVisibleInTree() == false)
            return;
        if(maxArmy == 0)
            Value = 0.0;
        else
            Value = Mathf.Clamp(newValue, 1.0, maxArmy);

        subtitle.Text = "Attack with " + Mathf.RoundToInt(Value) + " troops";
    }
}
