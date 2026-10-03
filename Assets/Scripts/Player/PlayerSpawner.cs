using SonTinhThuyTinh.CameraSystem;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.DevTools;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.UI;
using Unity.Cinemachine;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    // Spawns the character picked in character select at this transform and hooks up the scene's camera.
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] CharacterRoster roster;
        [Tooltip("Spawned when the scene is played directly, without going through character select.")]
        [SerializeField] CharacterId fallbackCharacter = CharacterId.ThuyTinh;
        [SerializeField] CinemachineCamera followCamera;
        [SerializeField] ThirdPersonCameraInput cameraInput;
        [Tooltip("Optional. The real player-facing health/stamina bars.")]
        [SerializeField] GameplayHUD gameplayHud;
        [Tooltip("Optional. Dev-only text overlay.")]
        [SerializeField] DebugHUD debugHud;

        public PlayerController Player { get; private set; }

        void Awake()
        {
            CharacterDefinition character = roster.Get(GameSession.SelectedCharacter ?? fallbackCharacter);
            Player = Instantiate(character.PlayerPrefab, transform.position, transform.rotation);
            Player.name = character.PlayerPrefab.name;

            followCamera.Target.TrackingTarget = Player.CameraTarget;
            cameraInput.Bind(Player.InputReader);
            if (gameplayHud != null) gameplayHud.Bind(Player, character);
            if (debugHud != null) debugHud.Bind(Player);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward);
        }
    }
}
