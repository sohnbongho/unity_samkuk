using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Samkuk.Compat;
using Samkuk.Data;
using Object = Samkuk.Compat.Object;

// 유니티 에디터 API 의 자리 (테스트 전용). 옮긴 테스트가 인스펙터에서 연결하던 값(private [SerializeField])을 넣고,
// 빌드 설정의 씬 순서를 확인하는 데 쓴다. 게임 코드는 이것을 쓰지 않는다.
namespace Samkuk.Tests
{
    /// <summary>객체의 직렬화 필드를 이름으로 읽고 쓴다 (유니티 SerializedObject 자리, 리플렉션).</summary>
    public sealed class SerializedObject
    {
        public readonly object targetObject;
        public SerializedObject(object target) { targetObject = target; }

        public SerializedProperty FindProperty(string name)
        {
            if (targetObject == null) return null;
            var field = SceneFile.FindField(targetObject.GetType(), name);
            return field != null ? new SerializedProperty(targetObject, field) : null;
        }

        public void Update() { }
        public bool ApplyModifiedProperties() => true;
        public bool ApplyModifiedPropertiesWithoutUndo() => true;
    }

    /// <summary>직렬화 필드 하나 (또는 목록의 한 칸).</summary>
    public sealed class SerializedProperty
    {
        readonly object owner;
        readonly FieldInfo field;
        readonly IList list;
        readonly int index = -1;

        internal SerializedProperty(object owner, FieldInfo field) { this.owner = owner; this.field = field; }
        SerializedProperty(IList list, int index) { this.list = list; this.index = index; }

        Type ValueType => list != null ? ElementType(list.GetType()) : field.FieldType;
        object Value
        {
            get => list != null ? list[index] : field.GetValue(owner);
            set { if (list != null) list[index] = value; else field.SetValue(owner, value); }
        }

        static Type ElementType(Type listType) => listType.IsArray ? listType.GetElementType() : listType.GetGenericArguments()[0];

        public Object objectReferenceValue
        {
            get => Value as Object;
            set
            {
                // 유니티처럼 형식에 맞춰 준다: 컴포넌트 필드에 게임 오브젝트를 넣으면 그 컴포넌트를, 반대면 게임 오브젝트를
                object v = value;
                var type = ValueType;
                if (v != null && !type.IsInstanceOfType(v))
                {
                    var go = v as GameObject ?? (v as Component)?.gameObject;
                    if (go != null) v = type == typeof(GameObject) ? go : (object)go.GetComponent(type);
                }
                Value = v;
            }
        }

        public float floatValue { get => Convert.ToSingle(Value); set => Value = value; }
        public int intValue { get => Convert.ToInt32(Value); set => Value = ValueType.IsEnum ? Enum.ToObject(ValueType, value) : value; }
        public bool boolValue { get => (bool)Value; set => Value = value; }
        public string stringValue { get => (string)Value; set => Value = value; }
        public Color colorValue { get => (Color)Value; set => Value = value; }
        public Vector2 vector2Value { get => (Vector2)Value; set => Value = value; }
        public Vector3 vector3Value { get => (Vector3)Value; set => Value = value; }
        public int enumValueIndex { get => Convert.ToInt32(Value); set => Value = Enum.ToObject(ValueType, value); }
        public bool isArray => Value is IList;

        public int arraySize
        {
            get => Value is IList l ? l.Count : 0;
            set
            {
                var type = field.FieldType;
                var elem = ElementType(type);
                if (type.IsArray)
                {
                    var old = (Array)Value;
                    var grown = Array.CreateInstance(elem, value);
                    int kept = old != null ? Math.Min(old.Length, value) : 0;
                    if (old != null) Array.Copy(old, grown, kept);
                    for (int k = kept; k < value; k++) grown.SetValue(NewElement(elem), k);
                    Value = grown;
                    return;
                }
                var l = (IList)Value ?? (IList)Activator.CreateInstance(type);
                while (l.Count > value) l.RemoveAt(l.Count - 1);
                while (l.Count < value) l.Add(NewElement(elem));
                Value = l;
            }
        }

        // 유니티처럼: 묶음 클래스의 칸은 빈 객체로, 참조·글자는 비워서
        static object NewElement(Type elem)
        {
            if (elem.IsValueType) return Activator.CreateInstance(elem);
            if (elem == typeof(string) || typeof(Object).IsAssignableFrom(elem) || elem.IsInterface || elem.IsAbstract) return null;
            return Activator.CreateInstance(elem);
        }

        public SerializedProperty GetArrayElementAtIndex(int i) => new SerializedProperty((IList)Value, i);
        public void InsertArrayElementAtIndex(int i)
        {
            if (field.FieldType.IsArray) { arraySize = arraySize + 1; return; }
            var l = (IList)Value;
            var elem = ElementType(field.FieldType);
            l.Insert(Math.Min(i, l.Count), NewElement(elem));
        }
        public void ClearArray() => arraySize = 0;

        /// <summary>묶음 클래스 안의 필드.</summary>
        public SerializedProperty FindPropertyRelative(string name)
        {
            object v = Value;
            if (v == null) return null;
            var f = SceneFile.FindField(v.GetType(), name);
            return f != null ? new SerializedProperty(v, f) : null;
        }
    }

    /// <summary>빌드 설정의 씬 목록 (유니티 EditorBuildSettings 자리). godot/data/project.json 의 씬 순서에서 만든다.</summary>
    public static class EditorBuildSettings
    {
        public sealed class EditorBuildSettingsScene
        {
            public string path;
            public bool enabled = true;
        }

        public static EditorBuildSettingsScene[] scenes
        {
            get
            {
                AssetDatabase.EnsureLoaded();
                var list = new List<EditorBuildSettingsScene>();
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                    list.Add(new EditorBuildSettingsScene { path = "Assets/Scenes/" + SceneManager.SceneNameAt(i) + ".unity" });
                return list.ToArray();
            }
        }
    }
}
