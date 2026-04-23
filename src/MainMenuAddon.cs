using UnityEngine;

namespace ControllerEverywhere
{
    // Main menu: force the virtual cursor on (console-style) so the player can
    // click the uGUI buttons directly. UINavigator still runs as a fallback —
    // it handles dpad-driven focus moves on Selectables that the cursor can't
    // easily reach (e.g. when the scroll list is longer than the viewport).
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class MainMenuAddon : MonoBehaviour
    {
        private float _repeatTimer;
        private Vector2 _lastNav;

        void Awake()
        {
            Bindings.Load();
            Log.Info("MainMenuAddon awake.");
        }

        void OnEnable()
        {
            VirtualCursor.ForceActive = true;
        }

        void OnDisable()
        {
            VirtualCursor.ForceActive = false;
        }

        void Update()
        {
            ControllerInput.Poll();
            VirtualCursor.Update(ControllerInput.Current);

            // Still drive UINavigator for discrete dpad navigation — both can
            // coexist because they operate on different signals (cursor =
            // pointerClick at screen pos; UINavigator = Selectable focus + A).
            // Skip when cursor has A consumed this frame to avoid double-click.
            if (!ControllerInput.Pressed(s => s.A))
                UINavigator.Drive(ControllerInput.Current, ref _repeatTimer, ref _lastNav);
        }

        void OnGUI()
        {
            VirtualCursor.Draw();
        }
    }
}
