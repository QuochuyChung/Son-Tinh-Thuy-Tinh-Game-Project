using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Player;
using SonTinhThuyTinh.Quest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SonTinhThuyTinh.EditorTools
{
    public static class NineLeggedTurtleBattleBuilder
    {
        const string ScenePath = "Assets/Scenes/Battle_Rua9Chan.unity";
        const string SourceScenePath = "Assets/Scenes/Sandbox_Combat.unity";
        const string MapScenePath = "Assets/Scenes/Map_ThuyTinh.unity";
        const string TurtlePrefabPath = "Assets/Prefabs/Characters/NineLeggedTurtle.prefab";
        const string GiftPath = "Assets/Data/Gifts/Gift_Rua9Chan.asset";
        const string MaterialPath = "Assets/Art/Characters/NineLeggedTurtle/Materials/M_TurtleArenaGround.mat";
        const string SetupRequestPath = "Temp/CodexBuildNineLeggedTurtle.request";

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

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Rua Chin Chan Battle")]
        public static void Build() => Build(true);

        static void Build(bool askToSaveOpenScenes)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Stop Play mode before building the turtle battle.");
                return;
            }
            if (askToSaveOpenScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            NineLeggedTurtleSetup.EnsureAssets();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
                throw new System.InvalidOperationException("Could not copy Sandbox_Combat to " + ScenePath);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string name in new[] { "TrainingDummies", "Pillars", "GiftPickups (test)", "GiftPickups", "ClimbBlocks", "GiftBanner", "GiftChecklist", "DebugHUD" })
                DestroyByName(name);

            ConfigureArena();
            PlayerSpawner spawner = ConfigurePlayer();
            NineLeggedTurtleBoss boss = BuildBoss();
            BuildController(spawner, boss);
            ConfigureLighting();
            RegisterScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            SetupMapAdditive();
            AssetDatabase.SaveAssets();
            Debug.Log("Battle_Rua9Chan built and Pickup_GaChinCua replaced in Map_ThuyTinh.");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build(false);
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
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
            ground.transform.localScale = new Vector3(36f, 1f, 36f);
            ground.GetComponent<Renderer>().sharedMaterial = ArenaMaterial();

            Transform arena = new GameObject("TurtleArena").transform;
            Material stone = ArenaMaterial();
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                Vector3 direction = new(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                GameObject rock = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                rock.name = "AncientStone_" + i.ToString("00");
                rock.transform.SetParent(arena, false);
                rock.transform.position = direction * 17f + Vector3.up * 1.25f;
                rock.transform.rotation = Quaternion.Euler((i % 3 - 1) * 4f, -angle * Mathf.Rad2Deg, (i % 5 - 2) * 2f);
                rock.transform.localScale = new Vector3(3.2f, 2.5f + (i % 4) * 0.42f, 1.35f);
                rock.GetComponent<Renderer>().sharedMaterial = stone;
            }

            foreach (Vector3 position in new[] { new Vector3(-11f, 4f, -11f), new Vector3(11f, 4f, -11f), new Vector3(-11f, 4f, 11f), new Vector3(11f, 4f, 11f) })
            {
                GameObject lightObject = new("JadeLight", typeof(Light));
                lightObject.transform.SetParent(arena, false);
                lightObject.transform.position = position;
                Light light = lightObject.GetComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.2f, 1f, 0.48f);
                light.range = 16f;
                light.intensity = 5.2f;
                light.shadows = LightShadows.Soft;
            }
        }

        static Material ArenaMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "M_TurtleArenaGround" };
            Color color = new(0.045f, 0.18f, 0.105f);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.48f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        static PlayerSpawner ConfigurePlayer()
        {
            PlayerSpawner spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            if (spawner == null) throw new MissingReferenceException("Sandbox has no PlayerSpawner.");
            spawner.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -9f), Quaternion.identity);
            SerializedObject data = new(spawner);
            data.FindProperty("fallbackCharacter").enumValueIndex = System.Array.IndexOf(System.Enum.GetNames(typeof(CharacterId)), nameof(CharacterId.ThuyTinh));
            data.FindProperty("spawnPoints").arraySize = 0;
            data.FindProperty("mapHud").objectReferenceValue = null;
            data.FindProperty("debugHud").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            return spawner;
        }

        static NineLeggedTurtleBoss BuildBoss()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TurtlePrefabPath);
            if (prefab == null) throw new MissingReferenceException("Missing " + TurtlePrefabPath);

            GameObject root = new("Rua9Chan_Boss");
            root.transform.SetPositionAndRotation(new Vector3(0f, 0.55f, 8f), Quaternion.Euler(0f, 180f, 0f));
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            visual.name = "Rua9Chan_Model";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.center = new Vector3(0f, 0f, 0.05f);
            capsule.height = 5.2f;
            capsule.radius = 1.35f;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

            Health health = root.AddComponent<Health>();
            SerializedObject healthData = new(health);
            healthData.FindProperty("max").floatValue = 260f;
            healthData.ApplyModifiedPropertiesWithoutUndo();

            NineLeggedTurtleBoss boss = root.AddComponent<NineLeggedTurtleBoss>();
            SerializedObject bossData = new(boss);
            bossData.FindProperty("moveSpeed").floatValue = 2.8f;
            bossData.FindProperty("turnSpeed").floatValue = 260f;
            bossData.FindProperty("stopDistance").floatValue = 3.15f;
            bossData.FindProperty("attackRange").floatValue = 3.9f;
            bossData.FindProperty("attackDamage").floatValue = 18f;
            bossData.FindProperty("attackWindup").floatValue = 0.78f;
            bossData.FindProperty("attackDuration").floatValue = 1.5f;
            bossData.FindProperty("attackCooldown").floatValue = 0.85f;
            bossData.FindProperty("specialOpeningDelay").floatValue = 2.2f;
            bossData.FindProperty("specialCooldown").floatValue = 4.6f;
            bossData.FindProperty("spinTriggerRange").floatValue = 5.5f;
            bossData.FindProperty("spinDamage").floatValue = 24f;
            bossData.FindProperty("spinWindup").floatValue = 0.55f;
            bossData.FindProperty("spinDuration").floatValue = 1.65f;
            bossData.FindProperty("spinMoveSpeed").floatValue = 6.5f;
            bossData.FindProperty("spinHitRadius").floatValue = 3.1f;
            bossData.FindProperty("shockwaveRadius").floatValue = 9f;
            bossData.FindProperty("shockwaveDamage").floatValue = 27f;
            bossData.FindProperty("shockwaveWindup").floatValue = 1.05f;
            bossData.FindProperty("shockwaveDuration").floatValue = 1.55f;
            bossData.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }

        static void BuildController(PlayerSpawner spawner, NineLeggedTurtleBoss boss)
        {
            GameObject go = new("NineLeggedTurtleBattleController");
            NineLeggedTurtleBattleController controller = go.AddComponent<NineLeggedTurtleBattleController>();
            SerializedObject data = new(controller);
            data.FindProperty("boss").objectReferenceValue = boss;
            data.FindProperty("turtleGift").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            data.FindProperty("playerSpawner").objectReferenceValue = spawner;
            data.FindProperty("resultDelay").floatValue = 3f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureLighting()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.transform.rotation = Quaternion.Euler(52f, -25f, 0f);
                light.color = new Color(0.65f, 1f, 0.76f);
                light.intensity = 1.45f;
                light.shadows = LightShadows.Soft;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.01f;
            RenderSettings.fogColor = new Color(0.025f, 0.12f, 0.075f);
            RenderSettings.ambientLight = new Color(0.16f, 0.28f, 0.2f);
        }

        static void SetupMapAdditive()
        {
            Scene map = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Additive);
            Transform generated = map.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "Generated");
            if (generated == null) throw new MissingReferenceException("Map_ThuyTinh has no Generated root.");
            NineLeggedTurtleSetup.AddToMap(generated);
            EditorSceneManager.MarkSceneDirty(map);
            EditorSceneManager.SaveScene(map);
            EditorSceneManager.CloseScene(map, true);
        }

        static void RegisterScene()
        {
            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
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
