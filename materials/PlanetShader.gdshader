shader_type spatial;
//render_mode unshaded; // Cool for debug

uniform sampler2D customTexture : filter_nearest, source_color;
uniform sampler3D waterNoise;// : repeat_enable;

uniform vec4 shallowWaterColor : source_color;
uniform vec4 deepWaterColor : source_color;
uniform vec4 waveWaterColor : source_color;

uniform float STRIPES_SCROLL_SPEED = 50.0;

varying flat vec3 passedColor;
varying vec3 vertexWorldPos;

void vertex()
{
	ivec2 texSize = textureSize(customTexture, 0);

	int x = VERTEX_ID % texSize.x;
	float percentX = (float(x)+0.5) / float(texSize.x);

	int y = VERTEX_ID / texSize.x;
	float percentY = (float(y) + 0.5) / float(texSize.y);

	vec2 samplePos = vec2(percentX, percentY);
	vec4 color = texture(customTexture, samplePos);
	passedColor = color.xyz;

	vertexWorldPos = VERTEX;
}

float triangle(float v)
{
	return 2.0 * abs(2.0 * (v - floor(v+0.5))) - 1.0;
}

float inlerp(float a, float b, float v)
{
	if(abs(a - b) < 0.001) return 0.0;
	return (v-a) / (b-a);
}

void fragment()
{
	if(UV.x > 0.0)
	{
		ALBEDO = passedColor;
		ROUGHNESS = 0.5;
		
		// add layer for state selection ,via UV.y
		// UV 0: nothing, 1: Selected(white), 2: Friend(Green), -1: Enemy(Red)
		if(UV.y > 0.5 || UV.y < -0.5)
		{
			float stripes = FRAGCOORD.x - FRAGCOORD.y;
			vec3 highlightColor = vec3(1.0, 1.0, 1.0); // Selected
			if(UV.y > 1.5)
				highlightColor = vec3(0.0, 1.0, 0.0); // Friend
			else if(UV.y < - 0.5)
			{
				highlightColor = vec3(1.0, 0.0, 0.0); // Enemy
				stripes = FRAGCOORD.x + FRAGCOORD.y;  // Reversed stripes
			}
			float interval = VIEWPORT_SIZE.y * 0.05;
			stripes += mod(TIME * STRIPES_SCROLL_SPEED, interval);
			float fillRatio = 0.6;
			float selectedMixAmount = step(mod(stripes, interval) / (interval - 1.0), fillRatio);
			float timeFactor = (0.5 * cos(TIME * PI)) + 0.5; // cos from -1 1 to 0 1
			ALBEDO = mix(ALBEDO, highlightColor, selectedMixAmount * timeFactor * 0.8 + 0.1);
			//ALBEDO = highlightColor; // useful for debug
		}
	}
	else
	{
		float x = 14.0;						// secs
		float t = mod(TIME, x) / x; 		// 0 -> 1 in x secs
		t *= 1.2;							// 0 -> 1.2 in x secs
		t = 1.0 - t;						// 1.0 -> -0.2 in x secs
		float t2 = (t+0.2) > 0.6 ? t - 0.6 : t + 0.6;
		float waveWidth = 0.05;
		float scaledDepth = clamp(-UV.x * 150.0, 0.0, 1.0);
		vec3 waterColor = mix(shallowWaterColor, deepWaterColor, scaledDepth).rgb;

		float amountMixed = 0.0;
		float minWaveDepth = 0.5;
		if(scaledDepth < minWaveDepth)
		{
			float sampleDepth = scaledDepth / minWaveDepth;
			if(abs(sampleDepth - t) < waveWidth)
			{
				amountMixed = inlerp(sampleDepth - waveWidth, sampleDepth + waveWidth, t);
				amountMixed *= 1.0 - t*t;
			}
			else if(abs(sampleDepth - t2) < waveWidth)
			{
				amountMixed = inlerp(sampleDepth - waveWidth, sampleDepth + waveWidth, t2);
				amountMixed *= 1.0 - t2*t2;
			}
		}

		ALBEDO = mix(waterColor, waveWaterColor.rgb, amountMixed);
		vec3 samplePos = vertexWorldPos * (1.0 + 0.5 * triangle(TIME * 0.05));
		vec3 noise = texture(waterNoise, samplePos).rgb;
		vec3 waveNormalMap = vec3(amountMixed);
		NORMAL_MAP = mix(NORMAL_MAP, noise * 0.5 + waveNormalMap, 0.05);

		ROUGHNESS = 0.3;
	}
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
