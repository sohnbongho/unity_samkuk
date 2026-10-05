using System.IO;
using Samkuk.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Samkuk.EditorTools
{
    /// <summary>Step 1: 플레이스홀더 스프라이트 생성, 카메라/물리 레이어 설정.</summary>
    public static class Step1Setup
    {
        const string SpriteDir = "Assets/Sprites";
        const string ScenePath = "Assets/Scenes/BattleScene.unity";
        const int PPU = 64;

        [MenuItem("Samkuk/Step 1 - Setup Project")]
        public static void Run()
        {
            Directory.CreateDirectory(SpriteDir);
            CreateSprite("Player", 56, new Color(0.25f, 0.55f, 1f), Shape.Circle);
            CreateSprite("Enemy", 48, new Color(0.9f, 0.25f, 0.25f), Shape.Circle);
            CreateSprite("Projectile", 20, new Color(1f, 0.9f, 0.3f), Shape.Circle);
            CreateSprite("ExpGem", 20, new Color(0.3f, 1f, 0.5f), Shape.Diamond);
            CreateSprite("Background", 256, new Color(0.22f, 0.32f, 0.2f), Shape.Ground);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SetupPhysicsLayers();
            SetupCamera();
            Debug.Log("[Samkuk] Step 1 setup 완료");
        }

        static void SetupPhysicsLayers()
        {
            int player = LayerMask.NameToLayer(GameLayers.Player);
            int enemy = LayerMask.NameToLayer(GameLayers.Enemy);
            int proj = LayerMask.NameToLayer(GameLayers.PlayerProjectile);
            int pickup = LayerMask.NameToLayer(GameLayers.Pickup);
            if (player < 0 || enemy < 0 || proj < 0 || pickup < 0)
            {
                Debug.LogError($"[Samkuk] 레이어가 없습니다. player={player} enemy={enemy} proj={proj} pickup={pickup} (TagManager.asset 확인)");
                return;
            }

            // 충돌 매트릭스/중력은 ProjectSettings/Physics2DSettings.asset에 저장되어 있다.
            // 허용 쌍: Player-Enemy(접촉 피해), PlayerProjectile-Enemy(명중), Player-Pickup(획득)
            bool ok =
                !Physics2D.GetIgnoreLayerCollision(player, enemy) &&
                !Physics2D.GetIgnoreLayerCollision(proj, enemy) &&
                !Physics2D.GetIgnoreLayerCollision(player, pickup) &&
                Physics2D.GetIgnoreLayerCollision(enemy, enemy) &&
                Physics2D.GetIgnoreLayerCollision(player, proj) &&
                Physics2D.GetIgnoreLayerCollision(pickup, enemy) &&
                Physics2D.gravity == Vector2.zero;
            Debug.Log(ok ? "[Samkuk] Physics2D 레이어 매트릭스 OK" : "[Samkuk] Physics2D 설정 불일치!");
        }

        static void SetupCamera()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Samkuk] Main Camera를 찾지 못했습니다.");
                return;
            }
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = new Color(0.12f, 0.16f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            EditorUtility.SetDirty(cam);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        enum Shape { Circle, Diamond, Ground }

        static void CreateSprite(string name, int size, Color color, Shape shape)
        {
            string path = $"{SpriteDir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            var rng = new System.Random(name.GetHashCode());

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    Color c = Color.clear;
                    switch (shape)
                    {
                        case Shape.Circle:
                        {
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d <= r - 1f) c = d > r - 3f ? color * 0.7f : color;
                            break;
                        }
                        case Shape.Diamond:
                            if (Mathf.Abs(dx) + Mathf.Abs(dy) <= r - 1f) c = color;
                            break;
                        case Shape.Ground:
                        {
                            float n = (float)rng.NextDouble() * 0.06f - 0.03f;
                            c = new Color(color.r + n, color.g + n, color.b + n);
                            bool grid = x % 128 == 0 || y % 128 == 0;
                            if (grid) c *= 0.9f;
                            break;
                        }
                    }
                    c.a = c == Color.clear ? 0f : 1f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PPU;
            imp.filterMode = FilterMode.Bilinear;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            if (shape == Shape.Ground) imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
        }
    }
}
