using Godot;
using System;

public class LerpHelper<T>
{
    public bool moving = false;
    public T target;
    public T origin;
    public double dtAccumulator;
    public double duration;

    Func<T, T, double, T> interpolateMethod;

    public LerpHelper(double _duration, Func<T, T, double, T> _interpolate) { duration = _duration; interpolateMethod = _interpolate; }

    public T lerp(double _dt)
    {
        if (moving == false || duration < Mathf.Epsilon)
            return target;

        dtAccumulator += _dt;
        double time = dtAccumulator / duration;
        if (time >= 1.0f)
        {
            moving = false;
            return target;
        }
        return interpolateMethod(origin, target, time);
    }

    public void startLerp(T from, T to)
    {
        target = to;
        origin = from;
        dtAccumulator = 0.0f;
        moving = true;
    }
}
