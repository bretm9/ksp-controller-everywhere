using System.Linq;
using UnityEngine;

namespace ControllerEverywhere
{
    // Console-inspired EVA kerbal controls. Drives a KerbalEVA component via
    // its public toggle methods and by writing the non-public cmdDir field
    // each frame (KerbalEVA uses this internally for walk/jetpack direction).
    //
    // Jumps apply an impulse directly to the kerbal's rigidbody because
    // KerbalEVA doesn't expose a Jump() API.
    internal static class EvaActions
    {
        public static bool IsActive
        {
            get
            {
                var v = FlightGlobals.ActiveVessel;
                return v != null && v.isEVA && v.evaController != null;
            }
        }

        public static KerbalEVA Controller => FlightGlobals.ActiveVessel?.evaController;

        // Feed left-stick input into the kerbal's cmdDir. Local-space:
        //   x = strafe right (+) / left (-)
        //   y = vertical (unused for walk — jump handled separately)
        //   z = forward (+) / back (-)
        public static void Walk(Vector2 stick)
        {
            var eva = Controller;
            if (eva == null) return;
            var dir = new Vector3(stick.x, 0f, stick.y);
            Reflector.Set(eva, "cmdDir", dir);
        }

        public static void Jump()
        {
            var eva = Controller;
            if (eva == null) return;
            var rb = Reflector.Get<Rigidbody>(eva, "_rigidbody");
            float force = Reflector.Get<float>(eva, "jumpForce");
            if (rb == null || force <= 0f) return;
            // Apply an impulse along the kerbal's local up so they jump relative
            // to the surface normal they're standing on.
            rb.AddForce(eva.transform.up * force, ForceMode.Impulse);
        }

        public static void ToggleJetpack()
        {
            Controller?.ToggleJetpack();
        }

        public static void ToggleLamp()
        {
            Controller?.ToggleLamp();
        }

        public static void PlantFlag()
        {
            Controller?.PlantFlag();
        }

        // Board the nearest airlock / part if we're in range. KerbalEVA tracks
        // this via `currentAirlockPart`; if set, we can board directly.
        public static void BoardNearest()
        {
            var eva = Controller;
            if (eva == null) return;
            var airlock = Reflector.Get<Part>(eva, "currentAirlockPart");
            if (airlock != null) { eva.BoardPart(airlock); return; }

            // Fall back to KerbalSeat within a short range.
            var myPos = eva.transform.position;
            KerbalSeat closest = null;
            float closestDist = float.MaxValue;
            foreach (var seat in Object.FindObjectsOfType<KerbalSeat>())
            {
                if (seat == null || seat.Occupant != null) continue;
                float d = (seat.transform.position - myPos).sqrMagnitude;
                if (d < closestDist) { closestDist = d; closest = seat; }
            }
            if (closest != null && closestDist < 9f) eva.BoardSeat(closest);
            else ScreenMessages.PostScreenMessage("Nothing to board in range", 1.2f, ScreenMessageStyle.UPPER_CENTER);
        }

        // Grab / release ladder. KerbalEVA has FSM events for this;
        // simplest external trigger is to write the State via KerbalFSM.
        public static void ToggleLadder()
        {
            var eva = Controller;
            if (eva == null) return;
            if (eva.OnALadder)
            {
                // Let go — nudge them off the ladder by a short impulse backwards.
                var rb = Reflector.Get<Rigidbody>(eva, "_rigidbody");
                if (rb != null) rb.AddForce(-eva.transform.forward * 5f, ForceMode.Impulse);
            }
            else
            {
                // Nothing trivial to do — the kerbal auto-grabs when touching a
                // ladder. Show a hint.
                ScreenMessages.PostScreenMessage("Walk into a ladder to grab it", 1.2f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        public static void NextKerbal()
        {
            var current = FlightGlobals.ActiveVessel;
            var kerbals = FlightGlobals.Vessels.Where(v => v != null && v.isEVA && v.loaded && v != current).ToList();
            if (kerbals.Count == 0)
            {
                ScreenMessages.PostScreenMessage("No other kerbals in range", 1.2f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            FlightGlobals.SetActiveVessel(kerbals[0]);
        }
    }
}
