using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Re-runnable setup for Human Spellcasting Animations FREE.
// Kept as a menu command so the mapping can be restored without silently changing assets on reload.
internal static class ThuyTinhSpellAnimationSetup
{
    const string ControllerPath = "Assets/Animations/Shared/AOC_ThuyTinh_v2.overrideController";

    static readonly SpellBinding[] Bindings =
    {
        new(
            "Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_gs_slide_attack.fbx",
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Directional/HumanM@MagicAttackDirect2H01.fbx",
            "Assets/Data/Combat/Spell_Wave.asset",
            1.15f,
            0.16f),
        new(
            "Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_gs_casting.fbx",
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Call/HumanM@MagicAttackCall1H01_L.fbx",
            "Assets/Data/Combat/Spell_Wind.asset",
            1.3f,
            0.48f),
        new(
            "Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_gs_power_up.fbx",
            "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Omnidirectional/HumanM@MagicAttackOmni01.fbx",
            "Assets/Data/Combat/Spell_Rain.asset",
            1f,
            0.16f),
    };

    [MenuItem("Tools/Son Tinh Thuy Tinh/Apply Thuy Tinh Spell Animations")]
    static void Apply()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Cannot load Thuy Tinh override controller at {ControllerPath}.");
            return;
        }

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);

        foreach (SpellBinding binding in Bindings)
        {
            AnimationClip original = overrides
                .Select(pair => pair.Key)
                .FirstOrDefault(clip => AssetDatabase.GetAssetPath(clip) == binding.OriginalClipPath);
            AnimationClip replacement = LoadClip(binding.ReplacementClipPath);

            if (original == null || replacement == null)
            {
                Debug.LogError($"Cannot bind Thuy Tinh spell animation: {binding.OriginalClipPath} -> {binding.ReplacementClipPath}.");
                continue;
            }

            controller[original] = replacement;
            SyncSpellTiming(binding, replacement.length);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Applied Human Spellcasting animations to Thuy Tinh: Wave=Direct2H, Wind=Omni, Rain=Call1H.");
    }

    static AnimationClip LoadClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
        .OfType<AnimationClip>()
        .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));

    static void SyncSpellTiming(SpellBinding binding, float clipLength)
    {
        UnityEngine.Object spell = AssetDatabase.LoadMainAssetAtPath(binding.SpellDataPath);
        if (spell == null) return;

        var serialized = new SerializedObject(spell);
        float duration = clipLength / binding.AnimationSpeed;
        serialized.FindProperty("animSpeed").floatValue = binding.AnimationSpeed;
        serialized.FindProperty("duration").floatValue = duration;
        serialized.FindProperty("comboStart").floatValue = duration * 0.78f;
        serialized.FindProperty("effectTime").floatValue = duration * binding.EffectNormalizedTime;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spell);
    }

    readonly struct SpellBinding
    {
        public readonly string OriginalClipPath;
        public readonly string ReplacementClipPath;
        public readonly string SpellDataPath;
        public readonly float AnimationSpeed;
        public readonly float EffectNormalizedTime;

        public SpellBinding(
            string originalClipPath,
            string replacementClipPath,
            string spellDataPath,
            float animationSpeed,
            float effectNormalizedTime)
        {
            OriginalClipPath = originalClipPath;
            ReplacementClipPath = replacementClipPath;
            SpellDataPath = spellDataPath;
            AnimationSpeed = animationSpeed;
            EffectNormalizedTime = effectNormalizedTime;
        }
    }
}
