using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    /// <summary>Imports and wires the Blender-authored forward water-casting clips.</summary>
    public sealed class ThuyTinhProjectileAttackInstaller : AssetPostprocessor
    {
        const string ControllerPath = "Assets/Animations/Shared/AOC_ThuyTinh_v2.overrideController";
        const string BaseControllerPath = "Assets/Animations/Shared/AC_Humanoid_Base.controller";
        const string Folder = "Assets/Animations/ThuyTinh_AzureSovereign/ProjectileAttacks/";
        const string LeftPath = Folder + "thuy_tinh_projectile_left.fbx";
        const string RightPath = Folder + "thuy_tinh_projectile_right.fbx";
        const string TwoHandPath = Folder + "thuy_tinh_projectile_two_hand.fbx";
        const string HeavyPath = Folder + "thuy_tinh_projectile_heavy.fbx";

        static readonly string[] CustomPaths = { LeftPath, RightPath, TwoHandPath, HeavyPath };
        static bool installing;

        [InitializeOnLoadMethod]
        static void ScheduleInstall() => EditorApplication.delayCall += Install;

        [MenuItem("Tools/Son Tinh Thuy Tinh/Thuy Tinh: Install Forward Projectile Attacks")]
        public static void Install()
        {
            if (installing) return;
            installing = true;
            try
            {
                bool requestedReimport = false;
                foreach (string path in CustomPaths)
                {
                    if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
                    bool changed = importer.animationType != ModelImporterAnimationType.Human ||
                                   importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                                   !importer.importAnimation;
                    if (!changed) continue;
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.sourceAvatar = null;
                    importer.importAnimation = true;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.animationCompression = ModelImporterAnimationCompression.Off;
                    importer.SaveAndReimport();
                    requestedReimport = true;
                }

                if (requestedReimport)
                {
                    EditorApplication.delayCall += Install;
                    return;
                }

                AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerPath);
                EnableCombatLookIk();
                AnimationClip left = LoadClip(LeftPath);
                AnimationClip right = LoadClip(RightPath);
                AnimationClip twoHand = LoadClip(TwoHandPath);
                AnimationClip heavy = LoadClip(HeavyPath);
                if (controller == null || left == null || right == null || twoHand == null || heavy == null)
                    throw new System.InvalidOperationException("Projectile controller or one of the four custom clips could not be loaded.");

                foreach (string path in CustomPaths)
                {
                    Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                    if (avatar == null || !avatar.isValid || !avatar.isHuman)
                        throw new System.InvalidOperationException($"Custom projectile Avatar is not a valid Humanoid: {path}");
                }

                var replacements = new Dictionary<string, AnimationClip>
                {
                    ["GS_Slash"] = left,
                    ["GS_Kick"] = right,
                    ["GS_Spin"] = twoHand,
                    ["GS_CastHeavy"] = heavy,
                    ["GS_CastWind"] = heavy,
                    ["GS_SlideAttack"] = twoHand,
                };

                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(overrides);
                bool controllerChanged = false;
                for (int index = 0; index < overrides.Count; index++)
                {
                    AnimationClip original = overrides[index].Key;
                    if (original == null || !replacements.TryGetValue(original.name, out AnimationClip replacement)) continue;
                    if (overrides[index].Value == replacement) continue;
                    overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
                    controllerChanged = true;
                }

                if (!controllerChanged)
                {
                    ValidateOverrides(controller, replacements);
                    Debug.Log("Thuy Tinh forward projectile attacks are already installed and validated.");
                    return;
                }
                controller.ApplyOverrides(overrides);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                ValidateOverrides(controller, replacements);
                Debug.Log("Thuy Tinh forward projectile attacks installed and validated: left, right, two-hand and heavy.");
            }
            finally
            {
                installing = false;
            }
        }

        static AnimationClip LoadClip(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));

        static void EnableCombatLookIk()
        {
            AnimatorController baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
            if (baseController == null)
                throw new System.InvalidOperationException("Shared humanoid Animator Controller could not be loaded.");

            bool changed = false;
            AnimatorControllerLayer[] layers = baseController.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                AnimatorControllerLayer layer = layers[i];
                if (layer.iKPass) continue;
                layer.iKPass = true;
                layers[i] = layer;
                changed = true;
            }
            if (changed)
            {
                baseController.layers = layers;
                EditorUtility.SetDirty(baseController);
                AssetDatabase.SaveAssets();
                Debug.Log("Combat look IK enabled on the shared humanoid Animator layers.");
            }
        }

        static void ValidateOverrides(
            AnimatorOverrideController controller,
            IReadOnlyDictionary<string, AnimationClip> expected)
        {
            var applied = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(applied);
            foreach (var pair in expected)
            {
                var match = applied.FirstOrDefault(item => item.Key != null && item.Key.name == pair.Key);
                if (match.Value != pair.Value)
                    throw new System.InvalidOperationException($"Projectile override validation failed for {pair.Key}.");
            }
        }

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Any(path => CustomPaths.Contains(path)))
                EditorApplication.delayCall += Install;
        }
    }
}
