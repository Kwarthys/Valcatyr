shader_type particles;
uniform vec4 colorA : source_color;
uniform vec4 colorB : source_color;
uniform vec4 colorC : source_color;

mat3 getXRotMatrix(float angle)
{
	return mat3(vec3(1.0, 0.0, 0.0),
				vec3(0.0, cos(angle), -sin(angle)),
				vec3(0.0, sin(angle), cos(angle)));
}

mat3 getYRotMatrix(float angle)
{
	return mat3(vec3(cos(angle), 0.0, sin(angle)),
				vec3(0.0, 1.0, 0.0),
				vec3(-sin(angle), 0.0, cos(angle)));
}

mat3 getZRotMatrix(float angle)
{
	return mat3(vec3(cos(angle), -sin(angle), 0.0),
				vec3(sin(angle), cos(angle), 0.0),
				vec3(0.0, 0.0, 1.0));
}

float lerp(float v, float a, float b)
{
	return (1.0-v) * a + v * b;
}

void start()
{
	if(NUMBER % 3u == 0u)
		COLOR = colorA;
	else if(NUMBER % 3u == 1u)
		COLOR = colorB;
	else
		COLOR = colorC;
	
	vec3 speeds = vec3(	sin(float(NUMBER * 210u)),
						1.0,
						-sin(float(NUMBER * 1520u)));

	speeds = normalize(speeds);
	
	VELOCITY.x = sin(float(NUMBER * 1789u)) * speeds.x * 5.0;
	VELOCITY.y = (sin(float(NUMBER)) * 0.5 + 0.5) * speeds.y * 3.0 + 5.0;
	VELOCITY.z = -sin(float(NUMBER * 666u)) * speeds.z * 5.0;
	
}

void process()
{	
	if(COLLIDED && VELOCITY.y < 0.01)
	{
		VELOCITY = vec3(0.0, 0.0, 0.0);
	}
	else
	{
		vec3 planetDir = -TRANSFORM[3].xyz; // Planet dir is minus position, minus 2 in y
		planetDir.y -= 2.0;
		float distSquared = planetDir.x * planetDir.x + planetDir.y * planetDir.y + planetDir.z * planetDir.z;
		float dist = sqrt(distSquared);
		if(distSquared > 0.1)
			planetDir = planetDir / dist; // normalize planet direciton
		else
			distSquared = 0.1;
		
		float gravityMassConstant = 0.08;
		VELOCITY += planetDir * gravityMassConstant / distSquared;
		VELOCITY *= 0.85;
		
		// Random rotations
		mat3 rot = mat3(TRANSFORM[0].xyz, TRANSFORM[1].xyz, TRANSFORM[2].xyz);
		rot = rot * getXRotMatrix(0.5 * sin(float(NUMBER)));
		rot = rot * getYRotMatrix(0.5 * cos(2.0 + float(NUMBER)));
		rot = rot * getZRotMatrix(0.5 * cos(2.0 - float(NUMBER)));
		TRANSFORM[0].xyz = rot[0].xyz;
		TRANSFORM[1].xyz = rot[1].xyz;
		TRANSFORM[2].xyz = rot[2].xyz;
	}
}
