using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Environment;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.Quest
{
    // Builds the arena at runtime as a safety net and keeps the copied combat scene
    // independent from editor-only setup scripts.
    public static class ManyFinnedSharkBattleBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            if (scene.name != SceneNames.ManyFinnedSharkBattle) return;
            BuildArena();
        }

        static void BuildArena()
        {
            if (GameObject.Find("ManyFinnedSharkBattleController") != null) return;

            foreach (string name in new[] { "TrainingDummies", "Pillars", "GiftPickups (test)", "GiftPickups", "ClimbBlocks", "GiftBanner", "GiftChecklist", "DebugHUD" })
            {
                GameObject found = GameObject.Find(name);
                if (found != null) Object.Destroy(found);
            }

            GameObject ground = GameObject.Find("Ground");
            if (ground != null)
            {
                ground.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
                ground.transform.localScale = new Vector3(42f, 1f, 42f);
                if (ground.TryGetComponent(out Renderer renderer))
                    renderer.material.color = new Color(0.035f, 0.18f, 0.23f);
            }

            PlayerSpawner spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            PlayerController player = spawner != null ? spawner.Player : Object.FindFirstObjectByType<PlayerController>();
            if (player != null) player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -10f), Quaternion.identity);

            BuildBoundary();
            ManyFinnedSharkBoss boss = BuildBoss();
            GiftItem gift = Resources.Load<GiftItem>("Gift_Map9Vay");

            var controllerObject = new GameObject("ManyFinnedSharkBattleController");
            ManyFinnedSharkBattleController controller = controllerObject.AddComponent<ManyFinnedSharkBattleController>();
            controller.Configure(boss, gift, spawner);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.02f, 0.13f, 0.19f);
            RenderSettings.ambientLight = new Color(0.12f, 0.3f, 0.4f);
        }

        static ManyFinnedSharkBoss BuildBoss()
        {
            GameObject prefab = Resources.Load<GameObject>("Map9Vay");
            var root = new GameObject("Map9Vay_Boss");
            root.transform.SetPositionAndRotation(new Vector3(0f, 1.15f, 9f), Quaternion.Euler(0f, 180f, 0f));
            if (prefab != null)
            {
                GameObject visual = Object.Instantiate(prefab, root.transform, false);
                visual.name = "Map9Vay_Model";
                ManyFinnedSharkSwimmer swimmer = visual.GetComponent<ManyFinnedSharkSwimmer>();
                if (swimmer != null)
                {
                    swimmer.enabled = false;
                    Object.Destroy(swimmer);
                }
            }
            else Debug.LogError("Missing Resources/Map9Vay prefab.");

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
            health.SetMaximum(220f);
            return root.AddComponent<ManyFinnedSharkBoss>();
        }

        static void BuildBoundary()
        {
            var arena = new GameObject("SharkArena").transform;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var reef = new Material(shader) { color = new Color(0.035f, 0.18f, 0.23f) };
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
                rock.GetComponent<Renderer>().sharedMaterial = reef;
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
    }
}
