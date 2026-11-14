// 로우폴리 3D 이펙트 (검기, 충격 고리, 번개, 불길, 탄환, 타격 입자). ModelMirror 가 종류마다 MultiMesh 한 묶음으로 그린다.
// 빛을 받지 않고 스스로 빛나는 색: 꼭짓점 색 x 인스턴스 색, 불투명도 = 인스턴스 색의 알파 (규칙 코드가 그림 색으로 정한 사라짐 그대로).
// UV.x > 0 인 꼭짓점(불꽃 끝)은 시간에 따라 일렁인다. UV.y = 그 꼭짓점이 옅어지는 정도.
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never, shadows_disabled;

uniform float glow = 1.35;

void vertex() {
	if (UV.x > 0.001) {
		float seed = INSTANCE_CUSTOM.x * 37.0 + VERTEX.x * 9.0 + VERTEX.z * 7.0;
		VERTEX.y *= 1.0 + 0.28 * sin(TIME * 11.0 + seed);
		VERTEX.x += 0.04 * sin(TIME * 7.0 + seed * 1.7) * UV.x;
	}
}

void fragment() {
	ALBEDO = COLOR.rgb * glow;
	ALPHA = clamp(COLOR.a * (1.0 - UV.y), 0.0, 1.0);
}
