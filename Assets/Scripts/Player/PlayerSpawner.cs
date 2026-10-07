using SonTinhThuyTinh.CameraSystem;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.DevTools;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.UI;
using SonTinhThuyTinh.UI.Map;
using Unity.Cinemachine;
using UnityEngine;

namespace SonTinhThuyTinh.Player
{
    // Spawns the character picked in character select at this transform and hooks up the scene's camera.
    public class PlayerSpawner : MonoBehaviour
    {
        [System.Serializable]
        struct CharacterSpawn
        {
            public CharacterId character;
            public Transform point;
        }

        [SerializeField] CharacterRoster roster;
        [Tooltip("Spawned when the scene is played directly, without going through character select.")]
        [SerializeField] CharacterId fallbackCharacter = CharacterId.ThuyTinh;
        [Tooltip("Optional. For a scene with a different entrance per character (the palace): where each of them starts instead of at this transform.")]
        [SerializeField] CharacterSpawn[] spawnPoints;
        [SerializeField] CinemachineCamera followCamera;
        [SerializeField] ThirdPersonCameraInput cameraInput;
        [Tooltip("Optional. The real player-facing health/stamina bars.")]
        [SerializeField] GameplayHUD gameplayHud;
        [Tooltip("Optional. The map circle and the full map.")]
        [SerializeField] MapHUD mapHud;
        [Tooltip("Optional. Dev-only text overlay.")]
        [SerializeField] DebugHUD debugHud;

        public PlayerController Player { get; private set; }

        void Awake()
        {
            CharacterId id = GameSession.SelectedCharacter ?? fallbackCharacter;
            CharacterDefinition character = roster.Get(id);

            Transform start = transform;
            if (spawnPoints != null)
                foreach (CharacterSpawn spawn in spawnPoints)
                    if (spawn.character == id && spawn.point != null) start = spawn.point;

            Player = Instantiate(character.PlayerPrefab, start.position, start.rotation);
            Player.name = character.PlayerPrefab.name;

            followCamera.Target.TrackingTarget = Player.CameraTarget;
            cameraInput.Bind(Player.InputReader);
            if (gameplayHud != null) gameplayHud.Bind(Player, character);
            if (mapHud != null) mapHud.Bind(Player, character);
            if (debugHud != null) debugHud.Bind(Player);

            // fighting characters: the camera shakes with their hits, and their spell slots show at the bottom of the screen
            if (Player.HasCombat)
            {
                if (followCamera.GetComponent<CinemachineImpulseListener>() == null) followCamera.gameObject.AddComponent<CinemachineImpulseListener>();
                SpellHud.Create(Player);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward);
        }
    }
}
