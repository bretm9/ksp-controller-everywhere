using UnityEngine;

namespace ControllerEverywhere
{
    // On-screen axis / button readout for calibrating an unfamiliar controller.
    // Toggled by Bindings.DebugOverlay (config file) or the in-game chord
    // LS + RS + Back held for ~0.5 s. Shows all 20 axes and all 20 buttons for
    // joystick index configured in controller.cfg.
    internal static class DebugOverlay
    {
        public static bool Visible;

        private static float _chordTimer;
        private static bool  _chordFired;

        public static void Poll(ControllerInput.Pad p)
        {
            bool chord = p.LS && p.RS && p.Back;
            if (chord) _chordTimer += Time.unscaledDeltaTime;
            else       { _chordTimer = 0f; _chordFired = false; }
            if (chord && _chordTimer >= 0.5f && !_chordFired)
            {
                _chordFired = true;
                Visible = !Visible;
                Log.Info("Debug overlay " + (Visible ? "ON" : "OFF"));
            }
            if (Bindings.DebugOverlay && !Visible) Visible = true;
        }

        public static void Draw()
        {
            if (!Visible) return;
            int j = ControllerInput.JoystickIndex;

            const float W = 340f, RowH = 14f;
            float H = 40f + 20f * RowH + 40f;
            float x = 12f, y = 12f;

            UiDraw.Fill(new Rect(x, y, W, H), new Color(0f, 0f, 0f, 0.78f));
            UiDraw.Outline(new Rect(x, y, W, H), new Color(0.4f, 0.9f, 0.4f, 1f), 1f);

            var title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12 };
            var row   = new GUIStyle(GUI.skin.label) { fontSize = 11 };

            float ly = y + 6f;
            GUI.Label(new Rect(x + 8f, ly, W - 16f, 18f),
                      $"Controller debug (joy{j}) — LS+RS+Back to toggle", title);
            ly += 20f;

            var names = Input.GetJoystickNames();
            string n = (j >= 0 && j < names.Length) ? names[j] : "(none)";
            GUI.Label(new Rect(x + 8f, ly, W - 16f, 16f), "Name: " + n, row);
            ly += 16f;

            for (int a = 0; a < 20; a++)
            {
                float v = ControllerInput.ReadAxis(j, a);
                string bar = MakeBar(v, 20);
                bool active = Mathf.Abs(v) > 0.1f;
                var prev = GUI.color;
                if (active) GUI.color = new Color(0.7f, 1f, 0.7f, 1f);
                GUI.Label(new Rect(x + 8f, ly, W - 16f, RowH),
                          $"axis {a,2}: {v,+6:0.00}  |{bar}|", row);
                GUI.color = prev;
                ly += RowH;
            }

            // Buttons 0-19 on one line
            string buttons = "buttons: ";
            for (int b = 0; b < 20; b++)
                buttons += Input.GetKey((KeyCode)((int)KeyCode.JoystickButton0 + b)) ? b.ToString() + " " : "";
            ly += 4f;
            GUI.Label(new Rect(x + 8f, ly, W - 16f, 16f), buttons, row);
        }

        private static string MakeBar(float v, int width)
        {
            int half = width / 2;
            int pos  = Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * half);
            var c = new char[width];
            for (int i = 0; i < width; i++) c[i] = '·';
            c[half] = '|';
            int start = Mathf.Min(half, half + pos);
            int end   = Mathf.Max(half, half + pos);
            for (int i = start; i <= end && i < width; i++) c[i] = '█';
            return new string(c);
        }
    }
}
