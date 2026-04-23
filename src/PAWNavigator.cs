using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ControllerEverywhere
{
    // Drives dpad + A/B navigation through open Part Action Windows. KSP's PAW
    // controls don't have reliable Unity Selectable navigation set up, so we
    // manually enumerate the interactable Selectables inside the top window,
    // sort them top-to-bottom and left-to-right, and step through the list.
    internal static class PAWNavigator
    {
        public static bool AnyOpen =>
            UIPartActionController.Instance != null &&
            UIPartActionController.Instance.windows != null &&
            UIPartActionController.Instance.windows.Count > 0;

        // Current focus — a GameObject inside a PAW. Nulled when PAW closes.
        public static GameObject Focused;

        private static float _repeatTimer;
        private static Vector2 _lastNav;
        private static UIPartActionWindow _lastTopWindow;
        private static readonly List<Selectable> _scratch = new List<Selectable>();

        public static void Tick(ControllerInput.Pad p)
        {
            var ctrl = UIPartActionController.Instance;
            if (ctrl == null || ctrl.windows == null || ctrl.windows.Count == 0)
            {
                Focused = null;
                return;
            }
            var top = ctrl.windows[ctrl.windows.Count - 1];
            if (top == null) return;

            // Rebuild selectable list if the top window changed or our focus died
            if (top != _lastTopWindow || Focused == null || !IsInsideWindow(Focused, top))
            {
                _lastTopWindow = top;
                var list = EnumerateSelectables(top);
                Focused = list.Count > 0 ? list[0].gameObject : null;
            }

            // DPad nav with initial delay + repeat
            Vector2 nav = p.Dpad;
            bool hasDir = nav.sqrMagnitude > 0.25f;
            const float RepeatInitial = 0.4f, RepeatInterval = 0.15f;
            if (hasDir)
            {
                bool justPressed = _lastNav.sqrMagnitude <= 0.25f;
                _repeatTimer -= Time.unscaledDeltaTime;
                if (justPressed || _repeatTimer <= 0f)
                {
                    Navigate(top, nav);
                    _repeatTimer = justPressed ? RepeatInitial : RepeatInterval;
                }
            }
            else _repeatTimer = 0f;
            _lastNav = nav;

            // Sliders: let triggers nudge the focused slider's value
            float triggerDelta = p.RightTrigger - p.LeftTrigger;
            if (Mathf.Abs(triggerDelta) > 0.1f) NudgeSlider(triggerDelta * Time.unscaledDeltaTime);

            // A = click / submit
            if (ControllerInput.Pressed(s => s.A)) Fire();

            // B = close PAW
            if (ControllerInput.Pressed(s => s.B)) CloseTop();
        }

        public static void ClearOnClose()
        {
            if (!AnyOpen) { Focused = null; _lastTopWindow = null; }
        }

        // ---- Selectable enumeration --------------------------------------------
        private static List<Selectable> EnumerateSelectables(UIPartActionWindow window)
        {
            _scratch.Clear();
            foreach (var sel in window.GetComponentsInChildren<Selectable>(false))
            {
                if (sel != null && sel.IsInteractable() && sel.gameObject.activeInHierarchy)
                    _scratch.Add(sel);
            }
            // Sort by screen-space Y descending (top to bottom) then X ascending.
            _scratch.Sort((a, b) =>
            {
                var pa = a.transform.position;
                var pb = b.transform.position;
                if (Mathf.Abs(pa.y - pb.y) > 2f) return pa.y > pb.y ? -1 : 1;
                return pa.x < pb.x ? -1 : 1;
            });
            return _scratch;
        }

        private static void Navigate(UIPartActionWindow window, Vector2 dir)
        {
            var list = EnumerateSelectables(window);
            if (list.Count == 0) { Focused = null; return; }

            int idx = Focused != null ? list.FindIndex(s => s.gameObject == Focused) : -1;
            if (idx < 0) { Focused = list[0].gameObject; return; }

            if (dir.y > 0.5f)       idx = Mathf.Max(idx - 1, 0);            // up
            else if (dir.y < -0.5f) idx = Mathf.Min(idx + 1, list.Count - 1); // down
            else if (dir.x < -0.5f) idx = Mathf.Max(idx - 1, 0);            // left
            else if (dir.x > 0.5f)  idx = Mathf.Min(idx + 1, list.Count - 1); // right

            Focused = list[idx].gameObject;
        }

        private static void Fire()
        {
            if (Focused == null) return;
            // Prefer direct Button.onClick for Unity buttons — some PAW buttons
            // don't route through the submitHandler.
            var btn = Focused.GetComponent<Button>();
            if (btn != null && btn.IsInteractable()) { btn.onClick.Invoke(); return; }

            var tog = Focused.GetComponent<Toggle>();
            if (tog != null && tog.IsInteractable()) { tog.isOn = !tog.isOn; return; }

            var es = EventSystem.current;
            if (es != null)
            {
                es.SetSelectedGameObject(Focused);
                ExecuteEvents.Execute(Focused, new BaseEventData(es), ExecuteEvents.submitHandler);
            }
        }

        private static void NudgeSlider(float amount)
        {
            if (Focused == null) return;
            var sl = Focused.GetComponent<Slider>();
            if (sl == null || !sl.IsInteractable()) return;
            float range = sl.maxValue - sl.minValue;
            sl.value = Mathf.Clamp(sl.value + amount * range * 0.5f, sl.minValue, sl.maxValue);
        }

        private static bool IsInsideWindow(GameObject go, UIPartActionWindow window)
        {
            if (go == null || window == null) return false;
            return go.transform.IsChildOf(window.transform);
        }

        private static void CloseTop()
        {
            var ctrl = UIPartActionController.Instance;
            if (ctrl == null || ctrl.windows == null || ctrl.windows.Count == 0) return;
            var top = ctrl.windows[ctrl.windows.Count - 1];
            if (top != null) Reflector.Set(top, "pinned", false);
            ctrl.Deselect(false);
            Focused = null;
            _lastTopWindow = null;
        }

        // ---- Highlight box ------------------------------------------------------
        public static void DrawHighlight()
        {
            if (Focused == null) return;
            var rt = Focused.GetComponent<RectTransform>();
            if (rt == null) return;

            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null ? canvas.worldCamera : null;

            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++)
                corners[i] = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);

            float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            float maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);

            // IMGUI Y flip
            float iy = Screen.height - maxY;
            float ih = maxY - minY;
            var rect = new Rect(minX - 2f, iy - 2f, (maxX - minX) + 4f, ih + 4f);

            UiDraw.Outline(rect, new Color(0.6f, 1f, 0.4f, 1f), 2f);
        }
    }
}
