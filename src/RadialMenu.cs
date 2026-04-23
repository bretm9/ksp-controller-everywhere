using System;
using System.Collections.Generic;
using UnityEngine;

namespace ControllerEverywhere
{
    // Screen-center radial menu. Two trigger modes:
    //   RsHold        — holding RS opens the wheel; releasing RS confirms
    //                   the hovered slice; a short tap fires ConsumePendingMapToggle.
    //   LatchedAfterHold — opened via OpenLatched(); stays open until the user
    //                   confirms (A) or cancels (B).
    internal class RadialMenu
    {
        public enum TriggerMode { RsHold, LatchedAfterHold }
        public TriggerMode Trigger = TriggerMode.RsHold;

        public struct Slice
        {
            public string Label;
            public Action Action;
            public Slice(string label, Action action) { Label = label; Action = action; }
        }

        private const float OpenHoldTime = 0.25f;
        private const float SelectThreshold = 0.35f;

        public bool IsOpen { get; private set; }
        public int Hovered { get; private set; } = -1;
        public List<Slice> Slices = new List<Slice>();

        private float _rsHeldTime;
        private bool _rsWasDown;
        private bool _pendingMapToggle;
        public bool ConsumePendingMapToggle() { bool b = _pendingMapToggle; _pendingMapToggle = false; return b; }

        public void SetSlices(List<Slice> s) => Slices = s ?? new List<Slice>();

        public void OpenLatched()
        {
            if (Trigger != TriggerMode.LatchedAfterHold) return;
            IsOpen = true;
            Hovered = -1;
        }

        // Returns true if the menu is active this frame.
        public bool Update(ControllerInput.Pad p)
        {
            switch (Trigger)
            {
                case TriggerMode.RsHold:        return UpdateRsHold(p);
                case TriggerMode.LatchedAfterHold: return UpdateLatched(p);
                default: return false;
            }
        }

        private bool UpdateRsHold(ControllerInput.Pad p)
        {
            if (p.RS && !_rsWasDown) { _rsHeldTime = 0f; }
            if (p.RS) _rsHeldTime += Time.unscaledDeltaTime;

            if (p.RS && !IsOpen && _rsHeldTime >= OpenHoldTime) IsOpen = true;

            if (IsOpen)
            {
                UpdateHover(p.RightStick);
                if (ControllerInput.Pressed(s => s.A)) Commit();
                else if (!p.RS && _rsWasDown)         Commit();
                if (ControllerInput.Pressed(s => s.B)) Cancel();
            }
            else if (!p.RS && _rsWasDown)
            {
                if (_rsHeldTime < OpenHoldTime) _pendingMapToggle = true;
                _rsHeldTime = 0f;
            }

            _rsWasDown = p.RS;
            return IsOpen;
        }

        private bool UpdateLatched(ControllerInput.Pad p)
        {
            if (!IsOpen) return false;
            UpdateHover(p.RightStick);
            if (ControllerInput.Pressed(s => s.A)) Commit();
            if (ControllerInput.Pressed(s => s.B)) Cancel();
            return IsOpen;
        }

        private void UpdateHover(Vector2 dir)
        {
            if (dir.magnitude < SelectThreshold) Hovered = -1;
            else
            {
                float angDeg = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                if (angDeg < 0f) angDeg += 360f;
                float step = 360f / Mathf.Max(1, Slices.Count);
                Hovered = Mathf.FloorToInt((angDeg + step * 0.5f) / step) % Slices.Count;
            }
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

        public void OnGUI()
        {
            if (!IsOpen || Slices.Count == 0) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float radius = Mathf.Min(Screen.width, Screen.height) * 0.22f;

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(cx - radius - 40f, cy - radius - 40f,
                                     (radius + 40f) * 2f, (radius + 40f) * 2f),
                            Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            var centerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            };
            GUI.Label(new Rect(cx - 80f, cy - 10f, 160f, 20f),
                      Hovered >= 0 ? Slices[Hovered].Label : "— select —", centerStyle);

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
