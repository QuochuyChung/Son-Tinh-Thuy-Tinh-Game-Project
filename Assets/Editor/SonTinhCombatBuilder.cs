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
            ("son_tinh_v2_earth_fist.fbx", "EarthFist", 0, 42, true),      // hand-keyed: tools/son_tinh_finisher.py
        };

        // clips taken as they are from the Kevin Iglesias spellcasting pack (humanoid, retargeted onto him)
        const string KevinDir = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/";
        static readonly Dictionary<string, string> Kevin = new()
        {
            ["RaiseRight"] = KevinDir + "Directional/HumanM@MagicAttackDirect1H01_R.fbx",   // the right arm thrust up to chest height: Đá nhọn
        };

        // base clip of the shared controller (Thuy Tinh's) -> Son Tinh's clip that replaces it. The spell states keep their names but now
        // play: SpellWind = Người đá (Z, MagicBolt), SpellWave = Tường đá (X, Slam: both hands down on the ground), SpellRain = Đá nhọn (C).
        static readonly (string baseClip, string mine)[] Mapping =
        {
            ("GS_Slash", "Punch"), ("GS_Kick", "Kick"), ("GS_Spin", "EarthFist"), ("GS_CastHeavy", "Uppercut"), ("GS_JumpAttack", "JumpAttack"),
            ("GS_CastWind", "MagicBolt"), ("GS_PowerUp", "RaiseRight"), ("GS_SlideAttack", "Slam"), ("GS_Impact", "Reaction"), ("GS_Roll", "Roll"),
        };

        // timings of the Kevin clips (seconds into the clip at normal speed; measured from the hands' speed)
        const float FistSpeed = 1.15f, FistClip = 42f / 30f, FistHitFrom = 0.52f, FistHitTo = 0.7f, FistComboAt = 1.0f;   // impact at 0.55 s
        const float RaiseSpeed = 1.2f, RaiseClip = 36f / 30f, RaiseAt = 0.4f, RaiseComboAt = 0.8f;

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Son Tinh Combat")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (var cut in Cuts) Configure(AnimDir + cut.file, cut.clip, cut.first, cut.last, cut.bakeHeight);
            AssetDatabase.Refresh();
            FillOverride();

            var vfx = EarthVfxBuilder.Build();
            var rocks = RockSkillPrefabs(vfx.shock);
            MoveSet set = BuildData(vfx.hit, vfx.shock, rocks.golem, rocks.wall, rocks.spikes, rocks.fist, vfx.slash, vfx.heavySlash);
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
            if (Kevin.TryGetValue(name, out string kevinPath))
                return AssetDatabase.LoadAllAssetsAtPath(kevinPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
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

        static MoveSet BuildData(GameObject hitSplash, GameObject shock, GameObject golemFx, GameObject wallFx, GameObject spikesFx, GameObject fistFx, GameObject slashVfx = null, GameObject heavySlashVfx = null)
        {
            // light combo: cross punch, kick, Địa Chấn Quyền (the finisher: he leaps and drives his fist into the ground, stone spikes fan out ahead)
            var punch = Asset<AttackData>("Attack_Punch");
            Timing(punch, "Attack1", 1.25f, 34f / 30f, 0.37f, 0.62f, 0.68f, 34f / 30f);
            punch.damage = 20f; punch.staminaCost = 8f; punch.heavy = false; punch.superArmor = false;
            punch.hitCenter = new Vector3(0f, 1.1f, 1.3f); punch.hitSize = new Vector3(2.2f, 1.9f, 2.2f); punch.hitAround = false;
            punch.knockback = 3.5f; punch.knockUp = 0f;
            punch.lungeDistance = 0.7f; punch.lungeStart = 0.12f; punch.lungeEnd = 0.4f;
            punch.hitStop = 0.05f; punch.shake = 0.2f; punch.swingEffect = slashVfx; punch.swingEffectTime = 0.28f; punch.swingEffectScale = 0.85f;

            var kick = Asset<AttackData>("Attack_Kick");
            Timing(kick, "Attack2", 1.2f, 36f / 30f, 0.22f, 0.5f, 0.58f, 36f / 30f);
            kick.damage = 18f; kick.staminaCost = 8f; kick.heavy = false; kick.superArmor = false;
            kick.hitCenter = new Vector3(0f, 0.9f, 1.4f); kick.hitSize = new Vector3(2f, 1.8f, 2f); kick.hitAround = false;
            kick.knockback = 6.5f; kick.knockUp = 0f;
            kick.lungeDistance = 0.5f; kick.lungeStart = 0.05f; kick.lungeEnd = 0.3f;
            kick.hitStop = 0.05f; kick.shake = 0.25f; kick.swingEffect = null;

            var fist = Asset<AttackData>("Attack_EarthFist");
            Timing(fist, "Attack3", FistSpeed, FistClip, FistHitFrom, FistHitTo, FistComboAt, FistClip);
            fist.damage = 39f; fist.staminaCost = 10f; fist.heavy = true; fist.superArmor = false;
            fist.hitCenter = new Vector3(0f, 0.8f, 2.2f); fist.hitSize = new Vector3(3.4f, 2f, 4.4f); fist.hitAround = false;   // the fan of spikes
            fist.knockback = 6f; fist.knockUp = 6f;
            fist.lungeDistance = 1.6f; fist.lungeStart = 0.3f / FistSpeed; fist.lungeEnd = FistHitFrom / FistSpeed;   // the leap
            fist.hitStop = 0.12f; fist.shake = 0.7f;
            fist.swingEffect = fistFx; fist.swingEffectTime = FistHitFrom / FistSpeed; fist.swingEffectScale = 1f;   // with the first frame of the hit (before its hit stop)

            // heavy attack (hold J): the uppercut that throws the target up, super-armoured
            var heavy = Asset<AttackData>("Attack_Uppercut");
            Timing(heavy, "HeavyAttack", 1f, 35f / 30f, 0.28f, 0.55f, 0.9f, 35f / 30f);
            heavy.damage = 34f; heavy.staminaCost = 25f; heavy.heavy = true; heavy.superArmor = true;
            heavy.hitCenter = new Vector3(0f, 1.2f, 1.4f); heavy.hitSize = new Vector3(2.4f, 2.6f, 2.2f); heavy.hitAround = false;
            heavy.knockback = 5f; heavy.knockUp = 7f;
            heavy.lungeDistance = 0.6f; heavy.lungeStart = 0.12f; heavy.lungeEnd = 0.4f;
            heavy.hitStop = 0.1f; heavy.shake = 0.6f;
            heavy.swingEffect = heavySlashVfx != null ? heavySlashVfx : shock; heavy.swingEffectTime = 0.35f; heavy.swingEffectScale = 1.2f;

            // jump attack (J in the air): the clip's strike lands with the character
            var jumpAttack = Asset<AttackData>("Attack_JumpAttack");
            Timing(jumpAttack, "JumpAttack", 1.1f, 36f / 30f, 0.6f, 0.85f, 0.95f, 36f / 30f);
            jumpAttack.damage = 44f; jumpAttack.staminaCost = 15f; jumpAttack.heavy = true; jumpAttack.superArmor = false;
            jumpAttack.hitCenter = new Vector3(0f, 0.8f, 1.5f); jumpAttack.hitSize = new Vector3(3.2f, 2.2f, 3f); jumpAttack.hitAround = false;
            jumpAttack.knockback = 7f; jumpAttack.knockUp = 2f;
            jumpAttack.lungeDistance = 2.5f; jumpAttack.lungeStart = 0.1f; jumpAttack.lungeEnd = 0.5f;
            jumpAttack.hitStop = 0.09f; jumpAttack.shake = 0.5f;
            jumpAttack.swingEffect = shock; jumpAttack.swingEffectTime = 0.6f; jumpAttack.swingEffectScale = 0.8f;

            // spells (Z, X, C); the animator states keep their Thuy Tinh names (see Mapping for the clips they play)
            // Z, Người đá: two rock golems charge ahead as far as Thủy Tinh's Sóng đẩy reaches, throw and stun (1 s) whoever they hit
            var golemSpell = Asset<SpellData>("Spell_RockGolem");
            Timing(golemSpell, "SpellWind", 1.3f, 46f / 30f, 0f, 0f, 0.85f, 46f / 30f);
            golemSpell.displayName = "Người đá"; golemSpell.color = new Color(0.95f, 0.72f, 0.38f); golemSpell.kind = SpellKind.Wave;
            golemSpell.cooldown = 10f; golemSpell.staminaCost = 0f; golemSpell.damage = 35f; golemSpell.heavy = true; golemSpell.superArmor = true;
            golemSpell.effectPrefab = golemFx; golemSpell.effectTime = 0.67f / 1.3f;
            golemSpell.range = 12f; golemSpell.speed = 13f; golemSpell.knockback = 14f; golemSpell.knockUp = 4f;   // Sóng đẩy: 12 m at 13 m/s
            golemSpell.stunSeconds = 1f; golemSpell.bossStunScale = 1f; golemSpell.slowAfterHit = 0f;
            golemSpell.lungeDistance = 0f; golemSpell.swingEffect = null; golemSpell.hitStop = 0.06f; golemSpell.shake = 0.5f;

            // X, Tường đá: both hands slam the ground and a 4 x 3 m wall of rock stands 2 s right in front of him; water attacks burst on it
            var wallSpell = Asset<SpellData>("Spell_RockWall");
            Timing(wallSpell, "SpellWave", 1.3f, 38f / 30f, 0f, 0f, 0.9f, 38f / 30f);
            wallSpell.displayName = "Tường đá"; wallSpell.color = new Color(0.78f, 0.66f, 0.5f); wallSpell.kind = SpellKind.Rain;
            wallSpell.cooldown = 9f; wallSpell.staminaCost = 0f; wallSpell.damage = 0f; wallSpell.heavy = false; wallSpell.superArmor = true;
            wallSpell.effectPrefab = wallFx; wallSpell.effectTime = 0.57f / 1.3f;
            wallSpell.radius = 2f; wallSpell.range = 2.2f; wallSpell.effectDuration = 2f; wallSpell.stunSeconds = 0f;
            wallSpell.lungeDistance = 0f; wallSpell.swingEffect = null; wallSpell.hitStop = 0f; wallSpell.shake = 0.5f;

            // C, Đá nhọn: the right arm comes up and four sharp spires stab up out of the ground one after another along a line ahead
            var spikeSpell = Asset<SpellData>("Spell_RockSpikes");
            Timing(spikeSpell, "SpellRain", RaiseSpeed, RaiseClip, 0f, 0f, RaiseComboAt, RaiseClip);
            spikeSpell.displayName = "Đá nhọn"; spikeSpell.color = new Color(0.62f, 0.82f, 0.45f); spikeSpell.kind = SpellKind.Wind;
            spikeSpell.cooldown = 9f; spikeSpell.staminaCost = 0f; spikeSpell.damage = 30f; spikeSpell.heavy = true; spikeSpell.superArmor = true;
            spikeSpell.effectPrefab = spikesFx; spikeSpell.effectTime = RaiseAt / RaiseSpeed;
            spikeSpell.range = 11f; spikeSpell.knockback = 3f; spikeSpell.knockUpSpeed = 8f; spikeSpell.stunSeconds = 0f;
            spikeSpell.lungeDistance = 0f; spikeSpell.swingEffect = null; spikeSpell.hitStop = 0.05f; spikeSpell.shake = 0.5f;


            var set = Asset<MoveSet>("MoveSet_SonTinh");
            set.lightCombo = new[] { punch, kick, fist };
            set.heavy = heavy;
            set.jumpAttack = jumpAttack;
            set.spells = new[] { golemSpell, wallSpell, spikeSpell };
            set.hitLightSpeed = 1.5f; set.hitLightDuration = 0.7f; set.hitHeavySpeed = 0.9f; set.hitHeavyDuration = 1.15f; set.staggerDamage = 25f;
            set.hitSplash = hitSplash;
            set.hudFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            foreach (Object o in new Object[] { punch, kick, fist, heavy, jumpAttack, golemSpell, wallSpell, spikeSpell, set }) EditorUtility.SetDirty(o);
            AssetDatabase.SaveAssets();
            return set;
        }

        // ---------------------------------------------------------------- rock skills

        // The effect prefabs of Người đá, Tường đá, Đá nhọn and the finisher's spike fan: the Meshy models (NewProps) and the dust burst,
        // on the skill scripts.
        static (GameObject golem, GameObject wall, GameObject spikes, GameObject fist) RockSkillPrefabs(GameObject dust)
        {
            GameObject golem = RockSkillPrefab<RockGolemCharge>("VFX_RockGolem", "golemModel", NewProps.Prefab(NewProps.Kind.RockGolem), dust);
            GameObject wall = RockSkillPrefab<RockWallBarrier>("VFX_RockWall", "wallModel", NewProps.Prefab(NewProps.Kind.RockWall), dust);
            GameObject spikes = RockSkillPrefab<RockSpikeLine>("VFX_RockSpikes", "spireModel", NewProps.Prefab(NewProps.Kind.RockSpire), dust);
            GameObject fist = RockSkillPrefab<EarthFistBurst>("VFX_EarthFist", "spireModel", NewProps.Prefab(NewProps.Kind.RockSpire), dust);
            return (golem, wall, spikes, fist);
        }

        static GameObject RockSkillPrefab<T>(string name, string modelField, GameObject model, GameObject dust) where T : MonoBehaviour
        {
            if (model == null) Debug.LogWarning($"{name}: the rock model is missing (Assets/Art/Props, tools/prep_new_assets.py).");
            var root = new GameObject(name);
            try
            {
                var so = new SerializedObject(root.AddComponent<T>());
                so.FindProperty(modelField).objectReferenceValue = model;
                so.FindProperty("dustPrefab").objectReferenceValue = dust;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Combat/" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
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
