using System.Collections.Generic;
using System.IO;
using System.Linq;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.EditorTools
{
    // Builds the epic final showdown arena (Valley of the End style):
    // - Towering canyon gorge with two colossal statues facing each other (Stone Sovereign & Feathered Sovereign)
    // - Massive cascading waterfall between them
    // - Central circular arena (28m diameter) on shallow water / stone platform
    // - Player & Boss spawn directly opposite each other
    // - Phase 2 mechanics hooked up (Water rising vs Rock pillars erupting)
    public static class FinalBattleArenaBuilder
    {
        public const string ScenePath = "Assets/Scenes/Map_FinalBattle.unity";
        const string SandboxPath = "Assets/Scenes/Sandbox_Combat.unity";
        const string GeneratedRootName = "GeneratedFinalBattle";
        const string RosterPath = "Assets/Data/Characters/CharacterRoster.asset";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Final Battle Arena")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Stop Play mode first before building the arena.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 1. Ensure Statue prefabs are ready
            var (stonePrefab, featheredPrefab) = StatueModelSetup.EnsurePrefabs();
            if (stonePrefab == null || featheredPrefab == null)
            {
                Debug.LogError("Failed to ensure statue prefabs. Aborting build.");
                return;
            }

            // 2. Prepare Scene file
            if (!File.Exists(ScenePath))
            {
                AssetDatabase.CopyAsset(SandboxPath, ScenePath);
                AssetDatabase.Refresh();
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 3. Clear sandbox leftovers and previous builds
            DestroyByName("Ground");
            DestroyByName("Pillars");
            DestroyByName("ClimbBlocks");
            DestroyByName("TrainingDummies");
            DestroyByName("DebugHUD");
            DestroyByName("GiftPickups");
            DestroyByName("GiftPickups (test)");   // the sandbox's test pickups: a rooster and gold capsules
            DestroyByName("GameplayHUD");          // the gift list of the quest maps, not wanted in the duel
            DestroyByName(GeneratedRootName);
            DestroyByName("Terrain_Gorge");

            var root = new GameObject(GeneratedRootName).transform;

            // 4. Build Environment (the canyon, pool, waterfall, statues and dressing are in FinalBattleScenery)
            FinalBattleScenery.BuildAtmosphere();
            FinalBattleScenery.BuildTerrain(root);
            FinalBattleScenery.BuildWaterfall(root);
            FinalBattleScenery.BuildStatues(root, stonePrefab, featheredPrefab);
            FinalBattleScenery.BuildWater(root, out Transform waterTransform, out GameObject stormFx);
            var (arenaPlatform, rockPillars, quakeFx) = BuildArenaPlatform(root);
            FinalBattleScenery.BuildDressing(root);

            // 5. Setup Spawners & Duel Flow
            var playerSpawn = MapDecor.Empty("PlayerSpawnPoint", root, new Vector3(0f, 0.25f, -8f), 0f);
            var bossSpawn = MapDecor.Empty("BossSpawnPoint", root, new Vector3(0f, 0.25f, 8f), 180f);

            SetupArenaManager(root, playerSpawn, bossSpawn, waterTransform, stormFx, rockPillars, quakeFx);
            SetupPlayerSpawner(playerSpawn);
            SetupBossDuel(root, bossSpawn);

            // 6. Register scene in Build Settings and save
            RegisterSceneInBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=cyan><b>Final Battle Arena successfully built at " + ScenePath + "!</b></color>");
        }

        static void DestroyByName(string name)
        {
            var targets = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t != null && t.name == name)
                .Select(t => t.gameObject)
                .ToList();
            foreach (var go in targets)
                Object.DestroyImmediate(go);
        }

        // ---------------------------------------------------------------- Central Arena Platform

        static (Transform arenaRoot, List<Transform> rockPillars, GameObject quakeFx) BuildArenaPlatform(Transform root)
        {
            var arenaGroup = new GameObject("Arena_Platform_28m").transform;
            arenaGroup.SetParent(root, false);

            Material stoneMat = MapDecor.Lit("Mat_ArenaStone", new Color(0.38f, 0.37f, 0.35f), 0.05f, 0.2f);
            Material borderMat = MapDecor.Lit("Mat_ArenaBorder", new Color(0.28f, 0.27f, 0.25f), 0.1f, 0.15f);

            // paving: the cobblestone texture of the Idyllic pack on the dais
            var cobble = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Idyllic Fantasy Nature/Terrain Layer/Cobblestone_Layer.terrainlayer");
            if (cobble != null && cobble.diffuseTexture != null)
            {
                stoneMat.SetTexture("_BaseMap", cobble.diffuseTexture);
                stoneMat.SetColor("_BaseColor", new Color(0.78f, 0.78f, 0.78f));
                stoneMat.SetTextureScale("_BaseMap", new Vector2(6f, 6f));
                if (cobble.normalMapTexture != null)
                {
                    stoneMat.SetTexture("_BumpMap", cobble.normalMapTexture);
                    stoneMat.SetTextureScale("_BumpMap", new Vector2(6f, 6f));
                    stoneMat.EnableKeyword("_NORMALMAP");
                }
                EditorUtility.SetDirty(stoneMat);
            }

            // 1. Central Circular Floor: 28m diameter (radius 14m), flat top
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor_StoneSlab";
            floor.transform.SetParent(arenaGroup, false);
            // a stone dais standing in the pool: its top is the floor (y = 0.16, just above the water), its body goes down to the bed
            floor.transform.localPosition = new Vector3(0f, 0.16f - 1.2f, 0f);
            floor.transform.localScale = new Vector3(28f, 1.2f, 28f);
            floor.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // 2. Outer decorative stone rim
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Floor_OuterRim";
            rim.transform.SetParent(arenaGroup, false);
            rim.transform.localPosition = new Vector3(0f, 0.08f - 1.0f, 0f);
            rim.transform.localScale = new Vector3(29.4f, 1.0f, 29.4f);
            rim.GetComponent<Renderer>().sharedMaterial = borderMat;

            // A Cylinder primitive comes with a CapsuleCollider; scaled this flat it becomes a sphere of radius 14 that the players spawn INSIDE,
            // so there is nothing to stand on and they fell for ever. A mesh collider of the disc itself is the floor; the rim is decoration.
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(rim.GetComponent<Collider>());

            // 3. Perimeter Torches at 4 quadrants (keeping center wide open for combat)
            float torchDist = 13.5f;
            MapDecor.Torch(arenaGroup, new Vector3(-torchDist, 0.15f, 0f));
            MapDecor.Torch(arenaGroup, new Vector3(torchDist, 0.15f, 0f));
            MapDecor.Torch(arenaGroup, new Vector3(0f, 0.15f, -torchDist));
            MapDecor.Torch(arenaGroup, new Vector3(0f, 0.15f, torchDist));

            // 5. Invisible Arena Barrier (Ring of 16 colliders so players & boss don't fall off into chasm)
            var barrierGroup = new GameObject("Arena_Invisible_Barrier").transform;
            barrierGroup.SetParent(arenaGroup, false);
            const int barrierCount = 16;
            float barrierRadius = 14.2f;
            for (int i = 0; i < barrierCount; i++)
            {
                float angle = i * (Mathf.PI * 2f / barrierCount);
                Vector3 pos = new Vector3(Mathf.Sin(angle) * barrierRadius, 2.5f, Mathf.Cos(angle) * barrierRadius);
                var colGo = new GameObject("Barrier_" + i);
                colGo.transform.SetParent(barrierGroup, false);
                colGo.transform.position = pos;
                colGo.transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                var box = colGo.AddComponent<BoxCollider>();
                box.size = new Vector3(6f, 6f, 1f);
            }

            // No rock pillars: the floor must stay empty for the fight (the earth boss's phase 2 is only the dust and the shaking now)
            var rockPillars = new List<Transform>();


            // Earthquake smoke / dust FX
            var quakeObj = new GameObject("Phase2_EarthQuake_FX");
            quakeObj.transform.SetParent(arenaGroup, false);
            quakeObj.transform.position = new Vector3(0f, 0.3f, 0f);

            var qps = quakeObj.AddComponent<ParticleSystem>();
            var qMain = qps.main;
            qMain.startLifetime = 1.8f;
            qMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            qMain.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            qMain.startColor = new Color(0.65f, 0.55f, 0.45f, 0.28f);
            qMain.maxParticles = 90;
            var qEmission = qps.emission;
            qEmission.rateOverTime = 35f;
            var qShape = qps.shape;
            qShape.shapeType = ParticleSystemShapeType.Circle;
            qShape.radius = 12f;
            // dust clouds (without a material a particle system renders magenta squares)
            quakeObj.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Combat/Mat_VfxAlpha.mat");
            quakeObj.SetActive(false);

            return (arenaGroup, rockPillars, quakeObj);
        }

        // ---------------------------------------------------------------- Setup Arena Manager & Spawners

        static void SetupArenaManager(
            Transform root,
            Transform playerSpawn,
            Transform bossSpawn,
            Transform waterTransform,
            GameObject stormFx,
            List<Transform> rockPillars,
            GameObject quakeFx)
        {
            var managerGo = new GameObject("FinalBattleArenaManager");
            managerGo.transform.SetParent(root, false);
            var manager = managerGo.AddComponent<FinalBattleArenaManager>();

            var so = new SerializedObject(manager);
            so.FindProperty("playerSpawn").objectReferenceValue = playerSpawn;
            so.FindProperty("bossSpawn").objectReferenceValue = bossSpawn;
            so.FindProperty("waterTransform").objectReferenceValue = waterTransform;
            so.FindProperty("waterRiseHeight").floatValue = 1.2f;
            so.FindProperty("waterRiseDuration").floatValue = 3.5f;
            so.FindProperty("waterStormFx").objectReferenceValue = stormFx;
            so.FindProperty("rockEruptHeight").floatValue = 6f;   // the spikes are 5.8 m tall: start hidden under the dais, end standing on it
            so.FindProperty("rockEruptDuration").floatValue = 2.5f;
            so.FindProperty("earthQuakeFx").objectReferenceValue = quakeFx;

            var pillarsProp = so.FindProperty("rockPillars");
            pillarsProp.arraySize = rockPillars.Count;
            for (int i = 0; i < rockPillars.Count; i++)
                pillarsProp.GetArrayElementAtIndex(i).objectReferenceValue = rockPillars[i];

            so.ApplyModifiedProperties();

            // anyone in the water or outside the platform is put back at the player's start after a moment
            var rescue = managerGo.AddComponent<OutOfBoundsRescue>();
            var rescueSo = new SerializedObject(rescue);
            rescueSo.FindProperty("respawn").objectReferenceValue = playerSpawn;
            rescueSo.ApplyModifiedProperties();

            // Game Over overlay when the player dies (guarded: FinalBattleBossBarBuilder may have placed one at scene root)
            if (Object.FindFirstObjectByType<FinalBattleDefeatWatch>(FindObjectsInactive.Include) == null)
                managerGo.AddComponent<FinalBattleDefeatWatch>();
        }

        static void SetupPlayerSpawner(Transform playerSpawn)
        {
            var spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            if (spawner != null)
            {
                spawner.transform.position = playerSpawn.position;
                spawner.transform.rotation = playerSpawn.rotation;
            }
        }

        static void SetupBossDuel(Transform root, Transform bossSpawn)
        {
            var duelGo = new GameObject("BossArenaDuel");
            duelGo.transform.SetParent(root, false);
            var duel = duelGo.AddComponent<BossArenaDuel>();

            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterPath);
            var arenaManager = root.GetComponentInChildren<FinalBattleArenaManager>();

            var so = new SerializedObject(duel);
            so.FindProperty("roster").objectReferenceValue = roster;
            so.FindProperty("bossSpawnPoint").objectReferenceValue = bossSpawn;
            so.FindProperty("arenaManager").objectReferenceValue = arenaManager;
            so.FindProperty("bossMaxHealth").floatValue = 500f;

            // played directly (not through character select) the boss must be the one opposite the spawner's fallback character, not a copy of it
            var spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            if (spawner != null)
                so.FindProperty("fallbackPlayer").enumValueIndex = new SerializedObject(spawner).FindProperty("fallbackCharacter").enumValueIndex;
            so.ApplyModifiedProperties();
        }

        static void RegisterSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
