# ControllerEverywhere — full controller support for KSP 1.12

Drives an Xbox / DualSense / generic Unity-compatible gamepad through every
scene in Kerbal Space Program 1.12: main menu, KSC, VAB/SPH, flight, map,
tracking station, IVA. Right stick controls the camera in every scene,
as requested.

This mod is **not** Advanced Fly-By-Wire: AFBW only covers flight. This
mod covers the whole game.

## Install

The build script installs automatically. From `/Users/bret/side-projects/ksp-controller-mod`:

```
cd src && dotnet build -c Release
```

Result: `GameData/ControllerEverywhere/Plugins/ControllerEverywhere.dll`
is copied into your KSP install. On first launch it writes a default
`GameData/ControllerEverywhere/controller.cfg` you can edit.

## Default bindings

### Normal flight (no modifier held)

| Input          | Action                                                   |
|----------------|----------------------------------------------------------|
| Left stick     | Pitch / Yaw (also rover wheel steer on ground)           |
| Right stick    | Camera orbit                                             |
| LT / RT        | Throttle down / up                                       |
| LB / RB        | Roll left / right                                        |
| **LB + RB**    | Open/close PAW for part at center reticle                |
| A              | Stage                                                    |
| B              | Toggle Gear                                              |
| X              | Toggle SAS                                               |
| Y              | Toggle RCS (held = translate mode, released = toggle)    |
| Y + A          | Abort                                                    |
| LS click       | Precision mode toggle                                    |
| RS click       | Toggle Map view                                          |
| DPad ← / →     | Time warp slower / faster                                |
| DPad ↑ / ↓     | Trim pitch up / down                                     |
| Start          | Pause menu                                               |

### Back is dual-purpose (quick-hold vs long-hold)

- **Quick-hold Back (< 0.4 s) + button → SAS mode picker**
- **Long-hold Back (≥ 0.4 s) + button → meta modifier** (AGs, quick load, vessel switch)

A "META" banner appears when the hold crosses the threshold so you can tell which mode is active. Release Back to exit either mode.

#### SAS mode picker (quick-hold Back)

| Input        | SAS mode          |
|--------------|-------------------|
| DPad ↑ / ↓   | Prograde / Retrograde |
| DPad ← / →   | Normal / Antinormal |
| A / B        | Target / Anti-Target |
| X / Y        | Radial In / Radial Out |
| LB / RB      | Stability Assist / Maneuver |

#### Meta modifier (long-hold Back)

| Input              | Action                           |
|--------------------|----------------------------------|
| A / B / X / Y      | Action Group 1 / 2 / 3 / 4       |
| LB / RB            | Action Group 5 / 6               |
| DPad ↑ / ↓ / ← / → | Action Group 7 / 8 / 9 / 10      |
| Start              | Quick load                       |
| LS / RS            | Switch vessel back / forward     |

Quick save lives on the radial menu (NW slice) only.

### RS radial menu — 8-slice action wheel

Hold **RS click** for ~0.25 s to open a wheel centered on the reticle.
Select with the right stick, release RS (or press A) to confirm; B cancels.
Tapping RS briefly (< 0.25 s) still toggles map view.

| Slice | Action                                                     |
|-------|------------------------------------------------------------|
| N     | Set target at reticle (picks docking port if hit)          |
| NE    | Focus camera on part at reticle (or body, in map view)     |
| E     | Toggle SAS "hold current direction" (Stability Assist on/off) |
| SE    | Toggle IVA ↔ Flight camera                                 |
| S     | Clear target                                               |
| SW    | Warp to next maneuver node (stops 60 s before the burn)    |
| W     | Switch to vessel at reticle (physics range)                |
| NW    | Quick save                                                 |

### PAW navigation — when a Part Action Window is open

Flight axes (left stick, triggers, bumpers for roll, right stick for camera)
keep working. The face/dpad buttons switch into menu mode:

| Input        | Action                                             |
|--------------|----------------------------------------------------|
| DPad         | Navigate between PAW controls                      |
| A            | Click / toggle the focused control                 |
| B            | Close the top PAW                                  |
| LB + RB      | Open/close PAW at reticle (same chord that opens)  |

### Map view open — maneuver node editor

| Input        | Action                                            |
|--------------|---------------------------------------------------|
| Right stick  | Orbit camera                                      |
| A            | Create maneuver node (at next Ap/Pe if close, else +60s) |
| B            | Delete selected node                              |
| LB / RB      | Cycle to previous / next node                     |
| DPad ↑ / ↓   | Nudge prograde / retrograde dV                    |
| DPad ← / →   | Nudge normal / antinormal dV                      |
| LT / RT      | Nudge radial-in / radial-out dV                   |
| LS / RS      | Nudge burn time earlier / later                   |
| X / Y        | dV step ×0.5 / ×2 (fine/coarse)                   |

All nudges are rate-based (per second of holding), so you can hold the
dpad for continuous adjustment. Max step ×20, min ×0.1.

## Config — `controller.cfg`

Written on first launch into `GameData/ControllerEverywhere/`. Edit:

- `camera.yawSpeed / pitchSpeed / zoomRate` — camera sensitivity
- `input.stickDeadzone / triggerDeadzone`
- `input.invertLY / invertRY` — stick Y-axis inversion
- `input.triggersBipolar = true` — on most Mac controllers triggers idle at
  -1, not 0. Keep this on unless your triggers feel stuck at 50%.
- `editor.moveSpeed / rotateStep`
- `axis.LX … axis.DY` — override the Unity joystick axis names if the
  auto-probe picks the wrong ones (unusual controller layout).

## Troubleshooting

Check KSP's log for `[ControllerEverywhere]` lines. Log locations:
- Mac: `~/Library/Logs/Unity/Player.log`
- In-game console: Alt-F12 → Debug → Debug Console

If sticks do nothing: check that your controller is listed in the log
line `Joysticks: …`. If the controller is listed but sticks don't move
anything, its axis names likely differ from the defaults — uncomment
the `axis.*` lines in `controller.cfg` and try other Joystick1 Axis N
values (1–10).

## Structure

```
src/
  ControllerEverywhere.csproj  — build config; auto-copies DLL on build
  ControllerInput.cs           — pad abstraction (sticks, triggers, buttons)
  Bindings.cs                  — config loader (controller.cfg)
  Reflector.cs                 — helper for non-public KSP fields
  CameraControl.cs             — right-stick camera in every scene
  FlightAddon.cs               — flight scene hooks
  EditorAddon.cs               — VAB/SPH hooks
  SpaceCentreAddon.cs          — KSC overview hooks
  TrackingAddon.cs             — tracking station hooks
  MainMenuAddon.cs             — main menu nav
  UINavigator.cs               — generic EventSystem driver
  Log.cs                       — tagged Debug.Log wrapper
GameData/ControllerEverywhere/
  Plugins/ControllerEverywhere.dll   — (generated)
  controller.cfg                     — (generated on first launch)
```
