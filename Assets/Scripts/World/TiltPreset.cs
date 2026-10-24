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
        /// <summary>바라보는 점에서 카메라까지 거리(유닛). 직교라 크기에는 영향이 없고 근/원 평면 안에 세계가 들어오게만 한다.</summary>
        public const float Distance = 19f;
        /// <summary>
        /// 직교 카메라 반높이(유닛) = 도트 가상 해상도 360 / 2 / PPU 32. 16:9 에서 폭 20유닛(예전 2D 화면과 같음), 바닥 세로는 반높이 / sin(기울기).
        /// 원근 대신 직교를 쓰는 이유: 원근은 세로로 늘린 키 큰 그림(나무)의 윗부분이 카메라에 가까워져 풍선처럼 커지고, 화면 아래·위에서 납작한 정도가 달랐다.
        /// 직교는 화면 어디서나 같은 배율이라 도트 밀도도 일정하다.
        /// </summary>
        public const float OrthoSize = 5.625f;
        public const float Near = 0.3f, Far = 100f;
        /// <summary>컬링 여유(유닛). 서 있는 그림은 눕힌 트랜스폼 범위보다 화면에서 더 높이 솟으므로 아래 가장자리에서 먼저 잘리지 않게.</summary>
        public const float CullMargin = 4f;
        /// <summary>바닥 그림(배경 타일, 얼룩, 강)의 z. 서 있는 그림의 발(z = 0)보다 살짝 뒤라 발 끝이 바닥에 묻히지 않는다.</summary>
        public const float GroundZ = 0.01f;
        /// <summary>전역광 색·세기를 햇빛과 환경광으로 나누는 비율: 빛 받는 곳 1.05, 그늘 0.45 (드리운 그림자 농도 0.3~0.4 와 비슷).</summary>
        public const float SunShare = 0.6f, AmbientShare = 0.45f;
        /// <summary>Light2D 세기를 3D 점광원 세기로 옮기는 배율 (감쇠 방식이 달라 어림값).</summary>
        public const float PointLightScale = 2.5f;
        /// <summary>플레이어 빛과 3D 점광원이 땅에서 떠 있는 높이(유닛).</summary>
        public const float GlowHeight = 0.6f;
        /// <summary>서 있는 소품(나무·바위 등)의 세로 배율 보정. 캐릭터는 1(원래 비율)이지만 소품까지 같이 늘리면 기울어진 바닥에 비해 유난히 커 보여 줄인다.</summary>
        public const float PropStretch = 0.7f;
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

        /// <summary>카메라의 위쪽 (단위 벡터). 땅에서 솟는 -z 성분을 가진다. 화면 흔들림과 보이는 범위 계산에 쓴다.</summary>
        public static Vector3 Up(float pitchDeg)
        {
            float r = pitchDeg * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Sin(r), -Mathf.Cos(r));
        }

        /// <summary>
        /// 서 있는 그림의 위쪽: 땅에서 **수직**(-z). 카메라를 향해 기울인 빌보드(<see cref="Up"/>)는 상체가 북쪽으로 기울어 절벽 앞에 서면 벽면 뒤로 파고들므로
        /// 수직으로 세운다. 카메라 기울기만큼(sin 55° ≈ 0.82) 세로가 납작해 보이지만 절벽·언덕과 겹치지 않는다.
        /// </summary>
        public static readonly Vector3 StandUp = Vector3.back;

        /// <summary>
        /// 수직으로 세운 그림의 세로 배율. 수직인 것은 화면에서 cos(기울기)배(55° 면 0.57)로 납작해 보이므로 그 역수(1.74)만큼 세로를 늘려
        /// 화면에서는 원래 도트 비율로 보이게 한다 (옥토패스와 같은 방식). 손 높이·체력 막대·횃불도 같은 배율로 올린다(<c>TiltView.Lift</c>).
        /// </summary>
        public static float StandScale(float pitchDeg) => 1f / Mathf.Max(0.2f, Mathf.Cos(pitchDeg * Mathf.Deg2Rad));

        /// <summary>서 있는 그림(트랜스폼을 직접 돌리는 데미지 숫자)의 회전: 위 = -z, 오른쪽 = +x (남쪽 카메라에서 글자가 뒤집히지 않게 앞면은 북쪽).</summary>
        public static readonly Quaternion StandRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);

        /// <summary>카메라(와 서 있는 그림)의 회전. 유니티 X 오일러는 양수가 아래를 보므로 90 - 기울기 만큼 뒤로 젖힌다.</summary>
        public static Quaternion Rotation(float pitchDeg) => Quaternion.Euler(pitchDeg - 90f, 0f, 0f);

        /// <summary>바라보는 점에서 <paramref name="distance"/> 만큼 물러난 카메라 자리와 회전.</summary>
        public static void CameraPose(Vector3 lookAt, float pitchDeg, float distance, out Vector3 position, out Quaternion rotation)
        {
            position = lookAt - Forward(pitchDeg) * distance;
            rotation = Rotation(pitchDeg);
        }

        /// <summary>
        /// 직교 카메라에 보이는 땅(z = 0)의 범위, 바라보는 점 기준 상대 좌표. 폭은 반높이 x 비율, 세로는 화면 반높이가 땅에서 1/sin(기울기)만큼 늘어난다.
        /// 원근이 없어 남북 대칭이다.
        /// </summary>
        public static Rect GroundFootprint(float pitchDeg, float orthoSize, float aspect)
        {
            float halfW = orthoSize * aspect;
            float halfH = orthoSize / Mathf.Max(0.1f, Mathf.Sin(pitchDeg * Mathf.Deg2Rad));
            return Rect.MinMaxRect(-halfW, -halfH, halfW, halfH);
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
