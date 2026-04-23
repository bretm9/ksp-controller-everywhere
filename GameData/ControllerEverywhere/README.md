# ControllerEverywhere — full controller support for KSP 1.12

Console-inspired control scheme for Kerbal Space Program 1.12: primary SAS
modes on the DPad, throttle on the triggers, roll on the bumpers, map-mode
maneuver editing, radial menus for action groups and aim-based utilities,
and on-screen indicators so you never have to memorise a binding.

## Install

Either drop `GameData/ControllerEverywhere/` from the release zip into your
KSP install, or install the `.ckan` through CKAN.

To build from source:
```
cd src && dotnet build -c Release
```
The DLL is auto-copied to the local KSP install.

## Default bindings (console-style)

The mode badge (top-left) and the key strip (bottom of screen) show
exactly what every button does in the current mode.

### Flight — no modifier

| Input          | Action                                                   |
|----------------|----------------------------------------------------------|
| Left stick     | Pitch / Yaw                                              |
| Right stick    | Camera                                                   |
| LT / RT        | Throttle down / up (hold LS + LT/RT = camera zoom)       |
| LB / RB        | Roll left / right                                        |
| DPad ↑ ↓ ← →   | SAS Prograde / Retrograde / Normal / Antinormal          |
| A              | Stage                                                    |
| B              | Cancel / back (closes dialogs)                           |
| X              | Open PAW for part at crosshair                           |
| Y              | Toggle map view                                          |
| LS             | Toggle SAS on / off                                      |
| RS             | Toggle RCS on / off                                      |
| RS hold (~0.25s)| Open action wheel (see below)                           |
| Back tap       | Open Action Groups wheel (AG 1-8)                        |
| Back hold      | Modifier — extended SAS + quick save/load + warp         |
| Start          | Pause menu                                               |

### Back held — extended modifier (flight)

| Input              | Action                                         |
|--------------------|------------------------------------------------|
| DPad ↑ / ↓         | SAS Radial In / Radial Out                     |
| DPad ← / →         | Time warp slower / faster                      |
| A                  | SAS Stability Assist                           |
| B                  | Abort                                          |
| X                  | SAS Maneuver                                   |
| Y                  | Toggle IVA                                     |
| LB / RB            | SAS Target / Anti-Target                       |
| LT / RT            | Quick load / Quick save                        |
| LS                 | Toggle precision mode                          |
| RS                 | Cycle camera mode                              |

### Map view (Y toggles) — additive on top of flight

| Input        | Action in map mode                                  |
|--------------|-----------------------------------------------------|
| A            | Create maneuver node (at next Ap/Pe or now+60s)     |
| B            | Delete selected maneuver node                       |
| LB / RB      | Cycle previous / next maneuver node (replaces roll) |
| DPad         | SAS modes (same as flight)                          |
| LT / RT      | Throttle (same as flight)                           |
| Left stick   | Pitch / yaw (same as flight)                        |
| Right stick  | Map camera orbit                                    |
| Y            | Exit map                                            |

### Back held inside map view — maneuver fine-tune

| Input        | Action                                    |
|--------------|-------------------------------------------|
| DPad ↑ / ↓   | Nudge prograde / retrograde dV            |
| DPad ← / →   | Nudge normal+ / antinormal+ dV            |
| LT / RT      | Nudge radial-in / radial-out dV           |
| LB / RB      | Nudge burn UT earlier / later             |
| Y            | Exit map                                  |

### Action wheel (hold RS ~0.25 s)

Right stick picks a slice, release RS (or press A) to confirm, B cancels.
A short RS tap (< 0.25 s) still toggles map view.

| Slice | Action                                                     |
|-------|------------------------------------------------------------|
| N     | Set target at reticle (picks docking port if hit)          |
| NE    | Focus camera on part at reticle                            |
| E     | Toggle SAS hold-current-direction                          |
| SE    | Toggle IVA ↔ Flight                                        |
| S     | Clear target                                               |
| SW    | Warp to next maneuver node (stops 60 s before burn)        |
| W     | Switch to vessel at reticle (physics range)                |
| NW    | Quick save                                                 |

### Action-groups wheel (Back tap)

Tap Back (short press < 0.2 s with no other button) to latch the AG wheel
open. Right stick picks AG 1-8, A fires, B cancels.

### PAW navigation (when a Part Action Window is open)

Flight axes keep working. Dpad / face buttons drive the menu:

| Input     | Action                                 |
|-----------|----------------------------------------|
| DPad      | Navigate (green highlight = focus)     |
| A         | Click / toggle focused control         |
| B         | Close PAW                              |
| LT / RT   | Adjust focused slider                  |

## On-screen indicators

| Where                  | What                                         |
|------------------------|----------------------------------------------|
| Top-left badge         | Current mode (FLIGHT / MAP / BACK HOLD / …)  |
| Bottom key strip       | Every binding for the current mode           |
| Navball SAS markers    | Which button picks each SAS mode (Back hold) |
| SAS / RCS toggle icons | Which button toggles them (LS / RS)          |
| Center reticle         | Where X / PAW-chord aims                     |
| PAW window             | Green outline on the focused control         |

## Config — `controller.cfg`

Written on first launch into `GameData/ControllerEverywhere/`. See the
file for the full list; the ones you're most likely to touch:

```
input.triggersBipolar = false     # true on Mac Xbox controllers
input.stickDeadzone   = 0.15
input.invertLY = true
input.invertRY = true

axis.LX = 0   # left stick X
axis.LY = 1
axis.RX = 3
axis.RY = 4
axis.LT = 8   # -1 disables; DX/DY = -1 falls back to d-pad buttons
axis.RT = 9
axis.DX = 5
axis.DY = 6
```

Use the in-game debug overlay (hold **LS + RS + Back** ~0.5 s, or
`input.debugOverlay = true` in the cfg) to see every joystick axis value
in real time — handy for calibrating an unfamiliar controller.

## Troubleshooting

KSP log: `~/Library/Logs/Unity/Player.log` (Mac) or
`%USERPROFILE%\AppData\LocalLow\Squad\Kerbal Space Program\Player.log` (Windows).

Look for `[ControllerEverywhere]` lines.

If sticks don't respond, the debug overlay (LS + RS + Back) shows which
axis index each stick uses on your controller — update the `axis.*` keys
accordingly.

## Differences from actual KSP Enhanced Edition

We're constrained to what Unity's legacy input system and KSP's public
API will let us do. Things KSP Enhanced Edition has that this mod does
not:

- Native rumble, adaptive triggers, DualSense touch, lightbar colour
- A virtual cursor that moves across the screen for arbitrary UI (we use
  a center-reticle + radial menus instead, which the project explicitly
  chose over a floating cursor)
- Custom on-screen button glyphs that look like Xbox / PlayStation icons
  (we use text labels — "A", "LB", "DPad ↑" — which work for any pad)
- Craft-editor (VAB/SPH) controller bindings — not the focus of this
  mod; use mouse/keyboard there

## Structure

```
src/
  ControllerEverywhere.csproj  — build config; auto-copies DLL on build
  ControllerInput.cs           — pad abstraction (sticks, triggers, buttons)
  Bindings.cs                  — config loader (controller.cfg)
  Reflector.cs                 — helper for non-public KSP fields
  CameraControl.cs             — right-stick camera in every scene
  FlightAddon.cs               — flight scene dispatch + OnFlyByWire
  FlightActions.cs             — time warp / save / node / target helpers
  EditorAddon.cs               — VAB/SPH (minimal — not focus of this mod)
  SpaceCentreAddon.cs          — KSC overview
  TrackingAddon.cs             — tracking station
  MainMenuAddon.cs             — main menu nav
  PAWNavigator.cs              — Part Action Window nav + highlight
  RadialMenu.cs                — 8-slice radial menus
  HudHints.cs                  — mode badge + key strip + glyphs
  UiDraw.cs                    — IMGUI outline / fill helpers
  UINavigator.cs               — generic EventSystem driver
  DebugOverlay.cs              — axis/button readout for calibration
  Log.cs                       — tagged Debug.Log wrapper
GameData/ControllerEverywhere/
  Plugins/ControllerEverywhere.dll   — the plugin
  controller.cfg                     — bindings + axis mapping
```
