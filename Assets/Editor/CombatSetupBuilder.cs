using System.IO;
using System.Linq;
using SonTinhThuyTinh.Combat;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Gives Thuy Tinh his fight (docs/progress.md 9.12): builds the effects (CombatVfxBuilder), the attack and spell data assets and the move
    // set, hangs the low-poly sword on the right hand of Player_ThuyTinh_v2 (same grip as the old prefab), measures the new walk and run
    // speeds of the Great Sword clips, tunes dodge and jump to those clips, and puts four training dummies in Sandbox_Combat.
    // Run CombatAnimationsBuilder first. Safe to run again.
    public static class CombatSetupBuilder
    {
        const string DataDir = "Assets/Data/Combat/";
        const string ArtDir = "Assets/Art/Combat/";
        const string PlayerV2 = "Assets/Prefabs/Player/Player_ThuyTinh_v2.prefab";
        const string PlayerV1 = "Assets/Prefabs/Player/Player_ThuyTinh.prefab";
        const string SwordPrefab = "Assets/Prefabs/Weapons/Sword.prefab";
        const string SandboxScene = "Assets/Scenes/Sandbox_Combat.unity";
        const string FontPath = "Assets/Art/Fonts/Roboto-Bold SDF.asset";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Thuy Tinh Combat")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var vfx = CombatVfxBuilder.Build();
            MoveSet set = BuildData(vfx.hitSplash, vfx.spinWave, vfx.wind, vfx.rain, vfx.wave, vfx.slash, vfx.heavySlash);
            TunePlayer(set);
            BuildSandboxDummies();
            AssetDatabase.SaveAssets();
            Debug.Log("Thuy Tinh combat built.");
        }

        // ---------------------------------------------------------------- data

        static T Asset<T>(string name) where T : ScriptableObject
        {
            Directory.CreateDirectory(DataDir);
            string path = DataDir + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        // Clip lengths and the playback speed give the real durations; the frame numbers of the hit windows were measured in Blender
        // (docs/progress.md 8.1c): the clip times below are seconds into the cut clip at normal speed, divided by the speed here.
        static void Timing(AttackData a, string state, float speed, float clipLength, float hitFrom, float hitTo, float comboAt, float endAt)
        {
            a.stateName = state;
            a.animSpeed = speed;
            a.duration = Mathf.Min(clipLength, endAt) / speed;
            a.hitStart = hitFrom / speed;
            a.hitEnd = hitTo / speed;
            a.comboStart = comboAt / speed;
        }

        static MoveSet BuildData(GameObject hitSplash, GameObject spinWave, GameObject wind, GameObject rain, GameObject wave, GameObject slashVfx = null, GameObject heavySlashVfx = null)
        {
            // light combo: Slash, Kick, Spin (the finisher, a ring of water all round)
            var slash = Asset<AttackData>("Attack_Slash");
            Timing(slash, "Attack1", 1.45f, 1.47f, 0.63f, 1.0f, 0.9f, 1.47f);
            slash.damage = 10f; slash.staminaCost = 8f; slash.heavy = false; slash.superArmor = false;
            slash.hitCenter = new Vector3(0f, 1f, 1.5f); slash.hitSize = new Vector3(2.8f, 2f, 2.6f); slash.hitAround = false;
            slash.knockback = 3.5f; slash.knockUp = 0f;
            slash.lungeDistance = 0.9f; slash.lungeStart = 0.25f; slash.lungeEnd = 0.6f;
            slash.hitStop = 0.05f; slash.shake = 0.2f; slash.swingEffect = slashVfx; slash.swingEffectTime = 0.44f; slash.swingEffectScale = 1.0f;

            var kick = Asset<AttackData>("Attack_Kick");
            Timing(kick, "Attack2", 1.5f, 1.33f, 0.57f, 0.8f, 0.75f, 1.33f);
            kick.damage = 8f; kick.staminaCost = 8f; kick.heavy = false; kick.superArmor = false;
            kick.hitCenter = new Vector3(0f, 0.9f, 1.4f); kick.hitSize = new Vector3(2f, 1.8f, 2f); kick.hitAround = false;
            kick.knockback = 6.5f; kick.knockUp = 0f;
            kick.lungeDistance = 0.6f; kick.lungeStart = 0.2f; kick.lungeEnd = 0.45f;
            kick.hitStop = 0.05f; kick.shake = 0.25f; kick.swingEffect = null;

            var spin = Asset<AttackData>("Attack_Spin");
            Timing(spin, "Attack3", 1.3f, 1.6f, 0.67f, 1.07f, 1.35f, 1.6f);
            spin.damage = 18f; spin.staminaCost = 10f; spin.heavy = true; spin.superArmor = false;
            spin.hitCenter = new Vector3(0f, 1f, 0f); spin.hitSize = new Vector3(3.4f, 2f, 3.4f); spin.hitAround = true;
            spin.knockback = 7f; spin.knockUp = 1.5f;
            spin.lungeDistance = 0.8f; spin.lungeStart = 0.2f; spin.lungeEnd = 0.6f;
            spin.hitStop = 0.08f; spin.shake = 0.5f;
            spin.swingEffect = spinWave; spin.swingEffectTime = 0.52f; spin.swingEffectScale = 0.9f;

            // heavy attack (hold J): the casting clip, slow, with a super-armoured swing
            var heavy = Asset<AttackData>("Attack_Heavy");
            Timing(heavy, "HeavyAttack", 0.9f, 1.67f, 0.4f, 0.73f, 1.2f, 1.5f);
            heavy.damage = 30f; heavy.staminaCost = 25f; heavy.heavy = true; heavy.superArmor = true;
            heavy.hitCenter = new Vector3(0f, 1f, 1.9f); heavy.hitSize = new Vector3(3.4f, 2.4f, 3.6f); heavy.hitAround = false;
            heavy.knockback = 8f; heavy.knockUp = 3f;
            heavy.lungeDistance = 0f; heavy.lungeStart = 0f; heavy.lungeEnd = 0f;
            heavy.hitStop = 0.1f; heavy.shake = 0.6f;
            heavy.swingEffect = heavySlashVfx != null ? heavySlashVfx : spinWave; heavy.swingEffectTime = 0.42f; heavy.swingEffectScale = 1.25f;

            // jump attack (J in the air)
            var jumpAttack = Asset<AttackData>("Attack_JumpAttack");
            Timing(jumpAttack, "JumpAttack", 1.1f, 1.6f, 0.73f, 1.13f, 1.2f, 1.6f);
            jumpAttack.damage = 22f; jumpAttack.staminaCost = 15f; jumpAttack.heavy = true; jumpAttack.superArmor = false;
            jumpAttack.hitCenter = new Vector3(0f, 0.8f, 1.7f); jumpAttack.hitSize = new Vector3(3.2f, 2.2f, 3f); jumpAttack.hitAround = false;
            jumpAttack.knockback = 7f; jumpAttack.knockUp = 2f;
            jumpAttack.lungeDistance = 3f; jumpAttack.lungeStart = 0.15f; jumpAttack.lungeEnd = 0.7f;
            jumpAttack.hitStop = 0.09f; jumpAttack.shake = 0.5f;
            jumpAttack.swingEffect = spinWave; jumpAttack.swingEffectTime = 0.7f; jumpAttack.swingEffectScale = 0.8f;

            // spells (U, I, O): no hit volume of their own (hit start = hit end), the effect prefab does the work
            var windSpell = Asset<SpellData>("Spell_Wind");
            Timing(windSpell, "SpellWind", 1.3f, 1.07f, 0f, 0f, 0.65f, 1.07f);
            windSpell.displayName = "Gọi gió"; windSpell.color = new Color(0.8f, 0.95f, 1f); windSpell.kind = SpellKind.Wind;
            windSpell.cooldown = 8f; windSpell.staminaCost = 0f; windSpell.damage = 6f; windSpell.heavy = false; windSpell.superArmor = true;
            windSpell.effectPrefab = wind; windSpell.effectTime = 0.27f / 1.3f;
            windSpell.radius = 8f; windSpell.speed = 9f; windSpell.knockUpSpeed = 7f; windSpell.effectDuration = 1.4f;
            windSpell.lungeDistance = 0f; windSpell.swingEffect = null; windSpell.hitStop = 0f; windSpell.shake = 0.3f;

            var rainSpell = Asset<SpellData>("Spell_Rain");
            Timing(rainSpell, "SpellRain", 1f, 1.33f, 0f, 0f, 0.9f, 1.2f);
            rainSpell.displayName = "Hô mưa"; rainSpell.color = new Color(0.4f, 0.65f, 1f); rainSpell.kind = SpellKind.Rain;
            rainSpell.cooldown = 18f; rainSpell.staminaCost = 0f; rainSpell.damage = 0f; rainSpell.heavy = false; rainSpell.superArmor = true;
            rainSpell.effectPrefab = rain; rainSpell.effectTime = 0.33f;
            rainSpell.radius = 6f; rainSpell.range = 8f; rainSpell.effectDuration = 6f; rainSpell.tickDamage = 2f; rainSpell.tickInterval = 0.5f; rainSpell.slowFactor = 0.5f;
            rainSpell.lungeDistance = 0f; rainSpell.swingEffect = null; rainSpell.hitStop = 0f; rainSpell.shake = 0f;

            var waveSpell = Asset<SpellData>("Spell_Wave");
            Timing(waveSpell, "SpellWave", 1.15f, 1.73f, 0f, 0f, 1.25f, 1.73f);
            waveSpell.displayName = "Sóng nước"; waveSpell.color = new Color(0.2f, 0.85f, 0.9f); waveSpell.kind = SpellKind.Wave;
            waveSpell.cooldown = 10f; waveSpell.staminaCost = 0f; waveSpell.damage = 16f; waveSpell.heavy = true; waveSpell.superArmor = true;
            waveSpell.effectPrefab = wave; waveSpell.effectTime = 0.45f / 1.15f;
            waveSpell.radius = 2.5f; waveSpell.range = 10f; waveSpell.speed = 14f; waveSpell.knockback = 10f; waveSpell.knockUp = 3f;
            waveSpell.lungeDistance = 3f; waveSpell.lungeStart = 0.25f / 1.15f; waveSpell.lungeEnd = 1.0f / 1.15f;
            waveSpell.swingEffect = null; waveSpell.hitStop = 0f; waveSpell.shake = 0.45f;

            var set = Asset<MoveSet>("MoveSet_ThuyTinh");
            set.lightCombo = new[] { slash, kick, spin };
            set.heavy = heavy;
            set.jumpAttack = jumpAttack;
            set.spells = new[] { windSpell, rainSpell, waveSpell };
            set.hitLightSpeed = 1.5f; set.hitLightDuration = 0.7f; set.hitHeavySpeed = 0.9f; set.hitHeavyDuration = 1.15f; set.staggerDamage = 25f;
            set.hitSplash = hitSplash;
            set.hudFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            foreach (Object o in new Object[] { slash, kick, spin, heavy, jumpAttack, windSpell, rainSpell, waveSpell, set }) EditorUtility.SetDirty(o);
            AssetDatabase.SaveAssets();
            return set;
        }

        // ---------------------------------------------------------------- the player prefab

        static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static void TunePlayer(MoveSet set)
        {
            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefab);
            GameObject v1 = PrefabUtility.LoadPrefabContents(PlayerV1);
            GameObject v2 = PrefabUtility.LoadPrefabContents(PlayerV2);

            // the sword: same grip as the old prefab, kept at the same size and place in the world
            Transform hand1 = Find(v1, "mixamorig:RightHand"), hand2 = Find(v2, "mixamorig:RightHand");
            Transform sword1 = hand1 != null ? hand1.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Sword") : null;
            if (hand1 == null || hand2 == null || sword1 == null) Debug.LogWarning("Could not find the hand or the old sword to copy the grip from.");
            else
            {
                foreach (Transform old in hand2.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Sword" && t.parent == hand2).ToList()) Object.DestroyImmediate(old.gameObject);
                var sword2 = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab, hand2);
                sword2.name = "Sword";
                Vector3 s1 = hand1.lossyScale, s2 = hand2.lossyScale;
                sword2.transform.localPosition = new Vector3(sword1.localPosition.x * s1.x / s2.x, sword1.localPosition.y * s1.y / s2.y, sword1.localPosition.z * s1.z / s2.z);
                sword2.transform.localRotation = sword1.localRotation;
                Vector3 world = sword1.lossyScale;
                sword2.transform.localScale = new Vector3(world.x / s2.x, world.y / s2.y, world.z / s2.z);
            }

            // dodge and jumps follow the Great Sword clips (see docs/progress.md 9.12)
            var so = new SerializedObject(v2.GetComponent<SonTinhThuyTinh.Player.PlayerController>());
            so.FindProperty("moveSet").objectReferenceValue = set;
            so.FindProperty("dodge.distance").floatValue = 4.5f;
            so.FindProperty("dodge.duration").floatValue = 0.75f;
            so.FindProperty("dodge.animationSpeed").floatValue = 1.07f / 0.75f;   // the 1.07 s roll clip fills the 0.75 s dodge
            so.FindProperty("dodge.invulnerableWindow").vector2Value = new Vector2(0.05f, 0.5f);
            so.FindProperty("jump.standingHeight").floatValue = 1.3f;            // GS_Jump: ~0.75 s in the air
            so.FindProperty("jump.standingTakeoffDelay").floatValue = 0.1f;
            so.FindProperty("jump.standingClipOffset").floatValue = 0f;
            so.FindProperty("jump.runningHeight").floatValue = 0.9f;             // GS_RunJump: ~0.6 s in the air
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(v2, PlayerV2);
            PrefabUtility.UnloadPrefabContents(v2);
            PrefabUtility.UnloadPrefabContents(v1);
            AssetDatabase.SaveAssets();

            MeasureSpeeds();
        }

        // Walk and run speed (m/s) of the armed clips: the model is stepped through three seconds of each with root motion on.
        static void MeasureSpeeds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerV2);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = new Vector3(0f, 900f, 0f);
            var controller = go.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            var animator = go.GetComponentInChildren<Animator>();
            animator.applyRootMotion = true;
            animator.Rebind();

            float Measure(float blend)
            {
                animator.Rebind();
                animator.SetFloat("LocomotionBlend", blend);
                animator.Play("Locomotion", 0, 0f);
                animator.Update(0f);
                Vector3 start = animator.transform.position;
                const float step = 1f / 60f;
                float t = 0f;
                for (int i = 0; i < 180; i++) { animator.Update(step); t += step; }
                Vector3 d = animator.transform.position - start;
                d.y = 0f;
                return d.magnitude / t;
            }

            float walk = Measure(1f), run = Measure(2f);
            Object.DestroyImmediate(go);
            Debug.Log($"Great Sword speeds: walk {walk:F2} m/s, run {run:F2} m/s");
            if (walk < 0.3f || run < 1.5f) { Debug.LogWarning("Measured speeds look wrong, not applied."); return; }

            GameObject v2 = PrefabUtility.LoadPrefabContents(PlayerV2);
            var so = new SerializedObject(v2.GetComponent<SonTinhThuyTinh.Player.PlayerController>());
            so.FindProperty("walkSpeed").floatValue = walk;
            so.FindProperty("runSpeed").floatValue = run;
            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(v2, PlayerV2);
            PrefabUtility.UnloadPrefabContents(v2);
        }

        // ---------------------------------------------------------------- training dummies in the sandbox

        static Material DummyMaterial()
        {
            const string path = ArtDir + "Mat_Dummy.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(ArtDir);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(0.78f, 0.62f, 0.38f));
            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void BuildSandboxDummies()
        {
            var scene = EditorSceneManager.OpenScene(SandboxScene, OpenSceneMode.Single);
            foreach (var old in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "TrainingDummies").ToList())
                Object.DestroyImmediate(old.gameObject);

            var spawner = Object.FindFirstObjectByType<SonTinhThuyTinh.Player.PlayerSpawner>();
            Vector3 origin = spawner != null ? spawner.transform.position : Vector3.zero;
            Quaternion facing = spawner != null ? Quaternion.Euler(0f, spawner.transform.eulerAngles.y, 0f) : Quaternion.identity;
            Material material = DummyMaterial();

            var group = new GameObject("TrainingDummies").transform;
            Vector3[] offsets = { new(0f, 0f, 6f), new(-4.5f, 0f, 9f), new(4.5f, 0f, 9f), new(0f, 0f, 13f) };
            for (int i = 0; i < offsets.Length; i++)
            {
                var root = new GameObject("TrainingDummy_" + (i + 1)).transform;
                root.SetParent(group, false);
                root.SetPositionAndRotation(origin + facing * offsets[i], facing * Quaternion.Euler(0f, 180f, 0f));   // faces the player

                var body = Primitive(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 1f, 0f), new Vector3(0.95f, 0.95f, 0.95f), material);
                Primitive(PrimitiveType.Sphere, "Head", root, new Vector3(0f, 2.15f, 0f), Vector3.one * 0.55f, material);
                Primitive(PrimitiveType.Cube, "Arms", root, new Vector3(0f, 1.55f, 0f), new Vector3(1.8f, 0.16f, 0.16f), material);
                Primitive(PrimitiveType.Cylinder, "Base", root, new Vector3(0f, 0.05f, 0f), new Vector3(1f, 0.05f, 1f), material);

                var capsule = root.gameObject.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 1.1f, 0f);
                capsule.radius = 0.55f;
                capsule.height = 2.3f;
                var health = root.gameObject.AddComponent<Health>();
                new SerializedObject(health).FindProperty("max").floatValue = 300f;
                var hs = new SerializedObject(health);
                hs.FindProperty("max").floatValue = 300f;
                hs.ApplyModifiedProperties();

                var dummy = root.gameObject.AddComponent<TrainingDummy>();
                var ds = new SerializedObject(dummy);
                SerializedProperty list = ds.FindProperty("renderers");
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
                list.arraySize = renderers.Length;
                for (int r = 0; r < renderers.Length; r++) list.GetArrayElementAtIndex(r).objectReferenceValue = renderers[r];
                ds.ApplyModifiedProperties();
                body.name = "Body";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
