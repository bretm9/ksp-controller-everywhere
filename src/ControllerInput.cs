using System;
using System.Collections.Generic;
using UnityEngine;

namespace ControllerEverywhere
{
    // Unity maps KeyCode.JoystickButton0..JoystickButton19 to joystick 1's buttons 0..19
    // without any InputManager configuration. For axes we have to probe candidate axis
    // names — different OSes / drivers expose different names. We try the most common
    // conventions and pick the first that yields any movement.
    internal static class ControllerInput
    {
        public struct Pad
        {
            public Vector2 LeftStick;      // x right, y up (-1..1)
            public Vector2 RightStick;     // x right, y up (-1..1)
            public float LeftTrigger;      // 0..1
            public float RightTrigger;     // 0..1
            public Vector2 Dpad;           // x=-1/0/1, y=-1/0/1
            public bool A, B, X, Y;        // face buttons (xbox labels)
            public bool LB, RB;            // shoulder bumpers
            public bool LS, RS;            // stick clicks
            public bool Back, Start;       // select, start
            public bool Home;              // guide / ps / xbox button
            public bool Connected;
        }

        public static Pad Current { get; private set; }
        public static Pad Previous { get; private set; }

        // Deadzone configurable via Bindings
        public static float StickDeadzone = 0.15f;
        public static float TriggerDeadzone = 0.05f;

        // Axis probe: list of (purpose, candidate axis names)
        // We probe these once and cache which ones actually return values.
        private static readonly string[] _axisLX = { "Joy1AxisX", "Joystick1 Axis 1", "Joystick1Axis1", "Joy0X", "Horizontal" };
        private static readonly string[] _axisLY = { "Joy1AxisY", "Joystick1 Axis 2", "Joystick1Axis2", "Joy0Y", "Vertical" };
        private static readonly string[] _axisRX = { "Joy1AxisRX", "Joystick1 Axis 4", "Joystick1Axis4", "Joy0Rx", "Joy0X" };
        private static readonly string[] _axisRY = { "Joy1AxisRY", "Joystick1 Axis 5", "Joystick1Axis5", "Joy0Ry", "Joy0Y" };
        private static readonly string[] _axisLT = { "Joy1AxisLT", "Joystick1 Axis 3", "Joystick1Axis3", "Joy0Z" };
        private static readonly string[] _axisRT = { "Joy1AxisRT", "Joystick1 Axis 6", "Joystick1Axis6", "Joy0Rz" };
        private static readonly string[] _axisDX = { "Joy1AxisDX", "Joystick1 Axis 7", "Joystick1Axis7", "Joy0DX" };
        private static readonly string[] _axisDY = { "Joy1AxisDY", "Joystick1 Axis 8", "Joystick1Axis8", "Joy0DY" };

        private static string _rLX, _rLY, _rRX, _rRY, _rLT, _rRT, _rDX, _rDY;
        private static bool _probed;
        private static readonly HashSet<string> _knownBadAxes = new HashSet<string>();

        // User-overridable axis names and inversions loaded from config
        public static string OverrideLX, OverrideLY, OverrideRX, OverrideRY, OverrideLT, OverrideRT, OverrideDX, OverrideDY;
        public static bool InvertLY = true;    // sticks usually report y-down-positive; invert so up=+1
        public static bool InvertRY = true;
        public static bool TriggersAreBipolar = false; // on some OSes the trigger axis idles at -1 and goes to +1

        public static void Probe()
        {
            if (_probed) return;
            _rLX = OverrideLX ?? PickResponsive(_axisLX);
            _rLY = OverrideLY ?? PickResponsive(_axisLY);
            _rRX = OverrideRX ?? PickResponsive(_axisRX);
            _rRY = OverrideRY ?? PickResponsive(_axisRY);
            _rLT = OverrideLT ?? PickResponsive(_axisLT);
            _rRT = OverrideRT ?? PickResponsive(_axisRT);
            _rDX = OverrideDX ?? PickResponsive(_axisDX);
            _rDY = OverrideDY ?? PickResponsive(_axisDY);
            _probed = true;
            Log.Info($"Axis names: LX={_rLX} LY={_rLY} RX={_rRX} RY={_rRY} LT={_rLT} RT={_rRT} DX={_rDX} DY={_rDY}");
            Log.Info("Joysticks: " + string.Join(", ", Input.GetJoystickNames()));
        }

        // Try each candidate and keep the first one that doesn't throw / isn't rejected
        // by Unity. We can't detect InputManager misses directly, but Unity logs an error
        // the first time we read an undefined axis — we mark it bad and try the next.
        private static string PickResponsive(string[] candidates)
        {
            foreach (var name in candidates)
            {
                if (_knownBadAxes.Contains(name)) continue;
                if (TryReadAxis(name, out _)) return name;
            }
            return candidates[0]; // fall back so reads are no-ops rather than crashes
        }

        private static bool TryReadAxis(string name, out float v)
        {
            try
            {
                v = Input.GetAxisRaw(name);
                return true;
            }
            catch (Exception)
            {
                _knownBadAxes.Add(name);
                v = 0f;
                return false;
            }
        }

        private static float ReadAxis(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0f;
            if (_knownBadAxes.Contains(name)) return 0f;
            try { return Input.GetAxisRaw(name); }
            catch { _knownBadAxes.Add(name); return 0f; }
        }

        private static float ApplyDeadzone(float v, float dz)
        {
            float a = Mathf.Abs(v);
            if (a < dz) return 0f;
            float sign = Mathf.Sign(v);
            return sign * (a - dz) / (1f - dz);
        }

        private static Vector2 ApplyStickDeadzone(Vector2 v, float dz)
        {
            float m = v.magnitude;
            if (m < dz) return Vector2.zero;
            float scaled = Mathf.Min(1f, (m - dz) / (1f - dz));
            return v.normalized * scaled;
        }

        public static void Poll()
        {
            if (!_probed) Probe();

            var p = new Pad();
            p.Connected = Input.GetJoystickNames().Length > 0;

            float lx = ReadAxis(_rLX);
            float ly = ReadAxis(_rLY); if (InvertLY) ly = -ly;
            float rx = ReadAxis(_rRX);
            float ry = ReadAxis(_rRY); if (InvertRY) ry = -ry;
            p.LeftStick  = ApplyStickDeadzone(new Vector2(lx, ly), StickDeadzone);
            p.RightStick = ApplyStickDeadzone(new Vector2(rx, ry), StickDeadzone);

            float lt = ReadAxis(_rLT);
            float rt = ReadAxis(_rRT);
            if (TriggersAreBipolar) { lt = (lt + 1f) * 0.5f; rt = (rt + 1f) * 0.5f; }
            p.LeftTrigger  = ApplyDeadzone(lt, TriggerDeadzone);
            p.RightTrigger = ApplyDeadzone(rt, TriggerDeadzone);

            // DPad — usually an axis on Xbox controllers, but also KeyCode.JoystickButton5..8 on some
            float dx = ReadAxis(_rDX);
            float dy = ReadAxis(_rDY);
            p.Dpad = new Vector2(Mathf.Abs(dx) > 0.5f ? Mathf.Sign(dx) : 0f,
                                 Mathf.Abs(dy) > 0.5f ? Mathf.Sign(dy) : 0f);

            // Xbox Mac-style mapping: A=0, B=1, X=2, Y=3, LB=4, RB=5, Back=6, Start=7, LS=8, RS=9, Home=10
            // PlayStation DualSense on Mac uses the same Unity mapping after the controller firmware update.
            p.A     = Input.GetKey(KeyCode.JoystickButton0);
            p.B     = Input.GetKey(KeyCode.JoystickButton1);
            p.X     = Input.GetKey(KeyCode.JoystickButton2);
            p.Y     = Input.GetKey(KeyCode.JoystickButton3);
            p.LB    = Input.GetKey(KeyCode.JoystickButton4);
            p.RB    = Input.GetKey(KeyCode.JoystickButton5);
            p.Back  = Input.GetKey(KeyCode.JoystickButton6);
            p.Start = Input.GetKey(KeyCode.JoystickButton7);
            p.LS    = Input.GetKey(KeyCode.JoystickButton8);
            p.RS    = Input.GetKey(KeyCode.JoystickButton9);
            p.Home  = Input.GetKey(KeyCode.JoystickButton10);

            Previous = Current;
            Current = p;
        }

        public static bool Pressed(Func<Pad, bool> f) => f(Current) && !f(Previous);
        public static bool Released(Func<Pad, bool> f) => !f(Current) && f(Previous);
    }
}
