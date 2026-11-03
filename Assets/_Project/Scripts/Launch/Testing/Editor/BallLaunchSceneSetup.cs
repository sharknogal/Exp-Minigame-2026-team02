using System;
using System.IO;
using System.Linq;
using BrickBreaker.Balls;
using BrickBreaker.Skills;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BrickBreaker.Launch.Testing
{
    public static class BallLaunchSceneSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/BallLaunchPlaytest.unity";
        public const string BalancePath = "Assets/_Project/Data/BallGaugeBalance.asset";
        public const string BallPath = "Assets/_Project/Prefabs/LaunchBall.prefab";
        private const string TeamScene = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Brick Breaker/Create Ball Launch Playtest")]
        public static void CreateFromMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("먼저 Play를 종료하세요.");
            if (File.Exists(ScenePath)) return "기존 발사 씬을 유지했습니다: " + ScenePath;

            // 팀의 최신 구매·일시정지·HUD를 그대로 사용할 수 있도록 저장된 씬을 복사한다.
            // 원본 씬, 다른 열린 씬의 저장하지 않은 변경, Build Settings는 수정하지 않는다.
            if (!AssetDatabase.CopyAsset(TeamScene, ScenePath))
                throw new IOException("샘플 씬 복사에 실패했습니다.");
            Scene previous = SceneManager.GetActiveScene();
            bool emptyStart = SceneManager.sceneCount == 1 && string.IsNullOrEmpty(previous.path) && !previous.isDirty;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, emptyStart ? OpenSceneMode.Single : OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                Install(scene, true);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("발사 테스트 씬을 저장하지 못했습니다.");
            }
            catch
            {
                // 경로에 불완전한 복사본을 남겨 다음 Build가 성공으로 오인하지 않게 한다.
                EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(ScenePath);
                throw;
            }
            return ScenePath;
        }

        [MenuItem("Tools/Brick Breaker/Connect Ball Launcher To Active Scene")]
        public static void ConnectActiveScene()
        {
            if (Application.isPlaying) return;
            Install(SceneManager.GetActiveScene(), false);
            // 현재 씬에 연결하는 메뉴는 사용자가 실행하고 결과를 확인한 뒤 직접 저장한다.
        }

        private static T InScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();

        private static void Install(Scene scene, bool playtest)
        {
            if (InScene<BallShotController>(scene) != null)
                throw new InvalidOperationException("발사 컨트롤러가 이미 연결되어 있습니다.");
            var turns = InScene<TurnManager>(scene);
            var inventory = InScene<AugmentInventory>(scene);
            var grid = InScene<GridManager>(scene);
            var gauge = InScene<SharedSkillGaugeController>(scene);
            var camera = InScene<Camera>(scene);
            var canvas = InScene<Canvas>(scene);
            var marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Ball");
            if (!turns || !inventory || !grid || !gauge || !camera || !canvas || !marker ||
                !marker.TryGetComponent(out SpriteRenderer markerSprite) || !markerSprite.sprite)
                throw new InvalidOperationException("SampleScene 구성(턴/인벤토리/격자/게이지/카메라/Canvas/Ball)이 필요합니다.");
            var blocks = new SerializedObject(grid).FindProperty("blockParent").objectReferenceValue as Transform;
            if (!blocks) throw new InvalidOperationException("GridManager의 블록 부모가 비어 있습니다.");

            var balance = AssetDatabase.LoadAssetAtPath<BallGaugeBalance>(BalancePath);
            if (!balance)
            {
                balance = ScriptableObject.CreateInstance<BallGaugeBalance>();
                AssetDatabase.CreateAsset(balance, BalancePath);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPath);
            if (!prefab)
            {
                var model = new GameObject("LaunchBall", typeof(SpriteRenderer), typeof(BallFlight));
                try
                {
                    var sprite = model.GetComponent<SpriteRenderer>();
                    sprite.sprite = markerSprite.sprite;
                    sprite.sharedMaterial = markerSprite.sharedMaterial;
                    sprite.sortingOrder = markerSprite.sortingOrder + 1;
                    var size = sprite.sprite.bounds.size;
                    model.transform.localScale = new Vector3(.2f / size.x, .2f / size.y, 1f);
                    prefab = PrefabUtility.SaveAsPrefabAsset(model, BallPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(model); }
            }

            var root = new GameObject("Ball Launcher");
            Undo.RegisterCreatedObjectUndo(root, "Connect Ball Launcher");
            var effects = InScene<BallEffectController>(scene);
            if (!effects) effects = root.AddComponent<BallEffectController>();
            Set(effects, ("grid", grid), ("blockRoot", blocks), ("gaugeBalance", balance));
            Set(gauge, ("effects", effects));
            var shots = root.AddComponent<BallShotController>();
            Set(shots, ("turns", turns), ("inventory", inventory), ("effects", effects), ("gauge", gauge),
                ("ballPrefab", prefab.GetComponent<BallFlight>()), ("originMarker", marker.transform),
                ("purchase", InScene<AugmentPurchaseManager>(scene)), ("pause", InScene<PauseUI>(scene)));

            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.widthMultiplier = .025f;
            line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            line.startColor = new Color(.3f,.9f,1f,.85f);
            line.endColor = new Color(.3f,.9f,1f,.25f);
            line.sortingOrder = 8;
            var aim = root.AddComponent<BallAimInput>();
            Set(aim, ("shots", shots), ("worldCamera", camera), ("preview", line),
                ("recallButton", CreateRecallButton(canvas, shots)));

            if (playtest)
            {
                var demo = root.AddComponent<BallLaunchPlaytest>();
                Set(demo, ("turns", turns), ("inventory", inventory), ("shots", shots),
                    ("testBall", AssetDatabase.LoadAssetAtPath<AugmentData>(
                        "Assets/_Project/Data/Augments/BallAugments/FireBall.asset")));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
        }

        private static Button CreateRecallButton(Canvas canvas, BallShotController shots)
        {
            var go = new GameObject("Recall All Balls", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(go, "Connect Recall Button");
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.57f,.92f);
            rect.sizeDelta = new Vector2(200f, 72f);
            var image = go.GetComponent<Image>();
            image.color = new Color(.14f,.24f,.34f,.96f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = image;
            btn.interactable = false;
            UnityEventTools.AddPersistentListener(btn.onClick, shots.RecallAll);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(rect,false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            // 팀이 이미 사용하는 한글 폰트와 텍스트 스타일을 재사용한다.
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_Project/UI/Fonts/Hakgyoansim Dunggeunmiso TTF B SDF.asset");
            label.text = "모든 공 회수";
            label.fontSize = 24; label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return btn;
        }

        private static void Set(UnityEngine.Object component, params (string field, UnityEngine.Object value)[] refs)
        {
            Undo.RecordObject(component, "Connect Ball Launcher");
            var obj = new SerializedObject(component);
            foreach (var pair in refs)
            {
                var field = obj.FindProperty(pair.field);
                if (field == null) throw new InvalidOperationException(component.name + ": " + pair.field);
                field.objectReferenceValue = pair.value;
            }
            obj.ApplyModifiedProperties();
        }
    }
}
