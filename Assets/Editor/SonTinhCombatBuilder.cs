using System.Collections.Generic;
using System.IO;
using System.Linq;
using SonTinhThuyTinh.Combat;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Gives Son Tinh his fight (docs/progress.md 9.14): the same moves as Thuy Tinh with bare hands and earth instead of water. Imports the
    // Mixamo clips of ArtSource/Mixamo/SonTinh_v2 (copied to Assets/Animations/SonTinh_v2) cut to the useful part (frames measured in Blender
    // from the limb speeds), fills his override controller so the shared combat states play them, builds the earth effects (EarthVfxBuilder),
    // the attack / spell data and the move set, and tunes Player_SonTinh_v2 (move set, roll, trails on fists and feet).
    // Run after CombatAnimationsBuilder (the states are made there). Safe to run again.
    public static class SonTinhCombatBuilder
    {
        const string AnimDir = "Assets/Animations/SonTinh_v2/";
        const string DataDir = "Assets/Data/Combat/SonTinh/";
        const string PlayerPrefab = "Assets/Prefabs/Player/Player_SonTinh_v2.prefab";
        const string Override = "Assets/Animations/Shared/AOC_SonTinh_v2.overrideController";
        const string FontPath = "Assets/Art/Fonts/Roboto-Bold SDF.asset";

        // file, clip name, first frame, last frame (0-based), body height baked into the pose
        static readonly (string file, string clip, int first, int last, bool bakeHeight)[] Cuts =
        {
            ("son_tinh_v2_punch.fbx", "Punch", 14, 48, true),
            ("son_tinh_v2_kick.fbx", "Kick", 4, 40, true),
            ("son_tinh_v2_hurricane_kick.fbx", "HurricaneKick", 0, 50, true),
            ("son_tinh_v2_uppercut.fbx", "Uppercut", 3, 38, true),
            ("son_tinh_v2_jump_attack.fbx", "JumpAttack", 28, 64, true),
            ("son_tinh_v2_magic_bolt.fbx", "MagicBolt", 4, 50, true),
            ("son_tinh_v2_magic_area.fbx", "MagicArea", 12, 62, true),
            ("son_tinh_v2_slam.fbx", "Slam", 8, 46, true),
            ("son_tinh_v2_roll.fbx", "Roll", 20, 52, true),
            ("son_tinh_v2_reaction.fbx", "Reaction", 0, 34, true),
        };

        // base clip of the shared controller (Thuy Tinh's) -> Son Tinh's clip that replaces it
        static readonly (string baseClip, string mine)[] Mapping =
        {
            ("GS_Slash", "Punch"), ("GS_Kick", "Kick"), ("GS_Spin", "HurricaneKick"), ("GS_CastHeavy", "Uppercut"), ("GS_JumpAttack", "JumpAttack"),
            ("GS_CastWind", "MagicBolt"), ("GS_PowerUp", "MagicArea"), ("GS_SlideAttack", "Slam"), ("GS_Impact", "Reaction"), ("GS_Roll", "Roll"),
        };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Son Tinh Combat")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (var cut in Cuts) Configure(AnimDir + cut.file, cut.clip, cut.first, cut.last, cut.bakeHeight);
            AssetDatabase.Refresh();
            FillOverride();

            var vfx = EarthVfxBuilder.Build();
            MoveSet set = BuildData(vfx.hit, vfx.shock, vfx.quake, vfx.zone, vfx.rockWave, vfx.slash, vfx.heavySlash);
            TunePlayer(set);
            AssetDatabase.SaveAssets();
            Debug.Log("Son Tinh combat built.");
        }

        // ---------------------------------------------------------------- animation clips

        static void Configure(string path, string name, int first, int last, bool bakeHeight)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { Debug.LogWarning("Missing animation file: " + path); return; }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;   // each of his files needs its own avatar (docs 2, Son Tinh v2)
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.SaveAndReimport();   // the default clip list only exists after a first import as Humanoid

            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults.Length == 0) { Debug.LogWarning("No animation in " + path); return; }
            ModelImporterClipAnimation clip = defaults[0];
            float start = clip.firstFrame, end = clip.lastFrame;
            clip.name = name;
            clip.firstFrame = start + first;
            clip.lastFrame = Mathf.Min(end, start + last);
            clip.loopTime = false;
            clip.loopPose = false;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = bakeHeight;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = false;   // Animator.applyRootMotion is off on the players: travel comes from code
            clip.keepOriginalPositionXZ = false;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }

        static AnimationClip Clip(string name)
        {
            foreach (var cut in Cuts)
                if (cut.clip == name)
                    return AssetDatabase.LoadAllAssetsAtPath(AnimDir + cut.file).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
            return null;
        }

        static void FillOverride()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Override);
            if (controller == null) { Debug.LogWarning("Missing override controller: " + Override); return; }
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
                foreach (var (baseClip, mine) in Mapping)
                    if (overrides[i].Key != null && overrides[i].Key.name == baseClip)
                    {
                        AnimationClip clip = Clip(mine);
                        if (clip == null) Debug.LogWarning("Clip not found: " + mine);
                        else overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
                    }
            controller.ApplyOverrides(overrides);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
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

        // Same convention as CombatSetupBuilder: times are seconds into the cut clip at normal speed, divided by the playback speed here.
        static void Timing(AttackData a, string state, float speed, float clipLength, float hitFrom, float hitTo, float comboAt, float endAt)
        {
            a.stateName = state;
            a.animSpeed = speed;
            a.duration = Mathf.Min(clipLength, endAt) / speed;
            a.hitStart = hitFrom / speed;
            a.hitEnd = hitTo / speed;
            a.comboStart = comboAt / speed;
        }

        static MoveSet BuildData(GameObject hitSplash, GameObject shock, GameObject quake, GameObject zone, GameObject rockWave, GameObject slashVfx = null, GameObject heavySlashVfx = null)
        {
            // light combo: cross punch, kick, hurricane kick (the finisher: spinning kicks all round)
            var punch = Asset<AttackData>("Attack_Punch");
            Timing(punch, "Attack1", 1.25f, 34f / 30f, 0.37f, 0.62f, 0.68f, 34f / 30f);
            punch.damage = 10f; punch.staminaCost = 8f; punch.heavy = false; punch.superArmor = false;
            punch.hitCenter = new Vector3(0f, 1.1f, 1.3f); punch.hitSize = new Vector3(2.2f, 1.9f, 2.2f); punch.hitAround = false;
            punch.knockback = 3.5f; punch.knockUp = 0f;
            punch.lungeDistance = 0.7f; punch.lungeStart = 0.12f; punch.lungeEnd = 0.4f;
            punch.hitStop = 0.05f; punch.shake = 0.2f; punch.swingEffect = slashVfx; punch.swingEffectTime = 0.28f; punch.swingEffectScale = 0.85f;

            var kick = Asset<AttackData>("Attack_Kick");
            Timing(kick, "Attack2", 1.2f, 36f / 30f, 0.22f, 0.5f, 0.58f, 36f / 30f);
            kick.damage = 9f; kick.staminaCost = 8f; kick.heavy = false; kick.superArmor = false;
            kick.hitCenter = new Vector3(0f, 0.9f, 1.4f); kick.hitSize = new Vector3(2f, 1.8f, 2f); kick.hitAround = false;
            kick.knockback = 6.5f; kick.knockUp = 0f;
            kick.lungeDistance = 0.5f; kick.lungeStart = 0.05f; kick.lungeEnd = 0.3f;
            kick.hitStop = 0.05f; kick.shake = 0.25f; kick.swingEffect = null;

            var hurricane = Asset<AttackData>("Attack_HurricaneKick");
            Timing(hurricane, "Attack3", 1.3f, 50f / 30f, 0.3f, 1.3f, 1.55f, 50f / 30f);
            hurricane.damage = 20f; hurricane.staminaCost = 10f; hurricane.heavy = true; hurricane.superArmor = false;
            hurricane.hitCenter = new Vector3(0f, 1f, 0f); hurricane.hitSize = new Vector3(3.2f, 2f, 3.2f); hurricane.hitAround = true;
            hurricane.knockback = 7f; hurricane.knockUp = 2f;
            hurricane.lungeDistance = 1.5f; hurricane.lungeStart = 0.1f; hurricane.lungeEnd = 0.9f;
            hurricane.hitStop = 0.08f; hurricane.shake = 0.5f;
            hurricane.swingEffect = shock; hurricane.swingEffectTime = 0.45f; hurricane.swingEffectScale = 0.9f;

            // heavy attack (hold J): the uppercut that throws the target up, super-armoured
            var heavy = Asset<AttackData>("Attack_Uppercut");
            Timing(heavy, "HeavyAttack", 1f, 35f / 30f, 0.28f, 0.55f, 0.9f, 35f / 30f);
            heavy.damage = 30f; heavy.staminaCost = 25f; heavy.heavy = true; heavy.superArmor = true;
            heavy.hitCenter = new Vector3(0f, 1.2f, 1.4f); heavy.hitSize = new Vector3(2.4f, 2.6f, 2.2f); heavy.hitAround = false;
            heavy.knockback = 5f; heavy.knockUp = 7f;
            heavy.lungeDistance = 0.6f; heavy.lungeStart = 0.12f; heavy.lungeEnd = 0.4f;
            heavy.hitStop = 0.1f; heavy.shake = 0.6f;
            heavy.swingEffect = heavySlashVfx != null ? heavySlashVfx : shock; heavy.swingEffectTime = 0.35f; heavy.swingEffectScale = 1.2f;

            // jump attack (J in the air): the clip's strike lands with the character
            var jumpAttack = Asset<AttackData>("Attack_JumpAttack");
            Timing(jumpAttack, "JumpAttack", 1.1f, 36f / 30f, 0.6f, 0.85f, 0.95f, 36f / 30f);
            jumpAttack.damage = 22f; jumpAttack.staminaCost = 15f; jumpAttack.heavy = true; jumpAttack.superArmor = false;
            jumpAttack.hitCenter = new Vector3(0f, 0.8f, 1.5f); jumpAttack.hitSize = new Vector3(3.2f, 2.2f, 3f); jumpAttack.hitAround = false;
            jumpAttack.knockback = 7f; jumpAttack.knockUp = 2f;
            jumpAttack.lungeDistance = 2.5f; jumpAttack.lungeStart = 0.1f; jumpAttack.lungeEnd = 0.5f;
            jumpAttack.hitStop = 0.09f; jumpAttack.shake = 0.5f;
            jumpAttack.swingEffect = shock; jumpAttack.swingEffectTime = 0.6f; jumpAttack.swingEffectScale = 0.8f;

            // spells (U, I, O); the animator states keep their Thuy Tinh names: SpellWind = first slot, SpellRain = second, SpellWave = third
            var rockSpell = Asset<SpellData>("Spell_RockLine");
            Timing(rockSpell, "SpellWind", 1.3f, 46f / 30f, 0f, 0f, 0.85f, 46f / 30f);
            rockSpell.displayName = "Núi mọc"; rockSpell.color = new Color(0.95f, 0.75f, 0.4f); rockSpell.kind = SpellKind.Wave;
            rockSpell.cooldown = 10f; rockSpell.staminaCost = 0f; rockSpell.damage = 16f; rockSpell.heavy = true; rockSpell.superArmor = true;
            rockSpell.effectPrefab = rockWave; rockSpell.effectTime = 0.67f / 1.3f;
            rockSpell.radius = 2.5f; rockSpell.range = 10f; rockSpell.speed = 14f; rockSpell.knockback = 10f; rockSpell.knockUp = 3f;
            rockSpell.lungeDistance = 0f; rockSpell.swingEffect = null; rockSpell.hitStop = 0f; rockSpell.shake = 0.45f;

            var zoneSpell = Asset<SpellData>("Spell_EarthZone");
            Timing(zoneSpell, "SpellRain", 1.3f, 50f / 30f, 0f, 0f, 1.05f, 1.4f);
            zoneSpell.displayName = "Núi dâng"; zoneSpell.color = new Color(0.85f, 0.65f, 0.38f); zoneSpell.kind = SpellKind.Rain;
            zoneSpell.cooldown = 18f; zoneSpell.staminaCost = 0f; zoneSpell.damage = 0f; zoneSpell.heavy = false; zoneSpell.superArmor = true;
            zoneSpell.effectPrefab = zone; zoneSpell.effectTime = 0.83f / 1.3f;
            zoneSpell.radius = 6f; zoneSpell.range = 8f; zoneSpell.effectDuration = 6f; zoneSpell.tickDamage = 2f; zoneSpell.tickInterval = 0.5f; zoneSpell.slowFactor = 0.5f;
            zoneSpell.lungeDistance = 0f; zoneSpell.swingEffect = null; zoneSpell.hitStop = 0f; zoneSpell.shake = 0.45f;

            var quakeSpell = Asset<SpellData>("Spell_Earthquake");
            Timing(quakeSpell, "SpellWave", 1.3f, 38f / 30f, 0f, 0f, 0.9f, 38f / 30f);
            quakeSpell.displayName = "Núi non"; quakeSpell.color = new Color(0.85f, 0.45f, 0.25f); quakeSpell.kind = SpellKind.Wind;
            quakeSpell.cooldown = 8f; quakeSpell.staminaCost = 0f; quakeSpell.damage = 10f; quakeSpell.heavy = true; quakeSpell.superArmor = true;
            quakeSpell.effectPrefab = quake; quakeSpell.effectTime = 0.57f / 1.3f;
            quakeSpell.radius = 8f; quakeSpell.speed = 0f; quakeSpell.knockUpSpeed = 7f; quakeSpell.effectDuration = 1.6f;   // no pull: the ground throws them up where they stand
            quakeSpell.lungeDistance = 0f; quakeSpell.swingEffect = null; quakeSpell.hitStop = 0f; quakeSpell.shake = 0.7f;

            var set = Asset<MoveSet>("MoveSet_SonTinh");
            set.lightCombo = new[] { punch, kick, hurricane };
            set.heavy = heavy;
            set.jumpAttack = jumpAttack;
            set.spells = new[] { rockSpell, zoneSpell, quakeSpell };
            set.hitLightSpeed = 1.5f; set.hitLightDuration = 0.7f; set.hitHeavySpeed = 0.9f; set.hitHeavyDuration = 1.15f; set.staggerDamage = 25f;
            set.hitSplash = hitSplash;
            set.hudFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            foreach (Object o in new Object[] { punch, kick, hurricane, heavy, jumpAttack, rockSpell, zoneSpell, quakeSpell, set }) EditorUtility.SetDirty(o);
            AssetDatabase.SaveAssets();
            return set;
        }

        // ---------------------------------------------------------------- the player prefab

        static void TunePlayer(MoveSet set)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                EarthVfxBuilder.Trails(root);

                // the move set, and the dodge now is a real roll (the 1.07 s Stand To Roll clip fills the 0.75 s dodge, like Thuy Tinh)
                var so = new SerializedObject(root.GetComponent<SonTinhThuyTinh.Player.PlayerController>());
                so.FindProperty("moveSet").objectReferenceValue = set;
                so.FindProperty("dodge.distance").floatValue = 4.5f;
                so.FindProperty("dodge.duration").floatValue = 0.75f;
                so.FindProperty("dodge.animationSpeed").floatValue = 1.07f / 0.75f;
                so.FindProperty("dodge.invulnerableWindow").vector2Value = new Vector2(0.05f, 0.5f);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
