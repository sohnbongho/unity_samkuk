// 도트 그림 (컷아웃): 서 있는 그림, 타일 바닥, 절벽·흙길 메시 (G4).
// 유니티 판 Assets/Shaders/PixelSprite.shader 와 같은 빛 규칙: 색 x (환경광 + 햇빛 x 그림자 + 점광원). 법선·N·L 은 쓰지 않는다
// (도트 그림은 평평하게, 그늘만 진다). 알파 0.5 아래는 버리고 깊이를 써서 앞뒤가 정해지고 그림자를 드리운다.
// 화면 층(WorldMirror)이 그림마다 재질을 만들어 tex 를 넣는다. filter_nearest 는 부드러운 그림용으로 filter_linear 로 바꾼 변형도 만든다.
shader_type spatial;
render_mode cull_disabled, specular_disabled, depth_draw_opaque;

uniform sampler2D tex : source_color, filter_nearest, repeat_enable;
uniform vec2 uv_scale = vec2(1.0, 1.0);

void fragment() {
	vec4 c = texture(tex, UV * uv_scale) * COLOR;
	ALBEDO = c.rgb;
	ALPHA = c.a;
	ALPHA_SCISSOR_THRESHOLD = 0.5;
}

void light() {
	DIFFUSE_LIGHT += ATTENUATION * LIGHT_COLOR / PI;
}
