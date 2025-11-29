shader_type sky;

uniform sampler2D skyPanorama : filter_linear, source_color, hint_default_black; // will have to create myself
uniform vec3 sunDir = vec3(0,0,-1);
uniform vec4 sunColor: source_color;
// would love some cluster of stars milhyway style

const float sunExt = -0.99;
const float sunIn = -0.993;

float inlerp(float a, float b, float v)
{
	if(abs(a - b) < 0.001) return 0.0;
	return (v-a) / (b-a);
}

void sky()
{
	// make nice stars with custom made texture noise
	vec3 skyColor = texture(skyPanorama, SKY_COORDS).xyz;

	// Display Sun
	float angle = dot(EYEDIR, sunDir);
	float sunMixAmount = 0.0;
	if(angle < sunExt)
		sunMixAmount = clamp(inlerp(sunExt, sunIn, angle), 0.0, 1.0);
	float sunBlindingFactor = 1.0;
	if(angle < 0.0)
		sunBlindingFactor = max(0.0, 0.985 - (-angle));
	COLOR = mix(skyColor * sunBlindingFactor, sunColor.xyz, sunMixAmount*sunMixAmount);
}
