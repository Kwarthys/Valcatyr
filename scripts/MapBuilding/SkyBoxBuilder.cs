using Godot;
using System;
using System.Linq;

public partial class SkyBoxBuilder : Node
{
    [Export]
    private ShaderMaterial shader;

    private const int HEIGHT = 1000;
    private const int WIDTH = 1000;
    private const int HEIGHT_MASK = HEIGHT / 10; // Inhibit too high and too low stars

    private const float GALAXY_WIDTH = 0.6f;
    private const float GALAXY_HEIGHT = 0.2f;

    public override void _Ready()
    {
        base._Ready();

        Image img = Image.CreateEmpty(WIDTH, HEIGHT, false, Image.Format.Rgb8);
        img.Fill(Colors.Black);

        for(int y = 0; y < HEIGHT; ++y)
        {
            for(int x = 0; x < WIDTH; ++x)
            {
                float normalizedDistToStart = Mathf.Abs((x - (WIDTH*0.5f)) / (WIDTH*0.5f));
                float normalizedDistToEquator = Mathf.Abs((y - (HEIGHT*0.5f)) / (HEIGHT*0.5f));
                float bias;
                if(normalizedDistToEquator < GALAXY_HEIGHT) // milky way like densty
                    bias = Mathf.Lerp(0.025f, 0.0025f, Mathf.Clamp(Mathf.Max(normalizedDistToEquator / GALAXY_HEIGHT, normalizedDistToStart / GALAXY_WIDTH), 0.0f, 1.0f));
                else // random low distribution
                    bias = Mathf.Lerp(0.0025f, 0.00005f, normalizedDistToEquator - GALAXY_HEIGHT);
                Color c = Colors.Black;
                if(y > HEIGHT_MASK && y < HEIGHT - HEIGHT_MASK && GD.Randf() < bias)
                    img.SetPixel(x,y,Colors.White);
            }
        }

        Texture2D tex= ImageTexture.CreateFromImage(img);
        shader.SetShaderParameter("skyPanorama", tex);
    }

}
