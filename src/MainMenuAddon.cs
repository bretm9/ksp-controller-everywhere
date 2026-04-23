using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ControllerEverywhere
{
    // Main menu: navigate the Unity uGUI buttons with DPad/left stick; A = submit,
    // B = cancel. The actual selection of a "starting" button is up to KSP; we just
    // help the user move focus.
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

        void Update()
        {
            ControllerInput.Poll();
            UINavigator.Drive(ControllerInput.Current, ref _repeatTimer, ref _lastNav);
        }
    }
}
