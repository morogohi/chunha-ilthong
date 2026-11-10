// UnityEditor API 대체 선언 (컴파일 검사용)
#pragma warning disable
using System;
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)] public class InitializeOnLoadAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class MenuItem : Attribute { public MenuItem(string p) { } }
    public static class EditorApplication { public delegate void CallbackFunction(); public static CallbackFunction delayCall; public static void Exit(int c) { } }
    public enum BuildTarget { Android, iOS, StandaloneWindows64 }
    [Flags] public enum BuildOptions { None = 0 }
    public struct BuildPlayerOptions { public string[] scenes; public string locationPathName; public BuildTarget target; public BuildOptions options; }
    public static class EditorUserBuildSettings { public static BuildTarget activeBuildTarget => BuildTarget.Android; }
    public static class BuildPipeline { public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o) => new UnityEditor.Build.Reporting.BuildReport(); }
    public static class SessionState { public static bool GetBool(string k, bool d) => d; public static void SetBool(string k, bool v) { } }
    public class EditorBuildSettingsScene { public EditorBuildSettingsScene(string path, bool enabled) { } }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes { get; set; } }
    public enum UIOrientation { Portrait, PortraitUpsideDown, LandscapeRight, LandscapeLeft, AutoRotation }
    public static class PlayerSettings
    {
        public static string productName { get; set; }
        public static string companyName { get; set; }
        public static string bundleVersion { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static void SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget t, string id) { }
    }
    public static class AssetDatabase { public static UnityEngine.Object[] LoadAllAssetsAtPath(string p) => null; public static void SaveAssets() { } }
    public class SerializedProperty { public int intValue { get; set; } }
    public class SerializedObject { public SerializedObject(UnityEngine.Object o) { } public SerializedProperty FindProperty(string n) => null; public bool ApplyModifiedProperties() => true; }
    public static class EditorUtility { public static bool DisplayDialog(string t, string m, string ok) => true; }
}
namespace UnityEditor.Build { public struct NamedBuildTarget { public static NamedBuildTarget Android => default; public static NamedBuildTarget iOS => default; } }
namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup s, NewSceneMode m) => default;
        public static bool SaveScene(UnityEngine.SceneManagement.Scene s, string path) => true;
        public static UnityEngine.SceneManagement.Scene OpenScene(string path) => default;
    }
}
namespace UnityEngine.SceneManagement { public struct Scene { } }

namespace UnityEditor.Build.Reporting { public enum BuildResult { Unknown, Succeeded, Failed, Cancelled } public struct BuildSummary { public BuildResult result; } public class BuildReport { public BuildSummary summary; } }
