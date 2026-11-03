using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BrickBreaker.Balls;
using BrickBreaker.Skills;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using Object = UnityEngine.Object;

namespace BrickBreaker.Launch.Testing
{
    // 새 테스트 씬의 Play 세션에서만 실행한다. 실제 입력 이벤트와 물리 프레임을 거치므로
    // 한 테스트가 끝나면 턴/인벤토리가 진행된다. 다시 실행할 때는 Play를 재시작한다.
    public static class BallShotChecks
    {
        public static string LastReport { get; private set; } = "IDLE";
        private static Session running;

        [MenuItem("Tools/Brick Breaker/Run Shot Integration Checks (Play Mode)")]
        private static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (running != null) return LastReport;
            if (!Application.isPlaying || SceneManager.sceneCount != 1 ||
                SceneManager.GetActiveScene().path != BallLaunchSceneSetup.ScenePath)
                return LastReport = "FAIL: open BallLaunchPlaytest alone, enter Play, and wait for turn 1.";
            try
            {
                running = new Session();
                running.Start();
                return LastReport;
            }
            catch (Exception e)
            {
                Finish(false, e.Message);
                return LastReport;
            }
        }

        private static void Watchdog()
        {
            if (running == null) return;
            if (!Application.isPlaying || EditorApplication.timeSinceStartup > running.Deadline)
                Finish(false, "Play stopped or real-time timeout: " + running.Step);
        }

        private static void Finish(bool success, string reason)
        {
            EditorApplication.update -= Watchdog;
            var session = running;
            running = null;
            if (session == null) { LastReport = "FAIL: " + reason; return; }
            try { session.Cleanup(); }
            catch (Exception e) { success = false; reason += " | cleanup: " + e.Message; }
            LastReport = $"{(success ? "PASS" : "FAIL")} {session.Passed.Count} checks: " +
                string.Join("; ", session.Passed) + (success ? "" : $" | {session.Step}: {reason}");
            if (success) Debug.Log(LastReport); else Debug.LogError(LastReport);
        }

        private sealed class Session
        {
            private readonly BallShotController shots = Find<BallShotController>();
            private readonly TurnManager turns = Find<TurnManager>();
            private readonly GridManager grid = Find<GridManager>();
            private readonly AugmentInventory inventory = Find<AugmentInventory>();
            private readonly AugmentPurchaseManager purchase = Find<AugmentPurchaseManager>();
            private readonly PauseUI pause = Find<PauseUI>();
            private readonly BallEffectController effects = Find<BallEffectController>();
            private readonly SharedSkillGaugeController gauge = Find<SharedSkillGaugeController>();
            private readonly Camera camera = Find<Camera>();
            private readonly float savedScale = Time.timeScale;
            private readonly bool savedBackground = Application.runInBackground;
            private readonly InputSettings originalSettings = InputSystem.settings;
            private InputSettings testSettings;
            private InputSettings.UpdateMode savedUpdateMode;
            private InputSettings.BackgroundBehavior savedFocusBehavior;
            private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
            private Mouse mouse;
            private Touchscreen touch;
            private GameObject overlay;
            private Action<DamageBatch> capture;
            private bool pausedByCheck;
            public readonly List<string> Passed = new List<string>();
            public double Deadline { get; private set; }
            public string Step { get; private set; } = "setup";

            private static T Find<T>() where T : Component =>
                SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>()).First();

            public void Start()
            {
                Require(shots.CanAim && turns.CurrentTurn == 1 && inventory.BallCount == 1 &&
                    shots.ActiveShotId < 0 && !purchase.IsSelecting && !pause.IsPaused,
                    "Start a fresh Play session with one normal ball and no menu open.");
                // Input System은 설정을 교체할 때 기존 임시 기본 설정을 즉시 파괴한다.
                // 임시 설정은 같은 객체에서 세 값만 바꾸고 복구한다. 에셋일 때만 복제한다.
                savedUpdateMode = originalSettings.updateMode;
                savedFocusBehavior = originalSettings.backgroundBehavior;
                savedEditorBehavior = originalSettings.editorInputBehaviorInPlayMode;
                testSettings = originalSettings;
                if (EditorUtility.IsPersistent(originalSettings))
                {
                    testSettings = Object.Instantiate(originalSettings);
                    testSettings.hideFlags = HideFlags.HideAndDontSave;
                    InputSystem.settings = testSettings;
                }
                testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                Application.runInBackground = true;
                Time.timeScale = 0.25f;
                mouse = InputSystem.AddDevice<Mouse>("ShotCheckMouse");
                touch = InputSystem.AddDevice<Touchscreen>("ShotCheckTouch");
                Deadline = EditorApplication.timeSinceStartup + 55;
                EditorApplication.update += Watchdog;
                LastReport = "RUNNING: input and shot lifecycle";
                shots.StartCoroutine(Drive(Cases()));
            }

            private IEnumerator Drive(IEnumerator cases)
            {
                while (running == this)
                {
                    bool more;
                    try { more = cases.MoveNext(); }
                    catch (Exception e) { Finish(false, e.Message); yield break; }
                    if (!more) { Finish(true, ""); yield break; }
                    yield return cases.Current;
                }
            }

            private static void Require(bool condition, string failure)
            {
                if (!condition) throw new InvalidOperationException(failure);
            }

            private void Check(string name, bool condition)
            {
                Require(condition, name);
                Passed.Add(name);
                LastReport = $"RUNNING {Passed.Count}: {name}";
            }

            private Vector2 ScreenAt(Vector2 world) => camera.WorldToScreenPoint(world);

            private void MouseStateAt(Vector2 screen, bool pressed)
            {
                var state = new MouseState { position = screen }.WithButton(MouseButton.Left, pressed);
                InputSystem.QueueStateEvent(mouse, state);
            }

            private void Finger(int id, TouchPhase phase, Vector2 world) =>
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = id, phase = phase, position = ScreenAt(world) });

            // 입력 이벤트가 다음 DynamicUpdate에서 소비되고 실제 BallAimInput.Update가 읽을 때까지 기다린다.
            private static IEnumerable Frames() { yield return null; yield return null; }

            private Button MakeOverlayButton(Vector2 screen)
            {
                overlay = new GameObject("Shot Check Overlay", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                var canvas = overlay.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;
                var item = new GameObject("Input Check", typeof(RectTransform), typeof(Image), typeof(Button));
                var rect = item.GetComponent<RectTransform>();
                rect.SetParent(overlay.transform, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.anchoredPosition = screen;
                rect.sizeDelta = new Vector2(60, 60);
                item.GetComponent<Image>().color = new Color(1f, .8f, .2f, .3f);
                Canvas.ForceUpdateCanvases();
                return item.GetComponent<Button>();
            }

            private IEnumerator Cases()
            {
                Vector2 origin = shots.LaunchOrigin;
                Vector2 cursor = origin + new Vector2(1f, 3f);
                Vector2 cursorScreen = ScreenAt(cursor);
                MouseStateAt(cursorScreen, false);
                foreach (var wait in Frames()) yield return wait;
                Step = "aim limits";
                Check("down/zero rejected", !shots.TryLaunch(Vector2.down) && !shots.TryLaunch(Vector2.zero));
                bool right = BallShotController.TryGetDirection(new Vector2(5, .01f), out var low);
                bool left = BallShotController.TryGetDirection(new Vector2(-5, .01f), out var high);
                Check("10..170 limits", right && left && Mathf.Abs(Vector2.Angle(Vector2.right, low) - 10f) < .01f &&
                    Mathf.Abs(Vector2.Angle(Vector2.right, high) - 170f) < .01f);

                Step = "pause";
                pausedByCheck = true;
                pause.Pause();
                MouseStateAt(cursorScreen, true);
                foreach (var wait in Frames()) yield return wait;
                Check("pause blocks real click and public launch", pause.IsPaused && !shots.CanAim &&
                    !shots.TryLaunch(Vector2.up) && shots.ActiveShotId < 0);
                MouseStateAt(cursorScreen, false);
                foreach (var wait in Frames()) yield return wait;
                pause.Resume();
                pausedByCheck = false;

                Step = "purchase";
                Require(purchase.TryPurchaseSingle(), "test scene's real shop failed to open");
                Check("ChoosingAugment blocks launch", purchase.IsSelecting && turns.State == TurnManager.TurnState.ChoosingAugment &&
                    !shots.TryLaunch(Vector2.up) && !shots.CanAim);
                purchase.SelectCandidate(1);
                Require(!purchase.IsSelecting && turns.State == TurnManager.TurnState.Aiming, "shop did not close");
                inventory.BeginGame(); // 무작위 구매 결과를 이후 테스트에 섞지 않는다.
                shots.ResetForNewGame();

                Step = "UI raycast";
                Button blocker = MakeOverlayButton(cursorScreen);
                int clicks = 0;
                blocker.onClick.AddListener(() => clicks++);
                MouseStateAt(cursorScreen, true);
                foreach (var wait in Frames()) yield return wait;
                MouseStateAt(cursorScreen, false);
                foreach (var wait in Frames()) yield return wait;
                Check("real button click blocked from shooting", shots.ActiveShotId < 0 && clicks == 1);

                Step = "mouse";
                Object.Destroy(blocker); // Image와 raycastTarget은 그대로 둔다.
                foreach (var wait in Frames()) yield return wait;
                Check("mouse hover previews one reflection", shots.ActiveShotId < 0 &&
                    shots.GetComponent<LineRenderer>().enabled && shots.GetComponent<LineRenderer>().positionCount == 3);
                MouseStateAt(cursorScreen, true);
                foreach (var wait in Frames()) yield return wait;
                var ball = Find<BallFlight>();
                Check("passive HUD allows mouse left-down", shots.ActiveBallCount == 1 && effects.IsShotActive &&
                    gauge.Gauge.IsShotActive && turns.State == TurnManager.TurnState.Shooting);
                Check("mouse direction from ball to cursor", Vector2.Dot(ball.Direction, (cursor - origin).normalized) > .999f);
                Check("second launch blocked during shot", !shots.TryLaunch(Vector2.up));
                MouseStateAt(cursorScreen, false);
                foreach (var wait in Frames()) yield return wait;
                var recall = GameObject.Find("Recall All Balls").GetComponent<Button>();
                var canvas = recall.GetComponentInParent<Canvas>();
                Vector2 recallScreen = RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, recall.transform.position);
                Require(recall.interactable, "recall button disabled during flight");
                MouseStateAt(recallScreen, true);
                foreach (var wait in Frames()) yield return wait;
                MouseStateAt(recallScreen, false);
                foreach (var wait in Frames()) yield return wait;
                Check("visible recall button returns volley then disables", shots.ActiveShotId < 0 && !recall.interactable);
                shots.RecallAll(); // 버튼과 외부 회수 요청이 겹쳐도 턴은 한 번만 넘어간다.
                Check("force recall once before any return", turns.CurrentTurn == 2 && shots.LaunchOrigin == origin &&
                    !effects.IsShotActive && !gauge.Gauge.IsShotActive && shots.ActiveBallCount == 0);
                Object.Destroy(overlay);
                foreach (var wait in Frames()) yield return wait;

                Step = "touch";
                Vector2 start = origin + new Vector2(.40f, .02f);
                Vector2 end = start + new Vector2(1f, 1.6f);
                Finger(20, TouchPhase.Began, origin + Vector2.up * 2f);
                foreach (var wait in Frames()) yield return wait;
                Finger(20, TouchPhase.Ended, end);
                foreach (var wait in Frames()) yield return wait;
                Check("touch must start near ball", shots.ActiveShotId < 0);
                Finger(21, TouchPhase.Began, start);
                foreach (var wait in Frames()) yield return wait;
                Finger(21, TouchPhase.Moved, end);
                foreach (var wait in Frames()) yield return wait;
                Check("drag previews without firing", shots.ActiveShotId < 0 && shots.GetComponent<LineRenderer>().enabled);
                Finger(21, TouchPhase.Canceled, end);
                foreach (var wait in Frames()) yield return wait;
                Check("canceled touch does not fire", shots.ActiveShotId < 0);
                Finger(22, TouchPhase.Began, start);
                foreach (var wait in Frames()) yield return wait;
                Finger(22, TouchPhase.Moved, end);
                foreach (var wait in Frames()) yield return wait;
                Finger(22, TouchPhase.Ended, end);
                foreach (var wait in Frames()) yield return wait;
                ball = Find<BallFlight>();
                Check("touch release fires in finger direction", shots.ActiveBallCount == 1 &&
                    Vector2.Dot(ball.Direction, (end - start).normalized) > .999f);
                shots.RecallAll();
                foreach (var wait in Frames()) yield return wait;

                Step = "multi-ball recall";
                Grant("FireBall"); Grant("NormalBall");
                foreach (Block block in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Block>()))
                    block.TakeDamage(block.CurrentHealth);
                foreach (var wait in Frames()) yield return wait;
                int previousTurn = turns.CurrentTurn;
                Require(shots.TryLaunch(new Vector2(.1f, 1f)), "failed to start multi-ball shot");
                Check("first ball immediate, two still scheduled", shots.ActiveBallCount == 1 && shots.PendingBallCount == 2);
                ball = Find<BallFlight>();
                ball.Advance(2f); // 실제 천장/바닥 접촉을 첫 공만 먼저 처리한다.
                float firstReturnX = ball.transform.position.x;
                Check("early natural return waits for scheduled balls", shots.ActiveBallCount == 0 &&
                    shots.PendingBallCount == 2 && turns.CurrentTurn == previousTurn && Mathf.Abs(firstReturnX - origin.x) > .1f);
                shots.RecallAll(); shots.RecallAll();
                Check("recall cancels pending and keeps first return X", shots.PendingBallCount == 0 &&
                    turns.CurrentTurn == previousTurn + 1 && Mathf.Abs(shots.LaunchOrigin.x - firstReturnX) < .001f);
                foreach (var wait in Frames()) yield return wait;

                Step = "natural sequence";
                grid.CreateBlock(5, 4, 1000);
                var received = new List<BallEffectType>();
                capture = batch =>
                {
                    foreach (var record in batch.Records)
                        if (record.IsDirect) received.Add(record.BallType);
                };
                effects.DamageBatchCompleted += capture;
                previousTurn = turns.CurrentTurn;
                double started = Time.fixedTimeAsDouble;
                Require(shots.TryLaunch(grid.GetCellPosition(5, 4) - shots.LaunchOrigin), "scheduled shot did not start");
                int sampleCount = 1;
                while (sampleCount < 3)
                {
                    yield return new WaitForFixedUpdate();
                    int emitted = 3 - shots.PendingBallCount;
                    if (emitted == sampleCount) continue;
                    Require(emitted == sampleCount + 1, "skipped or simultaneous launch");
                    double expected = sampleCount * BallShotController.BallInterval;
                    Require(Math.Abs((Time.fixedTimeAsDouble - started) - expected) <= Time.fixedDeltaTime * .51f,
                        "unexpected launch interval: " + (Time.fixedTimeAsDouble - started));
                    sampleCount = emitted;
                }
                Check("0.04s stagger and all three spawned", shots.ActiveBallCount == 3 && turns.CurrentTurn == previousTurn);
                Time.timeScale = 1f;
                while (shots.ActiveShotId >= 0) yield return null;
                Check("observed grant order Normal/Fire/Normal used for actual hits", received.Count >= 3 &&
                    received[0] == BallEffectType.Normal && received[1] == BallEffectType.Fire && received[2] == BallEffectType.Normal);
                Check("all natural returns advance exactly once", turns.CurrentTurn == previousTurn + 1 &&
                    turns.State == TurnManager.TurnState.Aiming && shots.ActiveBallCount == 0 && shots.PendingBallCount == 0 &&
                    !gauge.Gauge.IsShotActive && !effects.IsShotActive);
            }

            private void Grant(string name)
            {
                string path = "Assets/_Project/Data/Augments/BallAugments/" + name + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<AugmentData>(path);
                Require(data != null, path);
                inventory.ApplySelection(data);
                Require(shots.SyncInventory(), "failed to observe one grant: " + name);
            }

            public void Cleanup()
            {
                try
                {
                    if (effects != null && capture != null) effects.DamageBatchCompleted -= capture;
                    if (pausedByCheck && pause != null && pause.IsPaused) pause.Resume();
                    if (overlay != null) Object.Destroy(overlay);
                    if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                    if (touch != null && touch.added) InputSystem.RemoveDevice(touch);
                }
                finally
                {
                    Time.timeScale = savedScale;
                    Application.runInBackground = savedBackground;
                    if (testSettings != null && testSettings == originalSettings)
                    {
                        originalSettings.updateMode = savedUpdateMode;
                        originalSettings.backgroundBehavior = savedFocusBehavior;
                        originalSettings.editorInputBehaviorInPlayMode = savedEditorBehavior;
                    }
                    else if (testSettings != null && InputSystem.settings == testSettings)
                    {
                        // 패키지의 설정 setter가 HideAndDontSave 복사본을 즉시 해제한다.
                        InputSystem.settings = originalSettings;
                    }
                }
            }
        }
    }
}
