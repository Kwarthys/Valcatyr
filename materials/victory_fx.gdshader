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
		VELOCITY.y -= 0.05;
		VELOCITY *= 0.85;
		
		// Add parasitic X Z movements to mimic air resistance
		
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
