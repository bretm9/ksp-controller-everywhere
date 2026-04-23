using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ControllerEverywhere
{
    // Generic DPad / stick-based UI nav driver for Unity's EventSystem. We translate
    // DPad + left-stick direction into AxisEventData for Selectable navigation, and
    // A/B into submit/cancel events.
    internal static class UINavigator
    {
        private const float RepeatInitial = 0.4f;
        private const float RepeatInterval = 0.12f;
        private const float Threshold = 0.5f;

        public static void Drive(ControllerInput.Pad p, ref float repeatTimer, ref Vector2 lastNav)
        {
            var es = EventSystem.current;
            if (es == null) return;

            Vector2 nav = p.Dpad;
            if (nav.sqrMagnitude < 0.01f) nav = p.LeftStick;

            bool hasDir = nav.sqrMagnitude > Threshold * Threshold;

            // Edge-triggered nav: fire once on press, then repeat after initial delay
            if (hasDir)
            {
                bool justPressed = lastNav.sqrMagnitude <= Threshold * Threshold;
                repeatTimer -= Time.unscaledDeltaTime;
                if (justPressed || repeatTimer <= 0f)
                {
                    Move(nav);
                    repeatTimer = justPressed ? RepeatInitial : RepeatInterval;
                }
            }
            else
            {
                repeatTimer = 0f;
            }
            lastNav = nav;

            // Submit / cancel: fire on edge (press) only
            if (p.A && !_prevA) Submit();
            if (p.B && !_prevB) Cancel();
            _prevA = p.A; _prevB = p.B;
        }

        private static bool _prevA, _prevB;

        private static void Move(Vector2 dir)
        {
            var es = EventSystem.current;
            if (es == null) return;

            var data = new AxisEventData(es);
            data.moveVector = dir;
            float ax = Mathf.Abs(dir.x), ay = Mathf.Abs(dir.y);
            if (ax > ay) data.moveDir = dir.x > 0 ? MoveDirection.Right : MoveDirection.Left;
            else         data.moveDir = dir.y > 0 ? MoveDirection.Up    : MoveDirection.Down;

            var sel = es.currentSelectedGameObject;
            if (sel == null)
            {
                // Nothing selected — pick the first interactable Selectable we can find
                foreach (var s in Selectable.allSelectablesArray)
                {
                    if (s != null && s.IsInteractable() && s.gameObject.activeInHierarchy)
                    {
                        es.SetSelectedGameObject(s.gameObject);
                        return;
                    }
                }
                return;
            }
            ExecuteEvents.Execute(sel, data, ExecuteEvents.moveHandler);
        }

        private static void Submit()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null) return;
            var data = new BaseEventData(es);
            ExecuteEvents.Execute(sel, data, ExecuteEvents.submitHandler);
            // Many KSP buttons are Button components that don't wire submit — fire click as well.
            var btn = sel.GetComponent<Button>();
            if (btn != null && btn.IsInteractable()) btn.onClick.Invoke();
        }

        private static void Cancel()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null) return;
            var data = new BaseEventData(es);
            ExecuteEvents.Execute(sel, data, ExecuteEvents.cancelHandler);
        }
    }
}
