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
        private static UnityEngine.UI.Button _mapToggleBtn;
        private static float _refreshTimer;

        private static GUIStyle _glyphStyleSmall;
        private static GUIStyle _glyphStyleBig;
        private static GUIStyle _badgeStyle;
        private static GUIStyle _stripStyle;

        public static void Draw(bool inBackMod, bool inMap, bool inPaw, bool agOpen, bool radialOpen, bool cursor, bool inEva = false)
        {
            // OnGUI runs both Layout + Repaint each frame; skip Layout so we
            // don't double every FindObjectsOfType and text-style creation.
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
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
            DrawSasModeGlyphs();
            DrawMapToggleGlyph();
        }

        private static void EnsureStyles()
        {
            if (_glyphStyleSmall == null)
            {
                _glyphStyleSmall = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
            }
            if (_glyphStyleBig == null)
            {
                _glyphStyleBig = new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
            }
            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            if (_stripStyle == null)
            {
                _stripStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 14, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
            }
        }

        private static void RefreshRefs()
        {
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f && _agToggles.Count > 0 && _sasUi != null && _mapToggleBtn != null) return;
            // 3 s instead of 1 s — these objects rarely appear / disappear, and
            // FindObjectsOfType<Button>() over a full map-view scene is pricey.
            _refreshTimer = 3.0f;
            _agToggles.Clear();
            _agToggles.AddRange(UnityEngine.Object.FindObjectsOfType<ActionGroupToggleButton>());
            _sasUi = UnityEngine.Object.FindObjectOfType<VesselAutopilotUI>();
            FindMapToggleButton();
        }

        // Scan active Buttons for one whose GameObject name looks like the
        // altimeter's map-view toggle (varies across KSP patches — look for
        // several common names).
        private static void FindMapToggleButton()
        {
            _mapToggleBtn = null;
            foreach (var btn in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>())
            {
                if (btn == null || !btn.gameObject.activeInHierarchy) continue;
                string n = btn.gameObject.name;
                if (string.IsNullOrEmpty(n)) continue;
                string lower = n.ToLower();
                if ((lower.Contains("map") && (lower.Contains("view") || lower.Contains("toggle") || lower.Contains("btn")))
                    || lower == "mapviewbutton")
                {
                    _mapToggleBtn = btn;
                    return;
                }
            }
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
              "<b>DPad ↑↓</b> time warp  <b>DPad ←→</b> camera mode  " +
              "<b>LS tap</b> RCS on/off  <b>RS tap</b> SAS on/off  <b>RS hold</b> SAS modes  " +
              "<b>Back tap</b> AGs  <b>Back hold</b> meta  <b>Start</b> pause" },
            { Mode.Map,
              "<b>L-stick</b> orbit cursor (or mouse if map.cursorMode=virtual)  " +
              "<b>A</b> place node at cursor  <b>B / Y</b> exit map  <b>X</b> delete node  " +
              "<b>LB/RB</b> prev/next node  <b>LT/RT</b> throttle  " +
              "<b>DPad ↑↓</b> time warp  <b>DPad ←→</b> ±prograde dV  " +
              "<b>R-stick</b> camera  <b>Back hold</b> Ap/Pe snap + fine tune" },
            { Mode.BackMod,
              "<b>A</b> Abort  <b>B</b> Toggle Gear  <b>X</b> Toggle Lights  <b>Y</b> Toggle IVA  " +
              "<b>LB/RB</b> prev/next vessel  <b>LT/RT</b> quick load/save  " +
              "<b>DPad ↑</b> warp to node  <b>DPad ↓</b> focus cam  " +
              "<b>DPad ←</b> set target at reticle  <b>DPad →</b> clear target  " +
              "<b>LS</b> precision" },
            { Mode.BackModMap,
              "<b>A</b> cursor → Ap  <b>B</b> cursor → Pe  <b>X</b> cursor → target  " +
              "<b>DPad ←→</b> normal/anti dV  <b>LT/RT</b> radial in/out dV  " +
              "<b>LB/RB</b> UT earlier/later  <b>Y</b> exit map" },
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
              "<b>DPad ↑↓</b> time warp  <b>DPad ←→</b> SAS Pro/Retro  " +
              "<b>LS</b> SAS on/off  <b>RS</b> helmet lamp  <b>Back hold</b> kerbal modifier" },
            { Mode.BackModEva,
              "<b>X</b> next kerbal  <b>Y</b> toggle jetpack  (all other Back-held bindings work as normal)" },
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
        // SAS / RCS stay small (they're next to small circular icons).
        // Custom AGs + gear/brakes/lights/abort get the bigger glyph since
        // those panels are roomier.
        private static readonly Dictionary<KSPActionGroup, (string label, bool big)> _toggleLabels =
            new Dictionary<KSPActionGroup, (string, bool)>
        {
            { KSPActionGroup.SAS,      ("RS",        false) },
            { KSPActionGroup.RCS,      ("LS",        false) },
            { KSPActionGroup.Gear,     ("Back+B",    true)  },
            { KSPActionGroup.Light,    ("Back+X",    true)  },
            { KSPActionGroup.Abort,    ("Back+A",    true)  },
            { KSPActionGroup.Custom01, ("Back⏎ wheel", true) },
            { KSPActionGroup.Custom02, ("Back⏎ wheel", true) },
            { KSPActionGroup.Custom03, ("Back⏎ wheel", true) },
            { KSPActionGroup.Custom04, ("Back⏎ wheel", true) },
            { KSPActionGroup.Custom05, ("Back⏎ wheel", true) },
            { KSPActionGroup.Custom06, ("Back⏎ wheel", true) },
            { KSPActionGroup.Custom07, ("Back⏎ wheel", true) },
            { KSPActionGroup.Custom08, ("Back⏎ wheel", true) },
        };

        private static void DrawToggleGlyphs()
        {
            foreach (var tb in _agToggles)
            {
                if (tb == null || !tb.gameObject.activeInHierarchy) continue;
                if (!_toggleLabels.TryGetValue(tb.group, out var entry)) continue;
                var rt = tb.GetComponent<RectTransform>();
                if (rt == null) continue;
                DrawGlyphAt(rt, entry.label, offset: new Vector2(0f, -28f), big: entry.big);
            }
        }

        private static void DrawMapToggleGlyph()
        {
            if (_mapToggleBtn == null) return;
            var rt = _mapToggleBtn.GetComponent<RectTransform>();
            if (rt == null) return;
            // Offset to the side so it doesn't cover the icon itself. The
            // altimeter map toggle is near the top centre of the screen;
            // placing the glyph above it reads well.
            DrawGlyphAt(rt, "Y", offset: new Vector2(0f, -30f), big: true);
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

        // All SAS modes are chord bindings under RS-hold. The glyph is just
        // the direction (arrow / button) the user presses while holding RS.
        private static string SasGlyphFor(VesselAutopilot.AutopilotMode m)
        {
            switch (m)
            {
                case VesselAutopilot.AutopilotMode.Prograde:        return "RS+←";
                case VesselAutopilot.AutopilotMode.Retrograde:      return "RS+→";
                case VesselAutopilot.AutopilotMode.Normal:          return "RS+↑";
                case VesselAutopilot.AutopilotMode.Antinormal:      return "RS+↓";
                case VesselAutopilot.AutopilotMode.RadialIn:        return "RS+B";
                case VesselAutopilot.AutopilotMode.RadialOut:       return "RS+Y";
                case VesselAutopilot.AutopilotMode.Target:          return "RS+LB";
                case VesselAutopilot.AutopilotMode.AntiTarget:      return "RS+RB";
                case VesselAutopilot.AutopilotMode.StabilityAssist: return "RS+A";
                case VesselAutopilot.AutopilotMode.Maneuver:        return "RS+X";
                default: return null;
            }
        }

        private static void DrawGlyphAt(RectTransform rt, string text, Vector2 offset, bool big = false)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null ? canvas.worldCamera : null;

            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            float x = screenPos.x + offset.x;
            float y = (Screen.height - screenPos.y) + offset.y;

            float W = big ? 92f : 52f;
            float H = big ? 26f : 16f;
            var r = new Rect(x - W * 0.5f, y - H * 0.5f, W, H);
            var prev = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.12f, 0.82f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.8f, 1f, 0.8f, 1f);
            GUI.Label(r, text, big ? _glyphStyleBig : _glyphStyleSmall);
            GUI.color = prev;
        }
    }
}
