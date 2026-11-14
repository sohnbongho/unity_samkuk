// 로우폴리 3D 모델 (캐릭터·적·소품). 화면 층 ModelMirror 가 모델 종류마다 MultiMesh 한 묶음으로 그린다(인스턴싱).
// 색은 꼭짓점 색 x 인스턴스 색. 걷기는 뼈대 없이 꼭짓점 셰이더로: UV.x = 흔들 방향(다리·팔 ±1, 몸 0), UV.y = 흔들 축의 높이.
// INSTANCE_CUSTOM: x = 흔들 각(라디안, 이미 sin 을 곱한 값), y = 몸 들썩임(유닛), z = 불투명도(겹친 소품 반투명은 점묘로), w = 흰 번쩍임.
// 빛은 도트 그림과 같은 값(환경광 + 햇빛 x 그림자 + 점광원)에 면 방향만 부드럽게 얹는다(반 램버트): 해를 등진 면도 검게 죽지 않는다.
shader_type spatial;
render_mode cull_back, specular_disabled;

varying float v_alpha;
varying float v_flash;

void vertex() {
	float swing = UV.x * INSTANCE_CUSTOM.x;
	if (abs(UV.x) > 0.001) {
		float c = cos(swing), s = sin(swing);
		vec3 p = VERTEX - vec3(0.0, UV.y, 0.0);
		VERTEX = vec3(p.x, p.y * c - p.z * s, p.y * s + p.z * c) + vec3(0.0, UV.y, 0.0);
		NORMAL = vec3(NORMAL.x, NORMAL.y * c - NORMAL.z * s, NORMAL.y * s + NORMAL.z * c);
	}
	VERTEX.y += INSTANCE_CUSTOM.y;
	v_alpha = INSTANCE_CUSTOM.z;
	v_flash = INSTANCE_CUSTOM.w;
}

void fragment() {
	// 반투명은 점묘(화면 격자)로: 깊이·그림자·인스턴싱을 그대로 쓸 수 있다
	if (v_alpha < 0.99) {
		vec2 cell = floor(FRAGCOORD.xy);
		float threshold = fract(dot(cell, vec2(0.5, 0.25)) + 0.125);   // 2x4 격자
		if (v_alpha < threshold) discard;
	}
	ALBEDO = mix(COLOR.rgb, vec3(1.0), clamp(v_flash, 0.0, 1.0));
	ROUGHNESS = 1.0;
}

void light() {
	float wrap = dot(NORMAL, LIGHT) * 0.5 + 0.5;
	DIFFUSE_LIGHT += ATTENUATION * LIGHT_COLOR / PI * mix(0.62, 1.12, wrap);
}
