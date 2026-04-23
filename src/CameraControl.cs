using System;
using System.Reflection;
using UnityEngine;

namespace ControllerEverywhere
{
    // Right stick → camera yaw/pitch. Triggers → zoom. Works in:
    //  - Flight (FlightCamera.fetch) + IVA (InternalCamera via reflection like AFBW)
    //  - Map view & Tracking Station (PlanetariumCamera.fetch)
    //  - KSC overview (SpaceCenterCamera2)
    //  - Editor (EditorCamera transform orbiting around ship/editor center)
    internal static class CameraControl
    {
        private static FieldInfo _ivaPitch;
        private static FieldInfo _ivaYaw;
        private static bool _ivaFieldsResolved;

        private static void ResolveIvaFields()
        {
            if (_ivaFieldsResolved) return;
            var t = typeof(InternalCamera);
            var fs = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            int n = 0;
            foreach (var f in fs)
            {
                if (f.FieldType != typeof(float)) continue;
                if (n == 3) _ivaPitch = f;
                if (n == 4) _ivaYaw = f;
                n++;
            }
            _ivaFieldsResolved = true;
        }

        public static void Flight(Vector2 right, float zoom, float dt)
        {
            if (FlightCamera.fetch == null) return;
            float yawDelta   = right.x * Bindings.CameraYawSpeed   * dt;
            float pitchDelta = right.y * Bindings.CameraPitchSpeed * dt;

            var mode = (CameraManager.Instance != null) ? CameraManager.Instance.currentCameraMode
                                                        : CameraManager.CameraMode.Flight;

            switch (mode)
            {
                case CameraManager.CameraMode.Flight:
                    FlightCamera.CamHdg   += yawDelta   * Mathf.Deg2Rad;
                    FlightCamera.CamPitch += pitchDelta * Mathf.Deg2Rad;
                    FlightCamera.fetch.SetDistance(Mathf.Clamp(
                        FlightCamera.fetch.Distance - zoom * Bindings.CameraZoomRate * dt,
                        FlightCamera.fetch.minDistance,
                        FlightCamera.fetch.maxDistance));
                    break;

                case CameraManager.CameraMode.Map:
                    Map(right, zoom, dt);
                    break;

                case CameraManager.CameraMode.IVA:
                case CameraManager.CameraMode.Internal:
                    ResolveIvaFields();
                    var cam = InternalCamera.Instance;
                    if (cam != null && _ivaPitch != null && _ivaYaw != null)
                    {
                        _ivaPitch.SetValue(cam, (float)_ivaPitch.GetValue(cam) + pitchDelta);
                        _ivaYaw.SetValue(cam,   (float)_ivaYaw.GetValue(cam)   + yawDelta);
                    }
                    break;
            }
        }

        public static void Map(Vector2 right, float zoom, float dt)
        {
            var c = PlanetariumCamera.fetch;
            if (c == null) return;
            c.camHdg   += right.x * Bindings.CameraYawSpeed   * dt * Mathf.Deg2Rad;
            c.camPitch += right.y * Bindings.CameraPitchSpeed * dt * Mathf.Deg2Rad;
            if (Mathf.Abs(zoom) > 0.001f)
            {
                float d = c.Distance;
                // Map zoom is multiplicative — feels natural at planet/orbit scales
                d = Mathf.Clamp(d * (1f - zoom * Bindings.CameraZoomRate * 0.25f * dt),
                                c.minDistance, c.maxDistance);
                c.SetDistance(d);
            }
        }

        public static void KSC(Vector2 right, float zoom, float dt)
        {
            var cam = UnityEngine.Object.FindObjectOfType<SpaceCenterCamera2>();
            if (cam == null) return;
            // SpaceCenterCamera2 keeps its orbit state in non-public fields.
            float rot  = Reflector.Get<float>(cam, "rotationAngle")  + right.x * Bindings.CameraYawSpeed   * dt;
            float elev = Reflector.Get<float>(cam, "elevationAngle") + right.y * Bindings.CameraPitchSpeed * dt;
            float eMin = Reflector.Get<float>(cam, "elevationMin");
            float eMax = Reflector.Get<float>(cam, "elevationMax");
            float z    = Reflector.Get<float>(cam, "zoom") - zoom * Bindings.CameraZoomRate * 8f * dt;
            float zMin = Reflector.Get<float>(cam, "zoomMin");
            float zMax = Reflector.Get<float>(cam, "zoomMax");
            Reflector.Set(cam, "rotationAngle",  rot);
            Reflector.Set(cam, "elevationAngle", Mathf.Clamp(elev, eMin, eMax));
            Reflector.Set(cam, "zoom",           Mathf.Clamp(z,    zMin, zMax));
        }

        // Editor camera has no public orbit state — we manipulate the camera transform
        // around an orbit pivot (EditorLogic.fetch.editorBounds.center when available).
        private static float _editorHdg = 0f;
        private static float _editorPitch = 20f;
        private static float _editorDist = 12f;
        public static void Editor(Vector2 right, float zoom, float dt)
        {
            var cam = EditorCamera.Instance != null ? EditorCamera.Instance.cam : null;
            if (cam == null) return;
            Vector3 pivot = Vector3.zero;
            if (EditorLogic.fetch != null)
            {
                pivot = EditorLogic.fetch.editorBounds.center;
            }

            _editorHdg   += right.x * Bindings.CameraYawSpeed   * dt;
            _editorPitch -= right.y * Bindings.CameraPitchSpeed * dt;
            _editorPitch = Mathf.Clamp(_editorPitch, -85f, 85f);
            _editorDist  = Mathf.Clamp(_editorDist - zoom * Bindings.CameraZoomRate * 1.5f * dt, 2f, 60f);

            Quaternion rot = Quaternion.Euler(_editorPitch, _editorHdg, 0f);
            Vector3 offset = rot * new Vector3(0f, 0f, -_editorDist);
            cam.transform.position = pivot + offset;
            cam.transform.LookAt(pivot, Vector3.up);
        }

        public static void EditorTranslate(Vector2 left, float dt)
        {
            // Optional translation with left stick while holding a modifier — we route
            // this through the editor pivot by nudging _editorDist-plane offset.
            // (Left stick is reserved for UI/flight use — translate is only called when
            // the user is in a mode that supports it.)
            if (left.sqrMagnitude < 0.01f) return;
            Vector3 right = Quaternion.Euler(0f, _editorHdg, 0f) * Vector3.right;
            Vector3 up    = Vector3.up;
            Vector3 _     = right * (left.x * Bindings.EditorCamMoveSpeed * dt)
                          + up    * (left.y * Bindings.EditorCamMoveSpeed * dt);
            // We store pivot translations relative to editorBounds, so if the user
            // wants to roam, they can; but editorBounds doesn't accept translation,
            // so we fall back to moving the camera transform directly.
            if (EditorCamera.Instance != null && EditorCamera.Instance.cam != null)
            {
                EditorCamera.Instance.cam.transform.position += _;
            }
        }
    }
}
