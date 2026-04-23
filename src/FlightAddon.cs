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
        private bool _hooked;
        private float _dvNudgeMultiplier = 1f;      // toggled fine/coarse for maneuver edit
        private RadialMenu _radial = new RadialMenu();

        // Back is dual-purpose: quick-hold = SAS mode picker, long-hold = meta modifier.
        // Threshold in seconds; crossover flips mode and announces it once.
        private const float MetaHoldThreshold = 0.4f;
        private float _backHeldTime;
        private bool  _metaAnnounced;

        void Awake()
        {
            Bindings.Load();
            Log.Info("FlightAddon awake.");
        }

        void Start()
        {
            FlightInputHandler.OnFlyByWire += OnFlyByWire;
            _hooked = true;
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
            if (_hooked) FlightInputHandler.OnFlyByWire -= OnFlyByWire;
        }

        void Update()
        {
            ControllerInput.Poll();
            var p = ControllerInput.Current;

            // Debug overlay chord (LS + RS + Back held ~0.5s)
            DebugOverlay.Poll(p);

            // Radial menu owns the right stick while open — run first so we can
            // gate camera/RS handling below.
            bool radialActive = _radial.Update(p);

            // Right stick → camera (suppressed while radial open, since it's
            // used for slice selection there).
            if (!radialActive)
            {
                float zoom = p.RightTrigger - p.LeftTrigger;
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
            // Y toggles RCS on press only if it's not being held as the translate/abort modifier.
            if (ControllerInput.Released(s => s.Y) && !p.A) Toggle(KSPActionGroup.RCS);
            if (ControllerInput.Pressed(s => s.B)) Toggle(KSPActionGroup.Gear);

            // LS click = precision mode (only if user isn't chording a bumper for PAW)
            if (!(p.LB && p.RB) && ControllerInput.Pressed(s => s.LS) && FlightInputHandler.fetch != null)
                FlightInputHandler.fetch.precisionMode = !FlightInputHandler.fetch.precisionMode;

            // (RS click is handled at the top of Update via radial menu state — tap opens map, hold opens wheel.)

            // LB+RB chord (both held, edge-triggered on second bumper) = open PAW at reticle.
            // We detect the chord via "both down now, at least one was up last frame."
            if (p.LB && p.RB && (!ControllerInput.Previous.LB || !ControllerInput.Previous.RB))
                FlightActions.ToggleReticlePAW();

            // DPad L/R = time warp; DPad U/D = pitch trim.
            if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) FlightActions.WarpFaster();
            if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) FlightActions.WarpSlower();
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) NudgeTrim(pitch: +0.05f);
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) NudgeTrim(pitch: -0.05f);

            // Y held + A = abort (kept as a safety chord; stage is suppressed when Y held)
            if (p.Y && ControllerInput.Pressed(s => s.A)) Toggle(KSPActionGroup.Abort);
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

        // ---- Map view: maneuver node editor ------------------------------------
        private void DispatchMapMode(ControllerInput.Pad p)
        {
            // Face buttons
            if (ControllerInput.Pressed(s => s.A)) FlightActions.CreateManeuverNode();
            if (ControllerInput.Pressed(s => s.B)) FlightActions.DeleteSelectedNode();
            if (ControllerInput.Pressed(s => s.X)) _dvNudgeMultiplier = Mathf.Max(0.1f, _dvNudgeMultiplier * 0.5f);
            if (ControllerInput.Pressed(s => s.Y)) _dvNudgeMultiplier = Mathf.Min(20f,  _dvNudgeMultiplier * 2f);

            // Bumpers cycle nodes
            if (ControllerInput.Pressed(s => s.LB)) FlightActions.CycleNode(-1);
            if (ControllerInput.Pressed(s => s.RB)) FlightActions.CycleNode(+1);

            // DPad + triggers held = continuous nudges. Scale by unscaled dt so rate
            // is independent of time warp (which we might want to set while planning).
            float dt = Time.unscaledDeltaTime;
            float base_ = 1.0f * _dvNudgeMultiplier;       // 1 m/s per second at neutral multiplier
            float dProg = (p.Dpad.y > 0.5f ? 1f : p.Dpad.y < -0.5f ? -1f : 0f) * base_ * dt;
            float dNorm = (p.Dpad.x < -0.5f ? 1f : p.Dpad.x > 0.5f ? -1f : 0f) * base_ * dt;
            float dRad  = (p.RightTrigger - p.LeftTrigger) * base_ * dt * 0.5f;
            float dUT   = ((p.RS ? 1f : 0f) - (p.LS ? 1f : 0f)) * 5f * dt * _dvNudgeMultiplier;
            if (dProg != 0 || dNorm != 0 || dRad != 0 || dUT != 0)
                FlightActions.NudgeNode(dRad, dNorm, dProg, dUT);

            // Time warp still reachable via Start (ignored for pause above when
            // Home held; here, Start still pauses). Use LB+RB simultaneous to
            // faster-warp, LB+RS to slower-warp — niche, skip for now.
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

            // Per-button glyphs + contextual cheat sheet.
            HudHints.Draw(
                inSas:  _backHeldTime > 0f && _backHeldTime <  MetaHoldThreshold,
                inMeta: _backHeldTime >= MetaHoldThreshold,
                inMap:  MapView.MapIsEnabled,
                inPaw:  PAWNavigator.AnyOpen);

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

            // Suppress flight axis input when any modifier is held so we don't
            // bleed stick motion into the ship while the user is menuing.
            if (p.Back || p.Home) return;

            float precision = (FlightInputHandler.fetch != null && FlightInputHandler.fetch.precisionMode) ? 0.5f : 1f;
            float pitchIn = -p.LeftStick.y;
            float yawIn   =  p.LeftStick.x;
            float rollIn  = (p.RB ? 1f : 0f) - (p.LB ? 1f : 0f);

            s.pitch = Mathf.Clamp(s.pitch + pitchIn * precision, -1f, 1f);
            s.yaw   = Mathf.Clamp(s.yaw   + yawIn   * precision, -1f, 1f);
            s.roll  = Mathf.Clamp(s.roll  + rollIn  * precision, -1f, 1f);

            float dThr = (p.RightTrigger - p.LeftTrigger) * Time.unscaledDeltaTime;
            s.mainThrottle = Mathf.Clamp01(s.mainThrottle + dThr);

            s.wheelSteer    = Mathf.Clamp(s.wheelSteer    + yawIn, -1f, 1f);
            s.wheelThrottle = Mathf.Clamp(s.wheelThrottle + (p.RightTrigger - p.LeftTrigger), -1f, 1f);

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

        private void NudgeTrim(float pitch = 0f, float yaw = 0f, float roll = 0f)
        {
            var v = FlightGlobals.ActiveVessel;
            if (v == null) return;
            v.ctrlState.pitchTrim = Mathf.Clamp(v.ctrlState.pitchTrim + pitch, -1f, 1f);
            v.ctrlState.yawTrim   = Mathf.Clamp(v.ctrlState.yawTrim   + yaw,   -1f, 1f);
            v.ctrlState.rollTrim  = Mathf.Clamp(v.ctrlState.rollTrim  + roll,  -1f, 1f);
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
