using UnityEngine;

namespace ControllerEverywhere
{
    // A "glide cursor" that rides the active vessel's orbit in map view. The
    // left stick slides it forward/back along the orbit; pressing A drops a
    // maneuver node exactly at the cursor's UT. Back-chord shortcuts snap the
    // cursor to Ap / Pe / target-closest-approach.
    internal static class OrbitCursor
    {
        public static double UT;
        private static bool _initialized;
        private static Vessel _lastVessel;

        // Rate at full-deflection stick: quarter of one orbital period per second.
        // So periapsis → apoapsis takes ~2 s on a typical LKO.
        private const float RateFractionPerSecond = 0.25f;

        public static void Reset()
        {
            _initialized = false;
        }

        public static void Update(ControllerInput.Pad p)
        {
            var v = FlightGlobals.ActiveVessel;
            if (v == null || v.orbit == null) { _initialized = false; return; }

            if (v != _lastVessel) { _initialized = false; _lastVessel = v; }

            double now = Planetarium.GetUniversalTime();
            double period = v.orbit.period;
            bool bounded = period > 0 && !double.IsNaN(period) && !double.IsInfinity(period);
            if (!bounded) period = 3600.0;   // hyperbolic / escape: pretend a 1h window

            if (!_initialized)
            {
                UT = now + 60.0;
                _initialized = true;
            }

            double rate = period * RateFractionPerSecond;
            UT += p.LeftStick.x * rate * Time.unscaledDeltaTime;

            // Keep the cursor in "future" and within a few orbits.
            double maxFuture = bounded ? now + period * 3.0 : now + period;
            if (UT < now + 1.0) UT = now + 1.0;
            if (UT > maxFuture) UT = maxFuture;
        }

        public static void SnapToAp()
        {
            var v = FlightGlobals.ActiveVessel;
            if (v?.orbit == null) return;
            double now = Planetarium.GetUniversalTime();
            double t = v.orbit.timeToAp;
            if (double.IsNaN(t) || double.IsInfinity(t) || t < 0) return;
            UT = now + t;
            _initialized = true;
            ScreenMessages.PostScreenMessage("Cursor → apoapsis", 0.8f, ScreenMessageStyle.UPPER_CENTER);
        }

        public static void SnapToPe()
        {
            var v = FlightGlobals.ActiveVessel;
            if (v?.orbit == null) return;
            double now = Planetarium.GetUniversalTime();
            double t = v.orbit.timeToPe;
            if (double.IsNaN(t) || double.IsInfinity(t) || t < 0) return;
            UT = now + t;
            _initialized = true;
            ScreenMessages.PostScreenMessage("Cursor → periapsis", 0.8f, ScreenMessageStyle.UPPER_CENTER);
        }

        public static void SnapToTargetClosest()
        {
            var v = FlightGlobals.ActiveVessel;
            var target = FlightGlobals.fetch != null ? FlightGlobals.fetch.VesselTarget : null;
            if (v?.orbit == null || target == null) return;
            var tgtOrbit = target.GetOrbit();
            if (tgtOrbit == null) return;
            double now = Planetarium.GetUniversalTime();
            double period = v.orbit.period > 0 ? v.orbit.period : 3600.0;
            double bestUT = now + 60.0;
            double bestSq = double.MaxValue;
            const int samples = 120;
            for (int i = 0; i <= samples; i++)
            {
                double t = now + (double)i / samples * period;
                var dp = v.orbit.getPositionAtUT(t) - tgtOrbit.getPositionAtUT(t);
                double sq = dp.sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; bestUT = t; }
            }
            UT = bestUT;
            _initialized = true;
            ScreenMessages.PostScreenMessage("Cursor → closest approach", 0.8f, ScreenMessageStyle.UPPER_CENTER);
        }

        public static ManeuverNode PlaceNode()
        {
            var v = FlightGlobals.ActiveVessel;
            if (v == null || v.patchedConicSolver == null) return null;
            if (!_initialized) UT = Planetarium.GetUniversalTime() + 60.0;
            var node = v.patchedConicSolver.AddManeuverNode(UT);
            node.OnGizmoUpdated(node.DeltaV, node.UT);
            FlightActions.SelectedNode = node;
            return node;
        }

        // ---- Rendering ---------------------------------------------------------
        public static void Draw()
        {
            if (!MapView.MapIsEnabled || !_initialized) return;
            var v = FlightGlobals.ActiveVessel;
            if (v?.orbit == null) return;

            Camera cam = ScaledCamera.Instance != null ? ScaledCamera.Instance.cam : null;
            if (cam == null) return;

            Vector3d worldPos = v.orbit.getPositionAtUT(UT);
            Vector3 scaled = ScaledSpace.LocalToScaledSpace(worldPos);
            Vector3 screen = cam.WorldToScreenPoint(scaled);
            if (screen.z < 0) return;     // behind camera

            float x = screen.x;
            float y = Screen.height - screen.y;

            var col = new Color(1f, 0.9f, 0.2f, 1f);
            // Outer crosshair
            UiDraw.Fill(new Rect(x - 1f,  y - 14f, 2f,  28f), col);
            UiDraw.Fill(new Rect(x - 14f, y - 1f,  28f, 2f),  col);
            // Center dot
            UiDraw.Fill(new Rect(x - 3f, y - 3f, 6f, 6f), col);

            // T+offset label
            double now = Planetarium.GetUniversalTime();
            string label = FormatOffset(UT - now);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = false,
            };
            style.normal.textColor = col;
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(x - 55f, y + 14f, 110f, 20f), Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(new Rect(x - 55f, y + 14f, 110f, 20f), label, style);
        }

        private static string FormatOffset(double seconds)
        {
            if (seconds < 0) seconds = 0;
            if (seconds < 60)       return $"T+{seconds:0}s";
            if (seconds < 3600)     return $"T+{(int)(seconds / 60)}m{(int)(seconds % 60):00}s";
            if (seconds < 86400)    return $"T+{(int)(seconds / 3600)}h{(int)((seconds % 3600) / 60):00}m";
            return                     $"T+{(int)(seconds / 86400)}d{(int)((seconds % 86400) / 3600):00}h";
        }
    }
}
