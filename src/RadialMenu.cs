using System;
using System.Collections.Generic;
using UnityEngine;

namespace ControllerEverywhere
{
    // Screen-center radial menu. Held open with RS click (long press); the right
    // stick direction picks a slice; releasing RS confirms. A = confirm, B = cancel.
    internal class RadialMenu
    {
        public struct Slice
        {
            public string Label;
            public Action Action;
            public Slice(string label, Action action) { Label = label; Action = action; }
        }

        private const float OpenHoldTime = 0.25f;   // seconds of RS hold before opening
        private const float SelectThreshold = 0.35f;

        public bool IsOpen { get; private set; }
        public int Hovered { get; private set; } = -1;
        public List<Slice> Slices = new List<Slice>();

        private float _rsHeldTime;
        private bool _rsWasDown;
        private bool _pendingMapToggle;   // set when RS tap (quick release) — consumed by addon
        public bool ConsumePendingMapToggle() { bool b = _pendingMapToggle; _pendingMapToggle = false; return b; }

        public void SetSlices(List<Slice> s) => Slices = s ?? new List<Slice>();

        // Returns true if the radial menu is "active" this frame and the addon
        // should suppress its normal RS / right-stick behaviour.
        public bool Update(ControllerInput.Pad p)
        {
            // Track RS press/hold
            if (p.RS && !_rsWasDown) { _rsHeldTime = 0f; }
            if (p.RS) _rsHeldTime += Time.unscaledDeltaTime;

            // Open when held past threshold
            if (p.RS && !IsOpen && _rsHeldTime >= OpenHoldTime)
                IsOpen = true;

            if (IsOpen)
            {
                // Selection tracks the right stick direction
                var dir = p.RightStick;
                if (dir.magnitude < SelectThreshold) Hovered = -1;
                else
                {
                    float angDeg = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                    if (angDeg < 0f) angDeg += 360f;
                    float step = 360f / Mathf.Max(1, Slices.Count);
                    int idx = Mathf.FloorToInt((angDeg + step * 0.5f) / step) % Slices.Count;
                    Hovered = idx;
                }

                // Confirm: A press or RS release
                if (ControllerInput.Pressed(s => s.A)) { Commit(); }
                else if (!p.RS && _rsWasDown)         { Commit(); }

                // Cancel: B press
                if (ControllerInput.Pressed(s => s.B)) { Cancel(); }
            }
            else if (!p.RS && _rsWasDown)
            {
                // RS tap (pressed then released without menu opening) — fire a map-toggle
                if (_rsHeldTime < OpenHoldTime) _pendingMapToggle = true;
                _rsHeldTime = 0f;
            }

            _rsWasDown = p.RS;
            return IsOpen;
        }

        private void Commit()
        {
            if (Hovered >= 0 && Hovered < Slices.Count)
            {
                try { Slices[Hovered].Action?.Invoke(); }
                catch (Exception ex) { Log.Warn("Radial action error: " + ex.Message); }
            }
            Cancel();
        }

        private void Cancel()
        {
            IsOpen = false;
            Hovered = -1;
        }

        // ---- Rendering ----------------------------------------------------------
        // Draws on OnGUI (IMGUI). Keeps the look deliberately minimal — rings of
        // text labels. KSP's uGUI canvas is more polished but adds surface area
        // we don't want.
        public void OnGUI()
        {
            if (!IsOpen || Slices.Count == 0) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float radius = Mathf.Min(Screen.width, Screen.height) * 0.22f;

            // Dim backdrop
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(cx - radius - 40f, cy - radius - 40f,
                                     (radius + 40f) * 2f, (radius + 40f) * 2f),
                            Texture2D.whiteTexture);

            // Center label
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            var centerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            };
            GUI.Label(new Rect(cx - 80f, cy - 10f, 160f, 20f),
                      Hovered >= 0 ? Slices[Hovered].Label : "— select —", centerStyle);

            // Slice labels around the ring
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            for (int i = 0; i < Slices.Count; i++)
            {
                float ang = (i * (360f / Slices.Count) - 90f) * Mathf.Deg2Rad;
                float lx = cx + Mathf.Cos(ang) * radius;
                float ly = cy + Mathf.Sin(ang) * radius;
                GUI.color = (i == Hovered) ? new Color(0.6f, 1f, 0.6f, 1f) : new Color(1f, 1f, 1f, 0.75f);
                GUI.Label(new Rect(lx - 80f, ly - 10f, 160f, 20f), Slices[i].Label, labelStyle);
            }

            GUI.color = prev;
        }
    }
}
