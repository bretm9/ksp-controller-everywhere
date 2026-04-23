using System;
using System.IO;
using UnityEngine;

namespace ControllerEverywhere
{
    // A single config file at GameData/ControllerEverywhere/controller.cfg
    // keyed by dot-separated section.key lines. Kept deliberately simple — no
    // dependency on KSP's own config writer so it loads in any scene.
    internal static class Bindings
    {
        public static float CameraYawSpeed   = 90f;   // deg/sec at full stick deflection
        public static float CameraPitchSpeed = 60f;
        public static float CameraZoomRate   = 4f;    // distance units/sec (flight), relative rate (map)
        public static float StickDeadzone    = 0.15f;
        public static float TriggerDeadzone  = 0.05f;
        public static bool  InvertLY         = true;
        public static bool  InvertRY         = true;
        public static bool  TriggersBipolar  = false;

        public static float EditorCamMoveSpeed = 4f;  // m/s for left-stick translate in VAB/SPH
        public static float EditorPartRotateStep = 5f; // degrees per bumper press

        // Optional axis-name overrides
        public static string AxisLX, AxisLY, AxisRX, AxisRY, AxisLT, AxisRT, AxisDX, AxisDY;

        public static string ConfigPath
        {
            get
            {
                // KSPUtil.ApplicationRootPath is the KSP install dir
                var root = KSPUtil.ApplicationRootPath;
                return Path.Combine(root, "GameData/ControllerEverywhere/controller.cfg");
            }
        }

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
            ControllerInput.StickDeadzone   = StickDeadzone;
            ControllerInput.TriggerDeadzone = TriggerDeadzone;
            ControllerInput.InvertLY        = InvertLY;
            ControllerInput.InvertRY        = InvertRY;
            ControllerInput.TriggersAreBipolar = TriggersBipolar;
            ControllerInput.OverrideLX = string.IsNullOrEmpty(AxisLX) ? null : AxisLX;
            ControllerInput.OverrideLY = string.IsNullOrEmpty(AxisLY) ? null : AxisLY;
            ControllerInput.OverrideRX = string.IsNullOrEmpty(AxisRX) ? null : AxisRX;
            ControllerInput.OverrideRY = string.IsNullOrEmpty(AxisRY) ? null : AxisRY;
            ControllerInput.OverrideLT = string.IsNullOrEmpty(AxisLT) ? null : AxisLT;
            ControllerInput.OverrideRT = string.IsNullOrEmpty(AxisRT) ? null : AxisRT;
            ControllerInput.OverrideDX = string.IsNullOrEmpty(AxisDX) ? null : AxisDX;
            ControllerInput.OverrideDY = string.IsNullOrEmpty(AxisDY) ? null : AxisDY;
        }

        private static void Apply(string k, string v)
        {
            float f; bool b;
            switch (k)
            {
                case "camera.yawSpeed":   if (float.TryParse(v, out f)) CameraYawSpeed = f; break;
                case "camera.pitchSpeed": if (float.TryParse(v, out f)) CameraPitchSpeed = f; break;
                case "camera.zoomRate":   if (float.TryParse(v, out f)) CameraZoomRate = f; break;
                case "input.stickDeadzone":   if (float.TryParse(v, out f)) StickDeadzone = f; break;
                case "input.triggerDeadzone": if (float.TryParse(v, out f)) TriggerDeadzone = f; break;
                case "input.invertLY": if (bool.TryParse(v, out b)) InvertLY = b; break;
                case "input.invertRY": if (bool.TryParse(v, out b)) InvertRY = b; break;
                case "input.triggersBipolar": if (bool.TryParse(v, out b)) TriggersBipolar = b; break;
                case "editor.moveSpeed":   if (float.TryParse(v, out f)) EditorCamMoveSpeed = f; break;
                case "editor.rotateStep":  if (float.TryParse(v, out f)) EditorPartRotateStep = f; break;
                case "axis.LX": AxisLX = v; break;
                case "axis.LY": AxisLY = v; break;
                case "axis.RX": AxisRX = v; break;
                case "axis.RY": AxisRY = v; break;
                case "axis.LT": AxisLT = v; break;
                case "axis.RT": AxisRT = v; break;
                case "axis.DX": AxisDX = v; break;
                case "axis.DY": AxisDY = v; break;
            }
        }

        private static void WriteDefault(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var s = @"# ControllerEverywhere bindings
# Left stick = pitch/yaw (flight) or translate (editor) or UI nav (menus)
# Right stick = camera look EVERYWHERE (your request)
# Triggers   = throttle down/up (flight) or zoom in/out (other scenes)
# Bumpers    = roll left/right (flight) or prev/next (editor, tracking)
# DPad       = trim / action group / menu nav
# A          = stage (flight) / confirm (menus) / place part (editor)
# B          = cancel / back
# X          = toggle SAS (flight) / symmetry cycle (editor)
# Y          = toggle RCS (flight) / angle snap cycle (editor)
# LS click   = toggle precision mode (flight)
# RS click   = toggle map view (flight) / cycle camera mode
# Start      = pause menu
# Back       = toggle nav ball / quick action
# Home       = toggle mod UI

camera.yawSpeed   = 90
camera.pitchSpeed = 60
camera.zoomRate   = 4

input.stickDeadzone   = 0.15
input.triggerDeadzone = 0.05
input.invertLY = true
input.invertRY = true
# Set this to true if your triggers idle at -1 (most Mac controllers) instead of 0
input.triggersBipolar = true

editor.moveSpeed  = 4
editor.rotateStep = 5

# Axis name overrides — only set these if auto-probe picks wrong names.
# Uncomment and edit if controller input doesn't work:
# axis.LX = Joystick1 Axis 1
# axis.LY = Joystick1 Axis 2
# axis.RX = Joystick1 Axis 4
# axis.RY = Joystick1 Axis 5
# axis.LT = Joystick1 Axis 3
# axis.RT = Joystick1 Axis 6
# axis.DX = Joystick1 Axis 7
# axis.DY = Joystick1 Axis 8
";
            File.WriteAllText(path, s);
        }
    }
}
