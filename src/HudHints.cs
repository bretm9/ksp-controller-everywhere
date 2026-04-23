using System.Collections.Generic;
using KSP.UI.Screens.Flight;
using UnityEngine;
using UnityEngine.UI;

namespace ControllerEverywhere
{
    // On-screen help for controller users:
    //   - Always-visible mode badge (top-left corner) so you know what layer
    //     you're in.
    //   - Always-visible compact key strip (bottom-center) listing the key
    //     bindings for the current mode.
    //   - Contextual cheat sheet panel when a modifier is held (SAS / META /
    //     MAP / PAW).
    //   - Glyphs overlaid on KSP's SAS/RCS/Gear toggles and the navball SAS
    //     mode markers.
    internal static class HudHints
    {
        public enum Mode { Flight, Map, Sas, Meta, Paw, Radial }

        private static List<ActionGroupToggleButton> _agToggles = new List<ActionGroupToggleButton>();
        private static VesselAutopilotUI _sasUi;
        private static float _refreshTimer;

        private static GUIStyle _glyphStyle;
        private static GUIStyle _sheetKeyStyle;
        private static GUIStyle _sheetValStyle;
        private static GUIStyle _badgeStyle;
        private static GUIStyle _stripStyle;

        public static void Draw(bool inSas, bool inMeta, bool inMap, bool inPaw, bool radialOpen = false)
        {
            EnsureStyles();
            RefreshRefs();

            // Decide current mode (precedence: Radial > Sas > Meta > Paw > Map > Flight)
            Mode mode = Mode.Flight;
            if      (radialOpen) mode = Mode.Radial;
            else if (inSas)      mode = Mode.Sas;
            else if (inMeta)     mode = Mode.Meta;
            else if (inPaw)      mode = Mode.Paw;
            else if (inMap)      mode = Mode.Map;

            DrawModeBadge(mode);
            DrawKeyStrip(mode);

            DrawToggleGlyphs();
            if (inSas) DrawSasModeGlyphs();

            // Full cheat sheet (right side) only while a modifier is held or a
            // menu mode is open — keeps the main flight view clean.
            if      (inSas)  DrawCheatSheet("SAS MODES (hold Back)",     _sasSheet,  anchorRight: true);
            else if (inMeta) DrawCheatSheet("META (long-hold Back)",     _metaSheet, anchorRight: true);
            else if (inPaw)  DrawCheatSheet("PAW NAV",                   _pawSheet,  anchorRight: true);
        }

        private static void EnsureStyles()
        {
            if (_glyphStyle == null)
            {
                _glyphStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
            }
            if (_sheetKeyStyle == null)
            {
                _sheetKeyStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                _sheetValStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            }
            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            if (_stripStyle == null)
            {
                _stripStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 12, alignment = TextAnchor.MiddleCenter, richText = true };
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
                case Mode.Flight: text = "FLIGHT";        color = new Color(0.6f, 0.85f, 1f, 1f); break;
                case Mode.Map:    text = "MAP / PLANNING"; color = new Color(1f, 0.85f, 0.4f, 1f); break;
                case Mode.Sas:    text = "SAS PICKER";    color = new Color(0.5f, 1f, 0.6f, 1f);  break;
                case Mode.Meta:   text = "META";          color = new Color(1f, 0.6f, 1f, 1f);    break;
                case Mode.Paw:    text = "PART MENU";     color = new Color(0.5f, 1f, 0.6f, 1f);  break;
                case Mode.Radial: text = "ACTION WHEEL";  color = new Color(1f, 0.8f, 0.4f, 1f);  break;
                default:          text = "FLIGHT";        color = Color.white;                     break;
            }

            const float W = 150f, H = 22f;
            float x = 12f, y = 12f;
            UiDraw.Fill(new Rect(x, y, W, H), new Color(0f, 0f, 0f, 0.6f));
            UiDraw.Outline(new Rect(x, y, W, H), color, 1f);
            var prev = GUI.color;
            GUI.color = color;
            GUI.Label(new Rect(x, y, W, H), text, _badgeStyle);
            GUI.color = prev;
        }

        // ---- Compact key strip (bottom of screen) ------------------------------
        private static readonly Dictionary<Mode, string> _strip = new Dictionary<Mode, string>
        {
            { Mode.Flight,
              "<b>A</b> stage  <b>B</b> gear  <b>X</b> SAS  <b>Y</b> RCS  " +
              "<b>LB/RB</b> roll  <b>LT/RT</b> thr  " +
              "<b>L-stk</b> pitch/yaw  <b>R-stk</b> cam  " +
              "<b>DPad ←→</b> warp  <b>DPad ↑</b> cam mode  <b>DPad ↓</b> recenter  " +
              "<b>RS tap</b> map  <b>RS hold</b> wheel  <b>LS hold + LT/RT</b> zoom  " +
              "<b>Back</b> SAS modes (long→META)" },
            { Mode.Map,
              "<b>A</b> +node  <b>B</b> −node  <b>LB/RB</b> prev/next node  <b>X</b> SAS  <b>Y</b> RCS  " +
              "<b>DPad ↑↓</b> pro/retro dV  <b>DPad ←→</b> warp  " +
              "<b>Y + DPad ↑↓</b> nrm/anti dV  <b>Y + LT/RT</b> radial dV  " +
              "<b>LS hold + DPad ←→</b> UT  <b>RS tap</b> exit map" },
            { Mode.Sas,
              "<b>DPad ↑↓</b> Pro/Retro  <b>DPad ←→</b> Normal/Antinormal  " +
              "<b>A/B</b> Target/AntiTarget  <b>X/Y</b> Radial in/out  " +
              "<b>LB/RB</b> StabAssist/Maneuver" },
            { Mode.Meta,
              "<b>A B X Y</b> AG 1-4  <b>LB RB</b> AG 5-6  <b>DPad ↑↓←→</b> AG 7-10  " +
              "<b>Start</b> Quick Load  <b>LS/RS</b> Switch Vessel" },
            { Mode.Paw,
              "<b>DPad</b> navigate  <b>A</b> click  <b>B</b> close  <b>LT/RT</b> slider  <b>LB+RB</b> re-aim" },
            { Mode.Radial,
              "<b>Right stick</b> select  <b>release RS</b> or <b>A</b> confirm  <b>B</b> cancel" },
        };

        private static void DrawKeyStrip(Mode mode)
        {
            if (!_strip.TryGetValue(mode, out var text)) return;
            const float H = 22f;
            float W = Mathf.Min(Screen.width - 24f, 1100f);
            float x = (Screen.width - W) * 0.5f;
            float y = Screen.height - H - 8f;
            UiDraw.Fill(new Rect(x, y, W, H), new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(x, y, W, H), text, _stripStyle);
        }

        // ---- Per-button glyphs on flight toggles -------------------------------
        private static readonly Dictionary<KSPActionGroup, string> _toggleLabels = new Dictionary<KSPActionGroup, string>
        {
            { KSPActionGroup.SAS,  "X"  },
            { KSPActionGroup.RCS,  "Y"  },
            { KSPActionGroup.Gear, "B"  },
            { KSPActionGroup.Custom01, "Back⏎+A"  },
            { KSPActionGroup.Custom02, "Back⏎+B"  },
            { KSPActionGroup.Custom03, "Back⏎+X"  },
            { KSPActionGroup.Custom04, "Back⏎+Y"  },
            { KSPActionGroup.Custom05, "Back⏎+LB" },
            { KSPActionGroup.Custom06, "Back⏎+RB" },
            { KSPActionGroup.Custom07, "Back⏎+↑"  },
            { KSPActionGroup.Custom08, "Back⏎+↓"  },
            { KSPActionGroup.Custom09, "Back⏎+←"  },
            { KSPActionGroup.Custom10, "Back⏎+→"  },
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

        private static string SasGlyphFor(VesselAutopilot.AutopilotMode m)
        {
            switch (m)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist: return "LB";
                case VesselAutopilot.AutopilotMode.Prograde:        return "↑";
                case VesselAutopilot.AutopilotMode.Retrograde:      return "↓";
                case VesselAutopilot.AutopilotMode.Normal:          return "←";
                case VesselAutopilot.AutopilotMode.Antinormal:      return "→";
                case VesselAutopilot.AutopilotMode.RadialIn:        return "X";
                case VesselAutopilot.AutopilotMode.RadialOut:       return "Y";
                case VesselAutopilot.AutopilotMode.Target:          return "A";
                case VesselAutopilot.AutopilotMode.AntiTarget:      return "B";
                case VesselAutopilot.AutopilotMode.Maneuver:        return "RB";
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

            const float W = 52f, H = 18f;
            var r = new Rect(x - W * 0.5f, y - H * 0.5f, W, H);
            var prev = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.12f, 0.78f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.8f, 1f, 0.8f, 1f);
            GUI.Label(r, text, _glyphStyle);
            GUI.color = prev;
        }

        // ---- Cheat sheet panels ------------------------------------------------
        private static readonly (string key, string val)[] _sasSheet = new[]
        {
            ("DPad ↑ / ↓", "Prograde / Retrograde"),
            ("DPad ← / →", "Normal / Antinormal"),
            ("A / B",      "Target / Anti-Target"),
            ("X / Y",      "Radial In / Radial Out"),
            ("LB / RB",    "Stability Assist / Maneuver"),
        };

        private static readonly (string key, string val)[] _metaSheet = new[]
        {
            ("A B X Y",    "Action Groups 1-4"),
            ("LB RB",      "Action Groups 5-6"),
            ("DPad ↑↓←→",  "Action Groups 7-10"),
            ("Start",      "Quick load"),
            ("LS / RS",    "Switch vessel prev/next"),
        };

        private static readonly (string key, string val)[] _pawSheet = new[]
        {
            ("DPad",       "Navigate (green box = focus)"),
            ("A",          "Click / toggle"),
            ("B",          "Close PAW"),
            ("LT / RT",    "Adjust slider"),
            ("LB + RB",    "Re-aim at reticle"),
        };

        private static void DrawCheatSheet(string title, (string key, string val)[] rows, bool anchorRight)
        {
            const float W = 300f;
            float rowH = 18f;
            float H = 28f + rows.Length * rowH;
            float x = anchorRight ? Screen.width - W - 12f : 12f;
            float y = Screen.height - H - 40f;

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(x, y, W, H), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f, y + 4f, W - 16f, 20f), title,
                      new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            for (int i = 0; i < rows.Length; i++)
            {
                float ry = y + 28f + i * rowH;
                GUI.Label(new Rect(x + 8f,        ry, 130f,       rowH), rows[i].key, _sheetKeyStyle);
                GUI.Label(new Rect(x + 8f + 130f, ry, W - 16f - 130f, rowH), rows[i].val, _sheetValStyle);
            }
            GUI.color = prev;
        }
    }
}
