using UnityEngine;

namespace ControllerEverywhere
{
    // Tracking Station uses the PlanetariumCamera (same as Map view).
    // Right stick rotates, triggers zoom, DPad cycles targets, A = fly / open vessel.
    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class TrackingAddon : MonoBehaviour
    {
        void Awake()
        {
            Bindings.Load();
            Log.Info("TrackingAddon awake.");
        }

        void Update()
        {
            ControllerInput.Poll();
            var p = ControllerInput.Current;
            CameraControl.Map(p.RightStick, p.RightTrigger - p.LeftTrigger, Time.unscaledDeltaTime);

            var cam = PlanetariumCamera.fetch;
            if (cam != null && cam.targets != null && cam.targets.Count > 0)
            {
                if (ControllerInput.Pressed(s => s.Dpad.x >  0.5f)) cam.SetTarget((cam.targets.IndexOf(cam.target) + 1) % cam.targets.Count);
                if (ControllerInput.Pressed(s => s.Dpad.x < -0.5f))
                {
                    int i = cam.targets.IndexOf(cam.target);
                    i = (i - 1 + cam.targets.Count) % cam.targets.Count;
                    cam.SetTarget(i);
                }
            }

            if (ControllerInput.Pressed(s => s.Start))
            {
                if (PauseMenu.isOpen) PauseMenu.Close(); else PauseMenu.Display();
            }
        }
    }
}
