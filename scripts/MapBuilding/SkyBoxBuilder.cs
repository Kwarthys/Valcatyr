using Godot;
using System;
using System.Linq;
using System.Security.AccessControl;

public partial class SkyBoxBuilder : Node
{
    [Export]
    private ShaderMaterial shader;

    private const int HEIGHT = 2000;
    private const int WIDTH = 3000;
    private const int HEIGHT_MASK = HEIGHT / 10; // Inhibit too high and too low stars

    private const float GALAXY_WIDTH = 0.5f;
    private const float GALAXY_HEIGHT = 0.2f;

    [Export]
    private Gradient starColors;
    [Export]
    private Gradient cloudColors;
    [Export]
    private bool debugLightGeneration;

    public override void _Ready()
    {
        base._Ready();

        Image img = Image.CreateEmpty(WIDTH, HEIGHT, false, Image.Format.Rgb8);
        img.Fill(Colors.Black);

        if(debugLightGeneration)
        {
            _starPass(ref img, 1.0f);
        }
        else
        {
            // Generating clouds takes 10 times more time than stars, due to many perlin nosie sampling and not ignoring top and bottom part, where many points overlap
            float usecStart = Time.GetTicksUsec();
            // making use of Alpha by layering clouds and stars
            _starPass(ref img, 0.4f);
            _cloudPass(ref img, 0.5f);
            _starPass(ref img, 0.3f);
            _cloudPass(ref img, 1.3f);
            _cloudPass(ref img, 2.8f);
            _starPass(ref img, 0.1f);
            GD.Print("Creating Skybox image took " + ((Time.GetTicksUsec() - usecStart) * 0.000001) + " secs.");
        }


        Texture2D tex= ImageTexture.CreateFromImage(img);
        shader.SetShaderParameter("skyPanorama", tex);
    }

    private void _editPixel(int _x, int _y, Color _c, ref Image _img)
    {
        Color from = _img.GetPixel(_x,_y);
        Color finalColor = from.Lerp(_c, _c.A);
        _img.SetPixel(_x,_y,finalColor);
    }

    [Export]
    private Curve cloudsNoiseToAlpha;

    private void _cloudPass(ref Image _img, float _offset = 1.0f)
    {
        Color cloudColor = cloudColors.Sample(GD.Randf());
        Vector3 sampleOffset = new(_offset, _offset, _offset);

        for(int y = 0; y < HEIGHT; ++y)
        {
            for(int x = 0; x < WIDTH; ++x)
            {
                Vector2 uvs = new(x * 1.0f / WIDTH, y * 1.0f / HEIGHT);
                Vector3 worldPos = _uvToXYZ(uvs, 1.0f);
                float noiseValue = _sampleNoise(worldPos + sampleOffset);

                if(noiseValue < Mathf.Epsilon)
                    continue;

                float alpha = cloudsNoiseToAlpha.Sample(noiseValue);
                if(alpha > 0.99f)
                    _img.SetPixel(x,y,cloudColor);
                else
                    _editPixel(x,y, new(cloudColor, alpha), ref _img);
            }
        }
    }

    private void _starPass(ref Image _img, float _procCoef = 1.0f)
    {
        for(int y = 0; y < HEIGHT; ++y)
        {
            for(int x = 0; x < WIDTH; ++x)
            {
                float normalizedDistToStart = (x < WIDTH * 0.5f ? x : WIDTH - x) / (WIDTH * 0.5f); // Dist to x = 0%
                float normalizedDistToEquator = Mathf.Abs((y - (HEIGHT*0.5f)) / (HEIGHT*0.5f)); // Dist to y = 50%
                float bias;

                if(normalizedDistToEquator < GALAXY_HEIGHT)
                {
                    // milky way like density
                    bias = Mathf.Lerp(0.005f * _procCoef, 0.0005f * _procCoef, Mathf.Clamp(Mathf.Max(normalizedDistToEquator / GALAXY_HEIGHT, normalizedDistToStart / GALAXY_WIDTH), 0.0f, 1.0f));
                }
                else
                {
                    // random low distribution
                    bias = Mathf.Lerp(0.0005f * _procCoef, 0.00001f * _procCoef, normalizedDistToEquator - GALAXY_HEIGHT);
                }

                if(y > HEIGHT_MASK && y < HEIGHT - HEIGHT_MASK && GD.Randf() < bias)
                {
                    Color starColor = starColors.Sample(GD.Randf());
                    float sizeRand = GD.Randf();

                    if(sizeRand < 0.05f)
                    {
                        _drawHugeStar(x,y,starColor, ref _img);
                    }
                    else if(sizeRand < 0.2f)
                    {
                        _drawBigStar(x,y,starColor, ref _img);
                    }
                    else
                    {
                        // Smol star: single pixel
                        _img.SetPixel(x,y,starColor);
                    }
                }
            }
        }
    }

    private void _drawBigStar(int _x, int _y, Color _color, ref Image _img)
    {
        // Big Star
        //   x
        // x x x
        //   x
        _img.SetPixel(_x,_y,_color);
        _img.SetPixel(_capX(_x+1),_y,_color);
        _img.SetPixel(_x,_y+1,_color);
        _img.SetPixel(_capX(_x-1),_y,_color);
        _img.SetPixel(_x,_y-1,_color);
    }

    private void _drawHugeStar(int _x, int _y, Color _color, ref Image _img)
    {
        //                x
        //                x
        //              x x x
        // HUGE star: x x x x x   <-- Stars are WIDTH Streched on the skybox, thus star has reduced x span
        //              x x x
        //                x
        //                x
        for(int yOffset = -3; yOffset < 4; ++yOffset)
        {
            for(int xOffset = -2; xOffset < 3; ++xOffset)
            {
                if(xOffset == 0 || yOffset == 0)
                    _img.SetPixel(_capX(_x+xOffset), _y+yOffset, _color);
            }
        }
        // the 4 corners of the inner rectangle
        _img.SetPixel(_capX(_x+1),_y+1,_color);
        _img.SetPixel(_capX(_x+1),_y-1,_color);
        _img.SetPixel(_capX(_x-1),_y+1,_color);
        _img.SetPixel(_capX(_x-1),_y-1,_color);
    }

    private int _capX(int x)
    {
        if(x < 0)
            return WIDTH + x;
        else if (x >= WIDTH)
            return x - WIDTH;
        else
            return x;
    }

    private Vector3 seed_offset = new(GD.Randf(), GD.Randf(), GD.Randf());

    private float _sampleNoise(Vector3 _pos)
    {
        return Perlin.Fbm(_pos + seed_offset, 5);
    }

    private Vector3 _uvToXYZ(Vector2 _uvs, float _distance)
    {
        return _sphericalToCartesian(_uvToSphericalCoordinates(_uvs, _distance));
    }

    private Vector3 _uvToSphericalCoordinates(Vector2 _uvs, float _distance)
    {
        // spherical : R, TETA, PHI
        // -> distance, angle to polar axis, rotation around polar axis
        float distance = _distance;
        float teta = Mathf.Lerp(Mathf.Pi, 0.0f, _uvs.Y);
        float phi = Mathf.Lerp(0.0f, Mathf.Tau, _uvs.X);
        return new(distance, _epsCheck(teta), _epsCheck(phi));
    }

    private Vector3 _sphericalToCartesian(Vector3 _spherical)
    {
        float r = _spherical.X;
        float teta = _spherical.Y;
        float phi = _spherical.Z;
        // Twisting standard to work with Godot Y UP
        float x = r * Mathf.Sin(teta) * Mathf.Sin(phi);
        float y = r * Mathf.Cos(teta);
        float z = r * Mathf.Sin(teta) * Mathf.Cos(phi);

        return new(_epsCheck(x), _epsCheck(y), _epsCheck(z));
    }

    private float _epsCheck(float _v) { return Mathf.Abs(_v) > Mathf.Epsilon ? _v : 0.0f; }

}
