using System;
using System.IO;
using UnityEngine;

namespace ControllerEverywhere
{
    internal static class Bindings
    {
        public static float CameraYawSpeed   = 90f;
        public static float CameraPitchSpeed = 60f;
        public static float CameraZoomRate   = 4f;

        public static float StickDeadzone    = 0.15f;
        public static float TriggerDeadzone  = 0.05f;
        public static bool  InvertLY         = true;
        public static bool  InvertRY         = true;
        public static bool  TriggersBipolar  = false;

        public static float EditorCamMoveSpeed = 4f;
        public static float EditorPartRotateStep = 5f;

        // Numeric axis indices (0..19). -1 on DX/DY falls back to buttons.
        public static int JoystickIndex = 0;
        public static int AxisLX = 0, AxisLY = 1, AxisRX = 3, AxisRY = 4;
        public static int AxisLT = 8, AxisRT = 9, AxisDX = 5, AxisDY = 6;

        public static bool DebugOverlay = false;

        // "orbit": yellow cursor rides the vessel's orbit line, A places a node
        //          where it is. Left stick slides it.
        // "virtual": force the free-screen cursor on in map view so the player
        //          can click stock KSP UI directly (orbit, nav buttons, etc.).
        public static string MapCursorMode = "orbit";

        public static string ConfigPath =>
            Path.Combine(KSPUtil.ApplicationRootPath, "GameData/ControllerEverywhere/controller.cfg");

        public static void Load()
        {
            try
            {
                var path = ConfigPath;
                if (!File.Exists(path))
                {
                    WriteDefault(path);
                    Log.Info("Wrote default config to " + path);
                }
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;
                    var eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    var key = line.Substring(0, eq).Trim();
                    var val = line.Substring(eq + 1).Trim();
                    Apply(key, val);
                }
                Push();
                Log.Info("Config loaded.");
            }
            catch (Exception ex)
            {
                Log.Warn("Could not load config: " + ex.Message + "; using defaults.");
                Push();
            }
        }

        private static void Push()
        {
            ControllerInput.JoystickIndex      = JoystickIndex;
            ControllerInput.AxisLX             = AxisLX;
            ControllerInput.AxisLY             = AxisLY;
            ControllerInput.AxisRX             = AxisRX;
            ControllerInput.AxisRY             = AxisRY;
            ControllerInput.AxisLT             = AxisLT;
            ControllerInput.AxisRT             = AxisRT;
            ControllerInput.AxisDX             = AxisDX;
            ControllerInput.AxisDY             = AxisDY;
            ControllerInput.StickDeadzone      = StickDeadzone;
            ControllerInput.TriggerDeadzone    = TriggerDeadzone;
            ControllerInput.InvertLY           = InvertLY;
            ControllerInput.InvertRY           = InvertRY;
            ControllerInput.TriggersAreBipolar = TriggersBipolar;
        }

        private static void Apply(string k, string v)
        {
            float f; bool b; int i;
            switch (k)
            {
                case "camera.yawSpeed":   if (float.TryParse(v, out f)) CameraYawSpeed = f; break;
                case "camera.pitchSpeed": if (float.TryParse(v, out f)) CameraPitchSpeed = f; break;
                case "camera.zoomRate":   if (float.TryParse(v, out f)) CameraZoomRate = f; break;
                case "input.stickDeadzone":   if (float.TryParse(v, out f)) StickDeadzone = f; break;
                case "input.triggerDeadzone": if (float.TryParse(v, out f)) TriggerDeadzone = f; break;
                case "input.invertLY":        if (bool.TryParse(v, out b))  InvertLY = b; break;
                case "input.invertRY":        if (bool.TryParse(v, out b))  InvertRY = b; break;
                case "input.triggersBipolar": if (bool.TryParse(v, out b))  TriggersBipolar = b; break;
                case "input.debugOverlay":    if (bool.TryParse(v, out b))  DebugOverlay = b; break;
                case "map.cursorMode":        MapCursorMode = v.Trim().ToLower(); break;
                case "editor.moveSpeed":      if (float.TryParse(v, out f)) EditorCamMoveSpeed = f; break;
                case "editor.rotateStep":     if (float.TryParse(v, out f)) EditorPartRotateStep = f; break;
                case "axis.joystick":         if (int.TryParse(v, out i)) JoystickIndex = i; break;
                case "axis.LX": if (int.TryParse(v, out i)) AxisLX = i; break;
                case "axis.LY": if (int.TryParse(v, out i)) AxisLY = i; break;
                case "axis.RX": if (int.TryParse(v, out i)) AxisRX = i; break;
                case "axis.RY": if (int.TryParse(v, out i)) AxisRY = i; break;
                case "axis.LT": if (int.TryParse(v, out i)) AxisLT = i; break;
                case "axis.RT": if (int.TryParse(v, out i)) AxisRT = i; break;
                case "axis.DX": if (int.TryParse(v, out i)) AxisDX = i; break;
                case "axis.DY": if (int.TryParse(v, out i)) AxisDY = i; break;
            }
        }

        private static void WriteDefault(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var s = @"# ControllerEverywhere bindings

# --- Camera ---
camera.yawSpeed   = 90
camera.pitchSpeed = 60
camera.zoomRate   = 4

# --- Input ---
input.stickDeadzone   = 0.15
input.triggerDeadzone = 0.05
input.invertLY = true
input.invertRY = true
# Windows (XInput) triggers idle at 0 → keep this false.
# Mac / bipolar controllers idle at -1 → set to true.
input.triggersBipolar = false

# Toggle the debug axis/button overlay (also toggled in-game via LS + RS + Back chord).
input.debugOverlay = false

# Map-view cursor: ""orbit"" (yellow cursor slides along your orbit; A places a
# node at the cursor) or ""virtual"" (free-screen mouse cursor over stock KSP UI).
map.cursorMode = orbit

# --- Editor ---
editor.moveSpeed  = 4
editor.rotateStep = 5

# --- Joystick axis mapping ---
# KSP exposes each joystick's axes as integers 0-19. Which physical control
# lives at which index depends on OS + controller + driver. Defaults below are
# for Xbox controller on Windows (XInput). Set an axis to -1 to disable it;
# DX/DY set to -1 falls back to reading DPad as buttons.
#
# To calibrate on an unknown controller: set input.debugOverlay=true (or press
# LS + RS + Back in flight), then wiggle each stick/trigger and note which
# axis index lights up.

axis.joystick = 0

# Xbox / XInput defaults (Windows):
axis.LX = 0      # left stick X
axis.LY = 1      # left stick Y
axis.RX = 3      # right stick X
axis.RY = 4      # right stick Y
axis.LT = 8      # left trigger (0..1)
axis.RT = 9      # right trigger (0..1)
axis.DX = 5      # d-pad X
axis.DY = 6      # d-pad Y

# Common alternatives:
# Mac Xbox controller (355e driver):
#   axis.LT = 4, axis.RT = 5, axis.DX = -1, axis.DY = -1, triggersBipolar = true
# DualShock 4 on Windows (DS4Windows in XInput mode): same as Xbox defaults.
# DirectInput fallback (old wheels / generic HID):
#   try axis.RX = 2, axis.RY = 3 and experiment.
";
            File.WriteAllText(path, s);
        }
    }
}
