using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ControllerEverywhere
{
    // Generic DPad / stick UI nav driver. Uses manual spatial search across all
    // active Selectables instead of Unity's Selectable.navigation (KSP UI
    // doesn't reliably wire navigation.mode, so the stock AxisEventData pathway
    // is often a no-op).
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
            else repeatTimer = 0f;
            lastNav = nav;

            if (p.A && !_prevA) Submit();
            if (p.B && !_prevB) Cancel();
            _prevA = p.A; _prevB = p.B;
        }

        private static bool _prevA, _prevB;
        private static readonly List<Selectable> _buffer = new List<Selectable>();

        private static void CollectSelectables()
        {
            _buffer.Clear();
            foreach (var s in Selectable.allSelectablesArray)
            {
                if (s == null) continue;
                if (!s.IsInteractable()) continue;
                if (!s.gameObject.activeInHierarchy) continue;
                _buffer.Add(s);
            }
        }

        private static void Move(Vector2 dir)
        {
            var es = EventSystem.current;
            if (es == null) return;

            CollectSelectables();
            if (_buffer.Count == 0) return;

            var current = es.currentSelectedGameObject;
            if (current == null || !IsStillValid(current))
            {
                es.SetSelectedGameObject(_buffer[0].gameObject);
                return;
            }

            // Manual spatial search: pick the Selectable whose screen-space
            // position best lines up with `dir` from the current selection.
            Vector3 curWorld = current.transform.position;
            Vector2 curScreen = WorldToScreen(current.GetComponent<RectTransform>(), curWorld);

            Selectable best = null;
            float bestScore = float.MaxValue;
            Vector2 dirN = dir.normalized;

            foreach (var s in _buffer)
            {
                if (s.gameObject == current) continue;
                var rt = s.GetComponent<RectTransform>();
                if (rt == null) continue;

                Vector2 p2 = WorldToScreen(rt, s.transform.position);
                Vector2 delta = p2 - curScreen;
                if (delta.sqrMagnitude < 1f) continue;
                Vector2 deltaN = delta.normalized;

                // Projection onto desired direction.
                float along = Vector2.Dot(deltaN, dirN);
                if (along < 0.25f) continue;   // must be roughly in that direction

                float dist = delta.magnitude;
                // Score: prefer closer, and prefer more directional. Penalise
                // targets far off-axis.
                float perp = Mathf.Abs(Vector2.Dot(delta, new Vector2(-dirN.y, dirN.x)));
                float score = dist + perp * 1.5f;
                if (score < bestScore) { bestScore = score; best = s; }
            }

            if (best != null) es.SetSelectedGameObject(best.gameObject);
        }

        private static bool IsStillValid(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return false;
            var sel = go.GetComponent<Selectable>();
            return sel != null && sel.IsInteractable();
        }

        private static Vector2 WorldToScreen(RectTransform rt, Vector3 worldPos)
        {
            Canvas canvas = rt != null ? rt.GetComponentInParent<Canvas>() : null;
            Camera cam = canvas != null ? canvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        }

        private static void Submit()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null) return;

            // Prefer direct Button/Toggle — most KSP buttons don't wire submitHandler.
            var btn = sel.GetComponent<Button>();
            if (btn != null && btn.IsInteractable()) { btn.onClick.Invoke(); return; }

            var tog = sel.GetComponent<Toggle>();
            if (tog != null && tog.IsInteractable()) { tog.isOn = !tog.isOn; return; }

            ExecuteEvents.Execute(sel, new BaseEventData(es), ExecuteEvents.submitHandler);
        }

        private static void Cancel()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null) return;
            ExecuteEvents.Execute(sel, new BaseEventData(es), ExecuteEvents.cancelHandler);
        }
    }
}
