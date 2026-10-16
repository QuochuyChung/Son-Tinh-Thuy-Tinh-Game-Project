using SonTinhThuyTinh.CameraSystem;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.DevTools;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Quest;
using SonTinhThuyTinh.UI;
using SonTinhThuyTinh.UI.Map;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        // Which suitor actually spawned (after the character-select fallback), and the roster to look rivals up in.
        public CharacterId Character { get; private set; }
        public CharacterRoster Roster => roster;

        void Awake()
        {
            // A scene played on its own (no character select before it) uses its fallback; remember it, so the next scenes of that run keep
            // the same suitor instead of each falling back to its own (a Thủy Tinh run on Map_ThuyTinh used to reach the palace as Sơn Tinh).
            if (GameSession.SelectedCharacter == null) GameSession.SelectedCharacter = fallbackCharacter;
            CharacterId id = GameSession.SelectedCharacter ?? fallbackCharacter;
            Character = id;
            CharacterDefinition character = roster.Get(id);

            Transform start = transform;
            if (spawnPoints != null)
                foreach (CharacterSpawn spawn in spawnPoints)
                    if (spawn.character == id && spawn.point != null) start = spawn.point;

            Player = Instantiate(character.PlayerPrefab, start.position, start.rotation);
            Player.name = character.PlayerPrefab.name;

            if (GameSession.TryConsumeEncounterReturn(SceneManager.GetActiveScene().name, out Vector3 returnPosition, out Quaternion returnRotation))
                Player.transform.SetPositionAndRotation(returnPosition, returnRotation);

            followCamera.Target.TrackingTarget = Player.CameraTarget;
            cameraInput.Bind(Player.InputReader);
            if (gameplayHud != null) gameplayHud.Bind(Player, character);
            if (mapHud != null) mapHud.Bind(Player, character);
            if (debugHud != null) debugHud.Bind(Player);
            foreach (GiftTrackerHUD gifts in FindObjectsByType<GiftTrackerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                gifts.SetQuest(character.GiftQuest);   // each suitor's own sính lễ

            string scene = SceneManager.GetActiveScene().name;
            if (scene == SceneNames.SonTinhMap || scene == SceneNames.ThuyTinhMap)
                QuestKeeperNpc.Spawn(start, character.GiftQuest, character.Id, Player);

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
