using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.EditorTools
{
    // Builds the dedicated elephant arena from the existing combat sandbox so camera,
    // input, pause menu and player HUD remain identical to the rest of the game.
    public static class VoiChinNgaBattleBuilder
    {
        const string ScenePath = "Assets/Scenes/Battle_VoiChinNga.unity";
        const string SandboxPath = "Assets/Scenes/Sandbox_Combat.unity";
        const string ElephantPrefabPath = "Assets/Prefabs/Characters/VoiChinNga.prefab";
        const string GiftPath = "Assets/Data/Gifts/Gift_VoiChinNga.asset";
        const string ArenaMaterialPath = "Assets/Art/Characters/VoiChinNga/Materials/M_ElephantArenaGround.mat";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Voi Chin Nga Battle")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!File.Exists(SandboxPath)) { Debug.LogError("Missing " + SandboxPath); return; }

            // Re-copying gives this generated scene all current HUD/camera improvements.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SandboxPath, ScenePath)) throw new IOException("Could not create " + ScenePath);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string name in new[] { "TrainingDummies", "Pillars", "GiftPickups (test)", "GiftPickups", "ClimbBlocks", "GiftBanner", "GiftChecklist", "DebugHUD" })
                DestroyByName(name);

            ConfigureArena();
            PlayerSpawner spawner = ConfigurePlayer();
            VoiChinNgaBoss boss = BuildBoss();
            BuildController(spawner, boss);
            ConfigureLighting();
            RegisterScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            // Ensure the map uses the encounter trigger instead of a direct gift pickup.
            VoiChinNgaSetup.Setup();
            Debug.Log("Battle_VoiChinNga built and connected to Map_SonTinh.");
        }

        // Entry point for CI/batch verification.
        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        static void ConfigureArena()
        {
            GameObject ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Ground";
            }
            ground.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
            ground.transform.localScale = new Vector3(46f, 1f, 46f);
            ground.GetComponent<Renderer>().sharedMaterial = ArenaMaterial();

            var arena = new GameObject("ElephantArena").transform;
            Material stone = ArenaMaterial();
            for (int i = 0; i < 20; i++)
            {
                float angle = i * Mathf.PI * 2f / 20f;
                Vector3 direction = new(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "BoundaryStone_" + i.ToString("00");
                pillar.transform.SetParent(arena, false);
                pillar.transform.position = direction * 22f + Vector3.up * 2f;
                pillar.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, Random.Range(-5f, 5f));
                pillar.transform.localScale = new Vector3(4.8f, Random.Range(3.6f, 5.6f), 1.7f);
                pillar.GetComponent<Renderer>().sharedMaterial = stone;
            }

            // Four warm braziers make the combat space readable against the forest-green floor.
            foreach (Vector3 position in new[] { new Vector3(-15f, 2.5f, -15f), new Vector3(15f, 2.5f, -15f), new Vector3(-15f, 2.5f, 15f), new Vector3(15f, 2.5f, 15f) })
            {
                var lightObject = new GameObject("ArenaTorch", typeof(Light));
                lightObject.transform.SetParent(arena, false);
                lightObject.transform.position = position;
                Light light = lightObject.GetComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.48f, 0.18f);
                light.range = 18f;
                light.intensity = 8f;
                light.shadows = LightShadows.Soft;
            }
        }

        static Material ArenaMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ArenaMaterialPath);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = "M_ElephantArenaGround" };
            material.SetColor("_BaseColor", new Color(0.16f, 0.24f, 0.14f));
            material.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(material, ArenaMaterialPath);
            return material;
        }

        static PlayerSpawner ConfigurePlayer()
        {
            PlayerSpawner spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            if (spawner == null) throw new MissingReferenceException("Sandbox has no PlayerSpawner.");
            spawner.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -12f), Quaternion.identity);
            var serialized = new SerializedObject(spawner);
            serialized.FindProperty("fallbackCharacter").enumValueIndex = System.Array.IndexOf(System.Enum.GetNames(typeof(CharacterId)), nameof(CharacterId.SonTinh));
            serialized.FindProperty("spawnPoints").arraySize = 0;
            serialized.FindProperty("mapHud").objectReferenceValue = null;
            serialized.FindProperty("debugHud").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return spawner;
        }

        static VoiChinNgaBoss BuildBoss()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ElephantPrefabPath);
            if (prefab == null) throw new MissingReferenceException("Missing " + ElephantPrefabPath);

            var root = new GameObject("VoiChinNga_Boss");
            root.transform.SetPositionAndRotation(new Vector3(0f, 0f, 10f), Quaternion.Euler(0f, 180f, 0f));
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            visual.name = "VoiChinNga_Model";
            // The source elephant faces +X. Map its head to the boss root's +Z so the
            // visible model and the AI's transform.forward agree.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 4.3f, 0f);
            capsule.height = 8.6f;
            capsule.radius = 2.65f;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

            Health health = root.AddComponent<Health>();
            var healthData = new SerializedObject(health);
            healthData.FindProperty("max").floatValue = 260f;
            healthData.ApplyModifiedPropertiesWithoutUndo();

            VoiChinNgaBoss boss = root.AddComponent<VoiChinNgaBoss>();
            var bossData = new SerializedObject(boss);
            bossData.FindProperty("moveSpeed").floatValue = 2.8f;
            bossData.FindProperty("turnSpeed").floatValue = 130f;
            bossData.FindProperty("stopDistance").floatValue = 4.7f;
            bossData.FindProperty("attackRange").floatValue = 6.2f;
            bossData.FindProperty("attackDamage").floatValue = 18f;
            bossData.FindProperty("attackWindup").floatValue = 0.58f;
            bossData.FindProperty("attackDuration").floatValue = 1.15f;
            bossData.FindProperty("attackCooldown").floatValue = 1.1f;
            bossData.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }

        static void BuildController(PlayerSpawner spawner, VoiChinNgaBoss boss)
        {
            var go = new GameObject("VoiChinNgaBattleController");
            VoiChinNgaBattleController controller = go.AddComponent<VoiChinNgaBattleController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("boss").objectReferenceValue = boss;
            serialized.FindProperty("elephantGift").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            serialized.FindProperty("playerSpawner").objectReferenceValue = spawner;
            serialized.FindProperty("resultDelay").floatValue = 2.2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureLighting()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
                    light.color = new Color(1f, 0.82f, 0.64f);
                    light.intensity = 1.8f;
                    light.shadows = LightShadows.Soft;
                }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = new Color(0.12f, 0.19f, 0.16f);
            RenderSettings.ambientLight = new Color(0.28f, 0.34f, 0.27f);
        }

        static void RegisterScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(item => item.path == ScenePath);
            int mapIndex = scenes.FindIndex(item => item.path.EndsWith("/Map_SonTinh.unity"));
            scenes.Insert(mapIndex >= 0 ? mapIndex + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void DestroyByName(string name)
        {
            GameObject found = GameObject.Find(name);
            if (found != null) Object.DestroyImmediate(found);
        }
    }
}
