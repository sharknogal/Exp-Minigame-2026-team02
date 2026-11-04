using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace BrickBreaker.Launch
{
    // 입력을 월드 방향으로 바꾼다. 공 생성이나 턴 전환은 BallShotController만 담당한다.
    [DisallowMultipleComponent]
    public sealed class BallAimInput : MonoBehaviour
    {
        [SerializeField] private BallShotController shots;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LineRenderer preview;
        [SerializeField] private Button recallButton;
        [SerializeField, Min(0.01f)] private float touchStartRadius = 0.65f;
        [SerializeField, Min(0.01f)] private float minimumDrag = 0.10f;
        [Tooltip("구매/일시정지 외에 조준을 막아야 하는 팝업 영역 (선택)")]
        [SerializeField] private RectTransform[] blockingPanels;

        private TouchControl trackedTouch;
        private int trackedId;
        private Vector2 touchStart;
        private readonly Vector3[] points = new Vector3[3];
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private EventSystem cachedEventSystem;
        private PointerEventData pointer;

        private void OnEnable() { CancelAim(); }
        private void OnDisable() { CancelAim(); }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelAim(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelAim(); }

        private void Update()
        {
            if (recallButton != null)
                recallButton.interactable = shots != null && shots.ActiveShotId >= 0 && !shots.IsPaused;
            if (shots == null || worldCamera == null || !shots.CanAim)
            {
                CancelAim();
                return;
            }

            // 터치가 진행 중인 프레임은 마우스 입력과 중복 처리하지 않는다.
            if (UpdateTouch()) return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                HidePreview();
                return;
            }
            Vector2 screen = mouse.position.ReadValue();
            if (!TryPointerWorld(screen, out Vector2 cursor))
            {
                HidePreview();
                return;
            }

            Vector2 direction = cursor - shots.LaunchOrigin;
            DrawPreview(direction);
            if (mouse.leftButton.wasPressedThisFrame && shots.TryLaunch(direction)) HidePreview();
        }

        private bool UpdateTouch()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null)
            {
                if (trackedTouch != null) CancelAim();
                return false;
            }

            if (trackedTouch == null)
            {
                bool hasContact = false;
                foreach (TouchControl touch in screen.touches)
                {
                    if (!touch.press.isPressed && !touch.press.wasReleasedThisFrame) continue;
                    hasContact = true;
                    if (!touch.press.wasPressedThisFrame) continue;
                    if (!TryPointerWorld(touch.startPosition.ReadValue(), out Vector2 start)) continue;
                    if ((start - shots.LaunchOrigin).sqrMagnitude > touchStartRadius * touchStartRadius) continue;
                    trackedTouch = touch;
                    trackedId = touch.touchId.ReadValue();
                    touchStart = start;
                    break;
                }
                if (trackedTouch == null)
                {
                    if (hasContact) HidePreview();
                    return hasContact;
                }
            }

            var phase = trackedTouch.phase.ReadValue();
            if (trackedTouch.device != screen || trackedTouch.touchId.ReadValue() != trackedId ||
                phase == UnityEngine.InputSystem.TouchPhase.Canceled ||
                phase == UnityEngine.InputSystem.TouchPhase.None ||
                !TryPointerWorld(trackedTouch.position.ReadValue(), out Vector2 current))
            {
                CancelAim();
                return true;
            }

            // 손가락을 움직인 방향 그대로 발사한다. 시작점은 공 근처일 수 있어 별도로 기록했다.
            Vector2 direction = current - touchStart;
            bool moved = direction.sqrMagnitude >= minimumDrag * minimumDrag;
            if (phase == UnityEngine.InputSystem.TouchPhase.Ended || trackedTouch.press.wasReleasedThisFrame)
            {
                CancelAim();
                if (moved) shots.TryLaunch(direction);
            }
            else if (moved) DrawPreview(direction);
            else HidePreview();
            return true;
        }

        private bool TryPointerWorld(Vector2 screen, out Vector2 world)
        {
            world = Vector2.zero;
            if (!worldCamera.pixelRect.Contains(screen) || IsBlockedByUI(screen)) return false;
            Ray ray = worldCamera.ScreenPointToRay(screen);
            var boardPlane = new Plane(Vector3.forward, Vector3.zero);
            if (!boardPlane.Raycast(ray, out float distance)) return false;
            world = ray.GetPoint(distance);
            return true;
        }

        private bool IsBlockedByUI(Vector2 screen)
        {
            if (blockingPanels != null)
            {
                foreach (RectTransform panel in blockingPanels)
                {
                    if (panel == null || !panel.gameObject.activeInHierarchy) continue;
                    Canvas canvas = panel.GetComponentInParent<Canvas>();
                    Camera camera = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                        ? null : canvas.rootCanvas.worldCamera;
                    if (RectTransformUtility.RectangleContainsScreenPoint(panel, screen, camera)) return true;
                }
            }

            EventSystem system = EventSystem.current;
            if (system == null) return false;
            if (cachedEventSystem != system)
            {
                cachedEventSystem = system;
                pointer = new PointerEventData(system);
            }
            pointer.Reset();
            pointer.position = screen;
            uiHits.Clear();
            system.RaycastAll(pointer, uiHits);
            foreach (RaycastResult hit in uiHits)
            {
                // 화면 전체 HUD 이미지나 글자 위에서도 조준 가능하다. 버튼/스크롤 등의 입력만 보호한다.
                if (!(hit.module is GraphicRaycaster)) continue;
                if (hit.gameObject.GetComponentInParent<Selectable>() != null ||
                    ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit.gameObject) != null ||
                    ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) != null ||
                    ExecuteEvents.GetEventHandler<IBeginDragHandler>(hit.gameObject) != null)
                    return true;
            }
            return false;
        }

        private void DrawPreview(Vector2 direction)
        {
            if (preview == null) return;
            int count = shots.GetPreviewPoints(direction, points);
            if (count < 2)
            {
                HidePreview();
                return;
            }
            preview.useWorldSpace = true;
            preview.positionCount = count;
            for (int i = 0; i < count; ++i) preview.SetPosition(i, points[i]);
            preview.enabled = true;
        }

        private void HidePreview()
        {
            if (preview != null) preview.enabled = false;
        }

        private void CancelAim()
        {
            trackedTouch = null;
            HidePreview();
        }
    }
}
