using System.IO;
using Samkuk.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 14-5: 틸트 시프트. ① <c>Assets/Shaders/TiltShift.shader</c> 로 머티리얼(<c>Assets/Settings/TiltShift.mat</c>)을 만들고
    /// ② 2D 렌더러 에셋(<c>Renderer2D.asset</c>)에 URP Full Screen Pass 렌더러 기능("TiltShift", 후처리 전, 처음엔 꺼짐)을 하위 에셋으로 넣고
    /// ③ 전투 씬 Main Camera 에 <see cref="BattleTiltShift"/> 를 붙여 기능과 머티리얼을 연결한다. 멱등.
    /// 렌더러 기능을 넣는 방법은 URP 의 ScriptableRendererDataEditor.AddComponent 와 같다(하위 에셋 + m_RendererFeatures + m_RendererFeatureMap).
    /// </summary>
    public static class Step14TiltShiftSetup
    {
        const string ScenePath = "Assets/Scenes/BattleScene.unity";
        const string Renderer2DPath = "Assets/Settings/Renderer2D.asset";
        const string MaterialPath = "Assets/Settings/TiltShift.mat";
        const string FeatureName = "TiltShift";

        [MenuItem("Samkuk/Step 14-5 - Tilt Shift (틸트 시프트)")]
        public static void Run()
        {
            var shader = Shader.Find(TiltShiftPreset.ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Samkuk] Step 14-5: 셰이더 {TiltShiftPreset.ShaderName} 을 찾지 못했습니다 (Assets/Shaders/TiltShift.shader 컴파일 오류?)");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool materialCreated = false;
            if (material == null)
            {
                material = new Material(shader) { name = "TiltShift" };
                AssetDatabase.CreateAsset(material, MaterialPath);
                materialCreated = true;
            }
            else if (material.shader != shader) material.shader = shader;
            TiltShiftPreset.Apply(material, 1080);
            EditorUtility.SetDirty(material);

            var feature = EnsureFeature(material, out bool featureCreated);
            if (feature == null) return;
            AssetDatabase.SaveAssets();

            // 씬은 에셋 작업 뒤에 (씬 전환이 에셋 참조를 무효화할 수 있음)
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[Samkuk] Step 14-5: Main Camera 를 찾지 못했습니다."); return; }
            var tilt = cam.GetComponent<BattleTiltShift>();
            bool added = tilt == null;
            if (added) tilt = cam.gameObject.AddComponent<BattleTiltShift>();
            var so = new SerializedObject(tilt);
            so.FindProperty("feature").objectReferenceValue = feature;
            so.FindProperty("material").objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Samkuk] Step 14-5 완료: 머티리얼 {(materialCreated ? "생성" : "있음")}, 렌더러 기능 {(featureCreated ? "추가" : "있음")}, 카메라 컴포넌트 {(added ? "추가" : "있음")}");
        }

        /// <summary>Renderer2D.asset 에 "TiltShift" Full Screen Pass 기능이 없으면 넣는다. 돌려주는 값: 기능 (실패하면 null).</summary>
        static FullScreenPassRendererFeature EnsureFeature(Material material, out bool created)
        {
            created = false;
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(Renderer2DPath);
            if (data == null) { Debug.LogWarning($"[Samkuk] {Renderer2DPath} 를 찾지 못했습니다."); return null; }

            FullScreenPassRendererFeature feature = null;
            foreach (var f in data.rendererFeatures)
                if (f is FullScreenPassRendererFeature fs && f.name == FeatureName) { feature = fs; break; }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = FeatureName;
                feature.hideFlags |= HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                created = true;
            }

            // 값은 늘 코드 기준으로 맞춘다 (전투 동안만 BattleTiltShift 가 켠다)
            feature.passMaterial = material;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.requirements = ScriptableRenderPassInput.None;
            feature.fetchColorBuffer = true;
            feature.passIndex = 0;
            feature.SetActive(false);
            EditorUtility.SetDirty(feature);
            data.SetDirty();
            EditorUtility.SetDirty(data);
            return feature;
        }
    }
}
