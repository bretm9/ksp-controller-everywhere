using System;
using UnityEngine;

namespace ControllerEverywhere
{
    // KSP's InputManager defines 20 joystick axes per joystick using the naming
    // scheme "joy{J}.{A}" where J is 0-based joystick index and A is 0-based
    // axis index. Buttons use Unity's built-in KeyCode.JoystickButton0..19.
    //
    // Defaults assume Xbox controller via XInput on Windows. Other layouts (Mac,
    // DualShock, DirectInput) can be remapped in controller.cfg by setting the
    // integer indices. Use the debug overlay (LS + RS + Back chord) to see all
    // 20 axes and find the right indices for an unfamiliar controller.
    internal static class ControllerInput
    {
        public struct Pad
        {
            public Vector2 LeftStick;      // x right, y up (-1..1)
            public Vector2 RightStick;
            public float LeftTrigger;      // 0..1
            public float RightTrigger;
            public Vector2 Dpad;           // each axis -1 / 0 / +1
            public bool A, B, X, Y;
            public bool LB, RB;
            public bool LS, RS;
            public bool Back, Start, Home;
            public bool Connected;
        }

        public static Pad Current { get; private set; }
        public static Pad Previous { get; private set; }

        // Config-controlled
        public static int JoystickIndex = 0;
        public static int AxisLX = 0;
        public static int AxisLY = 1;
        public static int AxisRX = 3;
        public static int AxisRY = 4;
        public static int AxisLT = 8;     // -1 disables
        public static int AxisRT = 9;
        public static int AxisDX = 5;     // -1 = read DPad as buttons
        public static int AxisDY = 6;

        public static float StickDeadzone   = 0.15f;
        public static float TriggerDeadzone = 0.05f;
        public static bool  InvertLY        = true;
        public static bool  InvertRY        = true;
        public static bool  TriggersAreBipolar = false; // Windows XInput default

        // DPad button fallback — if DPad axes are disabled (axis = -1) we read
        // buttons 11-14 instead. This is correct for most DirectInput / Mac
        // controllers that expose DPad as discrete buttons.
        public static KeyCode BtnDpadUp    = KeyCode.JoystickButton13;
        public static KeyCode BtnDpadDown  = KeyCode.JoystickButton14;
        public static KeyCode BtnDpadLeft  = KeyCode.JoystickButton11;
        public static KeyCode BtnDpadRight = KeyCode.JoystickButton12;

        private static bool _logged;

        public static float ReadAxis(int j, int a)
        {
            if (a < 0 || a > 19 || j < 0 || j > 10) return 0f;
            try { return Input.GetAxisRaw($"joy{j}.{a}"); }
            catch { return 0f; }
        }

        public static void Poll()
        {
            if (!_logged)
            {
                _logged = true;
                var names = Input.GetJoystickNames();
                Log.Info($"Joysticks: [{string.Join(", ", names)}]");
                Log.Info($"Axis indices: LX={AxisLX} LY={AxisLY} RX={AxisRX} RY={AxisRY} LT={AxisLT} RT={AxisRT} DX={AxisDX} DY={AxisDY}");
            }

            var p = new Pad();
            var js = Input.GetJoystickNames();
            p.Connected = js.Length > 0 && !string.IsNullOrEmpty(js[Mathf.Min(JoystickIndex, js.Length - 1)]);

            float lx = ReadAxis(JoystickIndex, AxisLX);
            float ly = ReadAxis(JoystickIndex, AxisLY); if (InvertLY) ly = -ly;
            float rx = ReadAxis(JoystickIndex, AxisRX);
            float ry = ReadAxis(JoystickIndex, AxisRY); if (InvertRY) ry = -ry;
            p.LeftStick  = ApplyStickDeadzone(new Vector2(lx, ly), StickDeadzone);
            p.RightStick = ApplyStickDeadzone(new Vector2(rx, ry), StickDeadzone);

            float lt = ReadAxis(JoystickIndex, AxisLT);
            float rt = ReadAxis(JoystickIndex, AxisRT);
            if (TriggersAreBipolar) { lt = (lt + 1f) * 0.5f; rt = (rt + 1f) * 0.5f; }
            p.LeftTrigger  = ApplyAxisDeadzone(lt, TriggerDeadzone);
            p.RightTrigger = ApplyAxisDeadzone(rt, TriggerDeadzone);

            float dx = AxisDX >= 0 ? ReadAxis(JoystickIndex, AxisDX) : 0f;
            float dy = AxisDY >= 0 ? ReadAxis(JoystickIndex, AxisDY) : 0f;
            if (AxisDX < 0)
            {
                if (Input.GetKey(BtnDpadLeft))  dx = -1f;
                if (Input.GetKey(BtnDpadRight)) dx = +1f;
            }
            if (AxisDY < 0)
            {
                if (Input.GetKey(BtnDpadDown)) dy = -1f;
                if (Input.GetKey(BtnDpadUp))   dy = +1f;
            }
            p.Dpad = new Vector2(Mathf.Abs(dx) > 0.5f ? Mathf.Sign(dx) : 0f,
                                 Mathf.Abs(dy) > 0.5f ? Mathf.Sign(dy) : 0f);

            // Face / shoulder / stick-click / back / start / home — standard Unity
            // joystick button indices are consistent across Xbox/PS/generic on
            // every platform's Unity legacy input.
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
            Current  = p;
        }

        public static bool Pressed(Func<Pad, bool> f)  => f(Current) && !f(Previous);
        public static bool Released(Func<Pad, bool> f) => !f(Current) && f(Previous);

        private static float ApplyAxisDeadzone(float v, float dz)
        {
            float a = Mathf.Abs(v);
            if (a < dz) return 0f;
            return Mathf.Sign(v) * (a - dz) / (1f - dz);
        }

        private static Vector2 ApplyStickDeadzone(Vector2 v, float dz)
        {
            float m = v.magnitude;
            if (m < dz) return Vector2.zero;
            return v.normalized * Mathf.Min(1f, (m - dz) / (1f - dz));
        }
    }
}
