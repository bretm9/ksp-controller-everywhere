using UnityEngine;

namespace ControllerEverywhere
{
    // Flight-scene hub. Three modal layers:
    //   1) Hold Back  = SAS mode picker (dpad/face/bumpers pick navball marker)
    //   2) Hold Home  = meta modifier (AG 1-10, quick save/load, vessel switch)
    //   3) Map open   = maneuver node editor (face buttons create/delete/radial,
    //                    dpad nudges prograde/normal, bumpers cycle nodes,
    //                    LS/RS nudge UT)
    // Otherwise normal flight bindings.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FlightAddon : MonoBehaviour
    {
        private Vessel _hookedVessel;
        private RadialMenu _radial = new RadialMenu();

        // Back is dual-purpose: quick-hold = SAS mode picker, long-hold = meta modifier.
        // Threshold in seconds; crossover flips mode and announces it once.
        private const float MetaHoldThreshold = 0.4f;
        private float _backHeldTime;
        private bool  _metaAnnounced;

        // LS is a tap-for-precision, hold-for-zoom-chord dual-role. Track which.
        private float _lsHeldTime;
        private bool  _lsUsedAsChord;

        // Our authoritative throttle target. Trigger deltas accumulate into this
        // so throttle stays where the user left it and isn't reset by KSP each
        // frame. Synced from state.mainThrottle when outside input (e.g. Shift
        // key) moves it, so keyboard + controller coexist.
        private float _targetThrottle = 0f;
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
            // 8-slice wheel; slice 0 is North and indices proceed clockwise.
            _radial.SetSlices(new System.Collections.Generic.List<RadialMenu.Slice>
            {
                new RadialMenu.Slice("Set Target",     FlightActions.SetTargetAtReticle),      // N
                new RadialMenu.Slice("Focus Cam",      FlightActions.FocusCameraOnReticle),    // NE
                new RadialMenu.Slice("SAS Hold Dir",   FlightActions.ToggleSasHoldDirection),  // E
                new RadialMenu.Slice("Toggle IVA",     FlightActions.ToggleIVA),               // SE
                new RadialMenu.Slice("Clear Target",   FlightActions.ClearTarget),             // S
                new RadialMenu.Slice("Warp to Node",   FlightActions.WarpToNextNode),          // SW
                new RadialMenu.Slice("Switch Vessel",  FlightActions.SwitchToVesselAtReticle), // W
                new RadialMenu.Slice("Quick Save",     FlightActions.QuickSave),               // NW
            });
        }

        void OnDestroy()
        {
            GameEvents.onVesselChange.Remove(OnVesselChange);
            UnhookVessel();
        }

        // Per-vessel OnFlyByWire is the reliable callback (the static
        // FlightInputHandler one doesn't always fire). We re-hook on vessel
        // switch events so controls follow the active craft.
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

            // Safety: re-hook in case vessel change fired before Start.
            if (_hookedVessel != FlightGlobals.ActiveVessel) HookActiveVessel();

            // Debug overlay chord (LS + RS + Back held ~0.5s)
            DebugOverlay.Poll(p);

            // Radial menu owns the right stick while open — run first so we can
            // gate camera/RS handling below.
            bool radialActive = _radial.Update(p);

            // Track LS tap vs hold (hold = zoom chord)
            if (p.LS)
            {
                _lsHeldTime += Time.unscaledDeltaTime;
                if (Mathf.Abs(p.RightTrigger - p.LeftTrigger) > 0.05f) _lsUsedAsChord = true;
            }

            // Right stick → camera always. Triggers → camera zoom *only when LS
            // is held* (chord); otherwise they throttle via OnFlyByWire.
            if (!radialActive)
            {
                float zoom = p.LS ? (p.RightTrigger - p.LeftTrigger) : 0f;
                CameraControl.Flight(p.RightStick, zoom, Time.unscaledDeltaTime);
            }

            // Track Back-hold duration and announce the mode flip once.
            if (p.Back) _backHeldTime += Time.unscaledDeltaTime;
            else { _backHeldTime = 0f; _metaAnnounced = false; }
            bool inMeta = p.Back && _backHeldTime >= MetaHoldThreshold;
            bool inSas  = p.Back && !inMeta;
            if (inMeta && !_metaAnnounced)
            {
                ScreenMessages.PostScreenMessage("META", 1.0f, ScreenMessageStyle.UPPER_CENTER);
                _metaAnnounced = true;
            }

            // Start = pause, except when it's being used as a meta modifier combo.
            if (!inMeta && ControllerInput.Pressed(s => s.Start)) TogglePauseMenu();

            // RS tap (short press + release, menu didn't open) → toggle map
            if (_radial.ConsumePendingMapToggle()) ToggleMapView();

            if (radialActive) return;

            if (inSas)       DispatchSasMode(p);
            else if (inMeta) DispatchMetaMode(p);
            else if (PAWNavigator.AnyOpen) DispatchPAWMode(p);
            else if (MapView.MapIsEnabled) DispatchMapMode(p);
            else             DispatchNormalMode(p);
        }

        // ---- PAW open: uGUI dpad nav, B closes ----------------------------------
        private void DispatchPAWMode(ControllerInput.Pad p)
        {
            // Edge-detect LB+RB chord for open/close toggle so user can close PAW
            // with the same combo that opened it.
            if (p.LB && p.RB && (!ControllerInput.Previous.LB || !ControllerInput.Previous.RB))
            {
                FlightActions.ToggleReticlePAW();
                return;
            }

            // Delegate dpad/A/B into the PAW via EventSystem.
            PAWNavigator.Tick(p);
        }

        // ---- Normal flight ------------------------------------------------------
        private void DispatchNormalMode(ControllerInput.Pad p)
        {
            // Stage only when Y isn't held — Y+A is the abort chord.
            if (!p.Y && ControllerInput.Pressed(s => s.A)) Stage();
            if (ControllerInput.Pressed(s => s.X)) Toggle(KSPActionGroup.SAS);
            if (ControllerInput.Released(s => s.Y) && !p.A) Toggle(KSPActionGroup.RCS);
            if (ControllerInput.Pressed(s => s.B)) Toggle(KSPActionGroup.Gear);

            HandleLSTapVsHold(p);
            HandlePAWChord(p);

            // DPad L/R = time warp; DPad U/D = camera mode.
            if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) FlightActions.WarpFaster();
            if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) FlightActions.WarpSlower();
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) FlightActions.CycleCameraMode();
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) FlightActions.ResetCameraTarget();

            // Y held + A = abort (safety chord; stage is suppressed when Y held)
            if (p.Y && ControllerInput.Pressed(s => s.A)) Toggle(KSPActionGroup.Abort);
        }

        // LS tap → precision. LS hold gets consumed by the trigger-zoom chord or
        // (in map mode) a dpad UT nudge; either sets _lsUsedAsChord so tap doesn't fire.
        private void HandleLSTapVsHold(ControllerInput.Pad p)
        {
            if (ControllerInput.Released(s => s.LS))
            {
                bool wasTap = _lsHeldTime < 0.25f && !_lsUsedAsChord && !(p.LB && p.RB);
                if (wasTap && FlightInputHandler.fetch != null)
                    FlightInputHandler.fetch.precisionMode = !FlightInputHandler.fetch.precisionMode;
                _lsHeldTime = 0f;
                _lsUsedAsChord = false;
            }
        }

        private void HandlePAWChord(ControllerInput.Pad p)
        {
            if (p.LB && p.RB && (!ControllerInput.Previous.LB || !ControllerInput.Previous.RB))
                FlightActions.ToggleReticlePAW();
        }

        // ---- Hold Back: SAS mode picker ----------------------------------------
        private void DispatchSasMode(ControllerInput.Pad p)
        {
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) SetSas(VesselAutopilot.AutopilotMode.Prograde);
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) SetSas(VesselAutopilot.AutopilotMode.Retrograde);
            if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) SetSas(VesselAutopilot.AutopilotMode.Normal);
            if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) SetSas(VesselAutopilot.AutopilotMode.Antinormal);
            if (ControllerInput.Pressed(s => s.A))  SetSas(VesselAutopilot.AutopilotMode.Target);
            if (ControllerInput.Pressed(s => s.B))  SetSas(VesselAutopilot.AutopilotMode.AntiTarget);
            if (ControllerInput.Pressed(s => s.X))  SetSas(VesselAutopilot.AutopilotMode.RadialIn);
            if (ControllerInput.Pressed(s => s.Y))  SetSas(VesselAutopilot.AutopilotMode.RadialOut);
            if (ControllerInput.Pressed(s => s.LB)) SetSas(VesselAutopilot.AutopilotMode.StabilityAssist);
            if (ControllerInput.Pressed(s => s.RB)) SetSas(VesselAutopilot.AutopilotMode.Maneuver);
        }

        // ---- Long-press Back: meta modifier ------------------------------------
        private void DispatchMetaMode(ControllerInput.Pad p)
        {
            if (ControllerInput.Pressed(s => s.A))  FlightActions.ToggleAG(KSPActionGroup.Custom01);
            if (ControllerInput.Pressed(s => s.B))  FlightActions.ToggleAG(KSPActionGroup.Custom02);
            if (ControllerInput.Pressed(s => s.X))  FlightActions.ToggleAG(KSPActionGroup.Custom03);
            if (ControllerInput.Pressed(s => s.Y))  FlightActions.ToggleAG(KSPActionGroup.Custom04);
            if (ControllerInput.Pressed(s => s.LB)) FlightActions.ToggleAG(KSPActionGroup.Custom05);
            if (ControllerInput.Pressed(s => s.RB)) FlightActions.ToggleAG(KSPActionGroup.Custom06);
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) FlightActions.ToggleAG(KSPActionGroup.Custom07);
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) FlightActions.ToggleAG(KSPActionGroup.Custom08);
            if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) FlightActions.ToggleAG(KSPActionGroup.Custom09);
            if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) FlightActions.ToggleAG(KSPActionGroup.Custom10);

            // Quick save is radial-only now (NW slice); Start = Quick load in meta.
            if (ControllerInput.Pressed(s => s.Start)) FlightActions.QuickLoad();
            if (ControllerInput.Pressed(s => s.LS))    FlightActions.SwitchVessel(-1);
            if (ControllerInput.Pressed(s => s.RS))    FlightActions.SwitchVessel(+1);
        }

        // ---- Map view: additive on top of normal flight ------------------------
        // Full flight stays live (throttle, pitch/yaw, camera). Bumpers repurpose
        // to cycle nodes, face buttons to node create/delete + SAS/RCS (kept),
        // dpad to prograde/retrograde + time warp. Normal/antinormal and radial
        // nudges live under the Y modifier. UT nudge on LS-hold + DPad ←→.
        private void DispatchMapMode(ControllerInput.Pad p)
        {
            // Face buttons: A creates / Y-chord aborts; B deletes; X SAS; Y RCS.
            if (!p.Y && ControllerInput.Pressed(s => s.A)) FlightActions.CreateManeuverNode();
            if (ControllerInput.Pressed(s => s.B))         FlightActions.DeleteSelectedNode();
            if (ControllerInput.Pressed(s => s.X))         Toggle(KSPActionGroup.SAS);
            if (ControllerInput.Released(s => s.Y) && !p.A) Toggle(KSPActionGroup.RCS);
            if (p.Y && ControllerInput.Pressed(s => s.A))  Toggle(KSPActionGroup.Abort);

            // Bumpers cycle maneuver nodes (replaces roll in map mode).
            if (ControllerInput.Pressed(s => s.LB)) FlightActions.CycleNode(-1);
            if (ControllerInput.Pressed(s => s.RB)) FlightActions.CycleNode(+1);

            HandleLSTapVsHold(p);
            HandlePAWChord(p);

            // --- Time warp vs UT nudge on DPad ←→ ---
            // Normal:       DPad ←→ = time warp
            // LS-held chord: DPad ←→ = nudge UT earlier / later
            float dt = Time.fixedDeltaTime > 0 ? Time.fixedDeltaTime : Time.unscaledDeltaTime;
            if (p.LS)
            {
                float dUT = 0f;
                if (p.Dpad.x < -0.5f) { dUT = -5f * dt; _lsUsedAsChord = true; }
                if (p.Dpad.x >  0.5f) { dUT = +5f * dt; _lsUsedAsChord = true; }
                if (dUT != 0f) FlightActions.NudgeNode(0, 0, 0, dUT);
            }
            else
            {
                if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) FlightActions.WarpFaster();
                if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) FlightActions.WarpSlower();
            }

            // --- dV nudges on DPad ↑↓ + triggers ---
            // No Y:     DPad ↑↓ = prograde / retrograde
            // Y held:   DPad ↑↓ = normal / antinormal,  LT/RT = radial-in / radial-out
            float rate = 1.5f;          // m/s per second of hold
            float dProg = 0f, dNorm = 0f, dRad = 0f;
            float dy = p.Dpad.y > 0.5f ? 1f : p.Dpad.y < -0.5f ? -1f : 0f;
            if (p.Y)
            {
                dNorm = dy * rate * dt;
                dRad  = (p.RightTrigger - p.LeftTrigger) * rate * dt;
            }
            else
            {
                dProg = dy * rate * dt;
            }
            if (dProg != 0f || dNorm != 0f || dRad != 0f)
                FlightActions.NudgeNode(dRad, dNorm, dProg, 0f);
        }

        // ---- Reticle overlay + radial menu -------------------------------------
        void OnGUI()
        {
            // Center crosshair for PAW aiming. Hidden in map view.
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

            // Per-button glyphs + always-visible mode badge & key strip +
            // contextual cheat sheet.
            HudHints.Draw(
                inSas:      _backHeldTime > 0f && _backHeldTime <  MetaHoldThreshold,
                inMeta:     _backHeldTime >= MetaHoldThreshold,
                inMap:      MapView.MapIsEnabled,
                inPaw:      PAWNavigator.AnyOpen,
                radialOpen: _radial.IsOpen);

            // Highlight box around the currently-focused PAW selectable
            if (PAWNavigator.AnyOpen) PAWNavigator.DrawHighlight();

            _radial.OnGUI();

            // Debug axis readout on top of everything
            DebugOverlay.Draw();
        }

        // ---- OnFlyByWire (fly-axis driver) -------------------------------------
        private void OnFlyByWire(FlightCtrlState s)
        {
            var p = ControllerInput.Current;

            // Suppress flight axis input when a mode modifier is held.
            if (p.Back) return;

            float precision = (FlightInputHandler.fetch != null && FlightInputHandler.fetch.precisionMode) ? 0.5f : 1f;
            float pitchIn = -p.LeftStick.y;
            float yawIn   =  p.LeftStick.x;
            float rollIn  = (p.RB ? 1f : 0f) - (p.LB ? 1f : 0f);

            s.pitch = Mathf.Clamp(s.pitch + pitchIn * precision, -1f, 1f);
            s.yaw   = Mathf.Clamp(s.yaw   + yawIn   * precision, -1f, 1f);
            s.roll  = Mathf.Clamp(s.roll  + rollIn  * precision, -1f, 1f);

            // On first call, take KSP's throttle as the starting target so we
            // don't snap to 0 on entry into the flight scene.
            if (!_throttleInitialized) { _targetThrottle = s.mainThrottle; _throttleInitialized = true; }

            // Sync from keyboard / stock throttle keys. GameSettings exposes the
            // "throttle up / down / zero / full" bindings, which are the only
            // external sources that should move the target. We deliberately do
            // NOT adopt state.mainThrottle itself — KSP re-zeros it every frame
            // when the keyboard isn't pressed, which would reset our target.
            if (GameSettings.THROTTLE_FULL.GetKeyDown())  _targetThrottle = 1f;
            if (GameSettings.THROTTLE_CUTOFF.GetKeyDown()) _targetThrottle = 0f;
            if (GameSettings.THROTTLE_UP.GetKey())   _targetThrottle = Mathf.Clamp01(_targetThrottle + Time.fixedDeltaTime);
            if (GameSettings.THROTTLE_DOWN.GetKey()) _targetThrottle = Mathf.Clamp01(_targetThrottle - Time.fixedDeltaTime);

            // Triggers drive throttle UNLESS LS is held (then they zoom camera).
            // Full trigger = ~1.0/sec (0 → 100% in one second).
            if (!p.LS)
            {
                float dThr = (p.RightTrigger - p.LeftTrigger) * Time.fixedDeltaTime;
                _targetThrottle = Mathf.Clamp01(_targetThrottle + dThr);
                s.wheelThrottle = Mathf.Clamp(s.wheelThrottle + (p.RightTrigger - p.LeftTrigger), -1f, 1f);
            }

            // Force our target into the state + persistence fields every frame so
            // KSP's between-frame reset doesn't eat it.
            s.mainThrottle = _targetThrottle;
            if (FlightInputHandler.fetch != null)
                Reflector.Set(FlightInputHandler.fetch, "axisThrottle", _targetThrottle);

            s.wheelSteer = Mathf.Clamp(s.wheelSteer + yawIn, -1f, 1f);

            if (p.Y && FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.ActionGroups[KSPActionGroup.RCS])
            {
                s.X = Mathf.Clamp(s.X + p.LeftStick.x, -1f, 1f);
                s.Y = Mathf.Clamp(s.Y + p.LeftStick.y, -1f, 1f);
                s.Z = Mathf.Clamp(s.Z + rollIn,         -1f, 1f);
                s.pitch = 0f; s.yaw = 0f; s.roll = 0f;
            }
        }

        // ---- Primitive actions --------------------------------------------------
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
