using SonTinhThuyTinh.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SonTinhThuyTinh.CameraSystem
{
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class ThirdPersonCameraInput : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [SerializeField] float mouseSensitivity = 0.1f;
        [Tooltip("Degrees per second at full stick deflection.")]
        [SerializeField] float gamepadSensitivity = 180f;
        [SerializeField] bool invertY;

        CinemachineOrbitalFollow orbit;

        void Awake() => orbit = GetComponent<CinemachineOrbitalFollow>();

        void OnEnable() => SetCursorLocked(true);

        void OnDisable() => SetCursorLocked(false);

        void Update()
        {
            UpdateCursorLock();

            bool fromMouse = input.LookFromMouse;
            if (fromMouse && Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 look = input.Look;
            Vector2 delta = fromMouse ? look * mouseSensitivity : look * (gamepadSensitivity * Time.deltaTime);

            orbit.HorizontalAxis.Value = Mathf.Repeat(orbit.HorizontalAxis.Value + delta.x + 180f, 360f) - 180f;

            float pitchDelta = invertY ? delta.y : -delta.y;
            Vector2 range = orbit.VerticalAxis.Range;
            orbit.VerticalAxis.Value = Mathf.Clamp(orbit.VerticalAxis.Value + pitchDelta, range.x, range.y);
        }

        void UpdateCursorLock()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
