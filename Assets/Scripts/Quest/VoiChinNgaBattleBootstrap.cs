using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonTinhThuyTinh.Quest
{
    // Makes the copied combat scene self-contained at runtime. Keeping this setup in code
    // also means the arena survives future HUD/camera upgrades to Sandbox_Combat.
    public static class VoiChinNgaBattleBootstrap
    {
        // RuntimeInitializeOnLoadMethod(AfterSceneLoad) only covers the first scene that
        // starts Play Mode. The elephant arena is reached later through SceneLoader, so
        // listen to every completed scene load instead.
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
            if (scene.name != SceneNames.VoiChinNgaBattle) return;
            BuildArena();
        }

        static void BuildArena()
        {
            // Guard against accidental duplicate sceneLoaded callbacks when domain reload
            // is disabled in Enter Play Mode settings.
            if (GameObject.Find("VoiChinNgaBattleController") != null) return;

            foreach (string name in new[] { "TrainingDummies", "Pillars", "GiftPickups (test)", "GiftPickups", "ClimbBlocks", "GiftBanner", "GiftChecklist", "DebugHUD" })
            {
                GameObject found = GameObject.Find(name);
                if (found != null) Object.Destroy(found);
            }

            GameObject ground = GameObject.Find("Ground");
            if (ground != null)
            {
                ground.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
                ground.transform.localScale = new Vector3(46f, 1f, 46f);
                if (ground.TryGetComponent(out Renderer renderer)) renderer.material.color = new Color(0.16f, 0.24f, 0.14f);
            }

            PlayerSpawner spawner = Object.FindFirstObjectByType<PlayerSpawner>();
            PlayerController player = spawner != null ? spawner.Player : Object.FindFirstObjectByType<PlayerController>();
            if (player != null) player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -12f), Quaternion.identity);

            BuildBoundary();
            VoiChinNgaBoss boss = BuildBoss();
            GiftItem gift = Resources.Load<GiftItem>("Gift_VoiChinNga");

            var controllerObject = new GameObject("VoiChinNgaBattleController");
            VoiChinNgaBattleController controller = controllerObject.AddComponent<VoiChinNgaBattleController>();
            controller.Configure(boss, gift, spawner);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = new Color(0.12f, 0.19f, 0.16f);
        }

        static VoiChinNgaBoss BuildBoss()
        {
            GameObject prefab = Resources.Load<GameObject>("VoiChinNga");
            var root = new GameObject("VoiChinNga_Boss");
            root.transform.SetPositionAndRotation(new Vector3(0f, 0f, 10f), Quaternion.Euler(0f, 180f, 0f));
            if (prefab != null)
            {
                GameObject visual = Object.Instantiate(prefab, root.transform, false);
                visual.name = "VoiChinNga_Model";
                // The prefab root contains the 90-degree FBX correction that makes the
                // model readable. Its authored forward axis is +X, so -90 degrees maps
                // the elephant's head to the boss root's +Z (the AI forward direction).
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            }
            else Debug.LogError("Missing Resources/VoiChinNga prefab.");

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 4.3f, 0f);
            capsule.height = 8.6f;
            capsule.radius = 2.65f;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

            Health health = root.AddComponent<Health>();
            health.SetMaximum(260f);
            return root.AddComponent<VoiChinNgaBoss>();
        }

        static void BuildBoundary()
        {
            var arena = new GameObject("ElephantArena").transform;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var stone = new Material(shader) { color = new Color(0.12f, 0.16f, 0.11f) };
            for (int i = 0; i < 20; i++)
            {
                float angle = i * Mathf.PI * 2f / 20f;
                Vector3 direction = new(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "BoundaryStone_" + i.ToString("00");
                pillar.transform.SetParent(arena, false);
                pillar.transform.position = direction * 22f + Vector3.up * 2f;
                pillar.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, (i % 3 - 1) * 3f);
                pillar.transform.localScale = new Vector3(4.8f, 4f + (i % 4) * 0.45f, 1.7f);
                pillar.GetComponent<Renderer>().sharedMaterial = stone;
            }

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
    }
}
