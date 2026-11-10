// 메뉴 「천하일통 › 프로젝트 설정」: 씬 생성, 빌드 목록 등록, 세로 화면·앱 이름·입력 방식 설정.
// 프로젝트를 처음 열면 씬이 없을 때 한 번 자동으로 실행된다.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Chunha.EditorTools
{
    [InitializeOnLoad]
    public static class ChunhaSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static ChunhaSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !SessionState.GetBool("chunha-setup-done", false))
                {
                    SessionState.SetBool("chunha-setup-done", true);
                    Run();
                }
            };
        }

        [MenuItem("천하일통/프로젝트 설정 (씬·빌드·화면)")]
        public static void Run()
        {
            // 1) 씬: 비어 있어도 Bootstrap이 실행 시 게임을 만든다
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = new GameObject("Main Camera", typeof(Camera));
                cam.tag = "MainCamera";
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            // 2) 플레이어 설정
            PlayerSettings.productName = "천하일통";
            PlayerSettings.companyName = "Chunha";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.chunha.ilthong");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.chunha.ilthong");

            // 3) 입력: 기존 Input Manager를 쓰므로 Both로 맞춘다 (값: 0 = 기존, 1 = 새 시스템, 2 = 둘 다)
            var changedInput = false;
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings != null && settings.Length > 0)
            {
                var so = new SerializedObject(settings[0]);
                var p = so.FindProperty("activeInputHandler");
                if (p != null && p.intValue == 1) { p.intValue = 2; so.ApplyModifiedProperties(); changedInput = true; }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[천하일통] 설정 완료: Assets/Scenes/Main.unity 를 열고 Play를 누르세요.");
            if (changedInput && !Application.isBatchMode)
                EditorUtility.DisplayDialog("천하일통", "입력 방식을 'Both'로 바꿨습니다. 적용하려면 Unity를 다시 시작해 주세요.", "확인");
            EditorSceneManager.OpenScene(ScenePath);
        }

        // CI(game-ci) 진입점: -buildTarget 으로 지정된 플랫폼을 build/<플랫폼>/ 에 빌드한다.
        public static void CIBuild()
        {
            Run();
            var target = EditorUserBuildSettings.activeBuildTarget;
            var outDir = Path.Combine("..", "build", target.ToString());
            Directory.CreateDirectory(outDir);
            var path = target == BuildTarget.Android ? Path.Combine(outDir, "chunha-ilthong.apk") : outDir;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            var ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Debug.Log("[천하일통] CI 빌드 " + (ok ? "성공: " : "실패: ") + path);
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
