using System.Collections.Generic;
using KSP.UI.Screens.Flight;
using UnityEngine;
using UnityEngine.UI;

namespace ControllerEverywhere
{
    // On-screen help for controller users. Two parts:
    //   1) Per-button glyphs overlaid on KSP UI (SAS/RCS/Gear toggles and the
    //      10 navball SAS mode markers).
    //   2) A contextual cheat sheet panel shown while a modifier is active
    //      (quick-hold Back, long-hold Back, map view, PAW open).
    internal static class HudHints
    {
        // Cached UI references — refreshed occasionally in case the UI spawns late.
        private static List<ActionGroupToggleButton> _agToggles = new List<ActionGroupToggleButton>();
        private static VesselAutopilotUI _sasUi;
        private static float _refreshTimer;

        private static GUIStyle _glyphStyle;
        private static GUIStyle _sheetStyle;
        private static GUIStyle _sheetKeyStyle;
        private static GUIStyle _sheetValStyle;

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
            if (_sheetStyle == null)
            {
                _sheetStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    padding   = new RectOffset(8, 8, 6, 6),
                    fontSize  = 12
                };
                _sheetKeyStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                _sheetValStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 12, alignment = TextAnchor.MiddleLeft };
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

        public static void Draw(bool inSas, bool inMeta, bool inMap, bool inPaw)
        {
            EnsureStyles();
            RefreshRefs();

            DrawToggleGlyphs();
            if (inSas) DrawSasModeGlyphs();

            if      (inSas)  DrawCheatSheet("SAS MODES (hold Back)",     _sasSheet,  anchorRight: true);
            else if (inMeta) DrawCheatSheet("META (long-hold Back)",     _metaSheet, anchorRight: true);
            else if (inMap)  DrawCheatSheet("MANEUVER EDITOR (map view)", _mapSheet, anchorRight: true);
            else if (inPaw)  DrawCheatSheet("PAW NAV",                   _pawSheet,  anchorRight: true);
        }

        // ---- Per-button glyphs on flight toggles -------------------------------
        private static readonly Dictionary<KSPActionGroup, string> _toggleLabels = new Dictionary<KSPActionGroup, string>
        {
            { KSPActionGroup.SAS,  "X"  },
            { KSPActionGroup.RCS,  "Y"  },
            { KSPActionGroup.Gear, "B"  },
            // Custom01-10 get shown as "Back+A" etc. so the player can see which AG
            // maps to which meta button. (Only drawn if UI is actually visible.)
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
            // modeButtons is indexed by VesselAutopilot.AutopilotMode.
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
            // IMGUI has Y growing downward from the top of the screen.
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

        // ---- Cheat sheets ------------------------------------------------------
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

        private static readonly (string key, string val)[] _mapSheet = new[]
        {
            ("A / B",      "Create / delete node"),
            ("LB / RB",    "Cycle nodes"),
            ("DPad ↑↓",    "Prograde ± dV"),
            ("DPad ←→",    "Normal ± dV"),
            ("LT / RT",    "Radial ∓ dV"),
            ("LS / RS",    "UT earlier / later"),
            ("X / Y",      "Step ×0.5 / ×2"),
        };

        private static readonly (string key, string val)[] _pawSheet = new[]
        {
            ("DPad",       "Navigate"),
            ("A",          "Click / toggle"),
            ("B",          "Close PAW"),
            ("LB + RB",    "Re-aim at reticle"),
        };

        private static void DrawCheatSheet(string title, (string key, string val)[] rows, bool anchorRight)
        {
            const float W = 280f;
            float rowH = 18f;
            float H = 28f + rows.Length * rowH;
            float x = anchorRight ? Screen.width - W - 12f : 12f;
            float y = Screen.height - H - 12f;

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(x, y, W, H), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f, y + 4f, W - 16f, 20f), title,
                      new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            for (int i = 0; i < rows.Length; i++)
            {
                float ry = y + 28f + i * rowH;
                GUI.Label(new Rect(x + 8f,        ry, 120f,       rowH), rows[i].key, _sheetKeyStyle);
                GUI.Label(new Rect(x + 8f + 120f, ry, W - 16f - 120f, rowH), rows[i].val, _sheetValStyle);
            }
            GUI.color = prev;
        }
    }
}
