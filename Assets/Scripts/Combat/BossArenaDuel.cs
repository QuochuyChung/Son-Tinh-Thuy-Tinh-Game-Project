using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.UI;
using UnityEngine;

namespace SonTinhThuyTinh.Combat
{
    // Spawns the opposing rival as the Boss standing across the arena facing the Player.
    // If Player chose Son Tinh -> Boss is Thuy Tinh.
    // If Player chose Thuy Tinh -> Boss is Son Tinh.
    // Boss serves as combat target with Health and hit reactions, driven by BossAI through the normal state machine.
    // Triggers Phase 2 when Boss health drops below 50% (or triggered manually).
    public class BossArenaDuel : MonoBehaviour
    {
        [SerializeField] CharacterRoster roster;
        [SerializeField] CharacterId fallbackPlayer = CharacterId.SonTinh;
        [SerializeField] Transform bossSpawnPoint;
        [SerializeField] FinalBattleArenaManager arenaManager;
        [SerializeField] float bossMaxHealth = 500f;
        [Tooltip("Boss health bar at the top of the screen; built by Assets/Editor/FinalBattleBossBarBuilder.cs.")]
        [SerializeField] BossHealthBar bar;

        [Header("Runtime State")]
        [SerializeField] CharacterId activeBossId;
        [SerializeField] Health bossHealth;
        [SerializeField] GameObject bossInstance;
        [SerializeField] BossAI bossAi;
        [SerializeField] bool phase2Entered;

        public CharacterId ActiveBossId => activeBossId;
        public Health BossHealth => bossHealth;

        void Start()
        {
            SpawnBoss();
        }

        public void SpawnBoss()
        {
            UnbindHealth();

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

            // Setup Boss Health & Hit Reaction
            bossHealth = bossInstance.GetComponent<Health>();
            if (bossHealth == null)
                bossHealth = bossInstance.AddComponent<Health>();
            bossHealth.SetMax(bossMaxHealth);

            // The rival keeps its controller and input reader enabled: BossAI drives them through the
            // normal state machine, so the boss attacks, casts and moves exactly like the player does.
            bossAi = bossInstance.GetComponent<BossAI>();
            if (bossAi == null)
                bossAi = bossInstance.AddComponent<BossAI>();
            Player.PlayerSpawner spawner = FindFirstObjectByType<Player.PlayerSpawner>();
            bossAi.Begin(spawner != null && spawner.Player != null ? spawner.Player.transform : null);

            bossHealth.Damaged += OnBossDamaged;
            bossHealth.Died += OnBossDied;

            // Show the boss bar for this fight: both phase dots lit, full HP.
            phase2Entered = false;
            if (bar != null)
            {
                bar.Bind(bossHealth, bossDef.DisplayName);
                bar.Show(true);
            }
        }

        void UnbindHealth()
        {
            if (bossHealth == null) return;
            bossHealth.Damaged -= OnBossDamaged;
            bossHealth.Died -= OnBossDied;
            bossHealth = null;
        }

        void OnBossDied(DamageInfo info)
        {
            // Fight over: the bar just fades away through the existing Show(false) alpha tween.
            if (bar != null) bar.Show(false);
        }

        void OnBossDamaged(DamageInfo info)
        {
            // The player's own swings print no numbers, so boss damage pops up here instead.
            FloatingText.Spawn(info.HitPoint != Vector3.zero ? info.HitPoint + Vector3.up * 0.8f
                : (bossInstance != null ? bossInstance.transform.position : transform.position) + Vector3.up * 3f,
                Mathf.RoundToInt(info.Amount).ToString(), info.IsHeavy ? new Color(1f, 0.55f, 0.2f) : Color.white);

            if (!phase2Entered && bossHealth != null && bossHealth.Current <= bossHealth.Max * 0.5f)
            {
                // Single 50% crossing: flip the bar (dot 1 grays, fill goes purple, "Cuồng nộ")
                // and put the AI into phase 2 in the same moment the arena env reacts.
                phase2Entered = true;
                if (bar != null) bar.SetPhase2(true);
                if (bossAi != null) bossAi.SetPhase(2);

                if (arenaManager != null && !arenaManager.IsPhase2Active)
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
            UnbindHealth();
        }
    }
}
