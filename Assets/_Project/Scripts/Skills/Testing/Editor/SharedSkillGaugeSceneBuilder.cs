using System;
using System.IO;
using BrickBreaker.Balls;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BrickBreaker.Skills.Testing
{
    // 임시 화면과 연결을 만드는 편집기 도구다. 실제 게임 씬이나 팀원 프리팹은 수정하지 않는다.
    public static class SharedSkillGaugeSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/SharedSkillGaugeTest.unity";
        private const string BlockPath = "Assets/_Project/Prefabs/Block.prefab";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private static readonly Color Background = new Color(0.055f, 0.075f, 0.11f);
        private static readonly Color Panel = new Color(0.10f, 0.135f, 0.19f);
        private static readonly Color Accent = new Color(0.23f, 0.83f, 0.70f);
        private static readonly Color TextColor = new Color(0.92f, 0.95f, 1f);

        [MenuItem("Tools/Brick Breaker/Create Shared Skill Gauge Test Scene")]
        public static void CreateFromMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Play 모드 종료 후 씬을 생성하세요.");
            if (File.Exists(ScenePath))
                return "기존 테스트 씬을 유지했습니다: " + ScenePath;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (prefab == null || prefab.GetComponent<Block>() == null || font == null)
                throw new InvalidOperationException("Block 프리팹 또는 기본 TMP 폰트를 찾을 수 없습니다.");

            Scene previous = SceneManager.GetActiveScene();
            bool replaceInitialScene = SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(previous.path) && !previous.isDirty;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceInitialScene ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            CreateCameraAndLight();

            var boardObject = new GameObject("Gauge Test Board");
            var blocksObject = new GameObject("Blocks");
            blocksObject.transform.SetParent(boardObject.transform, false);
            GridManager grid = boardObject.AddComponent<GridManager>();
            GoldManager gold = boardObject.AddComponent<GoldManager>();
            SetReferences(grid, ("blockPrefab", prefab.GetComponent<Block>()),
                ("blockParent", blocksObject.transform), ("goldManager", gold));

            var controls = new GameObject("Shared Skill Gauge Controls");
            BallEffectController effects = controls.AddComponent<BallEffectController>();
            SharedSkillGaugeController controller = controls.AddComponent<SharedSkillGaugeController>();
            SharedSkillGaugeTestHarness harness = controls.AddComponent<SharedSkillGaugeTestHarness>();
            SetReferences(effects, ("grid", grid), ("blockRoot", blocksObject.transform));
            SetReferences(controller, ("effects", effects));

            Canvas canvas = CreateCanvas();
            RectTransform root = canvas.GetComponent<RectTransform>();
            // 카메라가 그리지 않는 부분도 매 프레임 불투명 배경으로 덮는다.
            // 배경 없이 글자만 갱신하면 이전 글자의 픽셀이 화면에 남을 수 있다.
            CreatePanel(root, "Screen Background Top", 0, 0.74f, 1, 1, Background);
            CreatePanel(root, "Screen Background Bottom", 0, 0, 1, 0.36f, Background);
            CreatePanel(root, "Screen Background Left", 0, 0.36f, 0.06f, 0.74f, Background);
            CreatePanel(root, "Screen Background Right", 0.94f, 0.36f, 1, 0.74f, Background);
            CreatePanel(root, "Gauge Panel", 0.04f, 0.77f, 0.96f, 0.99f, Panel);
            CreateText(root, font, "Prototype Label", "TEST SCENE - PROTOTYPE ART",
                0.08f, 0.951f, 0.92f, 0.977f, 24, Accent);
            CreateText(root, font, "Gauge Title", "SHARED SKILL ENERGY",
                0.08f, 0.909f, 0.92f, 0.95f, 42, TextColor);
            TMP_Text charge = CreateText(root, font, "Charge", "0 / 600   (0%)",
                0.08f, 0.866f, 0.92f, 0.909f, 43, TextColor);
            Slider slider = CreateSlider(root);
            TMP_Text status = CreateText(root, font, "Status", "Ability: None   |   Aiming",
                0.08f, 0.801f, 0.92f, 0.827f, 25, TextColor);
            TMP_Text activations = CreateText(root, font, "Activations", "Activation requests: 0",
                0.08f, 0.775f, 0.92f, 0.801f, 25, Accent);

            CreateText(root, font, "Scope Note", "Signals only - skill attacks are not connected yet",
                0.04f, 0.744f, 0.96f, 0.768f, 22, new Color(0.63f, 0.69f, 0.79f));
            TMP_Text selected = CreateText(root, font, "Selected Ball", "Ball: Normal",
                0.06f, 0.326f, 0.94f, 0.355f, 29, TextColor);

            // 버튼이 호출할 함수만 연결한다. 실제 처리는 Harness와 Controller가 담당한다.
            CreateButton(root, font, "New Game", 0, 0, harness.NewGame);
            CreateButton(root, font, "Ability On / Off", 1, 0, harness.ToggleAbilities);
            CreateButton(root, font, "Begin Shot", 0, 1, harness.BeginShot);
            CreateButton(root, font, "End Shot", 1, 1, harness.EndShot);
            CreateButton(root, font, "Next Ball Type", 0, 2, harness.CycleBallType);
            CreateButton(root, font, "Hit Block", 1, 2, harness.HitSelectedBall);
            CreateButton(root, font, "+1250 Test Charge", 0, 3, harness.Add1250ForTest);
            CreateButton(root, font, "Run All Tests", 1, 3, harness.RunAllCases);

            TMP_Text message = CreateText(root, font, "Message", "Press Play to prepare the test board.",
                0.06f, 0.015f, 0.94f, 0.077f, 25, TextColor);
            SharedSkillGaugeView view = canvas.gameObject.AddComponent<SharedSkillGaugeView>();
            SetReferences(view, ("controller", controller), ("gaugeSlider", slider),
                ("chargeLabel", charge), ("statusLabel", status), ("activationLabel", activations));
            SetReferences(harness, ("grid", grid), ("blockRoot", blocksObject.transform),
                ("effects", effects), ("controller", controller),
                ("messageLabel", message), ("selectedBallLabel", selected));

            new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("게이지 테스트 씬 저장에 실패했습니다.");

            Selection.activeGameObject = controls;
            return ScenePath;
        }

        private static void CreateCameraAndLight()
        {
            var cameraObject = new GameObject("Gauge Test Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(3f, 4f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            // 상단 게이지와 하단 조작부를 피해서 블록을 그린다.
            camera.rect = new Rect(0.06f, 0.36f, 0.88f, 0.38f);

            Light2D light = new GameObject("Gauge Test Light").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Gauge Test Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Slider CreateSlider(RectTransform parent)
        {
            RectTransform sliderRect = CreateRect(parent, "Energy Gauge", 0.08f, 0.835f, 0.92f, 0.857f);
            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            Image background = CreatePanel(sliderRect, "Background", 0, 0, 1, 1,
                new Color(0.035f, 0.055f, 0.09f));
            RectTransform fillArea = CreateRect(sliderRect, "Fill Area", 0, 0, 1, 1);
            fillArea.offsetMin = new Vector2(5, 5);
            fillArea.offsetMax = new Vector2(-5, -5);
            Image fill = CreatePanel(fillArea, "Fill", 0, 0, 1, 1, Accent);
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = background;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = 0;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        private static void CreateButton(RectTransform parent, TMP_FontAsset font,
            string label, int column, int row, UnityAction action)
        {
            float left = column == 0 ? 0.06f : 0.51f;
            float bottom = 0.266f - row * 0.059f;
            Image image = CreatePanel(parent, label, left, bottom, left + 0.43f, bottom + 0.049f, Panel);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.7f, 0.95f, 0.9f);
            button.colors = colors;
            CreateText(image.rectTransform, font, "Label", label, 0.02f, 0.05f, 0.98f, 0.95f,
                28, TextColor);
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        private static TMP_Text CreateText(RectTransform parent, TMP_FontAsset font, string name,
            string content, float left, float bottom, float right, float top, int fontSize, Color color)
        {
            RectTransform rect = CreateRect(parent, name, left, bottom, right, top);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static Image CreatePanel(RectTransform parent, string name,
            float left, float bottom, float right, float top, Color color)
        {
            Image image = CreateRect(parent, name, left, bottom, right, top).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(RectTransform parent, string name,
            float left, float bottom, float right, float top)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void SetReferences(UnityEngine.Object target,
            params (string name, UnityEngine.Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach (var reference in references)
            {
                SerializedProperty field = serialized.FindProperty(reference.name);
                if (field == null)
                    throw new InvalidOperationException($"{target.name}: {reference.name} 필드가 없습니다.");
                field.objectReferenceValue = reference.value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
