using System.Collections.Generic;
using UnityEngine;

namespace ControllerEverywhere
{
    // Console-inspired flight layout (KSP Enhanced Edition as reference):
    //   Left stick  → pitch / yaw       Right stick → camera
    //   LT / RT     → throttle          LB / RB     → roll
    //   DPad ↑↓←→   → SAS Pro/Retro/Normal/Antinormal  (direct, no modifier)
    //   A stage · B cancel · X PAW · Y map · LS SAS · RS RCS
    //   Back tap    → AG radial         Back hold   → modifier
    //   RS hold     → action wheel      LB+RB       → (reserved)
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FlightAddon : MonoBehaviour
    {
        private Vessel _hookedVessel;
        private RadialMenu _actionRadial = new RadialMenu();
        private RadialMenu _agRadial     = new RadialMenu();

        // Back modifier: tap opens AG radial, longer hold + chord is the modifier.
        private const float BackHoldThreshold = 0.2f;
        private float _backHeldTime;
        private bool  _backConsumed;    // true once Back has been used as modifier this press

        // LS tap-vs-hold for precision-vs-zoom-chord.
        private float _lsHeldTime;
        private bool  _lsUsedAsChord;

        private float _targetThrottle;
        private bool  _throttleInitialized;

        void Awake()
        {
            Bindings.Load();
            Log.Info("FlightAddon awake.");
        }

        void Start()
        {
            HookActiveVessel();
            GameEvents.onVesselChange.Add(OnVesselChange);

            // Action wheel (RS-held) — aim-based utilities
            _actionRadial.SetSlices(new List<RadialMenu.Slice>
            {
                new RadialMenu.Slice("Set Target",    FlightActions.SetTargetAtReticle),      // N
                new RadialMenu.Slice("Focus Cam",     FlightActions.FocusCameraOnReticle),    // NE
                new RadialMenu.Slice("SAS Hold Dir",  FlightActions.ToggleSasHoldDirection),  // E
                new RadialMenu.Slice("Toggle IVA",    FlightActions.ToggleIVA),               // SE
                new RadialMenu.Slice("Clear Target",  FlightActions.ClearTarget),             // S
                new RadialMenu.Slice("Warp to Node",  FlightActions.WarpToNextNode),          // SW
                new RadialMenu.Slice("Switch Vessel", FlightActions.SwitchToVesselAtReticle), // W
                new RadialMenu.Slice("Quick Save",    FlightActions.QuickSave),               // NW
            });

            // Action groups wheel (Back-tap) — custom 1-8
            _agRadial.SetSlices(new List<RadialMenu.Slice>
            {
                new RadialMenu.Slice("AG 1", () => FlightActions.ToggleAG(KSPActionGroup.Custom01)),
                new RadialMenu.Slice("AG 2", () => FlightActions.ToggleAG(KSPActionGroup.Custom02)),
                new RadialMenu.Slice("AG 3", () => FlightActions.ToggleAG(KSPActionGroup.Custom03)),
                new RadialMenu.Slice("AG 4", () => FlightActions.ToggleAG(KSPActionGroup.Custom04)),
                new RadialMenu.Slice("AG 5", () => FlightActions.ToggleAG(KSPActionGroup.Custom05)),
                new RadialMenu.Slice("AG 6", () => FlightActions.ToggleAG(KSPActionGroup.Custom06)),
                new RadialMenu.Slice("AG 7", () => FlightActions.ToggleAG(KSPActionGroup.Custom07)),
                new RadialMenu.Slice("AG 8", () => FlightActions.ToggleAG(KSPActionGroup.Custom08)),
            });
            _agRadial.Trigger = RadialMenu.TriggerMode.LatchedAfterHold; // stays open until A/B
        }

        void OnDestroy()
        {
            GameEvents.onVesselChange.Remove(OnVesselChange);
            UnhookVessel();
        }

        private void HookActiveVessel()
        {
            var v = FlightGlobals.ActiveVessel;
            if (v == null || v == _hookedVessel) return;
            UnhookVessel();
            v.OnFlyByWire += OnFlyByWire;
            _hookedVessel = v;
        }

        private void UnhookVessel()
        {
            if (_hookedVessel != null)
            {
                _hookedVessel.OnFlyByWire -= OnFlyByWire;
                _hookedVessel = null;
            }
        }

        private void OnVesselChange(Vessel v)
        {
            UnhookVessel();
            if (v != null)
            {
                v.OnFlyByWire += OnFlyByWire;
                _hookedVessel = v;
            }
        }

        void Update()
        {
            ControllerInput.Poll();
            var p = ControllerInput.Current;

            if (_hookedVessel != FlightGlobals.ActiveVessel) HookActiveVessel();

            DebugOverlay.Poll(p);

            // Radials. Action wheel on RS-held (same as before), AG wheel latched.
            bool radialActive = _actionRadial.Update(p) | _agRadial.Update(p);

            // Track LS tap/hold for zoom chord
            if (p.LS)
            {
                _lsHeldTime += Time.unscaledDeltaTime;
                if (Mathf.Abs(p.RightTrigger - p.LeftTrigger) > 0.05f) _lsUsedAsChord = true;
            }

            // Camera. Right stick + LS-chord zoom (unchanged).
            if (!radialActive)
            {
                float zoom = p.LS ? (p.RightTrigger - p.LeftTrigger) : 0f;
                CameraControl.Flight(p.RightStick, zoom, Time.unscaledDeltaTime);
            }

            // ---- Back press: tap-for-AG-wheel vs hold-for-modifier ----
            if (p.Back) _backHeldTime += Time.unscaledDeltaTime;
            bool backModifier = p.Back && (_backHeldTime >= BackHoldThreshold || _backConsumed);

            // RS tap (short press + release without menu) → toggle map (same as before)
            if (_actionRadial.ConsumePendingMapToggle()) ToggleMapView();

            if (radialActive) return;

            if (backModifier)
            {
                if (DispatchBackModifier(p)) _backConsumed = true;
            }
            else if (PAWNavigator.AnyOpen)
            {
                DispatchPAWMode(p);
            }
            else if (MapView.MapIsEnabled)
            {
                DispatchMapMode(p);
            }
            else
            {
                DispatchNormalMode(p);
            }

            // On Back release, decide: tap → open AG radial; hold → nothing more
            if (ControllerInput.Released(s => s.Back))
            {
                bool wasTap = _backHeldTime < BackHoldThreshold && !_backConsumed;
                if (wasTap) _agRadial.OpenLatched();
                _backHeldTime = 0f;
                _backConsumed = false;
            }
        }

        // ---- Normal flight ------------------------------------------------------
        private void DispatchNormalMode(ControllerInput.Pad p)
        {
            // Face buttons
            if (ControllerInput.Pressed(s => s.A))    Stage();
            if (ControllerInput.Pressed(s => s.X))    FlightActions.ToggleReticlePAW();
            if (ControllerInput.Pressed(s => s.Y))    ToggleMapView();
            // B: close any open dialog — PAW handled in its own dispatch, radial handled above.
            // Currently a no-op in pure flight, reserved for "cancel".

            // SAS modes directly on DPad (primary 4)
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) SetSas(VesselAutopilot.AutopilotMode.Prograde);
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) SetSas(VesselAutopilot.AutopilotMode.Retrograde);
            if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) SetSas(VesselAutopilot.AutopilotMode.Normal);
            if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) SetSas(VesselAutopilot.AutopilotMode.Antinormal);

            // Stick clicks toggle SAS / RCS
            if (ControllerInput.Pressed(s => s.LS) && !_lsUsedAsChord) Toggle(KSPActionGroup.SAS);
            if (ControllerInput.Pressed(s => s.RS)) Toggle(KSPActionGroup.RCS);

            if (ControllerInput.Released(s => s.LS)) { _lsHeldTime = 0f; _lsUsedAsChord = false; }

            if (ControllerInput.Pressed(s => s.Start)) TogglePauseMenu();
        }

        // ---- Back-held modifier -------------------------------------------------
        // Returns true if anything was consumed this frame (locks Back into modifier mode).
        private bool DispatchBackModifier(ControllerInput.Pad p)
        {
            bool consumed = false;

            if (MapView.MapIsEnabled)
            {
                // --- Map-specific overrides (maneuver fine-tune) ---
                float dt = Time.fixedDeltaTime > 0 ? Time.fixedDeltaTime : Time.unscaledDeltaTime;
                float rate = 1.5f;

                // Back + DPad ↑↓ = prograde / retrograde dV nudge (continuous)
                if (p.Dpad.y >  0.5f) { FlightActions.NudgeNode(0, 0, +rate * dt, 0); consumed = true; }
                if (p.Dpad.y < -0.5f) { FlightActions.NudgeNode(0, 0, -rate * dt, 0); consumed = true; }
                // Back + DPad ←→ = normal / antinormal dV
                if (p.Dpad.x >  0.5f) { FlightActions.NudgeNode(0, +rate * dt, 0, 0); consumed = true; }
                if (p.Dpad.x < -0.5f) { FlightActions.NudgeNode(0, -rate * dt, 0, 0); consumed = true; }
                // Back + triggers = radial dV
                float radialIn  = p.RightTrigger - p.LeftTrigger;
                if (Mathf.Abs(radialIn) > 0.05f) { FlightActions.NudgeNode(radialIn * rate * dt, 0, 0, 0); consumed = true; }
                // Back + bumpers = UT earlier/later
                if (p.LB) { FlightActions.NudgeNode(0, 0, 0, -5f * dt); consumed = true; }
                if (p.RB) { FlightActions.NudgeNode(0, 0, 0, +5f * dt); consumed = true; }

                // Back + Y = exit map
                if (ControllerInput.Pressed(s => s.Y)) { ToggleMapView(); consumed = true; }
                return consumed;
            }

            // Non-map Back modifier

            // Extended SAS modes
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) { SetSas(VesselAutopilot.AutopilotMode.RadialIn);   consumed = true; }
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) { SetSas(VesselAutopilot.AutopilotMode.RadialOut);  consumed = true; }
            if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) { FlightActions.WarpSlower(); consumed = true; }
            if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) { FlightActions.WarpFaster(); consumed = true; }

            if (ControllerInput.Pressed(s => s.A)) { SetSas(VesselAutopilot.AutopilotMode.StabilityAssist); consumed = true; }
            if (ControllerInput.Pressed(s => s.B)) { Toggle(KSPActionGroup.Abort); consumed = true; }
            if (ControllerInput.Pressed(s => s.X)) { SetSas(VesselAutopilot.AutopilotMode.Maneuver); consumed = true; }
            if (ControllerInput.Pressed(s => s.Y)) { FlightActions.ToggleIVA(); consumed = true; }
            if (ControllerInput.Pressed(s => s.LB)) { SetSas(VesselAutopilot.AutopilotMode.Target); consumed = true; }
            if (ControllerInput.Pressed(s => s.RB)) { SetSas(VesselAutopilot.AutopilotMode.AntiTarget); consumed = true; }

            // Triggers = quick load (LT) / quick save (RT) on press edge — but triggers are analog.
            // Use a threshold to detect "new pull" without holding.
            if (p.LeftTrigger  > 0.6f && ControllerInput.Previous.LeftTrigger  <= 0.6f) { FlightActions.QuickLoad(); consumed = true; }
            if (p.RightTrigger > 0.6f && ControllerInput.Previous.RightTrigger <= 0.6f) { FlightActions.QuickSave(); consumed = true; }

            if (ControllerInput.Pressed(s => s.LS) && FlightInputHandler.fetch != null)
            {
                FlightInputHandler.fetch.precisionMode = !FlightInputHandler.fetch.precisionMode;
                consumed = true;
            }
            if (ControllerInput.Pressed(s => s.RS)) { FlightActions.CycleCameraMode(); consumed = true; }

            return consumed;
        }

        // ---- Map view: additive on top of normal flight -----------------------
        private void DispatchMapMode(ControllerInput.Pad p)
        {
            // Face buttons — replace stage/gear with node create/delete since
            // you rarely stage from the map screen.
            if (ControllerInput.Pressed(s => s.A)) FlightActions.CreateManeuverNode();
            if (ControllerInput.Pressed(s => s.B)) FlightActions.DeleteSelectedNode();
            if (ControllerInput.Pressed(s => s.X)) FlightActions.ToggleReticlePAW();
            if (ControllerInput.Pressed(s => s.Y)) ToggleMapView();

            // Bumpers cycle maneuver nodes (replaces roll in map mode — roll here is rare).
            if (ControllerInput.Pressed(s => s.LB)) FlightActions.CycleNode(-1);
            if (ControllerInput.Pressed(s => s.RB)) FlightActions.CycleNode(+1);

            // DPad = SAS modes (same as normal) — useful while lining up a burn.
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) SetSas(VesselAutopilot.AutopilotMode.Prograde);
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) SetSas(VesselAutopilot.AutopilotMode.Retrograde);
            if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) SetSas(VesselAutopilot.AutopilotMode.Normal);
            if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) SetSas(VesselAutopilot.AutopilotMode.Antinormal);

            // Stick clicks same as normal
            if (ControllerInput.Pressed(s => s.LS) && !_lsUsedAsChord) Toggle(KSPActionGroup.SAS);
            if (ControllerInput.Pressed(s => s.RS)) Toggle(KSPActionGroup.RCS);
            if (ControllerInput.Released(s => s.LS)) { _lsHeldTime = 0f; _lsUsedAsChord = false; }

            if (ControllerInput.Pressed(s => s.Start)) TogglePauseMenu();

            // Maneuver node fine-tune is reachable under Back modifier (see DispatchBackModifier).
        }

        // ---- PAW open: dpad nav, B closes --------------------------------------
        private void DispatchPAWMode(ControllerInput.Pad p)
        {
            // Flight axes still apply via OnFlyByWire. Here we only drive the PAW.
            PAWNavigator.Tick(p);
        }

        // ---- OnFlyByWire (fly-axis driver) -------------------------------------
        private void OnFlyByWire(FlightCtrlState s)
        {
            var p = ControllerInput.Current;

            // Back modifier suppresses flight axis input so the player can menu
            // without bleeding stick motion into the craft.
            bool suppress = p.Back && (_backHeldTime >= BackHoldThreshold || _backConsumed);
            if (suppress) return;

            float precision = (FlightInputHandler.fetch != null && FlightInputHandler.fetch.precisionMode) ? 0.5f : 1f;
            float pitchIn = -p.LeftStick.y;
            float yawIn   =  p.LeftStick.x;
            float rollIn  = (p.RB ? 1f : 0f) - (p.LB ? 1f : 0f);

            s.pitch = Mathf.Clamp(s.pitch + pitchIn * precision, -1f, 1f);
            s.yaw   = Mathf.Clamp(s.yaw   + yawIn   * precision, -1f, 1f);
            s.roll  = Mathf.Clamp(s.roll  + rollIn  * precision, -1f, 1f);

            if (!_throttleInitialized) { _targetThrottle = s.mainThrottle; _throttleInitialized = true; }

            // Stock keyboard throttle bindings still work alongside the controller.
            if (GameSettings.THROTTLE_FULL.GetKeyDown())  _targetThrottle = 1f;
            if (GameSettings.THROTTLE_CUTOFF.GetKeyDown()) _targetThrottle = 0f;
            if (GameSettings.THROTTLE_UP.GetKey())   _targetThrottle = Mathf.Clamp01(_targetThrottle + Time.fixedDeltaTime);
            if (GameSettings.THROTTLE_DOWN.GetKey()) _targetThrottle = Mathf.Clamp01(_targetThrottle - Time.fixedDeltaTime);

            // Triggers throttle unless LS-held (zoom chord).
            if (!p.LS)
            {
                float dThr = (p.RightTrigger - p.LeftTrigger) * Time.fixedDeltaTime;
                _targetThrottle = Mathf.Clamp01(_targetThrottle + dThr);
                s.wheelThrottle = Mathf.Clamp(s.wheelThrottle + (p.RightTrigger - p.LeftTrigger), -1f, 1f);
            }
            s.mainThrottle = _targetThrottle;
            if (FlightInputHandler.fetch != null)
                Reflector.Set(FlightInputHandler.fetch, "axisThrottle", _targetThrottle);

            s.wheelSteer = Mathf.Clamp(s.wheelSteer + yawIn, -1f, 1f);
        }

        // ---- OnGUI --------------------------------------------------------------
        void OnGUI()
        {
            // Reticle only in flight (hidden in map and when overlays are open).
            if (!MapView.MapIsEnabled && CameraManager.Instance != null)
            {
                var mode = CameraManager.Instance.currentCameraMode;
                if (mode == CameraManager.CameraMode.Flight || mode == CameraManager.CameraMode.Internal)
                {
                    float cx = Screen.width  * 0.5f;
                    float cy = Screen.height * 0.5f;
                    var col = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.55f);
                    GUI.DrawTexture(new Rect(cx - 1f, cy - 9f, 2f, 18f), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx - 9f, cy - 1f, 18f, 2f), Texture2D.whiteTexture);
                    GUI.color = col;
                }
            }

            HudHints.Draw(
                inBackMod: _backConsumed || (ControllerInput.Current.Back && _backHeldTime >= BackHoldThreshold),
                inMap:     MapView.MapIsEnabled,
                inPaw:     PAWNavigator.AnyOpen,
                agOpen:    _agRadial.IsOpen,
                radialOpen:_actionRadial.IsOpen);

            if (PAWNavigator.AnyOpen) PAWNavigator.DrawHighlight();

            _actionRadial.OnGUI();
            _agRadial.OnGUI();

            DebugOverlay.Draw();
        }

        // ---- Primitives ---------------------------------------------------------
        private void Stage() => KSP.UI.Screens.StageManager.ActivateNextStage();

        private void Toggle(KSPActionGroup g)
        {
            var v = FlightGlobals.ActiveVessel;
            if (v != null) v.ActionGroups.ToggleGroup(g);
        }

        private void ToggleMapView()
        {
            if (MapView.MapIsEnabled) MapView.ExitMapView();
            else MapView.EnterMapView();
        }

        private void TogglePauseMenu()
        {
            if (PauseMenu.isOpen) PauseMenu.Close();
            else PauseMenu.Display();
        }

        private void SetSas(VesselAutopilot.AutopilotMode mode)
        {
            var v = FlightGlobals.ActiveVessel;
            if (v == null || v.Autopilot == null) return;
            if (!v.Autopilot.CanSetMode(mode))
            {
                ScreenMessages.PostScreenMessage("SAS: " + mode + " unavailable", 1.5f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            if (!v.ActionGroups[KSPActionGroup.SAS])
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
            v.Autopilot.SetMode(mode);
            ScreenMessages.PostScreenMessage("SAS: " + mode, 1f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}
