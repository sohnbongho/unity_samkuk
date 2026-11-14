// 3D 화면의 물 (연못, 강물) (G16). 도트 그림은 모양(알파)과 물빛만 주고, 표면은 세계 좌표로 그린다:
// 천천히 흐르는 물결을 몇 단의 면으로 끊어 로우폴리 모델과 어울리게 하고, 물마루의 반짝임과 물가의 거품 띠를 얹는다.
// 세계 좌표라서 겹쳐 놓은 강 토막끼리도 무늬가 이어진다. 빛 규칙은 바닥 그림과 같다(환경광 + 햇빛 x 그림자 + 점광원): 나무 그림자가 물 위에 진다.
shader_type spatial;
render_mode cull_disabled, specular_disabled, blend_mix, depth_draw_never, shadows_disabled;

uniform sampler2D tex : source_color, filter_linear;
uniform vec3 water_color : source_color = vec3(0.25, 0.55, 0.85);
uniform vec2 texel = vec2(0.01, 0.01);
// 거품 띠를 찾는 방향 (그림의 가로, 세로). 강물은 둥근 토막을 겹쳐 놓은 것이라 토막마다 띠가 생기므로 끈다 (0, 0): 물가는 아래의 강둑 그림이 맡는다
uniform vec2 foam_axes = vec2(1.0, 1.0);

varying vec2 ground;

void vertex() {
	ground = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
	vec4 c = texture(tex, UV);
	vec2 p = ground;
	float t = TIME;
	float w1 = sin(p.x * 2.6 + t * 1.2 + sin(p.y * 1.9 + t * 0.6) * 1.3);
	float w2 = sin(p.y * 3.4 - t * 0.9 + sin(p.x * 1.5 - t * 0.4) * 1.6);
	float wave = (w1 + w2) * 0.25 + 0.5;
	float band = floor(wave * 3.0 + 0.5) / 3.0;   // 면으로 끊는다
	vec3 col = mix(water_color * 0.68, mix(water_color, vec3(1.0), 0.2), band) * COLOR.rgb;
	col += smoothstep(0.9, 0.96, wave) * 0.3;     // 물마루 반짝임
	// 물가: 알파가 떨어지는 가장자리에 일렁이는 거품 띠
	vec2 d = texel * 4.0;
	float inner_x = min(texture(tex, UV + vec2(d.x, 0.0)).a, texture(tex, UV - vec2(d.x, 0.0)).a);
	float inner_y = min(texture(tex, UV + vec2(0.0, d.y)).a, texture(tex, UV - vec2(0.0, d.y)).a);
	float inner = min(mix(c.a, inner_x, foam_axes.x), mix(c.a, inner_y, foam_axes.y));
	float shore = clamp((c.a - inner) * 1.5, 0.0, 1.0) * (0.6 + 0.4 * sin(t * 2.0 + p.x * 3.0 + p.y * 2.0));
	col = mix(col, vec3(0.92, 0.97, 1.0), shore * 0.7);
	ALBEDO = col;
	ALPHA = c.a * COLOR.a;
}

void light() {
	DIFFUSE_LIGHT += ATTENUATION * LIGHT_COLOR / PI;
}
