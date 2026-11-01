using Samkuk.Audio;
using Samkuk.Core;
using Samkuk.Data;
using Samkuk.Weapons;
using UnityEngine;

namespace Samkuk.Field
{
    /// <summary>
    /// 야전 장수 부대의 무기 연출(15-7): 서바이버 전투의 휘두르기·찌르기 규칙(<see cref="SwingMotion"/>, <see cref="ThrustMotion"/>)과 검기(<see cref="WeaponTrail"/>),
    /// 들고 있는 그림(<see cref="WeaponData.heldSprite"/>)을 그대로 빌려 장수의 시작 무기로 때리는 모습을 보여 준다. 규칙(피해)은 전혀 없다.
    /// 종류별: 베기(연속 베기면 올려치기를 이어서), 찌르기, 극(찌르기·휩쓸기 번갈아), 그 밖은 베기로. 무기 그림은 아이소메트릭 부대 옆이라
    /// <see cref="Scale"/> 로 줄이고, 멀리 나는 검기(유비)는 <see cref="MaxTravel"/> 까지만. 동작 중엔 새 타격을 받지 않는다(그게 간격 조절).
    /// </summary>
    public sealed class FieldWeaponShow
    {
        /// <summary>전투 무기 그림(위에서 본 시점 크기)을 아이소메트릭 부대 옆에 맞추는 배율.</summary>
        public const float Scale = 0.6f;
        /// <summary>검기 최대 비거리 (유닛). 유비 검기 7유닛이 전장을 가로지르지 않게.</summary>
        public const float MaxTravel = 2.5f;
        public const float ComboGap = SlashWeapon.ComboGap;

        readonly WeaponData data;
        readonly GameObject root;
        readonly SpriteRenderer held;
        readonly WeaponTrail trail;
        readonly SwingMotion swing = new SwingMotion();
        readonly ThrustMotion thrust = new ThrustMotion();
        float comboDelay, comboAngle;
        bool nextIsSweep;

        public WeaponData Data => data;
        public SpriteRenderer HeldRenderer => held;
        public SpriteRenderer TrailRenderer => trail.Renderer;
        public bool Active => swing.Active || thrust.Active || comboDelay > 0f;
        /// <summary>시작한 타격 동작 수 (테스트용).</summary>
        public int Strikes { get; private set; }

        public FieldWeaponShow(Transform parent, WeaponData data)
        {
            this.data = data;
            root = new GameObject("GeneralWeapon");
            root.transform.SetParent(parent, false);
            trail = new WeaponTrail(MakeRenderer("Trail", WaveSprites.Arc(), 0));
            if (data.heldSprite != null)
            {
                held = MakeRenderer("Held", data.heldSprite, 1);
                held.transform.localScale = Vector3.one * Scale;
            }
        }

        SpriteRenderer MakeRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = GameLayers.Sorting.Effect;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }

        public void Hide()
        {
            swing.Stop();
            thrust.Stop();
            trail.Stop();
            comboDelay = 0f;
            if (held != null) held.enabled = false;
        }

        /// <summary>hand(장수 부대 손 높이)에서 target 쪽으로 한 번 때린다. 동작 중이면 거절(false).</summary>
        public bool Strike(Vector2 hand, Vector2 target)
        {
            if (Active) return false;
            Vector2 to = target - hand;
            float angle = to.sqrMagnitude > 1e-6f ? Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg : 0f;
            switch (data.type)
            {
                case WeaponType.Thrust:
                    thrust.Start(angle, data.range * ThrustWeapon.ReachRatio * Scale, data.swingDuration);
                    AudioManager.PlayWeapon(WeaponType.Thrust);
                    break;
                case WeaponType.Halberd:
                    if (nextIsSweep)
                    {
                        float arc = data.swingArcDegrees > 0f ? data.swingArcDegrees : HalberdWeapon.DefaultSweepArc;
                        swing.Start(angle, arc, data.swingDuration);
                        AudioManager.PlayWeapon(WeaponType.Slash);
                    }
                    else
                    {
                        thrust.Start(angle, data.range * HalberdWeapon.ReachRatio * Scale, data.swingDuration);
                        AudioManager.PlayWeapon(WeaponType.Thrust);
                    }
                    nextIsSweep = !nextIsSweep;
                    break;
                default:
                    swing.Start(angle, data.swingArcDegrees, data.swingDuration);
                    if (data.comboSwings && data.count >= 2)
                    {
                        // 연속 베기: 첫 베기가 끝나기 조금 전에 올려치기를 이어서
                        float seconds = data.swingDuration > 0f ? data.swingDuration : SwingMotion.DefaultDuration;
                        comboDelay = seconds * ComboGap;
                        comboAngle = angle;
                    }
                    AudioManager.PlayWeapon(WeaponType.Slash);
                    break;
            }
            Strikes++;
            return true;
        }

        /// <summary>매 틱 (정지 중엔 안 불린다). hand 는 장수 부대가 움직여도 따라가게 지금 자리.</summary>
        public void Tick(float dt, Vector2 hand)
        {
            trail.Tick(dt, data.tint);

            if (comboDelay > 0f)
            {
                comboDelay -= dt;
                if (comboDelay <= 0f)
                {
                    comboDelay = 0f;
                    swing.Start(comboAngle, data.swingArcDegrees, data.swingDuration, upward: true);
                    AudioManager.PlayWeapon(WeaponType.Slash);
                }
            }

            if (swing.Active)
            {
                if (swing.Advance(dt)) SpawnTrail(hand, swing.CenterAngle, arc: true);
                if (swing.Active) Place(hand, swing.Angle, swing.FacesLeft);
                else if (held != null) held.enabled = false;
            }
            else if (thrust.Active)
            {
                if (thrust.Advance(dt)) SpawnTrail(hand, thrust.Angle, arc: false);
                if (thrust.Active) Place(hand + Dir(thrust.Angle) * thrust.Offset, thrust.Angle, thrust.FacesLeft);
                else if (held != null) held.enabled = false;
            }
        }

        void Place(Vector2 position, float angle, bool facesLeft)
        {
            if (held == null) return;
            held.enabled = true;
            held.flipX = facesLeft;
            held.transform.position = position;
            held.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);   // 그림은 손잡이 아래·날 위
        }

        /// <summary>검기: 베기는 피해 원 자리에서 반원, 찌르기는 창 끝에서 뾰족한 띠. 크기와 비거리는 야전에 맞게 줄인다.</summary>
        void SpawnTrail(Vector2 hand, float angle, bool arc)
        {
            Vector2 dir = Dir(angle);
            trail.Renderer.sprite = arc ? WaveSprites.Arc() : WaveSprites.Point();
            float scale = (data.trailScale > 0f ? data.trailScale : 1f) * Scale;
            float radius = arc ? data.range * 0.6f * scale : Mathf.Max(0.3f, data.size * ThrustWeapon.TrailRadiusRatio) * scale;
            Vector2 from = arc ? hand + dir * (data.range * 0.5f * Scale) : hand + dir * (data.range * Scale);
            float travelDefault = data.range * (arc ? SlashWeapon.TrailTravel : ThrustWeapon.TrailTravel);
            float distance = Mathf.Min(WeaponTrail.DistanceFor(data, travelDefault) * Scale, MaxTravel);
            trail.Start(from, dir, angle, radius, WaveSprites.RadiusUnits, distance, WeaponTrail.SecondsFor(data, distance), data.tint);
        }

        static Vector2 Dir(float angle) => new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    }
}
