using UnityEngine;

namespace Samkuk.World
{
    /// <summary>
    /// 2.5D 틸트(Step 16-1, ADR 0003)의 수치표와 순수 기하. 좌표계는 탑다운 2D 그대로다: x 오른쪽, y 북쪽(화면 위), 땅은 z = 0,
    /// 땅에서 솟는 "위"는 -z(카메라 쪽). 카메라는 바라보는 점(플레이어)에서 남쪽·위로 물러나 <see cref="Pitch"/> 만큼 내려다보고,
    /// 서 있는 그림은 카메라를 향한 빌보드라 그림의 위쪽이 카메라의 위쪽(<see cref="Up"/>)과 같다. 값을 바꾸면 <c>TiltViewTests</c> 가 범위를 지킨다.
    /// </summary>
    public static class TiltPreset
    {
        /// <summary>수평에서 내려다보는 각도(도). 90 이면 완전 탑다운, 작을수록 비스듬하다.</summary>
        public const float Pitch = 55f;
        /// <summary>바라보는 점에서 카메라까지 거리(유닛).</summary>
        public const float Distance = 19f;
        /// <summary>세로 시야각(도). 16:9 에서 가운데 줄 폭이 예전 직교 화면(20유닛)과 비슷하게.</summary>
        public const float FieldOfView = 30f;
        public const float Near = 0.3f, Far = 100f;
        /// <summary>컬링용 시야각 여유(도). 서 있는 그림은 눕힌 트랜스폼 범위보다 화면에서 더 높이 솟으므로 아래 가장자리에서 먼저 잘리지 않게.</summary>
        public const float CullMargin = 8f;
        /// <summary>바닥 그림(배경 타일, 얼룩, 강)의 z. 서 있는 그림의 발(z = 0)보다 살짝 뒤라 발 끝이 바닥에 묻히지 않는다.</summary>
        public const float GroundZ = 0.01f;
        /// <summary>전역광 색·세기를 햇빛과 환경광으로 나누는 비율: 빛 받는 곳 1.05, 그늘 0.45 (드리운 그림자 농도 0.3~0.4 와 비슷).</summary>
        public const float SunShare = 0.6f, AmbientShare = 0.45f;
        /// <summary>Light2D 세기를 3D 점광원 세기로 옮기는 배율 (감쇠 방식이 달라 어림값).</summary>
        public const float PointLightScale = 2.5f;
        /// <summary>플레이어 빛과 3D 점광원이 땅에서 떠 있는 높이(유닛).</summary>
        public const float GlowHeight = 0.6f;
        /// <summary>손에 든 무기 그림을 몸 그림보다 카메라 쪽으로 밀어 두는 거리(같은 평면에서 깊이가 겹쳐 번쩍이지 않게).</summary>
        public const float HeldToward = 0.02f;
        /// <summary>체력 막대처럼 몸 위에 겹치는 반투명 그림의 카메라 쪽 거리.</summary>
        public const float OverlayToward = 0.03f;

        /// <summary>카메라가 보는 방향 (단위 벡터): 북쪽으로 기울어 땅(+z)을 향한다.</summary>
        public static Vector3 Forward(float pitchDeg)
        {
            float r = pitchDeg * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Cos(r), Mathf.Sin(r));
        }

        /// <summary>카메라의 위쪽 = 서 있는 그림의 위쪽 (단위 벡터). 땅에서 솟는 -z 성분을 가진다.</summary>
        public static Vector3 Up(float pitchDeg)
        {
            float r = pitchDeg * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Sin(r), -Mathf.Cos(r));
        }

        /// <summary>카메라(와 서 있는 그림)의 회전. 유니티 X 오일러는 양수가 아래를 보므로 90 - 기울기 만큼 뒤로 젖힌다.</summary>
        public static Quaternion Rotation(float pitchDeg) => Quaternion.Euler(pitchDeg - 90f, 0f, 0f);

        /// <summary>바라보는 점에서 <paramref name="distance"/> 만큼 물러난 카메라 자리와 회전.</summary>
        public static void CameraPose(Vector3 lookAt, float pitchDeg, float distance, out Vector3 position, out Quaternion rotation)
        {
            position = lookAt - Forward(pitchDeg) * distance;
            rotation = Rotation(pitchDeg);
        }

        /// <summary>
        /// 화면 네 귀퉁이 광선이 땅(z = 0)과 만나는 점을 감싼 사각형. 바라보는 점 기준 상대 좌표라 북쪽(yMax)이 남쪽(|yMin|)보다 멀다.
        /// 땅과 만나지 않는 광선(위를 보는 경우)은 <see cref="Far"/> 에서 자른다.
        /// </summary>
        public static Rect GroundFootprint(float pitchDeg, float distance, float fovDeg, float aspect)
        {
            Vector3 cam = -Forward(pitchDeg) * distance;
            Vector3 f = Forward(pitchDeg), u = Up(pitchDeg), r = Vector3.right;
            float t = Mathf.Tan(fovDeg * 0.5f * Mathf.Deg2Rad);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    Vector3 d = f + u * (t * sy) + r * (t * aspect * sx);
                    float s = d.z > 1e-4f ? -cam.z / d.z : Far;
                    Vector3 p = cam + d * Mathf.Min(s, Far);
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>바라보는 점에서 사각형의 가장 먼 귀퉁이까지 — 적이 화면 밖에서 태어나려면 이보다 멀어야 한다.</summary>
        public static float SpawnRadius(Rect footprint)
        {
            float x = Mathf.Max(Mathf.Abs(footprint.xMin), Mathf.Abs(footprint.xMax));
            float y = Mathf.Max(Mathf.Abs(footprint.yMin), Mathf.Abs(footprint.yMax));
            return Mathf.Sqrt(x * x + y * y);
        }

        /// <summary>
        /// 햇빛이 나아가는 방향(단위 벡터). 높이 1 인 점의 그림자가 바닥에서 (shearX, -length) 만큼 밀리게 — 드리운 그림자(<see cref="ShadowPreset"/>)와 같은 해 위치.
        /// </summary>
        public static Vector3 SunDirection(ShadowSettings s) => new Vector3(s.shearX, -s.length, 1f).normalized;
    }
}
