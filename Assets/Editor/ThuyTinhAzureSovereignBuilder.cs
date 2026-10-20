using System.Linq;
using System.Collections.Generic;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Player;
using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Imports the Blender-rigged Azure Sovereign, builds its materials/visual prefab, then swaps only
    // the visual child of Player_ThuyTinh_v2. Gameplay components, collision, movement tuning and the
    // existing Animator Override Controller remain unchanged.
    public static class ThuyTinhAzureSovereignBuilder
    {
        const string Root = "Assets/Art/Characters/ThuyTinh_AzureSovereign";
        const string ModelPath = Root + "/thuy_tinh_azure_sovereign.fbx";
        const string VisualPrefabPath = "Assets/Prefabs/Characters/ThuyTinh_AzureSovereign.prefab";
        const string PlayerPrefabPath = "Assets/Prefabs/Player/Player_ThuyTinh_v2.prefab";
        const string ControllerPath = "Assets/Animations/Shared/AOC_ThuyTinh_v2.overrideController";
        const string StableIdlePath = "Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_idle_3.fbx";
        const string BaseIdlePath = "Assets/Animations/SonTinh/son_tinh_idle.fbx";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Thuy Tinh: Install Azure Sovereign")]
        public static void Install()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureModel();
            ConfigureTextures();
            ConfigureStableIdle();

            Material body = BuildMaterial(
                Root + "/Materials/M_ThuyTinh_Azure_Body.mat",
                Root + "/Textures/thuy_tinh_azure_body_basecolor.png",
                Root + "/Textures/thuy_tinh_azure_body_normal.png",
                Root + "/Textures/thuy_tinh_azure_body_metallic_smoothness.png");
            Material mantle = BuildMaterial(
                Root + "/Materials/M_ThuyTinh_Azure_Mantle.mat",
                Root + "/Textures/thuy_tinh_azure_mantle_basecolor.png",
                Root + "/Textures/thuy_tinh_azure_mantle_normal.png",
                Root + "/Textures/thuy_tinh_azure_mantle_metallic_smoothness.png");

            GameObject visual = BuildVisualPrefab(body, mantle);
            ReplacePlayerVisual(visual);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            ValidateAttackAnimation();
            Debug.Log($"ThuyTinhAzureSovereignBuilder: installed. Avatar valid={avatar != null && avatar.isValid}, human={avatar != null && avatar.isHuman}.");
        }

        static void ConfigureStableIdle()
        {
            AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerPath);
            AnimationClip stableIdle = AssetDatabase.LoadAllAssetsAtPath(StableIdlePath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
            if (controller == null || stableIdle == null)
                throw new System.InvalidOperationException("Unable to load Thuy Tinh override controller or stable idle clip.");

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            int idleIndex = overrides.FindIndex(pair => AssetDatabase.GetAssetPath(pair.Key) == BaseIdlePath);
            if (idleIndex < 0)
                throw new System.InvalidOperationException("Base Idle override entry was not found.");

            AnimationClip original = overrides[idleIndex].Key;
            controller[original] = stableIdle;
            EditorUtility.SetDirty(controller);
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/Thuy Tinh: Validate Azure Sovereign Attack")]
        public static void ValidateAttackAnimation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException($"Missing player prefab: {PlayerPrefabPath}");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new System.InvalidOperationException("Azure Sovereign is missing a valid Humanoid Animator/Avatar.");

                Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (leftHand == null || rightHand == null)
                    throw new System.InvalidOperationException("Humanoid hand bones could not be resolved.");

                int layer = animator.GetLayerIndex("UpperBodyCombat");
                if (layer < 0)
                    throw new System.InvalidOperationException("Animator layer UpperBodyCombat was not found.");

                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
                animator.SetLayerWeight(layer, 1f);
                animator.Play("Attack1_Upper", layer, 0f);
                animator.Update(0.02f);
                Vector3 leftStart = leftHand.position;
                Vector3 rightStart = rightHand.position;
                animator.Update(0.25f);
                float leftDelta = Vector3.Distance(leftStart, leftHand.position);
                float rightDelta = Vector3.Distance(rightStart, rightHand.position);

                int rendererCount = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                bool hasSpringBones = instance.GetComponentInChildren<OutfitSpringBones>(true) != null;
                AnimatorOverrideController overridesController = animator.runtimeAnimatorController as AnimatorOverrideController;
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overridesController?.GetOverrides(overrides);
                AnimationClip idleOverride = overrides
                    .FirstOrDefault(pair => AssetDatabase.GetAssetPath(pair.Key) == BaseIdlePath).Value;
                bool usesStableIdle = AssetDatabase.GetAssetPath(idleOverride) == StableIdlePath;
                if (leftDelta < 0.05f || rightDelta < 0.05f)
                    throw new System.InvalidOperationException($"Attack hand movement is too small: left={leftDelta:F3}, right={rightDelta:F3}.");
                if (!usesStableIdle)
                    throw new System.InvalidOperationException("Idle still uses the unstable casting animation.");

                Debug.Log(
                    $"Azure Sovereign validation passed: avatarValid={animator.avatar.isValid}, human={animator.avatar.isHuman}, " +
                    $"renderers={rendererCount}, springBones={hasSpringBones}, stableIdle={usesStableIdle}, " +
                    $"leftHandDelta={leftDelta:F3}, rightHandDelta={rightDelta:F3}.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static void ConfigureModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new System.InvalidOperationException("Missing model: " + ModelPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importBlendShapes = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.optimizeGameObjects = false; // Cape_ bones must stay available to OutfitSpringBones.
            importer.SaveAndReimport();
        }

        static void ConfigureTextures()
        {
            string[] normals =
            {
                Root + "/Textures/thuy_tinh_azure_body_normal.png",
                Root + "/Textures/thuy_tinh_azure_mantle_normal.png"
            };
            foreach (string path in normals)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            string[] masks =
            {
                Root + "/Textures/thuy_tinh_azure_body_metallic_smoothness.png",
                Root + "/Textures/thuy_tinh_azure_mantle_metallic_smoothness.png"
            };
            foreach (string path in masks)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        static Material BuildMaterial(string output, string baseColor, string normal, string metallicSmoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(output);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, output);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseColor));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metallicSmoothness));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject BuildVisualPrefab(Material bodyMaterial, Material mantleMaterial)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new System.InvalidOperationException("Model did not import: " + ModelPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "ThuyTinh_AzureSovereign";

            foreach (SkinnedMeshRenderer renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string meshName = renderer.sharedMesh != null ? renderer.sharedMesh.name : renderer.name;
                renderer.sharedMaterial = meshName.Contains("Mantle") ? mantleMaterial : bodyMaterial;
                renderer.updateWhenOffscreen = true;
            }

            Animator animator = instance.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            animator.applyRootMotion = false;

            var springs = instance.AddComponent<OutfitSpringBones>();
            var serialized = new SerializedObject(springs);
            SerializedProperty groups = serialized.FindProperty("groups");
            groups.arraySize = 1;
            SerializedProperty group = groups.GetArrayElementAtIndex(0);
            group.FindPropertyRelative("name").stringValue = "Azure Plume Mantle";
            group.FindPropertyRelative("bonePrefix").stringValue = "Cape_";
            group.FindPropertyRelative("frequency").floatValue = 5.5f;
            group.FindPropertyRelative("dampingRatio").floatValue = 0.72f;
            group.FindPropertyRelative("drag").floatValue = 5f;
            group.FindPropertyRelative("gravity").floatValue = 0.65f;
            group.FindPropertyRelative("maxAngle").floatValue = 9f;
            group.FindPropertyRelative("inwardLimit").floatValue = 0.06f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, VisualPrefabPath);
            Object.DestroyImmediate(instance);
            return saved;
        }

        static void ReplacePlayerVisual(GameObject visualPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var player = root.GetComponent<PlayerController>();
                if (player == null) throw new System.InvalidOperationException("PlayerController missing from " + PlayerPrefabPath);
                var playerSerialized = new SerializedObject(player);
                Animator oldAnimator = playerSerialized.FindProperty("animator").objectReferenceValue as Animator;
                Transform oldVisual = oldAnimator != null ? oldAnimator.transform : root.transform.Find("Model");
                while (oldVisual != null && oldVisual.parent != null && oldVisual.parent != root.transform)
                    oldVisual = oldVisual.parent;
                Vector3 localPosition = oldVisual != null ? oldVisual.localPosition : Vector3.zero;
                Quaternion localRotation = oldVisual != null ? oldVisual.localRotation : Quaternion.identity;
                Vector3 localScale = oldVisual != null ? oldVisual.localScale : Vector3.one * 2.03f;
                if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);

                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, root.transform);
                visual.name = "Model";
                visual.transform.localPosition = localPosition;
                visual.transform.localRotation = localRotation;
                visual.transform.localScale = localScale;
                Animator animator = visual.GetComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                animator.applyRootMotion = false;
                playerSerialized.FindProperty("animator").objectReferenceValue = animator;
                playerSerialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
