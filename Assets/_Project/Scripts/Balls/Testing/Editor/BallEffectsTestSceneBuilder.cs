using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BrickBreaker.Balls.Testing
{
    public static class BallEffectsTestSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/BallEffectsTest.unity";
        private const string BlockPath = "Assets/_Project/Prefabs/Block.prefab";

        [MenuItem("Tools/Brick Breaker/Create Ball Effects Test Scene")]
        public static void CreateFromMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException("Play 모드 종료 후 씬을 생성하세요.");
            if (File.Exists(ScenePath))
                return "테스트 씬이 이미 있습니다: " + ScenePath;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath);
            if (prefab == null || prefab.GetComponent<Block>() == null)
                throw new System.InvalidOperationException("기존 Block 프리팹을 찾을 수 없습니다.");

            // Unity는 이름 없는 초기 씬 옆에 새 씬을 추가할 수 없다.
            // 변경 사항이 없는 초기 씬만 교체하고, 나머지 씬은 그대로 둔다.
            Scene previous = SceneManager.GetActiveScene();
            bool replaceInitialScene = SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(previous.path) && !previous.isDirty;
            NewSceneMode mode = replaceInitialScene ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            SceneManager.SetActiveScene(scene);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(3f, 4f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.10f, 0.14f);

            var lightObject = new GameObject("Global Light 2D");
            Light2D light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            var templateObject = new GameObject("Grid Template");
            GridManager template = templateObject.AddComponent<GridManager>();
            var parent = new GameObject("Blocks");
            parent.transform.SetParent(templateObject.transform, false);
            var templateFields = new SerializedObject(template);
            templateFields.FindProperty("blockPrefab").objectReferenceValue = prefab.GetComponent<Block>();
            templateFields.FindProperty("blockParent").objectReferenceValue = parent.transform;
            templateFields.ApplyModifiedPropertiesWithoutUndo();
            templateObject.SetActive(false);

            var controls = new GameObject("Ball Effect Test Controls");
            BallEffectController effects = controls.AddComponent<BallEffectController>();
            BallEffectTestHarness harness = controls.AddComponent<BallEffectTestHarness>();
            var harnessFields = new SerializedObject(harness);
            harnessFields.FindProperty("gridTemplate").objectReferenceValue = template;
            harnessFields.FindProperty("effects").objectReferenceValue = effects;
            harnessFields.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.IO.IOException("테스트 씬 저장에 실패했습니다.");
            Selection.activeGameObject = controls;
            return ScenePath;
        }
    }
}
