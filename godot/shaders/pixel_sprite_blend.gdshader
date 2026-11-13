// 도트 그림 (반투명): 바닥 그림(얼룩, 강, 연못, 바닥 장식)과 흐려진 서 있는 그림 (G4).
// 빛 규칙은 pixel_sprite_cut 과 같다(환경광 + 햇빛 x 그림자 + 점광원, N·L 없음). 알파로 섞고 깊이는 쓰지 않으며 그림자를 드리우지 않는다.
// 그리는 순서는 재질의 렌더 우선순위(정렬 레이어·순서)로 정한다.
shader_type spatial;
render_mode cull_disabled, specular_disabled, blend_mix, depth_draw_never, shadows_disabled;

uniform sampler2D tex : source_color, filter_nearest, repeat_enable;
uniform vec2 uv_scale = vec2(1.0, 1.0);

void fragment() {
	vec4 c = texture(tex, UV * uv_scale) * COLOR;
	ALBEDO = c.rgb;
	ALPHA = c.a;
}

void light() {
	DIFFUSE_LIGHT += ATTENUATION * LIGHT_COLOR / PI;
}
