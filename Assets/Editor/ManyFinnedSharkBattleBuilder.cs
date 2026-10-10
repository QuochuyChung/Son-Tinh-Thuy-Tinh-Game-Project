using System;
using System.Collections.Generic;
using System.IO;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Environment;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SonTinhThuyTinh.EditorTools
{
    public static class ManyFinnedSharkBattleBuilder
    {
        const string ScenePath = "Assets/Scenes/Battle_Map9Vay.unity";
        const string SourceScenePath = "Assets/Scenes/Sandbox_Combat.unity";
        const string SharkPrefabPath = "Assets/Prefabs/Characters/ManyFinnedShark.prefab";
        const string GiftPath = "Assets/Data/Gifts/Gift_Map9Vay.asset";
        const string MaterialPath = "Assets/Art/Characters/ManyFinnedShark/Materials/M_SharkArenaGround.mat";
        const string SetupRequestPath = "Temp/CodexBuildManyFinnedSharkBattle.request";

        static string SetupRequestFullPath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", SetupRequestPath));

        [InitializeOnLoadMethod]
        static void RunPendingBuild()
        {
            if (!File.Exists(SetupRequestFullPath)) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(SetupRequestFullPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                try
                {
                    Build(false);
                    File.Delete(SetupRequestFullPath);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Map 9 Vay Battle")]
        public static void Build() => Build(true);

        static void Build(bool askToSaveOpenScenes)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Stop Play mode before building the shark battle.");
                return;
            }
            if (askToSaveOpenScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
                throw new System.InvalidOperationException("Could not copy Sandbox_Combat to " + ScenePath);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string name in new[] { "TrainingDummies", "Pillars", "GiftPickups (test)", "GiftPickups", "ClimbBlocks", "GiftBanner", "GiftChecklist", "DebugHUD" })
                DestroyByName(name);

            ConfigureArena();
            PlayerSpawner spawner = ConfigurePlayer();
            ManyFinnedSharkBoss boss = BuildBoss();
            BuildController(spawner, boss);
            ConfigureLighting();
            RegisterScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            ManyFinnedSharkSetup.SetupAndPlaceAdditive();
            Debug.Log("Battle_Map9Vay built and connected to Map_ThuyTinh.");
        }

        // Entry point for command-line generation and validation.
        public static void BuildFromCommandLine()
        {
            Build(false);
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
            ground.transform.localScale = new Vector3(42f, 1f, 42f);
            ground.GetComponent<Renderer>().sharedMaterial = ArenaMaterial();

            var arena = new GameObject("SharkArena").transform;
            Material stone = ArenaMaterial();
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                Vector3 direction = new(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = "ReefRock_" + i.ToString("00");
                rock.transform.SetParent(arena, false);
                rock.transform.position = direction * 20f + Vector3.up * 1.35f;
                rock.transform.rotation = Quaternion.Euler((i % 3 - 1) * 5f, -angle * Mathf.Rad2Deg, (i % 5 - 2) * 3f);
                rock.transform.localScale = new Vector3(4f, 2.7f + (i % 4) * 0.45f, 1.5f);
                rock.GetComponent<Renderer>().sharedMaterial = stone;
            }

            foreach (Vector3 position in new[] { new Vector3(-13f, 4f, -13f), new Vector3(13f, 4f, -13f), new Vector3(-13f, 4f, 13f), new Vector3(13f, 4f, 13f) })
            {
                var lightObject = new GameObject("WaterLight", typeof(Light));
                lightObject.transform.SetParent(arena, false);
                lightObject.transform.position = position;
                Light light = lightObject.GetComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.12f, 0.68f, 1f);
                light.range = 18f;
                light.intensity = 6f;
                light.shadows = LightShadows.Soft;
            }
        }

        static Material ArenaMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "M_SharkArenaGround" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.035f, 0.18f, 0.23f));
            else material.color = new Color(0.035f, 0.18f, 0.23f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.55f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        static PlayerSpawner ConfigurePlayer()
        {
            PlayerSpawner spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            if (spawner == null) throw new MissingReferenceException("Sandbox has no PlayerSpawner.");
            spawner.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -10f), Quaternion.identity);
            var serialized = new SerializedObject(spawner);
            serialized.FindProperty("fallbackCharacter").enumValueIndex = System.Array.IndexOf(System.Enum.GetNames(typeof(CharacterId)), nameof(CharacterId.ThuyTinh));
            serialized.FindProperty("spawnPoints").arraySize = 0;
            serialized.FindProperty("mapHud").objectReferenceValue = null;
            serialized.FindProperty("debugHud").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return spawner;
        }

        static ManyFinnedSharkBoss BuildBoss()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharkPrefabPath);
            if (prefab == null) throw new MissingReferenceException("Missing " + SharkPrefabPath);

            var root = new GameObject("Map9Vay_Boss");
            root.transform.SetPositionAndRotation(new Vector3(0f, 1.15f, 9f), Quaternion.Euler(0f, 180f, 0f));
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            visual.name = "Map9Vay_Model";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            ManyFinnedSharkSwimmer swimmer = visual.GetComponent<ManyFinnedSharkSwimmer>();
            if (swimmer != null) Object.DestroyImmediate(swimmer);

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.center = new Vector3(0f, 0f, 0.15f);
            capsule.height = 8.4f;
            capsule.radius = 2.1f;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

            Health health = root.AddComponent<Health>();
            var healthData = new SerializedObject(health);
            healthData.FindProperty("max").floatValue = 220f;
            healthData.ApplyModifiedPropertiesWithoutUndo();

            ManyFinnedSharkBoss boss = root.AddComponent<ManyFinnedSharkBoss>();
            var bossData = new SerializedObject(boss);
            bossData.FindProperty("moveSpeed").floatValue = 3.8f;
            bossData.FindProperty("turnSpeed").floatValue = 240f;
            bossData.FindProperty("stopDistance").floatValue = 4.5f;
            bossData.FindProperty("attackRange").floatValue = 5.8f;
            bossData.FindProperty("attackDamage").floatValue = 14f;
            bossData.FindProperty("attackWindup").floatValue = 0.48f;
            bossData.FindProperty("attackDuration").floatValue = 1.05f;
            bossData.FindProperty("attackCooldown").floatValue = 0.9f;
            bossData.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }

        static void BuildController(PlayerSpawner spawner, ManyFinnedSharkBoss boss)
        {
            var go = new GameObject("ManyFinnedSharkBattleController");
            ManyFinnedSharkBattleController controller = go.AddComponent<ManyFinnedSharkBattleController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("boss").objectReferenceValue = boss;
            serialized.FindProperty("sharkGift").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            serialized.FindProperty("playerSpawner").objectReferenceValue = spawner;
            serialized.FindProperty("resultDelay").floatValue = 2.2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureLighting()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(52f, -25f, 0f);
                    light.color = new Color(0.55f, 0.8f, 1f);
                    light.intensity = 1.5f;
                    light.shadows = LightShadows.Soft;
                }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.02f, 0.13f, 0.19f);
            RenderSettings.ambientLight = new Color(0.12f, 0.3f, 0.4f);
        }

        static void RegisterScene()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(item => item.path == ScenePath);
            int mapIndex = scenes.FindIndex(item => item.path.EndsWith("/Map_ThuyTinh.unity"));
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
