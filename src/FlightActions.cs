using System.Linq;
using UnityEngine;

namespace ControllerEverywhere
{
    // Helpers for flight-scene "button-style" actions: time warp, quick save/load,
    // action groups, vessel switch, PAW open/close, maneuver node create/edit/delete.
    // Kept separate from FlightAddon to keep the dispatcher readable.
    internal static class FlightActions
    {
        // ---- Camera shortcuts --------------------------------------------------

        public static void CycleCameraMode()
        {
            if (FlightCamera.fetch == null) return;
            FlightCamera.fetch.SetNextMode();
            ScreenMessages.PostScreenMessage("Camera: " + FlightCamera.fetch.mode, 1f, ScreenMessageStyle.UPPER_CENTER);
        }

        public static void ResetCameraTarget()
        {
            if (FlightCamera.fetch == null) return;
            FlightCamera.fetch.TargetActiveVessel();
        }

        // ---- Time warp ----------------------------------------------------------

        public static void WarpFaster()
        {
            var tw = TimeWarp.fetch;
            if (tw == null) return;
            int max = (tw.Mode == TimeWarp.Modes.HIGH ? tw.warpRates.Length : tw.physicsWarpRates.Length) - 1;
            TimeWarp.SetRate(Mathf.Min(TimeWarp.CurrentRateIndex + 1, max), false, true);
        }

        public static void WarpSlower()
        {
            TimeWarp.SetRate(Mathf.Max(TimeWarp.CurrentRateIndex - 1, 0), false, true);
        }

        // ---- Quick save / load --------------------------------------------------

        public static void QuickSave()
        {
            try { QuickSaveLoad.QuickSave(); }
            catch (System.Exception ex) { Log.Warn("Quick save failed: " + ex.Message); }
        }

        public static void QuickLoad()
        {
            try
            {
                var game = GamePersistence.LoadGame("quicksave", HighLogic.SaveFolder, true, false);
                if (game == null || !game.compatible)
                {
                    ScreenMessages.PostScreenMessage("No compatible quicksave", 2f, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }
                if (game.flightState != null)
                    FlightDriver.StartAndFocusVessel(game, game.flightState.activeVesselIdx);
                else
                    HighLogic.LoadScene(GameScenes.SPACECENTER);
            }
            catch (System.Exception ex) { Log.Warn("Quick load failed: " + ex.Message); }
        }

        // ---- Action groups ------------------------------------------------------

        public static void ToggleAG(KSPActionGroup g)
        {
            var v = FlightGlobals.ActiveVessel;
            if (v != null) v.ActionGroups.ToggleGroup(g);
            ScreenMessages.PostScreenMessage("AG: " + g, 1f, ScreenMessageStyle.UPPER_CENTER);
        }

        // ---- Vessel switching ---------------------------------------------------

        public static void SwitchVessel(int delta)
        {
            var loaded = FlightGlobals.Vessels.Where(v => v != null && v.loaded && v != FlightGlobals.ActiveVessel).ToList();
            if (loaded.Count == 0)
            {
                ScreenMessages.PostScreenMessage("No other vessel in physics range", 1.5f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            // Sort by distance so "next" feels predictable.
            var active = FlightGlobals.ActiveVessel;
            loaded.Sort((a, b) =>
            {
                float da = active != null ? (float)(a.GetWorldPos3D() - active.GetWorldPos3D()).magnitude : 0f;
                float db = active != null ? (float)(b.GetWorldPos3D() - active.GetWorldPos3D()).magnitude : 0f;
                return da.CompareTo(db);
            });
            int i = delta > 0 ? 0 : loaded.Count - 1;
            FlightGlobals.SetActiveVessel(loaded[i]);
        }

        // ---- Part Action Window via center reticle -----------------------------

        public static void ToggleReticlePAW()
        {
            var ctrl = UIPartActionController.Instance;
            if (ctrl == null) return;

            var target = PickPartAtReticle();
            if (target == null)
            {
                // Fallback: open PAW for the root part so the user gets *something* useful.
                var v = FlightGlobals.ActiveVessel;
                target = v != null ? v.rootPart : null;
            }
            if (target == null) return;

            if (ctrl.ItemListContains(target, false))
            {
                // Already open — toggle off by unpinning and asking the controller to close.
                var w = ctrl.ItemListGet(target);
                if (w != null) Reflector.Set(w, "pinned", false);
                ctrl.Deselect(false);
            }
            else
            {
                ctrl.SpawnPartActionWindow(target);
            }
        }

        public static Part PickPartAtReticle()
        {
            var cam = FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : Camera.main;
            if (cam == null) return null;
            // Cast from screen center — the "reticle" the user sees.
            var ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            // Limit to the "parts" layer to avoid hitting scenery.
            int partMask = 1 << 0;   // Default — KSP parts live on layer 0/15 depending on scene; cast wide.
            partMask = ~0;           // Fine: just hit everything and filter by Part component.
            if (Physics.Raycast(ray, out var hit, 2500f, partMask))
            {
                var p = hit.collider.GetComponentInParent<Part>();
                return p;
            }
            return null;
        }

        // ---- SAS hold-direction toggle -----------------------------------------

        public static void ToggleSasHoldDirection()
        {
            var v = FlightGlobals.ActiveVessel;
            if (v == null || v.Autopilot == null) return;

            bool sasOn = v.ActionGroups[KSPActionGroup.SAS];
            var mode = v.Autopilot.Mode;

            if (sasOn && mode == VesselAutopilot.AutopilotMode.StabilityAssist)
            {
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                ScreenMessages.PostScreenMessage("SAS off", 1f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            if (!sasOn) v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
            if (v.Autopilot.CanSetMode(VesselAutopilot.AutopilotMode.StabilityAssist))
                v.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
            ScreenMessages.PostScreenMessage("SAS: hold direction", 1f, ScreenMessageStyle.UPPER_CENTER);
        }

        // ---- Targeting (for radial menu) ---------------------------------------

        public static void SetTargetAtReticle()
        {
            var part = PickPartAtReticle();
            if (part == null || part.vessel == null)
            {
                ScreenMessages.PostScreenMessage("No target at reticle", 1.5f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            // If the part is a docking port, target the port specifically — that's
            // what dockers expect (the navball aligns with the docking axis).
            var dock = part.FindModuleImplementing<ModuleDockingNode>();
            if (dock != null) FlightGlobals.fetch.SetVesselTarget(dock, true);
            else              FlightGlobals.fetch.SetVesselTarget(part.vessel, true);
            ScreenMessages.PostScreenMessage("Target: " + part.vessel.vesselName, 2f, ScreenMessageStyle.UPPER_CENTER);
        }

        public static void ClearTarget()
        {
            FlightGlobals.fetch.SetVesselTarget(null, true);
            ScreenMessages.PostScreenMessage("Target cleared", 1.5f, ScreenMessageStyle.UPPER_CENTER);
        }

        public static void FocusCameraOnReticle()
        {
            var part = PickPartAtReticle();
            if (part == null)
            {
                ScreenMessages.PostScreenMessage("Nothing to focus at reticle", 1.5f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (MapView.MapIsEnabled && PlanetariumCamera.fetch != null && part.vessel != null)
            {
                var mo = part.vessel.mapObject;
                if (mo != null) PlanetariumCamera.fetch.SetTarget(mo);
            }
            else if (FlightCamera.fetch != null)
            {
                FlightCamera.fetch.SetTargetPart(part);
            }
        }

        public static void ToggleIVA()
        {
            var cm = CameraManager.Instance;
            if (cm == null) return;
            var mode = cm.currentCameraMode;
            if (mode == CameraManager.CameraMode.IVA || mode == CameraManager.CameraMode.Internal)
                cm.SetCameraFlight();
            else
                cm.SetCameraIVA();
        }

        public static void WarpToNextNode()
        {
            var s = Solver;
            var tw = TimeWarp.fetch;
            if (s == null || tw == null || s.maneuverNodes.Count == 0)
            {
                ScreenMessages.PostScreenMessage("No maneuver node to warp to", 1.5f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            var n = s.maneuverNodes[0];
            // Stop a minute before the burn so the player can line up.
            double ut = n.UT - 60.0;
            if (ut <= Planetarium.GetUniversalTime()) ut = n.UT;
            tw.WarpTo(ut, 100000.0, 1.0);
        }

        public static void SwitchToVesselAtReticle()
        {
            var part = PickPartAtReticle();
            if (part == null || part.vessel == null || part.vessel == FlightGlobals.ActiveVessel)
            {
                ScreenMessages.PostScreenMessage("No other vessel at reticle", 1.5f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (!part.vessel.loaded)
            {
                ScreenMessages.PostScreenMessage("Target out of physics range", 1.5f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            FlightGlobals.SetActiveVessel(part.vessel);
        }

        // ---- Maneuver nodes -----------------------------------------------------

        public static ManeuverNode SelectedNode;

        public static PatchedConicSolver Solver
        {
            get
            {
                var v = FlightGlobals.ActiveVessel;
                return v != null ? v.patchedConicSolver : null;
            }
        }

        public static void CreateManeuverNode()
        {
            var s = Solver;
            var v = FlightGlobals.ActiveVessel;
            if (s == null || v == null || v.orbit == null) return;
            // Default: put the node ~1 minute into the future, or at next Ap/Pe if close.
            double now = Planetarium.GetUniversalTime();
            double ut  = now + 60.0;
            var o = v.orbit;
            if (o.eccentricity < 1.0)
            {
                double toAp = o.timeToAp, toPe = o.timeToPe;
                if (toAp > 0 && toAp < 600) ut = now + toAp;
                else if (toPe > 0 && toPe < 600) ut = now + toPe;
            }
            SelectedNode = s.AddManeuverNode(ut);
            SelectedNode.OnGizmoUpdated(SelectedNode.DeltaV, SelectedNode.UT);
            ScreenMessages.PostScreenMessage("Maneuver node added", 1f, ScreenMessageStyle.UPPER_CENTER);
        }

        public static void DeleteSelectedNode()
        {
            var s = Solver;
            if (s == null || SelectedNode == null) return;
            SelectedNode.RemoveSelf();
            SelectedNode = s.maneuverNodes.Count > 0 ? s.maneuverNodes[0] : null;
            ScreenMessages.PostScreenMessage("Maneuver node removed", 1f, ScreenMessageStyle.UPPER_CENTER);
        }

        public static void CycleNode(int delta)
        {
            var s = Solver;
            if (s == null || s.maneuverNodes.Count == 0) { SelectedNode = null; return; }
            int idx = SelectedNode != null ? s.maneuverNodes.IndexOf(SelectedNode) : -1;
            idx = (idx + delta + s.maneuverNodes.Count) % s.maneuverNodes.Count;
            SelectedNode = s.maneuverNodes[idx];
        }

        // Components for ManeuverNode.DeltaV: x = radial, y = normal, z = prograde.
        // Nudges are BATCHED — accumulating each frame and flushed once per
        // Update() pass via Flush(). OnGizmoUpdated triggers a full patched-conic
        // recomputation, so calling it on every input axis every frame is very
        // expensive; one call per frame is plenty.
        private static Vector3d _pendingDv;
        private static double _pendingDUT;
        private static bool _pendingAny;

        public static void NudgeNode(float dRadial, float dNormal, float dProgr, float dUT)
        {
            if (SelectedNode == null)
            {
                var s = Solver;
                SelectedNode = s != null && s.maneuverNodes.Count > 0 ? s.maneuverNodes[0] : null;
                if (SelectedNode == null) return;
            }
            _pendingDv.x += dRadial;
            _pendingDv.y += dNormal;
            _pendingDv.z += dProgr;
            _pendingDUT  += dUT;
            _pendingAny = true;
        }

        public static void FlushPendingNudges()
        {
            if (!_pendingAny) return;
            if (SelectedNode == null)
            {
                _pendingDv = Vector3d.zero; _pendingDUT = 0; _pendingAny = false;
                return;
            }
            var n = SelectedNode;
            var dv = new Vector3d(n.DeltaV.x + _pendingDv.x, n.DeltaV.y + _pendingDv.y, n.DeltaV.z + _pendingDv.z);
            n.OnGizmoUpdated(dv, n.UT + _pendingDUT);
            _pendingDv = Vector3d.zero;
            _pendingDUT = 0;
            _pendingAny = false;
        }
    }
}
