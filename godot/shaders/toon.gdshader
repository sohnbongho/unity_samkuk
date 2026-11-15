// 카툰 3D 모델 (캐릭터·적·말·무기·소품). 화면 층 ModelMirror 가 모델 종류마다 MultiMesh 한 묶음으로 그린다(인스턴싱).
// 색은 꼭짓점 색 x 인스턴스 색. 걷기는 뼈대 없이 꼭짓점 셰이더로: UV.x = 흔들 방향(다리·팔 ±1, 몸 0), UV.y = 흔들 축의 높이.
// INSTANCE_CUSTOM: x = 흔들 각(라디안, 이미 sin 을 곱한 값), y = 몸 들썩임(유닛), z = 불투명도(겹친 소품 반투명은 점묘로), w = 흰 번쩍임(양수) / 공격 자세(음수: 0 ~ -1).
// 공격 자세: UV2.x = 1 인 꼭짓점(무기 든 팔)을 걷기 흔들림 대신 앞·위로 든다.
// 빛(툰 셰이딩): 햇빛은 **두 단**(밝은 면 / 그늘 면)으로 끊는다. 드리운 그림자도 그늘 단과 같은 값이라 몸 위에서 얼룩지지 않는다.
// 그늘은 검게 죽이지 않고 푸른 보라 쪽으로 기운 어두운 색. 점광원(횃불·플레이어 빛)은 끊지 않고 부드럽게 더한다(끊으면 빛 가장자리에서 깜빡인다).
// 환경광은 엔진 것을 끄고 ambient_color 로 직접 더한다: 시간대 색조를 tint_keep 만큼만(캐릭터는 절반) 입히기 위해서다.
// 외곽선은 같은 MultiMesh 를 toon_outline.gdshader 로 한 번 더 그린다(뒤집은 껍질).
shader_type spatial;
render_mode cull_back, specular_disabled, ambient_light_disabled;

uniform vec3 ambient_color = vec3(0.45);
uniform float tint_keep = 1.0;                       // 빛 색조를 얼마나 따르는가 (1 = 그대로, 0 = 밝기만)
uniform float lit_level = 1.14;                      // 밝은 면의 햇빛 배율
uniform vec3 shade_level = vec3(0.50, 0.52, 0.68);   // 그늘 면의 햇빛 배율 (푸른 보라 쪽)
uniform float band_edge = 0.10;                      // 두 단이 갈리는 자리 (법선·빛의 내적)
uniform float band_soft = 0.035;                     // 경계를 부드럽게 하는 폭

varying float v_alpha;
varying float v_flash;

vec3 soften(vec3 c) {
	float l = dot(c, vec3(0.3, 0.59, 0.11));
	return mix(vec3(l), c, tint_keep);
}

void vertex() {
	float swing = UV.x * INSTANCE_CUSTOM.x;
	float attack = clamp(-INSTANCE_CUSTOM.w, 0.0, 1.0);
	if (UV2.x > 0.5) swing = mix(swing, -1.95, attack);
	if (abs(UV.x) > 0.001) {
		float c = cos(swing), s = sin(swing);
		vec3 p = VERTEX - vec3(0.0, UV.y, 0.0);
		VERTEX = vec3(p.x, p.y * c - p.z * s, p.y * s + p.z * c) + vec3(0.0, UV.y, 0.0);
		NORMAL = vec3(NORMAL.x, NORMAL.y * c - NORMAL.z * s, NORMAL.y * s + NORMAL.z * c);
	}
	VERTEX.y += INSTANCE_CUSTOM.y;
	v_alpha = INSTANCE_CUSTOM.z;
	v_flash = max(INSTANCE_CUSTOM.w, 0.0);
}

void fragment() {
	// 반투명은 점묘(화면 격자)로: 깊이·그림자·인스턴싱을 그대로 쓸 수 있다
	if (v_alpha < 0.99) {
		vec2 cell = floor(FRAGCOORD.xy);
		float threshold = fract(dot(cell, vec2(0.5, 0.25)) + 0.125);   // 2x4 격자
		if (v_alpha < threshold) discard;
	}
	vec3 base = mix(COLOR.rgb, vec3(1.0), clamp(v_flash, 0.0, 1.0));
	ALBEDO = base;
	ROUGHNESS = 1.0;
	EMISSION = base * soften(ambient_color);
}

void light() {
	vec3 color = soften(LIGHT_COLOR) / PI;
	if (LIGHT_IS_DIRECTIONAL) {
		float lit = smoothstep(band_edge - band_soft, band_edge + band_soft, dot(NORMAL, LIGHT));
		lit *= smoothstep(0.35, 0.65, ATTENUATION);   // 드리운 그림자 = 그늘 단
		DIFFUSE_LIGHT += color * mix(shade_level, vec3(lit_level), lit);
	} else {
		float wrap = dot(NORMAL, LIGHT) * 0.5 + 0.5;
		DIFFUSE_LIGHT += ATTENUATION * color * mix(0.62, 1.12, wrap);
	}
}
