using SonTinhThuyTinh.CameraSystem;
using SonTinhThuyTinh.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SonTinhThuyTinh.UI
{
    // Drop into any gameplay scene. Freezes time and gameplay input; does not touch dialogue/menu scenes.
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] CanvasGroup panel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button mainMenuButton;
        [SerializeField] Button quitButton;
        [SerializeField] string mainMenuScene = SceneNames.MainMenu;

        InputAction pauseAction;
        ThirdPersonCameraInput cameraInput;

        public bool IsPaused { get; private set; }

        void Awake()
        {
            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");

            SetVisible(false);
            resumeButton.onClick.AddListener(Resume);
            mainMenuButton.onClick.AddListener(() => { Time.timeScale = 1f; SceneLoader.Load(mainMenuScene); });
            quitButton.onClick.AddListener(Application.Quit);
        }

        void OnEnable() => pauseAction.Enable();
        void OnDisable() => pauseAction.Disable();
        void OnDestroy() => pauseAction.Dispose();

        void Update()
        {
            // Unscaled: the action must still fire while Time.timeScale is 0.
            // The map closes with Esc too; that same press must not also open the pause menu.
            if (pauseAction.WasPressedThisFrame() && !SceneLoader.IsLoading && !Map.MapHUD.IsOpen && Map.MapHUD.LastCloseFrame != Time.frameCount)
            {
                if (IsPaused) Resume();
                else Pause();
            }
        }

        void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;
            SetVisible(true);

            // Disabling it triggers its own OnDisable, which unlocks the cursor for us.
            if (cameraInput == null) cameraInput = Object.FindAnyObjectByType<ThirdPersonCameraInput>();
            if (cameraInput != null) cameraInput.enabled = false;

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        void Resume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            SetVisible(false);

            // Re-enabling triggers OnEnable, which re-locks the cursor.
            if (cameraInput != null) cameraInput.enabled = true;
        }

        void SetVisible(bool visible)
        {
            panel.alpha = visible ? 1f : 0f;
            panel.blocksRaycasts = visible;
            panel.interactable = visible;
        }
    }
}
