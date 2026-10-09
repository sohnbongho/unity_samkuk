using Samkuk.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Samkuk.World
{
    /// <summary>
    /// 전투 카메라의 후처리(Step 14-2): 카메라의 포스트 프로세싱을 켜고, 코드로 만든 프로필(<see cref="PostFxPreset"/>)을 가진
    /// 전역 Volume 을 자식으로 둔다. 셋업(Step 14-2)이 전투 씬의 Main Camera 에 붙인다. 시간대는 출진한 성(<see cref="GameSession.SortieCastle"/>)을 따른다.
    /// <see cref="Hd2dSettings.PostFx"/> 가 꺼져 있으면 Volume 무게 0 + 카메라 포스트 프로세싱 끔(비용도 들지 않게). 디버그 키 F6.
    /// UI(Screen Space Overlay 캔버스)는 후처리 뒤에 그려지므로 영향받지 않는다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BattlePostFx : MonoBehaviour
    {
        const string VolumeName = "PostFxVolume";

        Camera cam;
        Volume volume;
        VolumeProfile profile;

        public Volume Volume => volume;
        public VolumeProfile Profile => profile;
        public bool IsEnabled { get; private set; }
        public TimeOfDay TimeOfDay { get; private set; }

        void Awake()
        {
            cam = GetComponent<Camera>();
            TimeOfDay = CastleMood.Of(GameSession.SortieCastle);
            EnsureVolume();
            Apply(Hd2dSettings.PostFx);
        }

        /// <summary>켜면 후처리를 그리고, 끄면 카메라 포스트 프로세싱까지 꺼서 비용이 들지 않게 한다.</summary>
        public void Apply(bool enabled)
        {
            IsEnabled = enabled;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = enabled;
            if (volume != null) volume.weight = enabled ? 1f : 0f;
        }

        /// <summary>시간대를 바꾼다 (색온도만 달라진다).</summary>
        public void SetTimeOfDay(TimeOfDay time)
        {
            TimeOfDay = time;
            if (profile != null) PostFxPreset.ApplyTime(profile, time);
        }

        void EnsureVolume()
        {
            if (volume != null) return;
            var go = new GameObject(VolumeName);
            go.transform.SetParent(transform, false);
            go.layer = 0;   // Default: 카메라 볼륨 레이어 마스크(기본 Default)에 들어가도록
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "BattlePostFx (runtime)";
            profile.hideFlags = HideFlags.DontSave;
            PostFxPreset.Build(profile, TimeOfDay);
            volume.sharedProfile = profile;

            var data = cam.GetUniversalAdditionalCameraData();
            data.volumeLayerMask |= 1 << go.layer;
        }

        void OnDestroy()
        {
            if (profile != null) Destroy(profile);
        }
    }
}
