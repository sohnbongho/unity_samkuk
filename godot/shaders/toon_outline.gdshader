// 카툰 외곽선 (뒤집은 껍질). toon.gdshader 와 같은 MultiMesh 를 한 번 더 그린다: 앞면을 지우고 꼭짓점을 바깥으로 밀어 어두운 색으로.
// 걷기·공격 자세·들썩임·점묘 반투명은 toon.gdshader 와 **같은 계산**이어야 선이 몸을 따라간다(한쪽을 고치면 다른 쪽도).
// 미는 방향은 CUSTOM0.xyz(LowPolyMesh.Build 가 넣는 "같은 자리 꼭짓점들의 평균 법선"): 각진 면의 법선으로 밀면 모서리에서 껍질이 갈라진다.
// 굵기는 화면 기준으로 일정하다(1080p 에서 width_px 픽셀): 세계 단위로 밀면 원근 카메라에서 먼 적의 선이 사라지고 가까운 것만 굵어진다.
shader_type spatial;
render_mode cull_front, unshaded, shadows_disabled;

uniform vec3 line_color : source_color = vec3(0.102, 0.078, 0.094);   // #1a1418 (순검정은 저녁·새벽 조명에서 따로 논다)
uniform float width_px = 2.5;

varying float v_alpha;

void vertex() {
	vec3 n = CUSTOM0.xyz;
	float swing = UV.x * INSTANCE_CUSTOM.x;
	float attack = clamp(-INSTANCE_CUSTOM.w, 0.0, 1.0);
	if (UV2.x > 0.5) swing = mix(swing, -1.95, attack);
	if (abs(UV.x) > 0.001) {
		float c = cos(swing), s = sin(swing);
		vec3 p = VERTEX - vec3(0.0, UV.y, 0.0);
		VERTEX = vec3(p.x, p.y * c - p.z * s, p.y * s + p.z * c) + vec3(0.0, UV.y, 0.0);
		n = vec3(n.x, n.y * c - n.z * s, n.y * s + n.z * c);
	}
	VERTEX.y += INSTANCE_CUSTOM.y;
	v_alpha = INSTANCE_CUSTOM.z;

	vec4 clip = PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0));
	vec2 dir = (PROJECTION_MATRIX * vec4((MODELVIEW_MATRIX * vec4(n, 0.0)).xyz, 0.0)).xy;
	// 화면 비율을 풀어 픽셀 공간에서 방향을 잡는다
	dir *= VIEWPORT_SIZE;
	float len = length(dir);
	if (len > 1e-6) {
		float px = width_px * VIEWPORT_SIZE.y / 1080.0;
		clip.xy += dir / len * px * 2.0 / VIEWPORT_SIZE * clip.w;
	}
	POSITION = clip;
}

void fragment() {
	if (v_alpha < 0.99) {
		vec2 cell = floor(FRAGCOORD.xy);
		float threshold = fract(dot(cell, vec2(0.5, 0.25)) + 0.125);
		if (v_alpha < threshold) discard;
	}
	ALBEDO = line_color;
}
