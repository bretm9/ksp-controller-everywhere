using UnityEngine;

namespace ControllerEverywhere
{
    // VAB / SPH. Right stick orbits the camera; triggers zoom. Left stick translates
    // the camera. Bumpers rotate the selected (or held) part. Face buttons drive
    // the editor tools: A=place/root action, B=cancel, X=symmetry cycle, Y=angle
    // snap toggle. Start opens pause menu; Back opens editor menu (if any).
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class EditorAddon : MonoBehaviour
    {
        void Awake()
        {
            Bindings.Load();
            Log.Info("EditorAddon awake.");
        }

        void Update()
        {
            ControllerInput.Poll();
            var p = ControllerInput.Current;

            // Right stick orbits, triggers zoom, left stick translates
            float zoom = p.RightTrigger - p.LeftTrigger;
            CameraControl.Editor(p.RightStick, zoom, Time.unscaledDeltaTime);
            CameraControl.EditorTranslate(p.LeftStick, Time.unscaledDeltaTime);

            var ed = EditorLogic.fetch;
            if (ed == null) return;

            // Bumpers rotate selected part around its local Y axis
            if (p.LB || p.RB)
            {
                float angle = (p.RB ? 1f : -1f) * Bindings.EditorPartRotateStep * Time.unscaledDeltaTime * 10f;
                RotateSelectedPart(angle);
            }

            // X = cycle symmetry
            if (ControllerInput.Pressed(s => s.X))
                ed.symmetryMethod = ed.symmetryMethod == SymmetryMethod.Mirror ? SymmetryMethod.Radial : SymmetryMethod.Mirror;

            // Y = cycle angle snap (coarse toggle)
            if (ControllerInput.Pressed(s => s.Y))
                ed.srfAttachAngleSnap = ed.srfAttachAngleSnap > 0f ? 0f : 15f;

            // Start = pause menu (editor scenes also honor PauseMenu)
            if (ControllerInput.Pressed(s => s.Start))
            {
                if (PauseMenu.isOpen) PauseMenu.Close(); else PauseMenu.Display();
            }
        }

        private void RotateSelectedPart(float angle)
        {
            var part = EditorLogic.SelectedPart;
            if (part == null) return;
            part.transform.Rotate(Vector3.up, angle, Space.Self);
        }
    }
}
