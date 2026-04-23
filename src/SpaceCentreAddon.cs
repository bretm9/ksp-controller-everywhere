using System.Collections.Generic;
using UnityEngine;

namespace ControllerEverywhere
{
    // KSC overview. Right stick rotates / elevates the camera; triggers zoom.
    // DPad cycles selection between buildings; A clicks the highlighted building.
    // Start opens the pause menu; Back opens the space center menu (kerbals, etc.).
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class SpaceCentreAddon : MonoBehaviour
    {
        private List<SpaceCenterBuilding> _buildings = new List<SpaceCenterBuilding>();
        private int _selected = -1;
        private float _refreshTimer;

        void Awake()
        {
            Bindings.Load();
            Log.Info("SpaceCentreAddon awake.");
        }

        void Update()
        {
            ControllerInput.Poll();
            var p = ControllerInput.Current;

            // Right stick camera, triggers zoom
            CameraControl.KSC(p.RightStick, p.RightTrigger - p.LeftTrigger, Time.unscaledDeltaTime);

            // Refresh building list periodically — KSC is mostly static but building
            // discovery/upgrade can change it between loads.
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _buildings.Clear();
                _buildings.AddRange(FindObjectsOfType<SpaceCenterBuilding>());
                _buildings.RemoveAll(b => !Reflector.Get<bool>(b, "clickable"));
                _refreshTimer = 2f;
                if (_selected >= _buildings.Count) _selected = -1;
            }

            // DPad nav (edge-triggered)
            if (_buildings.Count > 0)
            {
                if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) Move(+1);
                if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f)) Move(-1);
                if (ControllerInput.Pressed(s => s.Dpad.y >  0.5f)) Move(+1);
                if (ControllerInput.Pressed(s => s.Dpad.y < -0.5f)) Move(-1);
            }

            // A = click selected building
            if (ControllerInput.Pressed(s => s.A) && _selected >= 0 && _selected < _buildings.Count)
            {
                var b = _buildings[_selected];
                b.OnClick.Fire(b);
            }

            // Start = pause menu
            if (ControllerInput.Pressed(s => s.Start))
            {
                if (PauseMenu.isOpen) PauseMenu.Close(); else PauseMenu.Display();
            }
        }

        private void Move(int delta)
        {
            if (_buildings.Count == 0) return;
            UnHighlight();
            _selected = (_selected + delta + _buildings.Count) % _buildings.Count;
            Highlight();
        }

        private void Highlight()
        {
            if (_selected < 0 || _selected >= _buildings.Count) return;
            var b = _buildings[_selected];
            // Tickle hover so KSP draws the outline/tooltip the user would see with a mouse
            b.ColliderHover(true);
        }

        private void UnHighlight()
        {
            if (_selected < 0 || _selected >= _buildings.Count) return;
            _buildings[_selected].ColliderHover(false);
        }

        void OnDisable() => UnHighlight();
    }
}
