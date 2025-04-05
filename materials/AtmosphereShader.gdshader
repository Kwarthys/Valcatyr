shader_type spatial;
render_mode unshaded;

const vec2 sphereCenter = vec2(0.5, 0.5);

const float atmFadeOut = 0.38;		// Both will have to change with zoom level (when it happens)
const float atmFadeStart = 0.35;	// They will be made uniforms and set from script
// (or just set uniform zoom and compute ?)

float inlerp(float a, float b, float v)
{
	if(abs(a - b) < 0.001) return 0.0;
	return (v-a) / (b-a);
}

void fragment()
{
	ALBEDO = vec3(0.01, 0.05, 1.0);
	float xDist = (sphereCenter.x - UV.x) * (sphereCenter.x - UV.x); // squaredDistances
	float yDist = (sphereCenter.x - UV.y) * (sphereCenter.x - UV.y);
	float sqrDist = xDist + yDist;
	
	ALPHA = clamp(inlerp(atmFadeOut, atmFadeStart, sqrt(sqrDist)), 0.0, 1.0);
}
