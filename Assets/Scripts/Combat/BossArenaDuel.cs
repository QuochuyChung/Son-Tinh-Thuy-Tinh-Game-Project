using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Flow;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Spawns the opposing rival as the Boss standing across the arena facing the Player.
    // If Player chose Son Tinh -> Boss is Thuy Tinh.
    // If Player chose Thuy Tinh -> Boss is Son Tinh.
    // Boss serves as combat target with Health and hit reactions until full boss AI is built.
    // Triggers Phase 2 when Boss health drops below 50% (or triggered manually).
    public class BossArenaDuel : MonoBehaviour
    {
        [SerializeField] CharacterRoster roster;
        [SerializeField] CharacterId fallbackPlayer = CharacterId.SonTinh;
        [SerializeField] Transform bossSpawnPoint;
        [SerializeField] FinalBattleArenaManager arenaManager;
        [SerializeField] float bossMaxHealth = 500f;

        [Header("Runtime State")]
        [SerializeField] CharacterId activeBossId;
        [SerializeField] Health bossHealth;
        [SerializeField] GameObject bossInstance;

        public CharacterId ActiveBossId => activeBossId;
        public Health BossHealth => bossHealth;

        void Start()
        {
            SpawnBoss();
        }

        public void SpawnBoss()
        {
            if (bossInstance != null)
                Destroy(bossInstance);

            CharacterId playerId = GameSession.SelectedCharacter ?? fallbackPlayer;
            activeBossId = (playerId == CharacterId.SonTinh) ? CharacterId.ThuyTinh : CharacterId.SonTinh;

            if (roster == null)
            {
                Debug.LogWarning("BossArenaDuel: CharacterRoster is null.");
                return;
            }

            CharacterDefinition bossDef = roster.Get(activeBossId);
            if (bossDef == null || bossDef.PlayerPrefab == null)
            {
                Debug.LogWarning($"BossArenaDuel: CharacterDefinition for {activeBossId} is missing.");
                return;
            }

            Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position : transform.position;
            Quaternion spawnRot = bossSpawnPoint != null ? bossSpawnPoint.rotation : transform.rotation;

            // Instantiate opponent model
            bossInstance = Instantiate(bossDef.PlayerPrefab.gameObject, spawnPos, spawnRot);
            bossInstance.name = $"Boss_{activeBossId}";

            // Disable player input and camera binding so it doesn't fight for local player controls
            var playerCtrl = bossInstance.GetComponent<Player.PlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.enabled = false;
            }
            var playerInput = bossInstance.GetComponent<Player.PlayerInputReader>();
            if (playerInput != null)
            {
                playerInput.enabled = false;
            }

            // Setup Boss Health & Hit Reaction
            bossHealth = bossInstance.GetComponent<Health>();
            if (bossHealth == null)
                bossHealth = bossInstance.AddComponent<Health>();
            bossHealth.SetMax(bossMaxHealth);

            // Setup dummy / damage receiver so player can attack the boss right away
            var dummy = bossInstance.GetComponent<TrainingDummy>();
            if (dummy == null)
            {
                dummy = bossInstance.AddComponent<TrainingDummy>();
                Renderer[] allRenderers = bossInstance.GetComponentsInChildren<Renderer>();
                dummy.SetRenderers(allRenderers);
            }

            bossHealth.Damaged += OnBossDamaged;
        }

        void OnBossDamaged(DamageInfo info)
        {
            if (arenaManager != null && !arenaManager.IsPhase2Active && bossHealth != null)
            {
                if (bossHealth.Current <= bossHealth.Max * 0.5f)
                {
                    ArenaBossType type = (activeBossId == CharacterId.ThuyTinh)
                        ? ArenaBossType.ThuyTinh
                        : ArenaBossType.SonTinh;
                    arenaManager.TriggerPhase2(type);
                }
            }
        }

        void OnDestroy()
        {
            if (bossHealth != null)
                bossHealth.Damaged -= OnBossDamaged;
        }
    }
}
