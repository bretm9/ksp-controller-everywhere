using System.Collections.Generic;
using KSP.UI.Screens.Flight;
using UnityEngine;
using UnityEngine.UI;

namespace ControllerEverywhere
{
    // On-screen help overlays tuned for a console-style layout:
    //   - Mode badge top-left (FLIGHT / MAP / BACK MOD / PAW / AG WHEEL / WHEEL)
    //   - Compact key strip bottom-center listing every binding for the mode
    //   - Per-button glyphs on KSP's SAS/RCS/Gear toggles and the navball SAS markers
    //   - Highlight box on the focused PAW control (drawn by PAWNavigator)
    internal static class HudHints
    {
        public enum Mode { Flight, Map, BackMod, BackModMap, Paw, Radial, AgWheel, Cursor, Eva, BackModEva }

        private static List<ActionGroupToggleButton> _agToggles = new List<ActionGroupToggleButton>();
        private static VesselAutopilotUI _sasUi;
        private static float _refreshTimer;

        private static GUIStyle _glyphStyle;
        private static GUIStyle _badgeStyle;
        private static GUIStyle _stripStyle;

        public static void Draw(bool inBackMod, bool inMap, bool inPaw, bool agOpen, bool radialOpen, bool cursor, bool inEva = false)
        {
            EnsureStyles();
            RefreshRefs();

            Mode mode;
            if      (cursor)     mode = Mode.Cursor;
            else if (radialOpen) mode = Mode.Radial;
            else if (agOpen)     mode = Mode.AgWheel;
            else if (inBackMod && inEva) mode = Mode.BackModEva;
            else if (inBackMod && inMap) mode = Mode.BackModMap;
            else if (inBackMod)  mode = Mode.BackMod;
            else if (inPaw)      mode = Mode.Paw;
            else if (inEva)      mode = Mode.Eva;
            else if (inMap)      mode = Mode.Map;
            else                 mode = Mode.Flight;

            DrawModeBadge(mode);
            DrawKeyStrip(mode);
            DrawToggleGlyphs();
            // Always draw SAS mode glyphs — the DPad bindings are direct (no
            // modifier) so the player should see them at all times, and the
            // extended-mode chords (Back+A etc.) are helpful to see even when
            // Back isn't held.
            DrawSasModeGlyphs();
        }

        private static void EnsureStyles()
        {
            if (_glyphStyle == null)
            {
                _glyphStyle = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
            }
            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            if (_stripStyle == null)
            {
                _stripStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 12, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
            }
        }

        private static void RefreshRefs()
        {
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f && _agToggles.Count > 0 && _sasUi != null) return;
            _refreshTimer = 1.0f;
            _agToggles.Clear();
            _agToggles.AddRange(UnityEngine.Object.FindObjectsOfType<ActionGroupToggleButton>());
            _sasUi = UnityEngine.Object.FindObjectOfType<VesselAutopilotUI>();
        }

        // ---- Mode badge --------------------------------------------------------
        private static void DrawModeBadge(Mode mode)
        {
            string text;
            Color color;
            switch (mode)
            {
                case Mode.Flight:     text = "FLIGHT";            color = new Color(0.6f, 0.85f, 1f, 1f); break;
                case Mode.Map:        text = "MAP / PLANNING";    color = new Color(1f, 0.85f, 0.4f, 1f); break;
                case Mode.BackMod:    text = "BACK HOLD";         color = new Color(0.5f, 1f, 0.6f, 1f);  break;
                case Mode.BackModMap: text = "BACK HOLD (MAP)";   color = new Color(1f, 1f, 0.5f, 1f);    break;
                case Mode.Paw:        text = "PART MENU";         color = new Color(0.5f, 1f, 0.6f, 1f);  break;
                case Mode.Radial:     text = "ACTION WHEEL";      color = new Color(1f, 0.8f, 0.4f, 1f);  break;
                case Mode.AgWheel:    text = "ACTION GROUPS";     color = new Color(0.9f, 0.7f, 1f, 1f);  break;
                case Mode.Cursor:     text = "CURSOR";            color = new Color(1f, 0.9f, 0.3f, 1f);  break;
                case Mode.Eva:        text = "EVA KERBAL";        color = new Color(1f, 0.5f, 0.5f, 1f);  break;
                case Mode.BackModEva: text = "BACK HOLD (EVA)";   color = new Color(1f, 0.75f, 0.75f, 1f); break;
                default:              text = "FLIGHT";            color = Color.white;                     break;
            }

            const float W = 180f, H = 22f;
            float x = 12f, y = 12f;
            UiDraw.Fill(new Rect(x, y, W, H), new Color(0f, 0f, 0f, 0.6f));
            UiDraw.Outline(new Rect(x, y, W, H), color, 1f);
            var prev = GUI.color;
            GUI.color = color;
            GUI.Label(new Rect(x, y, W, H), text, _badgeStyle);
            GUI.color = prev;
        }

        // ---- Compact key strip (bottom of screen) ------------------------------
        // Console-style hint strip — lists every primary binding for the mode.
        // Rich-text <b>…</b> bolds the button label so it scans quickly.
        private static readonly Dictionary<Mode, string> _strip = new Dictionary<Mode, string>
        {
            { Mode.Flight,
              "<b>A</b> stage  <b>B</b> cancel  <b>X</b> part menu  <b>Y</b> map  " +
              "<b>L-stick</b> pitch/yaw  <b>R-stick</b> camera  " +
              "<b>LT/RT</b> throttle  <b>LB/RB</b> roll  " +
              "<b>DPad</b> SAS (Pro/Retro/Nrm/Anti)  " +
              "<b>LS</b> SAS on/off  <b>RS</b> RCS on/off  <b>RS hold</b> wheel  " +
              "<b>Back tap</b> Action Groups  <b>Back hold</b> modifier  <b>Start</b> pause" },
            { Mode.Map,
              "<b>A</b> +node  <b>B</b> −node  <b>X</b> part menu  <b>Y</b> exit map  " +
              "<b>LB/RB</b> cycle nodes  <b>LT/RT</b> throttle  <b>DPad</b> SAS  " +
              "<b>LS</b> SAS  <b>RS</b> RCS  <b>Back hold</b> → fine-tune maneuver" },
            { Mode.BackMod,
              "<b>A</b> Stability  <b>B</b> Abort  <b>X</b> Maneuver  <b>Y</b> toggle IVA  " +
              "<b>DPad ↑↓</b> Radial in/out  <b>DPad ←→</b> time warp  " +
              "<b>LB/RB</b> Target/Anti-Target  <b>LT/RT</b> quick load/save  " +
              "<b>LS</b> precision  <b>RS</b> camera mode" },
            { Mode.BackModMap,
              "<b>DPad ↑↓</b> pro/retro dV  <b>DPad ←→</b> normal/anti dV  " +
              "<b>LT/RT</b> radial in/out dV  <b>LB/RB</b> UT earlier/later  " +
              "<b>Y</b> exit map" },
            { Mode.Paw,
              "<b>DPad</b> navigate (green box = focus)  <b>A</b> click  " +
              "<b>B</b> close  <b>LT/RT</b> adjust slider" },
            { Mode.Radial,
              "<b>Right stick</b> select slice  <b>release RS</b> or <b>A</b> confirm  <b>B</b> cancel" },
            { Mode.AgWheel,
              "<b>Right stick</b> select AG 1-8  <b>A</b> fire  <b>B</b> cancel" },
            { Mode.Cursor,
              "<b>R-stick</b> (or <b>L-stick</b>) move cursor  <b>A</b> click  <b>B</b> cancel / close dialog" },
            { Mode.Eva,
              "<b>L-stick</b> walk / jetpack dir  <b>R-stick</b> camera  <b>LB/RB</b> roll  " +
              "<b>A</b> jump  <b>B</b> board airlock/seat  <b>X</b> plant flag  <b>Y</b> toggle jetpack  " +
              "<b>DPad</b> SAS  <b>LS</b> SAS on/off  <b>RS</b> helmet lamp  " +
              "<b>Back hold</b> kerbal modifier" },
            { Mode.BackModEva,
              "<b>X</b> next kerbal  <b>Y</b> let go / ladder hint  (all other Back-held bindings work as normal)" },
        };

        private static void DrawKeyStrip(Mode mode)
        {
            if (!_strip.TryGetValue(mode, out var text)) return;
            // Two-line strip so the content doesn't clip on narrow windows.
            float W = Mathf.Min(Screen.width - 24f, 1180f);
            const float H = 40f;
            float x = (Screen.width - W) * 0.5f;
            float y = Screen.height - H - 8f;
            UiDraw.Fill(new Rect(x, y, W, H), new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(new Rect(x + 10f, y + 3f, W - 20f, H - 6f), text, _stripStyle);
        }

        // ---- Per-button glyphs on flight toggles -------------------------------
        private static readonly Dictionary<KSPActionGroup, string> _toggleLabels = new Dictionary<KSPActionGroup, string>
        {
            { KSPActionGroup.SAS,  "LS"  },
            { KSPActionGroup.RCS,  "RS"  },
            // Gear no longer has a direct binding (it was on B; B is now cancel).
            // Custom AGs reachable via Back-tap wheel:
            { KSPActionGroup.Custom01, "Back⏎" },
            { KSPActionGroup.Custom02, "Back⏎" },
            { KSPActionGroup.Custom03, "Back⏎" },
            { KSPActionGroup.Custom04, "Back⏎" },
            { KSPActionGroup.Custom05, "Back⏎" },
            { KSPActionGroup.Custom06, "Back⏎" },
            { KSPActionGroup.Custom07, "Back⏎" },
            { KSPActionGroup.Custom08, "Back⏎" },
        };

        private static void DrawToggleGlyphs()
        {
            foreach (var tb in _agToggles)
            {
                if (tb == null || !tb.gameObject.activeInHierarchy) continue;
                if (!_toggleLabels.TryGetValue(tb.group, out var label)) continue;
                var rt = tb.GetComponent<RectTransform>();
                if (rt == null) continue;
                DrawGlyphAt(rt, label, offset: new Vector2(0f, -26f));
            }
        }

        private static void DrawSasModeGlyphs()
        {
            if (_sasUi == null || _sasUi.modeButtons == null) return;
            for (int i = 0; i < _sasUi.modeButtons.Length; i++)
            {
                var btn = _sasUi.modeButtons[i];
                if (btn == null || !btn.gameObject.activeInHierarchy) continue;
                string label = SasGlyphFor((VesselAutopilot.AutopilotMode)i);
                if (string.IsNullOrEmpty(label)) continue;
                var rt = btn.GetComponent<RectTransform>();
                if (rt == null) continue;
                DrawGlyphAt(rt, label, offset: new Vector2(0f, -22f));
            }
        }

        // In console layout the Back-modifier picks the extended SAS modes.
        // Primary modes (Pro/Retro/Normal/Antinormal) are plain DPad — show them
        // during Back-hold since that's when all ten markers are relevant.
        private static string SasGlyphFor(VesselAutopilot.AutopilotMode m)
        {
            switch (m)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist: return "Back+A";
                case VesselAutopilot.AutopilotMode.Prograde:        return "DPad ↑";
                case VesselAutopilot.AutopilotMode.Retrograde:      return "DPad ↓";
                case VesselAutopilot.AutopilotMode.Normal:          return "DPad ←";
                case VesselAutopilot.AutopilotMode.Antinormal:      return "DPad →";
                case VesselAutopilot.AutopilotMode.RadialIn:        return "Back+↑";
                case VesselAutopilot.AutopilotMode.RadialOut:       return "Back+↓";
                case VesselAutopilot.AutopilotMode.Target:          return "Back+LB";
                case VesselAutopilot.AutopilotMode.AntiTarget:      return "Back+RB";
                case VesselAutopilot.AutopilotMode.Maneuver:        return "Back+X";
                default: return null;
            }
        }

        private static void DrawGlyphAt(RectTransform rt, string text, Vector2 offset)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null ? canvas.worldCamera : null;

            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            float x = screenPos.x + offset.x;
            float y = (Screen.height - screenPos.y) + offset.y;

            const float W = 64f, H = 18f;
            var r = new Rect(x - W * 0.5f, y - H * 0.5f, W, H);
            var prev = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.12f, 0.78f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.8f, 1f, 0.8f, 1f);
            GUI.Label(r, text, _glyphStyle);
            GUI.color = prev;
        }
    }
}
