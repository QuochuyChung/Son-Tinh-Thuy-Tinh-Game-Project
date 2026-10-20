using System;
using System.Collections.Generic;
using System.Linq;
using SonTinhThuyTinh.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class ThuyTinhMageCombatSetup
{
    const string ControllerPath = "Assets/Animations/Shared/AOC_ThuyTinh_v2.overrideController";
    const string BaseControllerPath = "Assets/Animations/Shared/AC_Humanoid_Base.controller";
    const string UpperBodyMaskPath = "Assets/Kevin Iglesias/Human Animations/Models/Avatar Masks/Human Body Upper Mask.mask";
    const string PlayerPrefabPath = "Assets/Prefabs/Player/Player_ThuyTinh_v2.prefab";
    const string MoveSetPath = "Assets/Data/Combat/MoveSet_ThuyTinh.asset";
    const string ProjectilePrefabPath = "Assets/Prefabs/Combat/VFX_WaterOrbProjectile.prefab";
    const string AttackFolder = "Assets/Data/Combat/ThuyTinh";

    const string IdleOriginalGuid = "552e6c0d8b2851e488789d96666b2bfc";
    const string Attack1OriginalGuid = "8332a4cf62c6b5b4084c170fb76485b2";
    const string Attack2OriginalGuid = "1ca536407f2482642a7e8ba959724ee9";
    const string Attack3OriginalGuid = "6b124f495462cac4faa0841f322115b4";
    const string HeavyOriginalGuid = "213dda367f817304b98ccd6fca7a25b5";
    const string WalkOriginalGuid = "55c9a576414f7c645bdda16d8bd7345e";
    const string RunOriginalGuid = "0e31b045bcfa6b64ca13f6d9ee4b5396";

    const string IdleClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/HumanM@CastingIdle01.fbx";
    const string LeftClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Directional/HumanM@MagicAttackDirect1H01_L.fbx";
    const string RightClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Directional/HumanM@MagicAttackDirect1H01_R.fbx";
    const string TwoHandClipPath = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Directional/HumanM@MagicAttackDirect2H01.fbx";
    const string SonTinhReactionPath = "Assets/Animations/SonTinh_v2/son_tinh_v2_reaction.fbx";   // cut by SonTinhCombatBuilder
    const string WalkClipPath ="Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_walk.fbx";
    const string RunClipPath = "Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_run.fbx";

    [MenuItem("Tools/Son Tinh Thuy Tinh/Apply Thuy Tinh Mage Combat")]
    public static void Apply()
    {
        AnimationClip idle = LoadClip(IdleClipPath);
        AnimationClip left = LoadClip(LeftClipPath);
        AnimationClip right = LoadClip(RightClipPath);
        AnimationClip twoHand = LoadClip(TwoHandClipPath);
        AnimationClip walk = LoadClip(WalkClipPath);
        AnimationClip run = LoadClip(RunClipPath);
        if (idle == null || left == null || right == null || twoHand == null || walk == null || run == null)
        {
            Debug.LogError("Thuy Tinh mage setup could not find one or more Human Spellcasting clips.");
            return;
        }

        GameObject projectilePrefab = EnsureProjectilePrefab();
        if (projectilePrefab == null) return;

        EnsureUpperBodyCombatLayer();
        ApplyAnimationOverrides(idle, walk, run, left, right, twoHand);
        RemoveSword();

        EnsureFolder(AttackFolder);
        ProjectileAttackData attack1 = ConfigureAttack(
            $"{AttackFolder}/Attack_WaterOrb_Left.asset", "Attack1", left, projectilePrefab,
            1.15f, 0.48f, 0.65f, 16f, 20f, 2.5f, false);
        ProjectileAttackData attack2 = ConfigureAttack(
            $"{AttackFolder}/Attack_WaterOrb_Right.asset", "Attack2", right, projectilePrefab,
            1.15f, 0.48f, 0.7f, 17f, 18f, 3.2f, false);
        ProjectileAttackData attack3 = ConfigureAttack(
            $"{AttackFolder}/Attack_WaterOrb_TwoHand.asset", "Attack3", twoHand, projectilePrefab,
            1.25f, 0.53f, 0.9f, 19f, 39f, 5.5f, true);
        attack3.staminaCost = 10f;
        ProjectileAttackData heavy = ConfigureAttack(
            $"{AttackFolder}/Attack_WaterOrb_Heavy.asset", "HeavyAttack", twoHand, projectilePrefab,
            0.95f, 0.58f, 1.35f, 15f, 34f, 8f, true);
        heavy.staminaCost = 25f;
        heavy.superArmor = true;
        heavy.shake = 0.55f;

        MoveSet moveSet = AssetDatabase.LoadAssetAtPath<MoveSet>(MoveSetPath);
        if (moveSet == null)
        {
            Debug.LogError($"Cannot load Thuy Tinh move set at {MoveSetPath}.");
            return;
        }

        moveSet.lightCombo = new AttackData[] { attack1, attack2, attack3 };
        moveSet.heavy = heavy;
        EditorUtility.SetDirty(moveSet);
        AssetDatabase.SaveAssets();
        Debug.Log("Thuy Tinh is now an unarmed water mage with a three-orb mouse combo.");
    }

    static void ApplyAnimationOverrides(AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip left, AnimationClip right, AnimationClip twoHand)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Cannot load Thuy Tinh controller at {ControllerPath}.");
            return;
        }

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        SetOverride(controller, overrides, IdleOriginalGuid, idle);
        SetOverride(controller, overrides, WalkOriginalGuid, walk);
        SetOverride(controller, overrides, RunOriginalGuid, run);
        SetOverride(controller, overrides, Attack1OriginalGuid, left);
        SetOverride(controller, overrides, Attack2OriginalGuid, right);
        SetOverride(controller, overrides, Attack3OriginalGuid, twoHand);
        SetOverride(controller, overrides, HeavyOriginalGuid, twoHand);

        // Getting hit: the same recoil as Sơn Tinh (his Mixamo reaction, retargeted). Her own greatsword impact barely moved her, so his
        // blows looked as if they did not land while hers threw him back.
        AnimationClip reaction = AssetDatabase.LoadAllAssetsAtPath(SonTinhReactionPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Reaction");
        AnimationClip impact = overrides.Select(pair => pair.Key).FirstOrDefault(clip => clip != null && clip.name == "GS_Impact");
        if (reaction != null && impact != null) controller[impact] = reaction;
        else Debug.LogError("Cannot give Thuy Tinh Son Tinh's hit reaction (Reaction clip or GS_Impact slot missing).");
        EditorUtility.SetDirty(controller);
    }

    static void EnsureUpperBodyCombatLayer()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        if (controller == null || mask == null)
        {
            Debug.LogError("Cannot create the upper-body combat layer: controller or avatar mask is missing.");
            return;
        }

        if (controller.layers.Any(layer => layer.name == "UpperBodyCombat")) return;

        AnimatorStateMachine baseMachine = controller.layers[0].stateMachine;
        var layerMachine = new AnimatorStateMachine { name = "UpperBodyCombat" };
        AssetDatabase.AddObjectToAsset(layerMachine, controller);
        AnimatorState empty = layerMachine.AddState("Empty");
        layerMachine.defaultState = empty;

        foreach (string stateName in new[] { "Attack1", "Attack2", "Attack3", "HeavyAttack" })
        {
            AnimatorState source = baseMachine.states.Select(child => child.state)
                .FirstOrDefault(state => state.name == stateName);
            if (source == null) continue;
            AnimatorState upper = layerMachine.AddState($"{stateName}_Upper");
            upper.motion = source.motion;
            upper.speedParameter = source.speedParameter;
            upper.speedParameterActive = source.speedParameterActive;
        }

        controller.AddLayer(new AnimatorControllerLayer
        {
            name = "UpperBodyCombat",
            defaultWeight = 0f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = mask,
            stateMachine = layerMachine
        });
        EditorUtility.SetDirty(controller);
    }

    static void SetOverride(
        AnimatorOverrideController controller,
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides,
        string originalGuid,
        AnimationClip replacement)
    {
        AnimationClip original = overrides.Select(pair => pair.Key).FirstOrDefault(clip =>
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip)) == originalGuid);
        if (original == null)
        {
            Debug.LogError($"Cannot find original controller clip with GUID {originalGuid}.");
            return;
        }

        controller[original] = replacement;
    }

    static ProjectileAttackData ConfigureAttack(
        string path,
        string stateName,
        AnimationClip clip,
        GameObject projectilePrefab,
        float animSpeed,
        float releaseNormalized,
        float radius,
        float speed,
        float damage,
        float knockback,
        bool heavy)
    {
        var attack = AssetDatabase.LoadAssetAtPath<ProjectileAttackData>(path);
        if (attack == null)
        {
            attack = ScriptableObject.CreateInstance<ProjectileAttackData>();
            AssetDatabase.CreateAsset(attack, path);
        }

        float duration = clip.length / animSpeed;
        attack.stateName = stateName;
        attack.animSpeed = animSpeed;
        attack.duration = duration;
        attack.hitStart = 0f;
        attack.hitEnd = 0f;
        attack.comboStart = duration * 0.68f;
        attack.staminaCost = 8f;   // as Sơn Tinh's punch and kick
        attack.damage = damage;
        attack.heavy = heavy;
        attack.superArmor = false;
        attack.knockback = knockback;
        attack.knockUp = heavy ? 1.2f : 0.3f;
        attack.lungeDistance = 0f;
        attack.lungeStart = 0f;
        attack.lungeEnd = 0f;
        attack.hitStop = 0.04f;
        attack.shake = heavy ? 0.35f : 0.16f;
        attack.swingEffect = null;
        attack.swingEffectTime = 0f;
        attack.projectilePrefab = projectilePrefab;
        attack.projectileTime = duration * releaseNormalized;
        attack.projectileSpeed = speed;
        attack.projectileRange = heavy ? 17f : 14f;
        attack.projectileRadius = radius;
        attack.projectileSpawnOffset = new Vector3(0f, 1.25f, 0.9f);
        EditorUtility.SetDirty(attack);
        return attack;
    }

    static GameObject EnsureProjectilePrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
        if (existing != null) return existing;

        Material waterMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/DevTools/M_WaterCyan.mat");
        if (waterMaterial == null)
        {
            Debug.LogError("Cannot create water orb projectile because M_WaterCyan is missing.");
            return null;
        }

        var root = new GameObject("VFX_WaterOrbProjectile");
        root.AddComponent<WaterOrbProjectile>();

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "WaterOrb";
        sphere.transform.SetParent(root.transform, false);
        sphere.transform.localScale = Vector3.one * 1.15f;
        Object.DestroyImmediate(sphere.GetComponent<Collider>());
        sphere.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;

        var trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.32f;
        trail.minVertexDistance = 0.04f;
        trail.widthCurve = AnimationCurve.EaseInOut(0f, 0.7f, 1f, 0f);
        trail.sharedMaterial = waterMaterial;
        trail.startColor = new Color(0.15f, 0.85f, 1f, 0.9f);
        trail.endColor = new Color(0.05f, 0.35f, 1f, 0f);

        var lightObject = new GameObject("WaterGlow");
        lightObject.transform.SetParent(root.transform, false);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.1f, 0.72f, 1f);
        light.intensity = 3.5f;
        light.range = 4f;
        light.shadows = LightShadows.None;

        var particlesObject = new GameObject("Droplets");
        particlesObject.transform.SetParent(root.transform, false);
        var particles = particlesObject.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true;
        main.startLifetime = 0.38f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.1f, 0.65f, 1f, 0.9f),
            new Color(0.65f, 0.95f, 1f, 0.9f));
        main.maxParticles = 64;
        var emission = particles.emission;
        emission.rateOverTime = 28f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.55f;
        particlesObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = waterMaterial;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void RemoveSword()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform sword = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == "Sword");
            if (sword != null) Object.DestroyImmediate(sword.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static AnimationClip LoadClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<AnimationClip>()
        .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
