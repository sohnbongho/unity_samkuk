using System;
using System.Collections.Generic;

namespace Samkuk.Compat
{
    // 장면 모형의 내장 컴포넌트 (G3). 전부 "데이터"다: 그리거나 충돌을 계산하지 않고, 규칙 코드가 쓰는 값만 든다.
    // 화면 층(Godot)이 SpriteRenderer 를 읽어 그림을 비추고, Camera 의 값으로 실제 카메라를 맞춘다.

    /// <summary>그려지는 것의 바탕: 켜짐, 정렬, 재질(화면 층이 해석하는 표시).</summary>
    public class Renderer : Component
    {
        public bool enabled = true;
        public string sortingLayerName = "Default";
        public int sortingOrder;
        /// <summary>재질 표시. 화면 층이 어떤 셰이더로 그릴지 고르는 데 쓴다 (null = 기본).</summary>
        public Material sharedMaterial;
        public Material material { get => sharedMaterial; set => sharedMaterial = value; }
        public bool receiveShadows;
        public ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;
        MaterialPropertyBlock block;
        /// <summary>이 렌더러에만 얹은 값. null 을 넣으면 지운다.</summary>
        public void SetPropertyBlock(MaterialPropertyBlock properties)
        {
            if (properties == null) { block = null; return; }
            block ??= new MaterialPropertyBlock();
            block.Clear();
            foreach (var kv in properties.floats) block.floats[kv.Key] = kv.Value;
        }
        public void GetPropertyBlock(MaterialPropertyBlock properties)
        {
            properties.Clear();
            if (block != null) foreach (var kv in block.floats) properties.floats[kv.Key] = kv.Value;
        }
        public bool HasPropertyBlock() => block != null;
        public int sortingLayerID { get => SortingLayer.NameToID(sortingLayerName); set => sortingLayerName = SortingLayer.IDToName(value); }

        internal override void CopyStateFrom(Component source)
        {
            var s = (Renderer)source;
            enabled = s.enabled; sortingLayerName = s.sortingLayerName; sortingOrder = s.sortingOrder; sharedMaterial = s.sharedMaterial;
        }
    }

    public enum SpriteDrawMode { Simple, Sliced, Tiled }

    /// <summary>세계 축에 맞춘 상자 (가운데 + 크기).</summary>
    public struct Bounds
    {
        public Vector3 center, size;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; }
        public Vector3 extents => size * 0.5f;
        public Vector3 min => center - extents;
        public Vector3 max => center + extents;
    }

    /// <summary>스프라이트 한 장을 어디에 어떤 색으로 그릴지.</summary>
    public class SpriteRenderer : Renderer
    {
        public Sprite sprite;
        public Color color = Color.white;
        public bool flipX, flipY;
        public SpriteDrawMode drawMode = SpriteDrawMode.Simple;
        public SpriteSortPoint spriteSortPoint = SpriteSortPoint.Center;
        /// <summary>Tiled/Sliced 일 때의 크기(유닛).</summary>
        public Vector2 size = Vector2.one;

        /// <summary>세계에서 차지하는 상자 (회전은 보지 않는다).</summary>
        public Bounds bounds
        {
            get
            {
                if (sprite == null) return new Bounds(transform.position, Vector3.zero);
                var scale = transform.lossyScale;
                float w = sprite.rect.width / sprite.pixelsPerUnit * Mathf.Abs(scale.x), h = sprite.rect.height / sprite.pixelsPerUnit * Mathf.Abs(scale.y);
                float px = sprite.rect.width > 0f ? sprite.pivot.x / sprite.rect.width : 0.5f, py = sprite.rect.height > 0f ? sprite.pivot.y / sprite.rect.height : 0.5f;
                var p = transform.position;
                return new Bounds(new Vector3(p.x + (0.5f - px) * w, p.y + (0.5f - py) * h, p.z), new Vector3(w, h, 0f));
            }
        }

        internal override void CopyStateFrom(Component source)
        {
            base.CopyStateFrom(source);
            var s = (SpriteRenderer)source;
            sprite = s.sprite; color = s.color; flipX = s.flipX; flipY = s.flipY; drawMode = s.drawMode; size = s.size; spriteSortPoint = s.spriteSortPoint;
        }
    }

    public enum RigidbodyType2D { Dynamic, Kinematic, Static }

    /// <summary>
    /// 몸체: 속도를 주면 물리 걸음마다 그만큼 움직인다. 충돌·중력은 없다(이 게임은 충돌을 규칙 코드가 직접 계산한다).
    /// </summary>
    public class Rigidbody2D : Component
    {
        public Vector2 linearVelocity;
        public float gravityScale;
        public bool simulated = true;
        public RigidbodyType2D bodyType = RigidbodyType2D.Dynamic;
        public bool freezeRotation;

        public Rigidbody2D() { SceneRuntime.bodies.Add(this); }

        public Vector2 position
        {
            get => transform.position;
            set => transform.position = new Vector3(value.x, value.y, transform.position.z);
        }

        public void MovePosition(Vector2 target) => position = target;

        internal void Step(float dt)
        {
            if (destroyed || !simulated || bodyType == RigidbodyType2D.Static || !gameObject.activeInHierarchy) return;
            if (linearVelocity.x == 0f && linearVelocity.y == 0f) return;
            position = position + linearVelocity * dt;
        }

        internal override void CopyStateFrom(Component source)
        {
            var s = (Rigidbody2D)source;
            gravityScale = s.gravityScale; simulated = s.simulated; bodyType = s.bodyType; freezeRotation = s.freezeRotation;
        }
    }

    /// <summary>원 충돌체의 수치 (반지름만 쓴다. 겹침 판정은 규칙 코드가 한다).</summary>
    public class Collider2D : Component
    {
        public bool isTrigger;
        public bool enabled = true;
        public Vector2 offset;
    }

    /// <summary>원 충돌체의 수치 (반지름만 쓴다).</summary>
    public class CircleCollider2D : Collider2D
    {
        public float radius = 0.5f;

        internal override void CopyStateFrom(Component source)
        {
            var s = (CircleCollider2D)source;
            radius = s.radius; isTrigger = s.isTrigger; enabled = s.enabled; offset = s.offset;
        }
    }

    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }

    /// <summary>듣는 귀 (표시만).</summary>
    public class AudioListener : Behaviour { }

    /// <summary>카메라의 수치: 규칙 코드가 화면 범위(스폰 링 등)를 재는 데 쓴다. 실제 카메라는 화면 층이 이 값을 보고 맞춘다.</summary>
    public class Camera : Behaviour
    {
        public bool orthographic = true;
        public float orthographicSize = 5f;
        /// <summary>원근 카메라의 세로 화각 (도).</summary>
        public float fieldOfView = 60f;
        public float aspect = 16f / 9f;
        public float nearClipPlane = 0.3f, farClipPlane = 1000f;
        public Color backgroundColor = Color.black;
        public void ResetCullingMatrix() { }
        public CameraClearFlags clearFlags = CameraClearFlags.SolidColor;

        /// <summary>화면 픽셀(왼쪽 아래 0,0) → 세계. 회전하지 않은 직교 카메라 기준 (맵 편집기의 탑다운 카메라).</summary>
        public Vector3 ScreenToWorldPoint(Vector3 screen)
        {
            float h = orthographicSize * 2f, w = h * aspect;
            var p = transform.position;
            return new Vector3(p.x + (screen.x / Mathf.Max(1, Screen.width) - 0.5f) * w, p.y + (screen.y / Mathf.Max(1, Screen.height) - 0.5f) * h, p.z + screen.z);
        }

        /// <summary>세계 → 화면 픽셀(왼쪽 아래 0,0). <see cref="ScreenToWorldPoint"/> 의 역: 회전하지 않은 직교 카메라 기준 (야전의 고정 카메라).</summary>
        public Vector3 WorldToScreenPoint(Vector3 world)
        {
            float h = orthographicSize * 2f, w = h * aspect;
            var p = transform.position;
            return new Vector3(((world.x - p.x) / w + 0.5f) * Screen.width, ((world.y - p.y) / h + 0.5f) * Screen.height, world.z - p.z);
        }

        /// <summary>"MainCamera" 태그가 붙은 켜진 카메라 (없으면 첫 카메라).</summary>
        public static Camera main
        {
            get
            {
                Camera first = null;
                foreach (var cam in Object.FindObjectsByType<Camera>())
                {
                    if (cam.gameObject.tag == "MainCamera") return cam;
                    first ??= cam;
                }
                return first;
            }
        }

        internal override void CopyStateFrom(Component source)
        {
            var s = (Camera)source;
            orthographic = s.orthographic; orthographicSize = s.orthographicSize; aspect = s.aspect; backgroundColor = s.backgroundColor;
        }
    }
}

namespace Samkuk.Compat.Pool
{
    /// <summary>오브젝트 풀 (유니티 UnityEngine.Pool.ObjectPool 자리).</summary>
    public sealed class ObjectPool<T> where T : class
    {
        readonly Stack<T> stack = new Stack<T>();
        readonly Func<T> createFunc;
        readonly Action<T> actionOnGet, actionOnRelease, actionOnDestroy;
        readonly int maxSize;
        readonly bool collectionCheck;

        public int CountAll { get; private set; }
        public int CountInactive => stack.Count;
        public int CountActive => CountAll - CountInactive;

        public ObjectPool(Func<T> createFunc, Action<T> actionOnGet = null, Action<T> actionOnRelease = null, Action<T> actionOnDestroy = null,
            bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
        {
            this.createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            this.actionOnGet = actionOnGet; this.actionOnRelease = actionOnRelease; this.actionOnDestroy = actionOnDestroy;
            this.maxSize = maxSize; this.collectionCheck = collectionCheck;
        }

        public T Get()
        {
            T item;
            if (stack.Count == 0) { item = createFunc(); CountAll++; }
            else item = stack.Pop();
            actionOnGet?.Invoke(item);
            return item;
        }

        public void Release(T item)
        {
            if (collectionCheck && stack.Count > 0 && stack.Contains(item))
                throw new InvalidOperationException("이미 풀에 돌아간 것을 다시 돌려보냈습니다.");
            actionOnRelease?.Invoke(item);
            if (stack.Count < maxSize) stack.Push(item);
            else { CountAll--; actionOnDestroy?.Invoke(item); }
        }

        public void Clear()
        {
            if (actionOnDestroy != null) foreach (var item in stack) actionOnDestroy(item);
            stack.Clear();
            CountAll = 0;
        }

        public void Dispose() => Clear();
    }
}
