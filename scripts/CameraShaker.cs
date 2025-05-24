using Godot;
using System;

public partial class CameraShaker : Node3D
{
    public static CameraShaker Instance;

    private float intensity = 0.0f;

    [Export]
    private float verticalMaxAmplitude = Mathf.Pi * 0.3f;
    [Export]
    private float horizontalMaxAmplitude = Mathf.Pi * 0.2f;
    [Export]
    private float maxIntensity = 10.0f;
    [Export]
    private double verticalDuration = 0.07f;
    [Export]
    private double horizontalDuration = 0.1f;

    private LerpHelper verticalLerp;
    private LerpHelper horizontalLerp;

    private const float SHAKE_FALLOFF = 0.95f;
    private const float MINIMAL_INTENSITY = 0.01f;
    private static float INTENSITY_INCREMENT = 0.1f;

    public override void _Ready()
    {
        Instance = this;
        verticalLerp = new(verticalDuration, verticalMaxAmplitude);
        horizontalLerp = new(horizontalDuration, horizontalMaxAmplitude);
    }

    public static void shake()
    {
        if(Instance != null)
            Instance.intensity += INTENSITY_INCREMENT;
    }

    public override void _Process(double _dt)
    {
        if (Input.IsActionJustPressed("Debug"))
            intensity += INTENSITY_INCREMENT;

        if (intensity < MINIMAL_INTENSITY)
            return;

        intensity *= SHAKE_FALLOFF;

        if (intensity < MINIMAL_INTENSITY)
        {
            // resel all trackers
            verticalLerp.reset();
            horizontalLerp.reset();
            Rotation = new();
            return;
        }

        float normalizedIntensity = Mathf.Min(intensity, maxIntensity) / maxIntensity;

        float vertical = verticalLerp.derp(_dt, normalizedIntensity);
        float horizontal = horizontalLerp.derp(_dt, normalizedIntensity);
        Rotation = new(vertical, horizontal, 0.0f);
    }

    class LerpHelper
    {
        public LerpHelper(double _duration, float _amplitude)
        {
            duration = _duration;
            amplitude = _amplitude;
        }

        public float from = 0.0f; // Angle
        public float to = 0.0f; // Angle
        public double dtAccumulator = 0.0f;
        bool lerping = false;

        private float amplitude;
        private double duration;

        public float derp(double _dt, float _intensity) // derp for doLerp ehe
        {
            if (lerping == false || dtAccumulator >= duration)
            {
                from = to;
                dtAccumulator = 0.0f;
                float frameMax = _intensity * amplitude;
                to = (to < 0.0f ? 1.0f : -1.0f) * frameMax;
                if (lerping == false)
                    to *= GD.Randf() > 0.5f ? 1.0f : -1.0f; // Randomize first movement
                lerping = true;
            }
            dtAccumulator += _dt;
            return Mathf.Lerp(from, to, (float)(dtAccumulator / duration));
        }

        public void reset()
        {
            lerping = false;
            to = 0.0f;
        }

        public override string ToString()
        {
            return dtAccumulator + ": " + from + "->" + to;
        }
    }
}
