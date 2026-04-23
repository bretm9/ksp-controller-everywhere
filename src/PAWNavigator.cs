using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ControllerEverywhere
{
    // Detects open Part Action Windows and drives dpad+A/B navigation through
    // their uGUI Selectables. Flight axes (left stick, triggers, bumpers, right
    // stick camera) remain live so the player can pilot while menuing.
    internal static class PAWNavigator
    {
        public static bool AnyOpen =>
            UIPartActionController.Instance != null &&
            UIPartActionController.Instance.windows != null &&
            UIPartActionController.Instance.windows.Count > 0;

        private static float _repeatTimer;
        private static Vector2 _lastNav;

        public static void Tick(ControllerInput.Pad p)
        {
            EnsureFocusInsidePAW();

            // DPad-only nav (left stick stays for flight)
            var es = EventSystem.current;
            if (es == null) return;

            Vector2 nav = p.Dpad;
            bool hasDir = nav.sqrMagnitude > 0.25f;

            const float RepeatInitial = 0.4f, RepeatInterval = 0.12f;
            if (hasDir)
            {
                bool justPressed = _lastNav.sqrMagnitude <= 0.25f;
                _repeatTimer -= Time.unscaledDeltaTime;
                if (justPressed || _repeatTimer <= 0f)
                {
                    Move(nav);
                    _repeatTimer = justPressed ? RepeatInitial : RepeatInterval;
                }
            }
            else _repeatTimer = 0f;
            _lastNav = nav;

            // A = submit (click the focused button/toggle/slider).
            if (ControllerInput.Pressed(s => s.A))
            {
                var sel = es.currentSelectedGameObject;
                if (sel != null)
                {
                    var data = new BaseEventData(es);
                    ExecuteEvents.Execute(sel, data, ExecuteEvents.submitHandler);
                    var btn = sel.GetComponent<Button>();
                    if (btn != null && btn.IsInteractable()) btn.onClick.Invoke();
                }
            }

            // B = close the top-most PAW.
            if (ControllerInput.Pressed(s => s.B)) CloseTop();
        }

        private static void Move(Vector2 dir)
        {
            var es = EventSystem.current;
            var sel = es.currentSelectedGameObject;
            if (sel == null) { EnsureFocusInsidePAW(); return; }
            var data = new AxisEventData(es) { moveVector = dir };
            float ax = Mathf.Abs(dir.x), ay = Mathf.Abs(dir.y);
            data.moveDir = ax > ay
                ? (dir.x > 0 ? MoveDirection.Right : MoveDirection.Left)
                : (dir.y > 0 ? MoveDirection.Up    : MoveDirection.Down);
            ExecuteEvents.Execute(sel, data, ExecuteEvents.moveHandler);
        }

        // Put EventSystem focus on the top-most PAW's first interactable Selectable
        // if focus isn't already inside one.
        private static void EnsureFocusInsidePAW()
        {
            var ctrl = UIPartActionController.Instance;
            if (ctrl == null || ctrl.windows == null || ctrl.windows.Count == 0) return;
            var es = EventSystem.current;
            if (es == null) return;

            var top = ctrl.windows[ctrl.windows.Count - 1];
            if (top == null) return;

            var cur = es.currentSelectedGameObject;
            if (cur != null && cur.transform.IsChildOf(top.transform)) return;

            foreach (var sel in top.GetComponentsInChildren<Selectable>(false))
            {
                if (sel != null && sel.IsInteractable() && sel.gameObject.activeInHierarchy)
                {
                    es.SetSelectedGameObject(sel.gameObject);
                    return;
                }
            }
        }

        private static void CloseTop()
        {
            var ctrl = UIPartActionController.Instance;
            if (ctrl == null || ctrl.windows == null || ctrl.windows.Count == 0) return;
            var top = ctrl.windows[ctrl.windows.Count - 1];
            if (top != null) Reflector.Set(top, "pinned", false);
            ctrl.Deselect(false);
        }
    }
}
