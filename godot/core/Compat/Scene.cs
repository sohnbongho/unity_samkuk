using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Samkuk.Compat
{
    // 장면 모형 (Godot 전환 G3, ADR 0004): 게임 규칙이 든 컴포넌트(PlayerStats, EnemyManager, 무기...)를 엔진 없이 돌리는 작은 오브젝트 모델.
    // 유니티와 같은 이름·같은 수명 주기(Awake → OnEnable → Start → FixedUpdate/Update/LateUpdate → OnDisable → OnDestroy)를 흉내 내어
    // 원본 코드와 테스트를 그대로 옮긴다. 그리지는 않는다: SpriteRenderer 는 "무엇을 어디에 그릴지"만 들고, 화면 층(Godot)이 매 프레임 그것을 비춘다.
    // 시간은 SceneRuntime.Tick(dt) 로만 흐르므로 테스트는 원하는 만큼 빨리 감는다.

    /// <summary>
    /// 장면 객체의 바탕. 유니티처럼 파괴된 객체는 null 과 같다고 본다(<c>==</c>, <c>!=</c>, bool 변환).
    /// </summary>
    public class Object
    {
        public HideFlags hideFlags;
        internal bool destroyed;

        public static bool operator ==(Object a, Object b)
        {
            bool an = a is null || a.destroyed, bn = b is null || b.destroyed;
            if (an || bn) return an && bn;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public static implicit operator bool(Object o) => !(o is null) && !o.destroyed;
        public override bool Equals(object obj) => obj is Object o ? this == o : obj is null && destroyed;
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        /// <summary>프레임 끝에 없앤다 (delay 초 뒤).</summary>
        public static void Destroy(Object o, float delay = 0f) => SceneRuntime.QueueDestroy(o, delay);
        /// <summary>바로 없앤다.</summary>
        public static void DestroyImmediate(Object o) => SceneRuntime.DestroyNow(o);
        public static void DontDestroyOnLoad(Object o)
        {
            var go = o as GameObject ?? (o as Component)?.gameObject;
            if (go != null) go.transform.root.gameObject.persistent = true;
        }

        public static T Instantiate<T>(T original) where T : Object => (T)SceneRuntime.Clone(original, null, false, default, default);
        public static T Instantiate<T>(T original, Transform parent) where T : Object => (T)SceneRuntime.Clone(original, parent, false, default, default);
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object => (T)SceneRuntime.Clone(original, null, true, position, rotation);
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object => (T)SceneRuntime.Clone(original, parent, true, position, rotation);

        public static T FindAnyObjectByType<T>() where T : class => SceneRuntime.Find<T>(false);
        public static T FindFirstObjectByType<T>() where T : class => SceneRuntime.Find<T>(false);
        public static T FindAnyObjectByType<T>(FindObjectsInactive inactive) where T : class => SceneRuntime.Find<T>(inactive == FindObjectsInactive.Include);
        public static T[] FindObjectsByType<T>(FindObjectsSortMode sort = FindObjectsSortMode.None) where T : class => SceneRuntime.FindAll<T>(false);
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort = FindObjectsSortMode.None) where T : class => SceneRuntime.FindAll<T>(inactive == FindObjectsInactive.Include);
    }

    public enum FindObjectsSortMode { None, InstanceID }
    public enum FindObjectsInactive { Exclude, Include }

    /// <summary>게임 오브젝트: 이름, 켜짐/꺼짐, 트랜스폼, 컴포넌트 목록.</summary>
    public sealed class GameObject : Object
    {
        public string name;
        public int layer;
        public string tag = "Untagged";
        public Transform transform { get; }
        internal readonly List<Component> components = new List<Component>();
        internal bool active = true, prefab, persistent;

        public GameObject() : this("GameObject") { }
        public GameObject(string name, params Type[] componentTypes) : this(name, false, componentTypes) { }

        GameObject(string name, bool prefab, Type[] componentTypes)
        {
            this.name = name;
            this.prefab = prefab;
            transform = new RectTransform { gameObject = this };   // 모든 트랜스폼이 사각 배치 값도 든다 (UI 로 쓰면 그 값을 본다)
            components.Add(transform);
            if (!prefab) SceneRuntime.Register(this);
            if (componentTypes != null) foreach (var t in componentTypes) AddComponent(t);
        }

        /// <summary>
        /// 프리팹(원형): 장면에 있지 않아 수명 주기가 돌지 않고 찾기에도 안 잡힌다. <see cref="Object.Instantiate{T}(T)"/> 로 복제해 쓴다.
        /// 유니티의 프리팹 에셋 자리이며 코드로 조립한다.
        /// </summary>
        /// <summary>이름으로 켜진 오브젝트를 찾는다 (없으면 null).</summary>
        public static GameObject Find(string name)
        {
            foreach (var go in SceneRuntime.Objects) if (go.name == name && go.activeInHierarchy) return go;
            return null;
        }

        public static GameObject CreatePrefab(string name, params Type[] componentTypes) => new GameObject(name, true, componentTypes);

        internal bool IsPrefab => transform.root.gameObject.prefab;
        public bool activeSelf => active;
        public bool activeInHierarchy
        {
            get
            {
                if (destroyed || IsPrefab) return false;
                for (var t = transform; t != null; t = t.parent) if (!t.gameObject.active) return false;
                return true;
            }
        }

        public void SetActive(bool value)
        {
            if (active == value) return;
            bool before = activeInHierarchy;
            active = value;
            bool after = activeInHierarchy;
            if (before != after) SceneRuntime.HierarchyActiveChanged(this, after);
        }

        public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));

        public Component AddComponent(Type type)
        {
            if (type == typeof(Transform)) return transform;
            foreach (var req in type.GetCustomAttributes<RequireComponent>(true))
                foreach (var t in req.types)
                    if (t != null && GetComponent(t) == null) AddComponent(t);

            var c = (Component)Activator.CreateInstance(type);
            c.gameObject = this;
            components.Add(c);
            if (c is MonoBehaviour mb)
            {
                SceneRuntime.Track(mb);
                if (activeInHierarchy && !SceneRuntime.Building) mb.WakeUp();
            }
            return c;
        }

        public T GetComponent<T>() where T : class
        {
            for (int i = 0; i < components.Count; i++) if (components[i] is T t && !components[i].destroyed) return t;
            return null;
        }
        public Component GetComponent(Type type)
        {
            for (int i = 0; i < components.Count; i++) if (type.IsInstanceOfType(components[i]) && !components[i].destroyed) return components[i];
            return null;
        }
        public bool TryGetComponent<T>(out T component) where T : class => (component = GetComponent<T>()) != null;

        public T GetComponentInChildren<T>(bool includeInactive = false) where T : class
        {
            if (includeInactive || active) { var own = GetComponent<T>(); if (own != null) return own; }
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (!includeInactive && !child.active) continue;
                var found = child.GetComponentInChildren<T>(includeInactive);
                if (found != null) return found;
            }
            return null;
        }
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : class
        {
            var list = new List<T>();
            Collect(this, includeInactive, list);
            return list.ToArray();
        }
        static void Collect<T>(GameObject go, bool includeInactive, List<T> list) where T : class
        {
            if (!includeInactive && !go.active) return;
            foreach (var c in go.components) if (c is T t && !c.destroyed) list.Add(t);
            for (int i = 0; i < go.transform.childCount; i++) Collect(go.transform.GetChild(i).gameObject, includeInactive, list);
        }
        public T[] GetComponents<T>() where T : class
        {
            var list = new List<T>();
            foreach (var c in components) if (c is T t && !c.destroyed) list.Add(t);
            return list.ToArray();
        }
        public T GetComponentInParent<T>() where T : class
        {
            for (var t = transform; t != null; t = t.parent) { var c = t.gameObject.GetComponent<T>(); if (c != null) return c; }
            return null;
        }
        public bool CompareTag(string other) => tag == other;
        public override string ToString() => name;
    }

    /// <summary>게임 오브젝트에 붙는 것의 바탕.</summary>
    public class Component : Object
    {
        public GameObject gameObject { get; internal set; }
        public Transform transform => gameObject.transform;
        public string name { get => gameObject.name; set => gameObject.name = value; }
        public string tag { get => gameObject.tag; set => gameObject.tag = value; }

        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
        public Component GetComponent(Type type) => gameObject.GetComponent(type);
        public bool TryGetComponent<T>(out T component) where T : class => gameObject.TryGetComponent(out component);
        public T GetComponentInChildren<T>(bool includeInactive = false) where T : class => gameObject.GetComponentInChildren<T>(includeInactive);
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : class => gameObject.GetComponentsInChildren<T>(includeInactive);
        public T[] GetComponents<T>() where T : class => gameObject.GetComponents<T>();
        public T GetComponentInParent<T>() where T : class => gameObject.GetComponentInParent<T>();
        public bool CompareTag(string other) => gameObject.CompareTag(other);
        /// <summary>이 오브젝트의 컴포넌트들에서 그 이름의 메서드를 찾아 부른다 (테스트가 Awake 같은 것을 직접 부를 때).</summary>
        public void SendMessage(string method)
        {
            foreach (var c in gameObject.components.ToArray())
                for (var t = c.GetType(); t != null && t != typeof(Component); t = t.BaseType)
                {
                    var m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                    if (m != null) { m.Invoke(c, null); break; }
                }
        }

        /// <summary>복제할 때 public 필드로 옮겨지지 않는 상태를 옮긴다 (내장 컴포넌트용).</summary>
        internal virtual void CopyStateFrom(Component source) { }
        public override string ToString() => $"{gameObject?.name} ({GetType().Name})";
    }

    /// <summary>켜고 끌 수 있는 컴포넌트.</summary>
    public class Behaviour : Component
    {
        internal bool enabledFlag = true;
        public virtual bool enabled
        {
            get => enabledFlag;
            set => enabledFlag = value;
        }
        public bool isActiveAndEnabled => enabledFlag && gameObject.activeInHierarchy;
    }

    /// <summary>
    /// 규칙 컴포넌트의 바탕. 수명 주기 메서드(Awake, OnEnable, Start, FixedUpdate, Update, LateUpdate, OnDisable, OnDestroy)는
    /// 유니티처럼 이름으로 찾아 부른다(private 이어도 된다).
    /// </summary>
    public class MonoBehaviour : Behaviour
    {
        internal bool awoken, started, enabledCalled;
        internal Action awake, onEnable, start, fixedUpdate, update, lateUpdate, onDisable, onDestroy;
        bool bound;

        public override bool enabled
        {
            get => enabledFlag;
            set
            {
                if (enabledFlag == value) return;
                enabledFlag = value;
                if (!awoken || !gameObject.activeInHierarchy) return;
                if (value) CallEnable(); else CallDisable();
            }
        }

        internal void Bind()
        {
            if (bound) return;
            bound = true;
            awake = Find("Awake"); onEnable = Find("OnEnable"); start = Find("Start");
            fixedUpdate = Find("FixedUpdate"); update = Find("Update"); lateUpdate = Find("LateUpdate");
            onDisable = Find("OnDisable"); onDestroy = Find("OnDestroy");
        }

        Action Find(string method)
        {
            for (var t = GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                var m = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (m != null && m.ReturnType == typeof(void)) return (Action)Delegate.CreateDelegate(typeof(Action), this, m);
            }
            return null;
        }

        /// <summary>켜진 계층에 처음 들어왔을 때: Awake, 이어서 OnEnable.</summary>
        internal void WakeUp()
        {
            if (destroyed) return;
            Bind();
            if (!awoken)
            {
                awoken = true;
                awake?.Invoke();
            }
            if (enabledFlag && !destroyed && gameObject.activeInHierarchy) CallEnable();
        }

        internal void CallEnable()
        {
            if (enabledCalled) return;
            enabledCalled = true;
            onEnable?.Invoke();
        }

        internal void CallDisable()
        {
            if (!enabledCalled) return;
            enabledCalled = false;
            onDisable?.Invoke();
        }

        internal bool Running => !destroyed && awoken && enabledFlag && enabledCalled;
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute
    {
        public readonly Type[] types;
        public RequireComponent(Type a) { types = new[] { a }; }
        public RequireComponent(Type a, Type b) { types = new[] { a, b }; }
        public RequireComponent(Type a, Type b, Type c) { types = new[] { a, b, c }; }
    }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class AddComponentMenu : Attribute { public AddComponentMenu(string menu) { } }

    /// <summary>위치·회전·크기와 부모/자식. 좌표계는 로직의 것(x 오른쪽, y 북쪽, 땅 z = 0, 위 = -z).</summary>
    public class Transform : Component, IEnumerable
    {
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
        Transform parentRef;
        readonly List<Transform> children = new List<Transform>();

        public Transform parent
        {
            get => parentRef;
            set => SetParent(value, true);
        }

        public Transform root
        {
            get { var t = this; while (t.parentRef != null) t = t.parentRef; return t; }
        }

        public int childCount => children.Count;
        public Transform GetChild(int index) => children[index];
        public IEnumerator GetEnumerator() => children.ToArray().GetEnumerator();

        public void SetParent(Transform newParent, bool worldPositionStays = true)
        {
            if (parentRef == newParent) return;
            bool before = gameObject.activeInHierarchy;
            Vector3 worldPos = position;
            Quaternion worldRot = rotation;
            Vector3 worldScale = lossyScale;

            parentRef?.children.Remove(this);
            bool wasRoot = parentRef == null;
            parentRef = newParent;
            newParent?.children.Add(this);
            if (wasRoot && newParent != null) SceneRuntime.RootChanged(gameObject, false);
            else if (!wasRoot && newParent == null) SceneRuntime.RootChanged(gameObject, true);

            if (worldPositionStays)
            {
                position = worldPos;
                rotation = worldRot;
                if (newParent != null)
                {
                    var ps = newParent.lossyScale;
                    localScale = new Vector3(Div(worldScale.x, ps.x), Div(worldScale.y, ps.y), Div(worldScale.z, ps.z));
                }
                else localScale = worldScale;
            }

            bool after = gameObject.activeInHierarchy;
            if (before != after) SceneRuntime.HierarchyActiveChanged(gameObject, after);
        }

        static float Div(float a, float b) => Mathf.Abs(b) < 1e-9f ? 0f : a / b;

        public Vector3 lossyScale
        {
            get
            {
                var s = localScale;
                for (var t = parentRef; t != null; t = t.parentRef) s = new Vector3(s.x * t.localScale.x, s.y * t.localScale.y, s.z * t.localScale.z);
                return s;
            }
        }

        public Quaternion rotation
        {
            get => parentRef == null ? localRotation : parentRef.rotation * localRotation;
            set => localRotation = parentRef == null ? value : Quaternion.Inverse(parentRef.rotation) * value;
        }

        public Vector3 position
        {
            get => parentRef == null ? localPosition : parentRef.TransformPoint(localPosition);
            set => localPosition = parentRef == null ? value : parentRef.InverseTransformPoint(value);
        }

        public Vector3 eulerAngles { get => rotation.eulerAngles; set => rotation = Quaternion.Euler(value.x, value.y, value.z); }
        public Vector3 localEulerAngles { get => localRotation.eulerAngles; set => localRotation = Quaternion.Euler(value.x, value.y, value.z); }
        public Vector3 right => rotation * Vector3.right;
        public Vector3 up => rotation * Vector3.up;
        public Vector3 forward => rotation * Vector3.forward;

        public Vector3 TransformPoint(Vector3 local)
        {
            var s = localScale;
            var p = localRotation * new Vector3(local.x * s.x, local.y * s.y, local.z * s.z) + localPosition;
            return parentRef == null ? p : parentRef.TransformPoint(p);
        }

        public Vector3 InverseTransformPoint(Vector3 world)
        {
            var p = parentRef == null ? world : parentRef.InverseTransformPoint(world);
            p = Quaternion.Inverse(localRotation) * (p - localPosition);
            var s = localScale;
            return new Vector3(Div(p.x, s.x), Div(p.y, s.y), Div(p.z, s.z));
        }

        public void SetPositionAndRotation(Vector3 pos, Quaternion rot) { position = pos; rotation = rot; }
        public void SetLocalPositionAndRotation(Vector3 pos, Quaternion rot) { localPosition = pos; localRotation = rot; }
        public void Translate(Vector3 delta) => position += delta;
        public void Rotate(float x, float y, float z) => localRotation = localRotation * Quaternion.Euler(x, y, z);

        public Transform Find(string path)
        {
            int slash = path.IndexOf('/');
            string head = slash < 0 ? path : path.Substring(0, slash);
            foreach (var c in children)
                if (c.gameObject.name == head) return slash < 0 ? c : c.Find(path.Substring(slash + 1));
            return null;
        }

        public void SetAsLastSibling()
        {
            if (parentRef == null) return;
            parentRef.children.Remove(this);
            parentRef.children.Add(this);
        }
        public void SetAsFirstSibling()
        {
            if (parentRef == null) return;
            parentRef.children.Remove(this);
            parentRef.children.Insert(0, this);
        }
        public int GetSiblingIndex() => parentRef == null ? 0 : parentRef.children.IndexOf(this);

        internal void DetachForDestroy()
        {
            parentRef?.children.Remove(this);
            parentRef = null;
        }

        internal override void CopyStateFrom(Component source)
        {
            var s = (Transform)source;
            localPosition = s.localPosition; localRotation = s.localRotation; localScale = s.localScale;
        }
    }

    /// <summary>회전 (유니티와 같은 왼손 좌표계·오일러 순서 Z → X → Y).</summary>
    [Serializable]
    public struct Quaternion : IEquatable<Quaternion>
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

        public static Quaternion Euler(float xDeg, float yDeg, float zDeg)
        {
            float hx = xDeg * Mathf.Deg2Rad * 0.5f, hy = yDeg * Mathf.Deg2Rad * 0.5f, hz = zDeg * Mathf.Deg2Rad * 0.5f;
            var qx = new Quaternion(Mathf.Sin(hx), 0f, 0f, Mathf.Cos(hx));
            var qy = new Quaternion(0f, Mathf.Sin(hy), 0f, Mathf.Cos(hy));
            var qz = new Quaternion(0f, 0f, Mathf.Sin(hz), Mathf.Cos(hz));
            return qy * qx * qz;
        }
        public static Quaternion Euler(Vector3 euler) => Euler(euler.x, euler.y, euler.z);

        public static Quaternion AngleAxis(float degrees, Vector3 axis)
        {
            var n = axis.normalized;
            float h = degrees * Mathf.Deg2Rad * 0.5f, s = Mathf.Sin(h);
            return new Quaternion(n.x * s, n.y * s, n.z * s, Mathf.Cos(h));
        }

        public static Quaternion Inverse(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);
        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        public static float Angle(Quaternion a, Quaternion b) => Mathf.Acos(Mathf.Min(Mathf.Abs(Dot(a, b)), 1f)) * 2f * Mathf.Rad2Deg;

        /// <summary>forward 를 바라보고 위쪽이 up 에 가깝게 되는 회전.</summary>
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            var f = forward.normalized;
            if (f.sqrMagnitude < 1e-10f) return identity;
            var r = Vector3.Cross(up, f).normalized;
            if (r.sqrMagnitude < 1e-10f) r = Vector3.Cross(Mathf.Abs(f.y) < 0.99f ? Vector3.up : Vector3.right, f).normalized;
            var u = Vector3.Cross(f, r);
            // 회전 행렬(열 = r, u, f)을 사원수로
            float m00 = r.x, m01 = u.x, m02 = f.x, m10 = r.y, m11 = u.y, m12 = f.y, m20 = r.z, m21 = u.z, m22 = f.z;
            float trace = m00 + m11 + m22;
            if (trace > 0f)
            {
                float s = Mathf.Sqrt(trace + 1f) * 2f;
                return new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }
            if (m00 > m11 && m00 > m22)
            {
                float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
                return new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            if (m11 > m22)
            {
                float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
                return new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            {
                float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
                return new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s);
            }
        }
        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);

        public Vector3 eulerAngles
        {
            get
            {
                // q = qy * qx * qz 를 푼다
                float sinX = 2f * (w * x - y * z);
                float ex, ey, ez;
                if (Mathf.Abs(sinX) > 0.9999f)
                {
                    ex = Mathf.Sign(sinX) * 90f;
                    ey = Mathf.Atan2(2f * (w * y - x * z), 1f - 2f * (y * y + z * z)) * Mathf.Rad2Deg;
                    ez = 0f;
                }
                else
                {
                    ex = Mathf.Asin(sinX) * Mathf.Rad2Deg;
                    ey = Mathf.Atan2(2f * (x * z + w * y), 1f - 2f * (x * x + y * y)) * Mathf.Rad2Deg;
                    ez = Mathf.Atan2(2f * (x * y + w * z), 1f - 2f * (x * x + z * z)) * Mathf.Rad2Deg;
                }
                return new Vector3(Wrap(ex), Wrap(ey), Wrap(ez));
            }
        }

        static float Wrap(float deg)
        {
            deg %= 360f;
            if (deg < 0f) deg += 360f;
            return Mathf.Abs(deg - 360f) < 1e-4f ? 0f : deg;
        }

        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float x2 = q.x * 2f, y2 = q.y * 2f, z2 = q.z * 2f;
            float xx = q.x * x2, yy = q.y * y2, zz = q.z * z2, xy = q.x * y2, xz = q.x * z2, yz = q.y * z2, wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;
            return new Vector3(
                (1f - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z,
                (xy + wz) * v.x + (1f - (xx + zz)) * v.y + (yz - wx) * v.z,
                (xz - wy) * v.x + (yz + wx) * v.y + (1f - (xx + yy)) * v.z);
        }

        public static bool operator ==(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w > 0.999999f;
        public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);
        public bool Equals(Quaternion o) => x == o.x && y == o.y && z == o.z && w == o.w;
        public override bool Equals(object obj) => obj is Quaternion q && Equals(q);
        public override int GetHashCode() => HashCode.Combine(x, y, z, w);
        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3}, {w:F3})";
    }

    /// <summary>게임 시간. <see cref="SceneRuntime.Tick"/> 이 채운다.</summary>
    public static class Time
    {
        public static float timeScale = 1f;
        public static float fixedDeltaTime = 0.02f;
        public static float deltaTime { get; internal set; }
        public static float unscaledDeltaTime { get; internal set; }
        public static float time { get; internal set; }
        public static float unscaledTime { get; internal set; }
        public static float fixedTime { get; internal set; }
        public static int frameCount { get; internal set; }
        public static float realtimeSinceStartup => unscaledTime;
    }

    /// <summary>
    /// 장면 모형을 굴리는 곳: 오브젝트 등록, 수명 주기, 프레임(<see cref="Tick"/>), 파괴, 복제, 찾기.
    /// 게임은 Godot 의 _Process 에서 Tick 을 부르고, 테스트는 [UnityTest] 의 yield 마다 부른다.
    /// </summary>
    public static class SceneRuntime
    {
        static readonly List<GameObject> objects = new List<GameObject>();        // 장면의 모든 오브젝트 (프리팹 제외), 만든 순서
        static readonly List<MonoBehaviour> behaviours = new List<MonoBehaviour>();   // 만든 순서 = 실행 순서
        static readonly List<MonoBehaviour> buffer = new List<MonoBehaviour>();
        static readonly List<(Object target, float at)> destroyQueue = new List<(Object, float)>();
        static float fixedAccumulator;
        static int building;

        /// <summary>복제 중: 컴포넌트를 다 붙인 뒤 한꺼번에 깨운다.</summary>
        internal static bool Building => building > 0;
        /// <summary>씬·프리팹 파일을 읽는 동안: 오브젝트를 다 만든 뒤 한꺼번에 깨우도록 수명 주기를 잠시 멈춘다.</summary>
        internal static void BeginBuild() => building++;
        internal static void EndBuild() => building--;
        /// <summary>장면의 모든 오브젝트 (화면 층이 비춘다). 순회 중 고치지 않는다.</summary>
        public static IReadOnlyList<GameObject> Objects => objects;
        /// <summary>오브젝트가 생기고 사라질 때 (화면 층이 노드를 만들고 치운다).</summary>
        public static event Action<GameObject> ObjectCreated, ObjectDestroyed;
        /// <summary>물리 한 걸음 뒤 (Rigidbody2D 를 움직인다).</summary>
        internal static readonly List<Rigidbody2D> bodies = new List<Rigidbody2D>();

        internal static void Register(GameObject go)
        {
            objects.Add(go);
            ObjectCreated?.Invoke(go);
        }

        internal static void RootChanged(GameObject go, bool isRootNow) { }

        internal static void Track(MonoBehaviour mb) => behaviours.Add(mb);

        internal static void HierarchyActiveChanged(GameObject go, bool nowActive)
        {
            if (go.IsPrefab) return;
            var list = new List<MonoBehaviour>();
            CollectBehaviours(go, list, onlyActiveSelf: true, skipRootCheck: true);
            foreach (var mb in list)
            {
                if (mb.destroyed) continue;
                if (nowActive) { if (mb.gameObject.activeInHierarchy) mb.WakeUp(); }
                else if (mb.awoken && mb.enabledFlag) mb.CallDisable();
            }
        }

        static void CollectBehaviours(GameObject go, List<MonoBehaviour> list, bool onlyActiveSelf, bool skipRootCheck)
        {
            if (onlyActiveSelf && !skipRootCheck && !go.active) return;
            foreach (var c in go.components) if (c is MonoBehaviour mb) list.Add(mb);
            for (int i = 0; i < go.transform.childCount; i++) CollectBehaviours(go.transform.GetChild(i).gameObject, list, onlyActiveSelf, false);
        }

        /// <summary>한 프레임. dt 는 실제 흐른 시간(초)이고 게임 시간은 timeScale 을 곱한다.</summary>
        public static void Tick(float dt)
        {
            Time.unscaledDeltaTime = dt;
            Time.unscaledTime += dt;
            Time.deltaTime = dt * Time.timeScale;
            Time.time += Time.deltaTime;
            Time.frameCount++;

            RunStarts();

            fixedAccumulator += Time.deltaTime;
            int steps = 0;
            while (fixedAccumulator >= Time.fixedDeltaTime - 1e-6f && steps < 8)
            {
                fixedAccumulator -= Time.fixedDeltaTime;
                Time.fixedTime += Time.fixedDeltaTime;
                steps++;
                Snapshot();
                foreach (var mb in buffer) if (mb.Running && mb.fixedUpdate != null) mb.fixedUpdate();
                for (int i = 0; i < bodies.Count; i++) bodies[i].Step(Time.fixedDeltaTime);
            }
            if (steps == 8) fixedAccumulator = 0f;   // 너무 밀리면 버린다

            Snapshot();
            foreach (var mb in buffer) if (mb.Running && mb.update != null) { EnsureStarted(mb); mb.update(); }
            Snapshot();
            foreach (var mb in buffer) if (mb.Running && mb.lateUpdate != null) { EnsureStarted(mb); mb.lateUpdate(); }

            FlushDestroy();
            InputSystem.InputState.EndFrame();
        }

        /// <summary>물리 한 걸음만 (테스트의 WaitForFixedUpdate).</summary>
        public static void FixedStep()
        {
            RunStarts();
            Time.fixedTime += Time.fixedDeltaTime;
            Snapshot();
            foreach (var mb in buffer) if (mb.Running && mb.fixedUpdate != null) mb.fixedUpdate();
            for (int i = 0; i < bodies.Count; i++) bodies[i].Step(Time.fixedDeltaTime);
        }

        static void Snapshot()
        {
            buffer.Clear();
            buffer.AddRange(behaviours);
        }

        static void RunStarts()
        {
            Snapshot();
            foreach (var mb in buffer) EnsureStarted(mb);
        }

        static void EnsureStarted(MonoBehaviour mb)
        {
            if (mb.started || !mb.Running) return;
            mb.started = true;
            mb.start?.Invoke();
        }

        /// <summary>프레임 끝 처리만 한다: Destroy 로 예약된 것을 없앤다 (테스트 사이에 유니티가 프레임을 넘기며 하던 일).</summary>
        public static void EndFrame() => FlushDestroy();

        internal static void QueueDestroy(Object o, float delay)
        {
            if (o is null || o.destroyed) return;
            destroyQueue.Add((o, Time.time + delay));
        }

        static void FlushDestroy()
        {
            for (int i = 0; i < destroyQueue.Count; i++)
            {
                if (destroyQueue[i].at > Time.time) continue;
                var target = destroyQueue[i].target;
                destroyQueue.RemoveAt(i--);
                DestroyNow(target);
            }
        }

        internal static void DestroyNow(Object o)
        {
            if (o is null || o.destroyed) return;
            if (o is GameObject go) DestroyObject(go, true);
            else if (o is Transform t) DestroyObject(t.gameObject, true);
            else if (o is Component c)
            {
                if (c is MonoBehaviour mb) Kill(mb, c.gameObject.activeInHierarchy);
                c.destroyed = true;
                c.gameObject.components.Remove(c);
                if (c is Rigidbody2D rb) bodies.Remove(rb);
            }
            else o.destroyed = true;   // 데이터·그림 손잡이: 표시만
        }

        static void DestroyObject(GameObject go, bool detach)
        {
            if (go.destroyed) return;
            bool wasActive = go.activeInHierarchy;
            for (int i = go.transform.childCount - 1; i >= 0; i--) DestroyObject(go.transform.GetChild(i).gameObject, false);
            foreach (var c in go.components.ToArray())
            {
                if (c is MonoBehaviour mb) Kill(mb, wasActive);
                if (c is Rigidbody2D rb) bodies.Remove(rb);
                c.destroyed = true;
            }
            go.destroyed = true;
            if (detach) go.transform.DetachForDestroy();
            if (!go.prefab && objects.Remove(go)) ObjectDestroyed?.Invoke(go);
        }

        static void Kill(MonoBehaviour mb, bool wasActive)
        {
            if (mb.destroyed) return;
            if (mb.awoken)
            {
                if (wasActive) mb.CallDisable();
                mb.onDestroy?.Invoke();   // 유니티처럼 Awake 가 불린 것만
            }
            mb.destroyed = true;
            behaviours.Remove(mb);
        }

        internal static T Find<T>(bool includeInactive) where T : class
        {
            for (int i = 0; i < objects.Count; i++)
            {
                var go = objects[i];
                if (!includeInactive && !go.activeInHierarchy) continue;
                foreach (var c in go.components)
                    if (c is T t && !c.destroyed && (includeInactive || !(c is Behaviour b) || b.enabled)) return t;
            }
            return null;
        }

        internal static T[] FindAll<T>(bool includeInactive) where T : class
        {
            var list = new List<T>();
            for (int i = 0; i < objects.Count; i++)
            {
                var go = objects[i];
                if (!includeInactive && !go.activeInHierarchy) continue;
                foreach (var c in go.components)
                    if (c is T t && !c.destroyed && (includeInactive || !(c is Behaviour b) || b.enabled)) list.Add(t);
            }
            return list.ToArray();
        }

        /// <summary>
        /// 장면을 비운다 (씬 전환). <see cref="Object.DontDestroyOnLoad"/> 로 표시한 것은 남긴다. everything 이면 그것까지 (테스트 정리).
        /// </summary>
        public static void Clear(bool everything = false)
        {
            foreach (var go in objects.ToArray())
            {
                if (go.destroyed || go.transform.parent != null) continue;
                if (!everything && go.persistent) continue;
                DestroyObject(go, true);
            }
            destroyQueue.RemoveAll(d => d.target.destroyed);
            if (everything)
            {
                fixedAccumulator = 0f;
                Time.timeScale = 1f;
            }
        }

        // ---- 복제 (프리팹 → 장면) ----

        internal static Object Clone(Object original, Transform parent, bool place, Vector3 position, Quaternion rotation)
        {
            if (original is null) throw new ArgumentNullException(nameof(original), "복제할 원본이 없습니다");
            var sourceGo = original as GameObject ?? ((Component)original).gameObject;
            var map = new Dictionary<Object, Object>();
            building++;
            GameObject copy;
            try
            {
                copy = CloneObject(sourceGo, map);
                CopyFields(sourceGo, map);
                if (parent != null) copy.transform.SetParent(parent, false);
                if (place) { copy.transform.position = position; copy.transform.rotation = rotation; }
            }
            finally { building--; }

            lastCloneMap = map;
            // 다 붙인 뒤 깨운다 (유니티: 복제된 계층 전체가 준비된 다음 Awake). 씬 파일을 읽는 중이면 읽기가 끝나고 깨운다
            if (copy.activeInHierarchy && !Building)
            {
                var list = new List<MonoBehaviour>();
                CollectBehaviours(copy, list, onlyActiveSelf: true, skipRootCheck: true);
                foreach (var mb in list) if (mb.gameObject.activeInHierarchy) mb.WakeUp();
            }
            return original is GameObject ? copy : (Object)map[original];
        }

        static Dictionary<Object, Object> lastCloneMap;

        /// <summary>복제하고 "원본 객체 → 복제본 객체" 표도 돌려준다 (씬의 프리팹 인스턴스가 고친 값을 얹을 때 쓴다).</summary>
        internal static GameObject CloneWithMap(GameObject original, out Dictionary<Object, Object> map)
        {
            var copy = (GameObject)Clone(original, null, false, default, default);
            map = lastCloneMap;
            return copy;
        }

        static GameObject CloneObject(GameObject source, Dictionary<Object, Object> map)
        {
            var copy = new GameObject(source.name + "(Clone)") { layer = source.layer, tag = source.tag, active = source.active };
            map[source] = copy;
            map[source.transform] = copy.transform;
            copy.transform.CopyStateFrom(source.transform);
            foreach (var c in source.components)
            {
                if (c is Transform) continue;
                var existing = FindUnmapped(copy, c.GetType(), map);   // RequireComponent 로 이미 붙은 것
                var cc = existing ?? copy.AddComponent(c.GetType());
                map[c] = cc;
            }
            for (int i = 0; i < source.transform.childCount; i++)
            {
                var child = CloneObject(source.transform.GetChild(i).gameObject, map);
                child.transform.SetParent(copy.transform, false);
            }
            return copy;
        }

        static Component FindUnmapped(GameObject copy, Type type, Dictionary<Object, Object> map)
        {
            foreach (var c in copy.components)
                if (c.GetType() == type && !map.ContainsValue(c)) return c;
            return null;
        }

        static void CopyFields(GameObject source, Dictionary<Object, Object> map)
        {
            foreach (var c in source.components)
            {
                if (c is Transform) continue;
                var target = (Component)map[c];
                target.CopyStateFrom(c);
                if (c is Behaviour b) ((Behaviour)target).enabledFlag = b.enabledFlag;
                for (var t = c.GetType(); t != null && t != typeof(Component) && t != typeof(Object); t = t.BaseType)
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (f.IsInitOnly || f.IsLiteral || f.IsDefined(typeof(NonSerializedAttribute))) continue;
                        if (!f.IsPublic && !f.IsDefined(typeof(SerializeFieldAttribute))) continue;   // 유니티처럼 직렬화되는 필드만 옮긴다
                        f.SetValue(target, Remap(f.GetValue(c), map));
                    }
            }
            for (int i = 0; i < source.transform.childCount; i++) CopyFields(source.transform.GetChild(i).gameObject, map);
        }

        static object Remap(object value, Dictionary<Object, Object> map)
        {
            if (value is Object o) return map.TryGetValue(o, out var mapped) ? mapped : o;   // 계층 안의 참조는 복제본으로, 밖(데이터·그림)은 그대로
            if (value is IList list && value.GetType().IsGenericType)
            {
                var copy = (IList)Activator.CreateInstance(value.GetType());
                foreach (var item in list) copy.Add(Remap(item, map));
                return copy;
            }
            if (value is Array array)
            {
                var copy = (Array)array.Clone();
                for (int i = 0; i < copy.Length; i++) copy.SetValue(Remap(copy.GetValue(i), map), i);
                return copy;
            }
            return value;
        }
    }
}
