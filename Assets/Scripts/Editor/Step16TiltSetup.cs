using Samkuk.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Samkuk.EditorTools
{
    /// <summary>
    /// Step 16-1: 2.5D 틸트의 프로젝트 쪽 준비.
    ///  ① 3D 렌더러 에셋 <c>Assets/Settings/Renderer3D.asset</c>(URP Universal Renderer, Forward)을 만들고 후처리 리소스를 2D 렌더러에서 빌린다
    ///  ② URP 에셋(<c>UniversalRP.asset</c>)의 렌더러 목록에 넣는다 — 기본(0번)은 2D 렌더러 그대로, 전투 카메라만 실행 중에 이쪽으로 바꾼다
    ///  ③ 틸트 시프트(Step 14-5) 기능을 3D 렌더러에도 넣고 <see cref="BattleTiltShift"/> 에 연결
    ///  ④ 전투 씬 Main Camera 에 <see cref="BattleTiltCamera"/> 를 붙인다
    /// 재질·빛은 실행 중 코드가 만들므로(<see cref="TiltView"/>, <see cref="BattleLighting"/>) 에셋이 더 없다. 멱등. Step 14-5 뒤에 실행한다.
    /// </summary>
    public static class Step16TiltSetup
    {
        const string ScenePath = "Assets/Scenes/BattleScene.unity";
        const string UrpAssetPath = "Assets/Settings/UniversalRP.asset";
        const string Renderer2DPath = "Assets/Settings/Renderer2D.asset";
        public const string Renderer3DPath = "Assets/Settings/Renderer3D.asset";
        const string MaterialPath = "Assets/Settings/TiltShift.mat";

        [MenuItem("Samkuk/Step 16-1 - Tilt View (2.5D 틸트)")]
        public static void Run()
        {
            var shader = Shader.Find(TiltView.ShaderName);
            bool shaderIncluded = false;
            if (shader == null)
                Debug.LogWarning($"[Samkuk] Step 16-1: 셰이더 {TiltView.ShaderName} 을 찾지 못했습니다 (Assets/Shaders/PixelSprite.shader 컴파일 오류?). 셋업은 계속하지만 실행 중 틸트는 켜지지 않습니다.");
            else shaderIncluded = EnsureAlwaysIncluded(shader);   // 재질을 실행 중 코드로 만들므로 빌드에서 잘리지 않게

            var data = EnsureRenderer3D(out bool created);
            if (data == null) return;
            bool registered = RegisterRenderer(data);

            FullScreenPassRendererFeature tiltFeature = null;
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) tiltFeature = Step14TiltShiftSetup.EnsureFeature(material, data, out _);
            else Debug.LogWarning($"[Samkuk] Step 16-1: {MaterialPath} 가 없어 3D 렌더러의 틸트 시프트는 건너뜁니다 (Step 14-5 먼저)");
            AssetDatabase.SaveAssets();

            // 씬은 에셋 작업 뒤에 (씬 전환이 에셋 참조를 무효화할 수 있음)
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[Samkuk] Step 16-1: Main Camera 를 찾지 못했습니다."); return; }
            bool added = cam.GetComponent<BattleTiltCamera>() == null;
            if (added) cam.gameObject.AddComponent<BattleTiltCamera>();
            var tiltShift = cam.GetComponent<BattleTiltShift>();
            if (tiltShift != null && tiltFeature != null)
            {
                var so = new SerializedObject(tiltShift);
                so.FindProperty("tiltFeature").objectReferenceValue = tiltFeature;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(cam);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Samkuk] Step 16-1 완료: 3D 렌더러 {(created ? "생성" : "있음")}, URP 등록 {(registered ? "추가" : "이미 있음")}, 틸트 시프트 {(tiltFeature != null ? "연결" : "없음")}, 셰이더 항상 포함 {(shaderIncluded ? "추가" : "이미 있음/없음")}, 카메라 컴포넌트 {(added ? "추가" : "있음")}");
        }

        /// <summary>그래픽 설정의 "항상 포함되는 셰이더"에 넣는다 (에셋이 참조하지 않는 셰이더는 빌드에서 잘린다). 돌려주는 값: 새로 넣었는가.</summary>
        static bool EnsureAlwaysIncluded(Shader shader)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) { Debug.LogWarning("[Samkuk] GraphicsSettings.asset 을 열지 못했습니다."); return false; }
            var so = new SerializedObject(assets[0]);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return false;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>Renderer3D.asset 이 없으면 만든다. 후처리 리소스가 비어 있으면 2D 렌더러의 것을 쓴다(같은 패키지 에셋).</summary>
        static UniversalRendererData EnsureRenderer3D(out bool created)
        {
            created = false;
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Renderer3DPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                data.name = TiltView.RendererName;
                AssetDatabase.CreateAsset(data, Renderer3DPath);
                created = true;
            }
            if (data.postProcessData == null)
            {
                var r2d = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(Renderer2DPath);
                if (r2d != null)
                {
                    var pp = new SerializedObject(r2d).FindProperty("m_PostProcessData");
                    if (pp != null) data.postProcessData = pp.objectReferenceValue as PostProcessData;
                }
                if (data.postProcessData == null) Debug.LogWarning("[Samkuk] Step 16-1: 3D 렌더러의 후처리 리소스(PostProcessData)를 채우지 못했습니다. Renderer3D.asset 인스펙터에서 확인하세요.");
            }
            if (data.renderingMode != RenderingMode.Forward) data.renderingMode = RenderingMode.Forward;
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>URP 에셋의 렌더러 목록에 넣는다 (기본 렌더러는 바꾸지 않는다). 돌려주는 값: 새로 넣었는가.</summary>
        static bool RegisterRenderer(ScriptableRendererData data)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (asset == null) { Debug.LogWarning($"[Samkuk] {UrpAssetPath} 를 찾지 못했습니다."); return false; }
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) return false;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return true;
        }
    }
}
