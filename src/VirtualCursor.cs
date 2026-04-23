using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ControllerEverywhere
{
    // Console-style virtual cursor for menu navigation. Activates automatically
    // when a PopupDialog (pause menu, R&D confirmation, etc.) is open. The
    // right stick moves a crosshair; A fires a pointer-click event on the
    // uGUI element under it; B passes through to the dialog as "cancel".
    //
    // This complements UINavigator/PAWNavigator (which drive Selectable nav
    // for button-list UIs). The cursor handles arbitrary clickable regions
    // that don't have Selectable.navigation wired up.
    internal static class VirtualCursor
    {
        public static bool Active;
        private static Vector2 _pos;
        private static bool _initialized;
        private static float _dialogCheckTimer;
        private static int _cachedDialogCount;

        public static float Speed = 900f;    // px/sec at full stick deflection

        // Call from FlightAddon.Update (and other scene addons as needed).
        // Returns true if the cursor is active this frame, in which case the
        // caller should suppress right-stick camera and A/B default bindings.
        public static bool Update(ControllerInput.Pad p)
        {
            _dialogCheckTimer -= Time.unscaledDeltaTime;
            if (_dialogCheckTimer <= 0f)
            {
                _dialogCheckTimer = 0.25f;
                _cachedDialogCount = CountActiveDialogs();
            }

            Active = _cachedDialogCount > 0;
            if (!Active) { _initialized = false; return false; }

            if (!_initialized)
            {
                _pos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                _initialized = true;
            }

            // Right stick moves cursor. Left stick also moves (faster) so users
            // who still want to use right stick for e.g. navball orient can.
            Vector2 move = p.RightStick;
            if (move.sqrMagnitude < 0.04f) move = p.LeftStick * 0.8f;
            _pos += move * Speed * Time.unscaledDeltaTime;
            _pos.x = Mathf.Clamp(_pos.x, 0f, Screen.width);
            _pos.y = Mathf.Clamp(_pos.y, 0f, Screen.height);

            if (ControllerInput.Pressed(s => s.A)) ClickAt(_pos);
            return true;
        }

        private static int CountActiveDialogs()
        {
            // instantiatedPopUps is a non-public static List<PopupDialog> in KSP.
            // Access via reflection to stay forward-compatible with patches.
            var list = Reflector.Get<System.Collections.Generic.List<PopupDialog>>(typeof(PopupDialog), "instantiatedPopUps");
            if (list == null) return 0;
            int count = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].gameObject != null && list[i].gameObject.activeInHierarchy)
                    count++;
            return count;
        }

        private static void ClickAt(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return;

            var ped = new PointerEventData(es) { position = screenPos };
            var results = new List<RaycastResult>();
            es.RaycastAll(ped, results);

            // Raycast results come back front-to-back. Click the first that
            // has a handler; fall back to Selectable.Click() if it's a Button.
            foreach (var r in results)
            {
                var go = r.gameObject;
                var handled = ExecuteEvents.ExecuteHierarchy(go, ped, ExecuteEvents.pointerClickHandler);
                if (handled != null) return;

                var btn = go.GetComponentInParent<UnityEngine.UI.Button>();
                if (btn != null && btn.IsInteractable()) { btn.onClick.Invoke(); return; }

                var tog = go.GetComponentInParent<UnityEngine.UI.Toggle>();
                if (tog != null && tog.IsInteractable()) { tog.isOn = !tog.isOn; return; }
            }
        }

        public static void Draw()
        {
            if (!Active) return;
            // IMGUI uses top-left origin; Unity screen Y is bottom-origin.
            float y = Screen.height - _pos.y;
            var col = new Color(1f, 0.9f, 0.3f, 0.95f);
            UiDraw.Fill(new Rect(_pos.x - 1f, y - 10f, 2f, 20f), col);
            UiDraw.Fill(new Rect(_pos.x - 10f, y - 1f, 20f, 2f), col);
            // Little dot in the center for visibility
            UiDraw.Fill(new Rect(_pos.x - 2f, y - 2f, 4f, 4f), col);
        }
    }
}
