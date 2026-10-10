using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SonTinhThuyTinh.DevTools
{
    // Sandbox training button (F9): spawn the rival suitor as a live BossAI sparring partner
    // in front of the player, using the same prefab + BossAI wiring as the final battle.
    public class SandboxBossSpawner : MonoBehaviour
    {
        [Tooltip("How far in front of the player the boss appears.")]
        [SerializeField] float spawnDistance = 7f;
        [SerializeField] float spawnHeight = 0.1f;

        GameObject current;
        GUIStyle buttonStyle;

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) Toggle();
        }

        void Toggle()
        {
            if (current != null) Despawn();
            else Spawn();
        }

        void Spawn()
        {
            PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>();
            if (spawner == null || spawner.Player == null)
            {
                Debug.LogWarning("SandboxBossSpawner: no spawned player found.");
                return;
            }
            if (spawner.Roster == null)
            {
                Debug.LogWarning("SandboxBossSpawner: PlayerSpawner has no roster.");
                return;
            }

            CharacterId bossId = spawner.Character == CharacterId.SonTinh ? CharacterId.ThuyTinh : CharacterId.SonTinh;
            CharacterDefinition boss = spawner.Roster.Get(bossId);
            if (boss == null || boss.PlayerPrefab == null)
            {
                Debug.LogWarning($"SandboxBossSpawner: CharacterDefinition for {bossId} is missing.");
                return;
            }

            Transform player = spawner.Player.transform;

            Vector3 dir = player.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 pos = player.position + dir * spawnDistance + Vector3.up * spawnHeight;

            Vector3 face = player.position - pos;
            face.y = 0f;
            Quaternion rot = face.sqrMagnitude > 0.001f ? Quaternion.LookRotation(face, Vector3.up) : Quaternion.identity;

            current = Instantiate(boss.PlayerPrefab.gameObject, pos, rot);
            current.name = $"Boss_{bossId}";

            BossAI ai = current.GetComponent<BossAI>();
            if (ai == null) ai = current.AddComponent<BossAI>();
            ai.Begin(player);
        }

        void Despawn()
        {
            Destroy(current);
            current = null;
        }

        void OnGUI()
        {
            if (buttonStyle == null) buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            string label = current != null ? "Despawn Boss (F9)" : "Spawn Rival Boss (F9)";
            if (GUI.Button(new Rect(Screen.width - 260f, 16f, 240f, 44f), label, buttonStyle)) Toggle();
        }
    }
}
