shader_type sky;

uniform sampler2D skyPanorama : filter_linear, source_color, hint_default_black; // will have to create myself
// would love some cluster of stars milhyway style

void sky()
{
	// make nice stars with custom made texture noise
	COLOR = texture(skyPanorama, SKY_COORDS).xyz;
}
