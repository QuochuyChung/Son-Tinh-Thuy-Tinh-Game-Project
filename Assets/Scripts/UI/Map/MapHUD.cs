using SonTinhThuyTinh.CameraSystem;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI.Map
{
    // The map circle in the corner (a zoomed piece of the illustrated map centred on the player) and the full map that opens
    // with M / gamepad Select / a click on the circle, with a "you are here" marker and the gift stops. Opening freezes the game
    // like the pause menu does. Layout is built by Assets/Editor/MapHudBuilder.cs.
    public class MapHUD : MonoBehaviour
    {
        [SerializeField] WorldMapData map;
        [SerializeField] MapRoute route;

        [Header("Map circle")]
        [SerializeField] RawImage minimapImage;
        [SerializeField] RectTransform minimapArrow;
        [SerializeField] Button minimapButton;
        [Tooltip("Width of the part of the picture shown inside the circle, as a fraction of the whole picture.")]
        [SerializeField] float minimapZoom = 0.2f;

        [Header("Full map")]
        [SerializeField] CanvasGroup panel;
        [SerializeField] Button closeButton;
        [SerializeField] RectTransform fullArrow;
        [SerializeField] Image[] giftPins;
        [SerializeField] Color pinPending = new(0.2f, 0.2f, 0.2f, 0.9f);
        [SerializeField] Color pinCollected = new(1f, 0.82f, 0.25f, 1f);

        InputAction toggle;
        PlayerController player;
        ThirdPersonCameraInput cameraInput;
        PauseMenu pauseMenu;

        public static bool IsOpen { get; private set; }

        // Frame in which the map was closed, so the pause menu does not also react to the same Esc press.
        public static int LastCloseFrame { get; private set; } = -1;

        void Awake()
        {
            toggle = new InputAction("Map", InputActionType.Button);
            toggle.AddBinding("<Keyboard>/m");
            toggle.AddBinding("<Gamepad>/select");

            SetPanelVisible(false);
            if (minimapButton != null) minimapButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        void OnEnable() => toggle.Enable();

        void OnDisable() => toggle.Disable();

        void OnDestroy()
        {
            toggle.Dispose();
            if (IsOpen) { IsOpen = false; Time.timeScale = 1f; }
        }

        // Keeps state clean when Enter Play Mode Options skip the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            IsOpen = false;
            LastCloseFrame = -1;
        }

        public void Bind(PlayerController target, CharacterDefinition character = null) => player = target;

        void Update()
        {
            if (IsOpen)
            {
                bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                bool back = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
                if (toggle.WasPressedThisFrame() || escape || back) Close();
            }
            else if (toggle.WasPressedThisFrame())
            {
                Open();
            }
        }

        void LateUpdate()
        {
            if (player == null || route == null || map == null) return;
            if (!route.TryLocate(player.transform.position, player.transform.eulerAngles.y, out Vector2 position, out float angle)) return;

            UpdateCircle(position, angle);
            if (IsOpen) UpdateFullMap(position, angle);
        }

        void UpdateCircle(Vector2 position, float angle)
        {
            // Texture coordinates have v pointing up, the picture positions point down.
            Vector2 centre = new(position.x, 1f - position.y);
            float width = minimapZoom, height = width * map.Aspect;   // a square patch of the picture, whatever its aspect
            float x = Mathf.Clamp(centre.x - width * 0.5f, 0f, 1f - width);
            float y = Mathf.Clamp(centre.y - height * 0.5f, 0f, 1f - height);
            minimapImage.uvRect = new Rect(x, y, width, height);

            // The arrow sits where the player is inside that patch (the middle, except near the edges of the picture).
            Vector2 inside = new((centre.x - x) / width - 0.5f, (centre.y - y) / height - 0.5f);
            Vector2 size = minimapImage.rectTransform.rect.size;
            minimapArrow.anchoredPosition = new Vector2(inside.x * size.x, inside.y * size.y);
            minimapArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        void UpdateFullMap(Vector2 position, float angle)
        {
            fullArrow.anchorMin = fullArrow.anchorMax = new Vector2(position.x, 1f - position.y);
            fullArrow.anchoredPosition = Vector2.zero;
            fullArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
            float pulse = 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 4f);
            fullArrow.localScale = Vector3.one * pulse;

            if (giftPins == null) return;
            var pins = route.GiftPins;
            for (int i = 0; i < giftPins.Length && i < pins.Count; i++)
            {
                RectTransform pin = giftPins[i].rectTransform;
                pin.anchorMin = pin.anchorMax = new Vector2(pins[i].map.x, 1f - pins[i].map.y);
                pin.anchoredPosition = Vector2.zero;
                giftPins[i].color = GiftTracker.Has(pins[i].gift) ? pinCollected : pinPending;
            }
        }

        void Open()
        {
            if (IsOpen || SceneLoader.IsLoading) return;
            if (pauseMenu == null) pauseMenu = Object.FindAnyObjectByType<PauseMenu>();
            if (pauseMenu != null && pauseMenu.IsPaused) return;

            IsOpen = true;
            Time.timeScale = 0f;
            SetPanelVisible(true);

            // Disabling the camera input also frees the cursor, so the map can be clicked to close.
            if (cameraInput == null) cameraInput = Object.FindAnyObjectByType<ThirdPersonCameraInput>();
            if (cameraInput != null) cameraInput.enabled = false;
        }

        void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            LastCloseFrame = Time.frameCount;
            Time.timeScale = 1f;
            SetPanelVisible(false);
            if (cameraInput != null) cameraInput.enabled = true;
        }

        void SetPanelVisible(bool visible)
        {
            if (panel == null) return;
            panel.alpha = visible ? 1f : 0f;
            panel.blocksRaycasts = visible;
            panel.interactable = visible;
        }
    }
}
