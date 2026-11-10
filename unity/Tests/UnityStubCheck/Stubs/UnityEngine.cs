// UnityEngine API의 컴파일 검사용 대체 선언. 실제 Unity 6 공개 API 시그니처를 따른다 (동작은 없음).
#pragma warning disable
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }
        public static void Destroy(Object obj, float t = 0) { }
        public static void DontDestroyOnLoad(Object target) { }
        public static T FindFirstObjectByType<T>() where T : Object => null;
        public static implicit operator bool(Object o) => o != null;
    }

    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public string tag { get; set; }
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
    }

    public class Behaviour : Component { public bool enabled { get; set; } }

    public class Coroutine { }
    public class YieldInstruction { }
    public sealed class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => null;
        public void StopAllCoroutines() { }
        public void StopCoroutine(Coroutine c) { }
    }

    public sealed class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }
        public Transform transform => null;
        public string tag { get; set; }
        public bool activeSelf => true;
        public void SetActive(bool v) { }
        public T AddComponent<T>() where T : Component => null;
        public T GetComponent<T>() => default;
        public static GameObject CreatePrimitive(PrimitiveType t) => null;
    }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Transform parent { get; set; }
        public int childCount => 0;
        public Transform GetChild(int i) => null;
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetParent(Transform p) { }
        public void Rotate(float x, float y, float z) { }
        public void SetAsLastSibling() { }
        public IEnumerator GetEnumerator() => null;
    }

    public sealed class RectTransform : Transform
    {
        public Rect rect => default;
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => default; public static Vector2 one => new Vector2(1, 1);
        public float magnitude => 0;
        public static float Distance(Vector2 a, Vector2 b) => 0;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a;
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => a;
        public static Vector2 operator +(Vector2 a, Vector2 b) => a;
        public static Vector2 operator -(Vector2 a, Vector2 b) => a;
        public static Vector2 operator *(Vector2 a, float d) => a;
        public static Vector2 operator *(float d, Vector2 a) => a;
        public static implicit operator Vector3(Vector2 v) => default;
        public static implicit operator Vector2(Vector3 v) => default;
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero => default; public static Vector3 one => default; public static Vector3 up => default; public static Vector3 down => default;
        public Vector3 normalized => this;
        public float magnitude => 0;
        public static float Distance(Vector3 a, Vector3 b) => 0;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => a;
        public static Vector3 operator +(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a) => a;
        public static Vector3 operator *(Vector3 a, float d) => a;
        public static Vector3 operator *(float d, Vector3 a) => a;
        public static Vector3 operator /(Vector3 a, float d) => a;
    }

    public struct Vector4 { public Vector4(float x, float y, float z, float w) { } public static Vector4 zero => default; }

    public struct Quaternion
    {
        public static Quaternion identity => default;
        public static Quaternion Euler(float x, float y, float z) => default;
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => a;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => default; public static Color black => default; public static Color magenta => default; public static Color clear => default;
        public static Color Lerp(Color a, Color b, float t) => a;
    }

    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static implicit operator Color32(Color c) => default; }

    public struct Rect
    {
        public Rect(float x, float y, float w, float h) { }
        public Vector2 center => default;
        public float width => 0; public float height => 0; public float xMin => 0; public float xMax => 0; public float yMin => 0; public float yMax => 0;
        public static bool operator ==(Rect a, Rect b) => true; public static bool operator !=(Rect a, Rect b) => false;
        public override bool Equals(object o) => o is Rect; public override int GetHashCode() => 0;
    }
    public struct Vector2Int { public int x, y; public Vector2Int(int x, int y) { this.x = x; this.y = y; } public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y; public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b); public override bool Equals(object o) => o is Vector2Int v && v == this; public override int GetHashCode() => x ^ y; }

    public static class Mathf
    {
        public const float PI = 3.14159265f, Rad2Deg = 57.29578f, Deg2Rad = 0.0174533f;
        public static float Sin(float f) => (float)Math.Sin(f); public static float Cos(float f) => (float)Math.Cos(f); public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Exp(float f) => (float)Math.Exp(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x); public static float Abs(float f) => Math.Abs(f); public static int Abs(int f) => Math.Abs(f);
        public static float Floor(float f) => (float)Math.Floor(f); public static float Pow(float f, float p) => (float)Math.Pow(f, p); public static float Repeat(float t, float l) => t - (float)Math.Floor(t / l) * l;
        public static float Min(float a, float b) => Math.Min(a, b); public static int Min(int a, int b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b); public static int Max(int a, int b) => Math.Max(a, b);
        public static float Clamp(float v, float a, float b) => Math.Max(a, Math.Min(b, v)); public static int Clamp(int v, int a, int b) => Math.Max(a, Math.Min(b, v));
        public static float Clamp01(float v) => Clamp(v, 0, 1); public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Math.Sign(t - c) * d;
        public static int RoundToInt(float f) => (int)Math.Round(f); public static int CeilToInt(float f) => (int)Math.Ceiling(f);
    }

    public static class Random
    {
        public static float value => 0;
        public static float Range(float a, float b) => 0;
        public static int Range(int a, int b) => 0;
        public static Vector2 insideUnitCircle => default;
    }

    public static class Time { public static float deltaTime, time, unscaledTime, unscaledDeltaTime; public static float timeScale { get; set; } }
    public static class Debug { public static void Log(object m) { } public static void LogWarning(object m) { } }
    public static class Application { public static int targetFrameRate { get; set; } public static bool isBatchMode => false; }
    public enum ScreenOrientation { Portrait = 1, PortraitUpsideDown, LandscapeLeft, LandscapeRight, AutoRotation }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    public static class Screen { public static ScreenOrientation orientation { get; set; } public static Rect safeArea => default; public static int width => 1080; public static int height => 1920; }

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct Touch { public Vector2 position => default; public TouchPhase phase => default; public int fingerId => 0; }
    public static class Input
    {
        public static int touchCount => 0;
        public static bool multiTouchEnabled { get; set; }
        public static Touch GetTouch(int i) => default;
        public static Vector3 mousePosition => default;
        public static Vector2 mouseScrollDelta => default;
        public static bool GetMouseButtonDown(int b) => false;
        public static bool GetMouseButton(int b) => false;
        public static bool GetMouseButtonUp(int b) => false;
    }

    public enum TextureFormat { RGBA32 = 4 }
    public enum TextureWrapMode { Repeat, Clamp }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public class Texture : Object { public TextureWrapMode wrapMode { get; set; } public FilterMode filterMode { get; set; } }
    public sealed class Texture2D : Texture
    {
        public Texture2D(int w, int h, TextureFormat f, bool mip) { }
        public void SetPixels32(Color32[] c) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
    }
    public enum SpriteMeshType { FullRect, Tight }
    public sealed class Sprite : Object
    {
        public Texture2D texture => null;
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu, uint extrude, SpriteMeshType mt, Vector4 border) => null;
    }

    public sealed class Shader : Object { public static Shader Find(string n) => null; }
    public class Material : Object
    {
        public Material(Shader s) { }
        public Material(Material m) { }
        public Texture mainTexture { get; set; }
        public Vector2 mainTextureScale { get; set; }
        public void SetFloat(string n, float v) { }
        public void SetColor(string n, Color c) { }
        public Color GetColor(string n) => default;
    }

    public class Renderer : Component
    {
        public bool enabled { get; set; }
        public int sortingOrder { get; set; }
        public Material sharedMaterial { get; set; }
        public Material material { get; set; }
    }
    public sealed class MeshRenderer : Renderer { }
    public sealed class SpriteRenderer : Renderer { public Sprite sprite { get; set; } public Color color { get; set; } public bool flipX { get; set; } }
    public enum LineTextureMode { Stretch, Tile }
    public sealed class LineRenderer : Renderer
    {
        public bool useWorldSpace { get; set; }
        public int positionCount { get; set; }
        public void SetPositions(Vector3[] p) { }
        public float widthMultiplier { get; set; }
        public Color startColor { get; set; }
        public Color endColor { get; set; }
        public int numCornerVertices { get; set; }
        public int numCapVertices { get; set; }
        public LineTextureMode textureMode { get; set; }
        public Gradient colorGradient { get; set; }
    }

    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum TextAlignment { Left, Center, Right }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public sealed class Font : Object { public Material material => null; }
    public sealed class TextMesh : Component
    {
        public string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public float characterSize { get; set; }
        public TextAnchor anchor { get; set; }
        public TextAlignment alignment { get; set; }
        public Color color { get; set; }
    }

    public struct GradientColorKey { public GradientColorKey(Color c, float t) { } }
    public struct GradientAlphaKey { public GradientAlphaKey(float a, float t) { } }
    public class Gradient { public void SetKeys(GradientColorKey[] c, GradientAlphaKey[] a) { } }
    public class AnimationCurve
    {
        public static AnimationCurve EaseInOut(float t0, float v0, float t1, float v1) => null;
        public static AnimationCurve Linear(float t0, float v0, float t1, float v1) => null;
    }

    public enum ParticleSystemSimulationSpace { Local, World, Custom }
    public enum ParticleSystemShapeType { Sphere = 0, Box = 5, Circle = 10 }
    public enum ParticleSystemStopBehavior { StopEmittingAndClear, StopEmitting }
    public enum ParticleSystemRenderMode { Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh }
    public sealed class ParticleSystem : Component
    {
        public struct MinMaxCurve
        {
            public MinMaxCurve(float c) { }
            public MinMaxCurve(float min, float max) { }
            public MinMaxCurve(float mult, AnimationCurve curve) { }
            public static implicit operator MinMaxCurve(float c) => default;
        }
        public struct MinMaxGradient
        {
            public MinMaxGradient(Color c) { }
            public MinMaxGradient(Gradient g) { }
            public static implicit operator MinMaxGradient(Color c) => default;
        }
        public struct MainModule
        {
            public bool playOnAwake { get; set; } public bool loop { get; set; } public float duration { get; set; }
            public ParticleSystemSimulationSpace simulationSpace { get; set; } public int maxParticles { get; set; }
            public MinMaxCurve startLifetime { get; set; } public MinMaxCurve startSpeed { get; set; } public MinMaxCurve startSize { get; set; }
            public MinMaxCurve startRotation { get; set; } public MinMaxGradient startColor { get; set; } public MinMaxCurve gravityModifier { get; set; }
        }
        public struct EmissionModule { public MinMaxCurve rateOverTime { get; set; } }
        public struct ShapeModule { public ParticleSystemShapeType shapeType { get; set; } public float radius { get; set; } public Vector3 scale { get; set; } }
        public struct VelocityOverLifetimeModule { public bool enabled { get; set; } public ParticleSystemSimulationSpace space { get; set; } public MinMaxCurve x { get; set; } public MinMaxCurve y { get; set; } public MinMaxCurve z { get; set; } }
        public struct ColorOverLifetimeModule { public bool enabled { get; set; } public MinMaxGradient color { get; set; } }
        public struct SizeOverLifetimeModule { public bool enabled { get; set; } public MinMaxCurve size { get; set; } }
        public struct RotationOverLifetimeModule { public bool enabled { get; set; } public MinMaxCurve z { get; set; } }
        public struct NoiseModule { public bool enabled { get; set; } public MinMaxCurve strength { get; set; } public float frequency { get; set; } }
        public MainModule main => default;
        public EmissionModule emission => default;
        public ShapeModule shape => default;
        public VelocityOverLifetimeModule velocityOverLifetime => default;
        public ColorOverLifetimeModule colorOverLifetime => default;
        public SizeOverLifetimeModule sizeOverLifetime => default;
        public RotationOverLifetimeModule rotationOverLifetime => default;
        public NoiseModule noise => default;
        public void Play() { }
        public void Stop(bool withChildren, ParticleSystemStopBehavior b) { }
        public void Emit(int n) { }
        public void Simulate(float t, bool withChildren, bool restart) { }
    }
    public sealed class ParticleSystemRenderer : Renderer { public ParticleSystemRenderMode renderMode { get; set; } public float lengthScale { get; set; } }

    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }
    public sealed class Camera : Behaviour
    {
        public static Camera main => null;
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public float aspect => 0;
        public Vector3 ScreenToWorldPoint(Vector3 p) => p;
    }
    public sealed class AudioListener : Behaviour { }
    public class Collider : Component { }
    public class Collider2D : Behaviour { }
    public sealed class CircleCollider2D : Collider2D { public float radius { get; set; } }
    public static class Physics2D { public static Collider2D OverlapPoint(Vector2 p) => null; }

    public static class PlayerPrefs
    {
        public static void SetString(string k, string v) { } public static string GetString(string k) => ""; public static string GetString(string k, string d) => d;
        public static int GetInt(string k, int d) => d; public static void SetInt(string k, int v) { }
        public static bool HasKey(string k) => false; public static void DeleteKey(string k) { } public static void Save() { }
    }
    public static class JsonUtility { public static string ToJson(object o) => ""; public static T FromJson<T>(string s) => default; }
    public static class Resources
    {
        public static T Load<T>(string path) where T : Object => null;
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
    }
    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string s, out Color c) { c = default; return true; }
        public static string ToHtmlStringRGB(Color c) => "";
    }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad }
    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public sealed class Canvas : Behaviour { public RenderMode renderMode { get; set; } public int sortingOrder { get; set; } }
    public sealed class CanvasGroup : Behaviour { public float alpha { get; set; } public bool blocksRaycasts { get; set; } }
    public class RectOffset { public RectOffset(int l, int r, int t, int b) { } }
}


namespace UnityEngine
{
    public sealed class AudioClip : Object
    {
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => null;
        public bool SetData(float[] data, int offsetSamples) => true;
    }
    public sealed class AudioSource : Behaviour
    {
        public bool playOnAwake { get; set; } public bool loop { get; set; } public float volume { get; set; } public float pitch { get; set; }
        public bool mute { get; set; } public AudioClip clip { get; set; } public bool isPlaying => false;
        public void Play() { } public void PlayOneShot(AudioClip c, float volumeScale) { }
    }
    public sealed class WaitForSecondsRealtime : CustomYieldInstruction { public WaitForSecondsRealtime(float t) { } public override bool keepWaiting => false; }
    public abstract class CustomYieldInstruction : System.Collections.IEnumerator { public abstract bool keepWaiting { get; } public object Current => null; public bool MoveNext() => keepWaiting; public void Reset() { } }
    public static class Handheld { public static void Vibrate() { } }
    public class GUIUtility { public static string systemCopyBuffer { get; set; } }
    public struct UIVertex { }
    public class VertexHelper
    {
        public void Clear() { }
        public void AddVert(Vector3 position, Color32 color, Vector4 uv0) { }
        public void AddTriangle(int a, int b, int c) { }
    }
    public static partial class PlayerPrefsExt { }
}
namespace UnityEngine.EventSystems
{
    public interface IEventSystemHandler { }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData e); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData e); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData e); }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public class UnityEvent { public void AddListener(UnityAction a) { } }
}

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : MonoBehaviour { }
    public struct RaycastResult { }
    public class BaseEventData { public BaseEventData(EventSystem es) { } }
    public class PointerEventData : BaseEventData { public PointerEventData(EventSystem es) : base(es) { } public Vector2 position { get; set; } }
    public class EventSystem : UIBehaviour
    {
        public void RaycastAll(PointerEventData d, System.Collections.Generic.List<RaycastResult> results) { }
        public static EventSystem current => null;
        public bool IsPointerOverGameObject() => false;
        public bool IsPointerOverGameObject(int pointerId) => false;
    }
    public class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
}

namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;
    public abstract class Graphic : UIBehaviour
    {
        public virtual Color color { get; set; }
        public bool raycastTarget { get; set; }
        public RectTransform rectTransform => null;
        public virtual void SetVerticesDirty() { }
        protected virtual void OnPopulateMesh(VertexHelper vh) { }
    }
    public abstract class MaskableGraphic : Graphic { }
    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public Sprite sprite { get; set; }
        public Type type { get; set; }
        public FillMethod fillMethod { get; set; }
        public float fillAmount { get; set; }
        public int fillOrigin { get; set; }
        public float pixelsPerUnitMultiplier { get; set; }
    }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public class Text : MaskableGraphic
    {
        public Font font { get; set; }
        public virtual string text { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool supportRichText { get; set; }
        public float lineSpacing { get; set; }
    }
    public struct ColorBlock { public Color normalColor { get; set; } public Color highlightedColor { get; set; } public Color pressedColor { get; set; } public Color disabledColor { get; set; } }
    public class Selectable : UIBehaviour
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
        public ColorBlock colors { get; set; }
        public Graphic targetGraphic { get; set; }
        public bool interactable { get; set; }
        public Transition transition { get; set; }
    }
    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }
        public ButtonClickedEvent onClick { get; set; }
    }
    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
    }
    public class GraphicRaycaster : UIBehaviour { }
    public class LayoutElement : UIBehaviour
    {
        public float preferredWidth { get; set; } public float preferredHeight { get; set; } public float minHeight { get; set; }
        public float flexibleWidth { get; set; } public float flexibleHeight { get; set; }
    }
    public abstract class LayoutGroup : UIBehaviour { public RectOffset padding { get; set; } }
    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }
        public bool childControlHeight { get; set; } public bool childControlWidth { get; set; }
        public bool childForceExpandHeight { get; set; } public bool childForceExpandWidth { get; set; }
    }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class GridLayoutGroup : LayoutGroup
    {
        public enum Constraint { Flexible, FixedColumnCount, FixedRowCount }
        public Constraint constraint { get; set; } public int constraintCount { get; set; }
        public Vector2 spacing { get; set; } public Vector2 cellSize { get; set; }
    }
    public class ContentSizeFitter : UIBehaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode verticalFit { get; set; }
    }
    public class ScrollRect : UIBehaviour
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public bool horizontal { get; set; }
        public MovementType movementType { get; set; }
        public float scrollSensitivity { get; set; }
        public RectTransform viewport { get; set; }
        public RectTransform content { get; set; }
    }
    public class RectMask2D : UIBehaviour { }
}
