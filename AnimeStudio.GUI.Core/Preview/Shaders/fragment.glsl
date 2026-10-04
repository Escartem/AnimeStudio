in vec3 normal;

out vec4 outputColor;

void main()
{
	vec3 unitNormal = normalize(normal);
	float nDotProduct = clamp(dot(unitNormal, vec3(0.707, 0, 0.707)), 0, 1);
	vec2 ContributionWeightsSqrt = vec2(0.5, 0.5f) + vec2(0.5f, -0.5f) * unitNormal.y;
	vec2 ContributionWeights = ContributionWeightsSqrt * ContributionWeightsSqrt;

	vec3 color = nDotProduct * vec3(1, 0.957, 0.839) / 3.14159;
	color += vec3(0.779, 0.716, 0.453) * ContributionWeights.y;
	color += vec3(0.368, 0.477, 0.735) * ContributionWeights.x;
	outputColor = vec4(sqrt(color), 1);
}
