// 전투 화면 후처리 (G4): 틸트 시프트(화면 위아래 띠를 흐리게) + 비네트 + 색온도.
// 값은 순수 로직의 TiltShiftPreset / PostFxPreset 이 정하고 화면 층(ScreenFx.cs)이 넣는다.
// 유니티 판의 Assets/Shaders/TiltShift.shader 와 URP 비네트·화이트 밸런스를 한 패스로 합친 것이다.
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear, repeat_disable;
uniform float tilt_amount = 0.0;     // 0 = 없음
uniform float tilt_focus = 0.5;      // 또렷한 띠의 가운데 (0 아래 ~ 1 위)
uniform float tilt_band = 0.18;      // 또렷한 띠의 반높이
uniform float tilt_radius = 6.0;     // 화면 끝의 흐림 반지름(픽셀)
uniform float vignette_intensity = 0.0;
uniform float vignette_smoothness = 0.45;
uniform vec3 balance = vec3(1.0);    // 색온도·기울기를 RGB 배율로

void fragment() {
	vec2 uv = SCREEN_UV;
	// 화면 좌표는 위가 0 이므로 초점 높이를 뒤집는다
	float d = abs(uv.y - (1.0 - tilt_focus)) - tilt_band;
	float t = clamp(d / max(0.001, 0.5 - tilt_band), 0.0, 1.0);
	float radius = tilt_radius * tilt_amount * t * t;   // 띠 밖으로 갈수록 2차 곡선으로 흐려진다
	vec3 col = texture(screen_tex, uv).rgb;
	if (radius >= 0.5) {
		// 유니티 판과 같은 13탭: 가운데 x2, 안쪽 고리 4탭(반지름 절반), 바깥 고리 8탭
		vec2 px = SCREEN_PIXEL_SIZE * radius;
		vec3 sum = col * 2.0;
		sum += texture(screen_tex, uv + vec2(0.5, 0.0) * px).rgb;
		sum += texture(screen_tex, uv + vec2(-0.5, 0.0) * px).rgb;
		sum += texture(screen_tex, uv + vec2(0.0, 0.5) * px).rgb;
		sum += texture(screen_tex, uv + vec2(0.0, -0.5) * px).rgb;
		sum += texture(screen_tex, uv + vec2(1.0, 0.0) * px).rgb;
		sum += texture(screen_tex, uv + vec2(-1.0, 0.0) * px).rgb;
		sum += texture(screen_tex, uv + vec2(0.0, 1.0) * px).rgb;
		sum += texture(screen_tex, uv + vec2(0.0, -1.0) * px).rgb;
		sum += texture(screen_tex, uv + vec2(0.707, 0.707) * px).rgb;
		sum += texture(screen_tex, uv + vec2(-0.707, 0.707) * px).rgb;
		sum += texture(screen_tex, uv + vec2(0.707, -0.707) * px).rgb;
		sum += texture(screen_tex, uv + vec2(-0.707, -0.707) * px).rgb;
		col = sum / 14.0;
	}
	col *= balance;
	if (vignette_intensity > 0.0) {
		vec2 v = abs(uv - vec2(0.5)) * vignette_intensity * 3.0;
		float f = pow(clamp(1.0 - dot(v, v), 0.0, 1.0), vignette_smoothness * 5.0);
		col *= f;
	}
	COLOR = vec4(col, 1.0);
}
