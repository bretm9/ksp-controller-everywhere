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
        private RadialMenu _agRadial = new RadialMenu();

        // Back modifier: tap opens AG radial, longer hold + chord is the modifier.
        private const float BackHoldThreshold = 0.2f;
        private float _backHeldTime;
        private bool  _backConsumed;    // true once Back has been used as modifier this press

        // LS tap-vs-hold for RCS-vs-zoom-chord.
        private float _lsHeldTime;
        private bool  _lsUsedAsChord;

        // RS is now the SAS stick: tap = SAS on/off, hold + chord = SAS mode.
        private float _rsHeldTime;
        private bool  _rsUsedAsChord;

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

            // RS-hold is now the SAS modifier — no longer an action wheel. The
            // old action wheel's useful actions (set target, warp to node, etc.)
            // live on Back + DPad chords now.

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

            // Force the virtual cursor ON while the player is in map view with
            // MapCursorMode = "virtual". PopupDialogs auto-activate the cursor
            // on their own (see VirtualCursor.CountActiveDialogs), so nested
            // modals spawned on top of a PAW are already covered.
            bool mapVirtualCursor = MapView.MapIsEnabled && Bindings.MapCursorMode == "virtual";
            VirtualCursor.ForceActive = mapVirtualCursor;
            bool cursorActive = VirtualCursor.Update(p);

            // AG wheel latched — opens on Back-tap below.
            bool radialActive = _agRadial.Update(p);

            // LS tap/hold — zoom chord (+triggers). Reset on each press edge.
            if (ControllerInput.Pressed(s => s.LS)) { _lsHeldTime = 0f; _lsUsedAsChord = false; }
            if (p.LS)
            {
                _lsHeldTime += Time.unscaledDeltaTime;
                if (Mathf.Abs(p.RightTrigger - p.LeftTrigger) > 0.05f) _lsUsedAsChord = true;
            }

            // RS tap/hold — SAS modifier.
            if (ControllerInput.Pressed(s => s.RS)) { _rsHeldTime = 0f; _rsUsedAsChord = false; }
            if (p.RS) _rsHeldTime += Time.unscaledDeltaTime;

            // Camera. Right stick → orbit / pan camera. LS-hold + triggers = zoom
            // chord. Suppressed while radial or cursor owns the right stick.
            if (!radialActive && !cursorActive)
            {
                float zoom = p.LS ? (p.RightTrigger - p.LeftTrigger) : 0f;
                // Don't pan camera from right stick while RS is held — the SAS
                // modifier owns it; otherwise the player's aim drifts.
                Vector2 camStick = p.RS ? Vector2.zero : p.RightStick;
                CameraControl.Flight(camStick, zoom, Time.unscaledDeltaTime);
            }

            // ---- Back press: tap-for-AG-wheel vs hold-for-modifier ----
            if (p.Back) _backHeldTime += Time.unscaledDeltaTime;
            bool backModifier = p.Back && (_backHeldTime >= BackHoldThreshold || _backConsumed);

            if (radialActive) return;
            if (cursorActive) return;

            // RS-hold = SAS modifier. Check chord BEFORE normal dispatch so DPad
            // doesn't also fire warp when the user is picking an extended mode.
            if (!backModifier && p.RS && DispatchRsSasModifier(p))
            {
                _rsUsedAsChord = true;
            }
            else if (backModifier)
            {
                if (DispatchBackModifier(p)) _backConsumed = true;
            }
            else if (PAWNavigator.AnyOpen)
            {
                DispatchPAWMode(p);
            }
            else if (EvaActions.IsActive)
            {
                DispatchEvaMode(p);
            }
            else if (MapView.MapIsEnabled)
            {
                DispatchMapMode(p);
            }
            else
            {
                DispatchNormalMode(p);
            }

            // On RS release: if it was a quick tap with no chord, toggle SAS.
            if (ControllerInput.Released(s => s.RS))
            {
                if (!_rsUsedAsChord && _rsHeldTime < 0.3f) Toggle(KSPActionGroup.SAS);
                _rsHeldTime = 0f; _rsUsedAsChord = false;
            }

            // Apply accumulated maneuver-node nudges once per frame (perf —
            // OnGizmoUpdated does a full patched-conic re-solve).
            FlightActions.FlushPendingNudges();

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
            // B: no-op in pure flight. PAW / cursor / map each handle B in
            // their own dispatch to mean "close / cancel / back".

            // DPad: time warp up/down + camera mode cycle (left/right). All SAS
            // mode selection lives on the RS-hold modifier now.
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) FlightActions.WarpFaster();
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) FlightActions.WarpSlower();
            if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) FlightActions.CycleCameraMode();
            if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) FlightActions.CycleCameraMode();

            // LS tap toggles RCS on release (LS-hold-for-zoom doesn't also fire).
            if (ControllerInput.Released(s => s.LS))
            {
                if (!_lsUsedAsChord && _lsHeldTime < 0.3f) Toggle(KSPActionGroup.RCS);
                _lsHeldTime = 0f; _lsUsedAsChord = false;
            }
            // RS tap toggles SAS — handled in Update's release branch.

            if (ControllerInput.Pressed(s => s.Start)) TogglePauseMenu();
        }

        // ---- RS-held: SAS mode picker ------------------------------------------
        // Holding RS and pressing a button/dpad dir picks any of the 10 SAS modes.
        // On release without a chord, RS toggles SAS on/off (handled in Update).
        //   DPad ← Prograde   DPad → Retrograde
        //   DPad ↑ Normal     DPad ↓ Antinormal
        //   A Stability       X Maneuver
        //   B Radial In       Y Radial Out
        //   LB Target         RB Anti-Target
        private bool DispatchRsSasModifier(ControllerInput.Pad p)
        {
            bool consumed = false;
            if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) { SetSas(VesselAutopilot.AutopilotMode.Prograde);   consumed = true; }
            if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) { SetSas(VesselAutopilot.AutopilotMode.Retrograde); consumed = true; }
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) { SetSas(VesselAutopilot.AutopilotMode.Normal);     consumed = true; }
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) { SetSas(VesselAutopilot.AutopilotMode.Antinormal); consumed = true; }
            if (ControllerInput.Pressed(s => s.A))  { SetSas(VesselAutopilot.AutopilotMode.StabilityAssist); consumed = true; }
            if (ControllerInput.Pressed(s => s.B))  { SetSas(VesselAutopilot.AutopilotMode.RadialIn);        consumed = true; }
            if (ControllerInput.Pressed(s => s.X))  { SetSas(VesselAutopilot.AutopilotMode.Maneuver);        consumed = true; }
            if (ControllerInput.Pressed(s => s.Y))  { SetSas(VesselAutopilot.AutopilotMode.RadialOut);       consumed = true; }
            if (ControllerInput.Pressed(s => s.LB)) { SetSas(VesselAutopilot.AutopilotMode.Target);          consumed = true; }
            if (ControllerInput.Pressed(s => s.RB)) { SetSas(VesselAutopilot.AutopilotMode.AntiTarget);      consumed = true; }
            return consumed;
        }

        // ---- Back-held modifier -------------------------------------------------
        // Returns true if anything was consumed this frame (locks Back into modifier mode).
        private bool DispatchBackModifier(ControllerInput.Pad p)
        {
            bool consumed = false;

            if (EvaActions.IsActive)
            {
                // EVA-specific Back chords layered over the common modifier set.
                if (ControllerInput.Pressed(s => s.X)) { EvaActions.NextKerbal(); consumed = true; }
                if (ControllerInput.Pressed(s => s.Y)) { EvaActions.ToggleJetpack(); consumed = true; }
                // Fall through for the common modifier set (warp / save / SAS).
            }

            if (MapView.MapIsEnabled && Bindings.MapCursorMode == "orbit")
            {
                // --- Map-specific Back chords (orbit-cursor mode) ---
                if (ControllerInput.Pressed(s => s.A)) { OrbitCursor.SnapToAp();            consumed = true; }
                if (ControllerInput.Pressed(s => s.B)) { OrbitCursor.SnapToPe();            consumed = true; }
                if (ControllerInput.Pressed(s => s.X)) { OrbitCursor.SnapToTargetClosest(); consumed = true; }
                if (ControllerInput.Pressed(s => s.Y)) { ToggleMapView();                   consumed = true; }

                float dt = Time.fixedDeltaTime > 0 ? Time.fixedDeltaTime : Time.unscaledDeltaTime;
                float rate = 1.5f;
                if (p.Dpad.x >  0.5f) { FlightActions.NudgeNode(0, +rate * dt, 0, 0); consumed = true; }
                if (p.Dpad.x < -0.5f) { FlightActions.NudgeNode(0, -rate * dt, 0, 0); consumed = true; }
                float radialIn  = p.RightTrigger - p.LeftTrigger;
                if (Mathf.Abs(radialIn) > 0.05f) { FlightActions.NudgeNode(radialIn * rate * dt, 0, 0, 0); consumed = true; }
                if (p.LB) { FlightActions.NudgeNode(0, 0, 0, -5f * dt); consumed = true; }
                if (p.RB) { FlightActions.NudgeNode(0, 0, 0, +5f * dt); consumed = true; }
                return consumed;
            }

            // Non-map Back modifier — utility chords only. SAS modes moved
            // to RS-held; Back is now "meta actions" + targeting shortcuts.
            if (ControllerInput.Pressed(s => s.A)) { Toggle(KSPActionGroup.Abort); consumed = true; }
            if (ControllerInput.Pressed(s => s.B)) { Toggle(KSPActionGroup.Gear); consumed = true; }
            if (ControllerInput.Pressed(s => s.X)) { Toggle(KSPActionGroup.Light); consumed = true; }
            if (ControllerInput.Pressed(s => s.Y)) { FlightActions.ToggleIVA(); consumed = true; }
            if (ControllerInput.Pressed(s => s.LB)) { FlightActions.SwitchVessel(-1); consumed = true; }
            if (ControllerInput.Pressed(s => s.RB)) { FlightActions.SwitchVessel(+1); consumed = true; }

            // Back + DPad: utility actions that used to live on the RS action wheel.
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) { FlightActions.WarpToNextNode();       consumed = true; }
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) { FlightActions.FocusCameraOnReticle();  consumed = true; }
            if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) { FlightActions.SetTargetAtReticle();    consumed = true; }
            if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) { FlightActions.ClearTarget();           consumed = true; }

            // Triggers = quick load (LT) / quick save (RT) on press edge — analog, use threshold.
            if (p.LeftTrigger  > 0.6f && ControllerInput.Previous.LeftTrigger  <= 0.6f) { FlightActions.QuickLoad(); consumed = true; }
            if (p.RightTrigger > 0.6f && ControllerInput.Previous.RightTrigger <= 0.6f) { FlightActions.QuickSave(); consumed = true; }

            if (ControllerInput.Pressed(s => s.LS) && FlightInputHandler.fetch != null)
            {
                FlightInputHandler.fetch.precisionMode = !FlightInputHandler.fetch.precisionMode;
                consumed = true;
            }

            return consumed;
        }

        // ---- Map view ----------------------------------------------------------
        // Two modes selectable via map.cursorMode in controller.cfg:
        //   "orbit"   — orbit-cursor planning: L-stick slides cursor on orbit, A places.
        //   "virtual" — stock mouse cursor forced on; player clicks KSP UI directly.
        private void DispatchMapMode(ControllerInput.Pad p)
        {
            bool orbitMode = Bindings.MapCursorMode == "orbit";

            if (orbitMode)
            {
                OrbitCursor.Update(p);
                if (ControllerInput.Pressed(s => s.A)) OrbitCursor.PlaceNode();
                if (ControllerInput.Pressed(s => s.X)) FlightActions.DeleteSelectedNode();
            }
            // In virtual-cursor mode: A/X/right-stick are consumed by the cursor.
            // (See early-return in Update when cursorActive == true.)

            if (ControllerInput.Pressed(s => s.B)) ToggleMapView();
            if (ControllerInput.Pressed(s => s.Y)) ToggleMapView();

            if (ControllerInput.Pressed(s => s.LB)) FlightActions.CycleNode(-1);
            if (ControllerInput.Pressed(s => s.RB)) FlightActions.CycleNode(+1);

            // DPad: warp (↑↓) always; ←→ direct pro/retro dV nudge in orbit mode only.
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) FlightActions.WarpFaster();
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) FlightActions.WarpSlower();
            if (orbitMode)
            {
                float dtMap = Time.fixedDeltaTime > 0 ? Time.fixedDeltaTime : Time.unscaledDeltaTime;
                float nudgeRate = 1.5f;
                if (p.Dpad.x < -0.5f) FlightActions.NudgeNode(0, 0, -nudgeRate * dtMap, 0);
                if (p.Dpad.x >  0.5f) FlightActions.NudgeNode(0, 0, +nudgeRate * dtMap, 0);
            }

            // Stick clicks follow the normal-mode mapping (LS = RCS, RS = SAS).
            if (ControllerInput.Released(s => s.LS))
            {
                if (!_lsUsedAsChord && _lsHeldTime < 0.3f) Toggle(KSPActionGroup.RCS);
                _lsHeldTime = 0f; _lsUsedAsChord = false;
            }

            if (ControllerInput.Pressed(s => s.Start)) TogglePauseMenu();
        }

        // ---- PAW open: dpad nav, B closes --------------------------------------
        private void DispatchPAWMode(ControllerInput.Pad p)
        {
            // Flight axes still apply via OnFlyByWire. Here we only drive the PAW.
            PAWNavigator.Tick(p);
        }

        // ---- EVA kerbal on foot / in jetpack ----------------------------------
        private void DispatchEvaMode(ControllerInput.Pad p)
        {
            // Walk/jetpack direction from the left stick (local kerbal space).
            EvaActions.Walk(p.LeftStick);

            // Face buttons: console EVA scheme
            if (ControllerInput.Pressed(s => s.A)) EvaActions.Jump();
            if (ControllerInput.Pressed(s => s.B)) EvaActions.BoardNearest();
            if (ControllerInput.Pressed(s => s.X)) EvaActions.PlantFlag();
            if (ControllerInput.Pressed(s => s.Y)) EvaActions.ToggleJetpack();

            // Bumpers — LB/RB still roll the kerbal (useful in jetpack).

            // DPad: time warp + primary SAS (matches flight).
            if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) FlightActions.WarpFaster();
            if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) FlightActions.WarpSlower();
            if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) SetSas(VesselAutopilot.AutopilotMode.Prograde);
            if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) SetSas(VesselAutopilot.AutopilotMode.Retrograde);

            // Stick clicks: LS tap toggles helmet lamp (kerbals have no RCS);
            // RS tap toggles SAS (handled in Update release branch).
            if (ControllerInput.Released(s => s.LS))
            {
                if (!_lsUsedAsChord && _lsHeldTime < 0.3f) EvaActions.ToggleLamp();
                _lsHeldTime = 0f; _lsUsedAsChord = false;
            }

            if (ControllerInput.Pressed(s => s.Start)) TogglePauseMenu();
        }

        // ---- OnFlyByWire (fly-axis driver) -------------------------------------
        private void OnFlyByWire(FlightCtrlState s)
        {
            var p = ControllerInput.Current;

            // Back modifier suppresses flight axis input so the player can menu
            // without bleeding stick motion into the craft.
            bool suppress = p.Back && (_backHeldTime >= BackHoldThreshold || _backConsumed);
            if (suppress) return;

            // EVA — DispatchEvaMode drives KerbalEVA.cmdDir directly for walking
            // and jetpack translation. Overlaying pitch/yaw/roll from the stick
            // on top of that was making the kerbal both strafe and tumble from
            // the same input. Only keep roll on bumpers (useful for jetpack bank).
            if (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.isEVA)
            {
                float rollEva = (p.RB ? 1f : 0f) - (p.LB ? 1f : 0f);
                s.roll = Mathf.Clamp(s.roll + rollEva, -1f, 1f);
                return;
            }

            // Map view — left stick is the orbit cursor. Don't bleed it into
            // pitch/yaw (player isn't actively flying while planning).
            if (MapView.MapIsEnabled)
            {
                float rollMap = (p.RB ? 1f : 0f) - (p.LB ? 1f : 0f);
                s.roll = Mathf.Clamp(s.roll + rollMap, -1f, 1f);
                float dThrM = (p.RightTrigger - p.LeftTrigger) * Time.fixedDeltaTime;
                _targetThrottle = Mathf.Clamp01(_targetThrottle + dThrM);
                s.mainThrottle = _targetThrottle;
                if (FlightInputHandler.fetch != null)
                    Reflector.Set(FlightInputHandler.fetch, "axisThrottle", _targetThrottle);
                return;
            }

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
                radialOpen: false,
                cursor:    VirtualCursor.Active,
                inEva:     EvaActions.IsActive);

            if (PAWNavigator.AnyOpen) PAWNavigator.DrawHighlight();

            // Orbit cursor (only visible in map view with orbit mode).
            if (Bindings.MapCursorMode == "orbit") OrbitCursor.Draw();

            _agRadial.OnGUI();
            VirtualCursor.Draw();

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
