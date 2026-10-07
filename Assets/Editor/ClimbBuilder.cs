using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Hooks the climb animation of both characters (ArtSource/Mixamo/<Character>_v2/*_v2_climb.fbx, copied to Assets/Animations/<Character>_v2) into
    // the shared animator (docs/progress.md 9.13, 9.14): imports each as a Humanoid clip cut to the part after the run-up (frame numbers measured in
    // Blender; it is the same Mixamo clip, so the same numbers fit both), adds the state `Climb` to AC_Humanoid_Base (Thuy Tinh's clip is the base
    // one), fills the override controllers and switches the climb on in both player prefabs. The body's rise and forward travel are left out of the
    // pose (nothing baked in) because PlayerClimbState moves the character along the wall from code. Safe to run again.
    public static class ClimbBuilder
    {
        public const string ClipFile = "Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_climb.fbx";
        public const string ClipName = "Climb";
        const string Base = "Assets/Animations/Shared/AC_Humanoid_Base.controller";
        const string StateName = "Climb";

        // clip file, override controller and player prefab of each character
        static readonly (string clipFile, string overrideController, string prefab)[] Rigs =
        {
            (ClipFile, "Assets/Animations/Shared/AOC_ThuyTinh_v2.overrideController", "Assets/Prefabs/Player/Player_ThuyTinh_v2.prefab"),
            ("Assets/Animations/SonTinh_v2/son_tinh_v2_climb.fbx", "Assets/Animations/Shared/AOC_SonTinh_v2.overrideController", "Assets/Prefabs/Player/Player_SonTinh_v2.prefab"),
        };

        // Blender frames 1..116 = Unity frames 0..115. The run-up (first ~26: the character leaves the ground next to the wall at the cut) and the settled crouch at the end are cut off.
        public const int FirstFrame = 26;
        public const int LastFrame = 104;

        [MenuItem("Tools/Son Tinh Thuy Tinh/Set Up Climb")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }

            var clips = new List<AnimationClip>();
            foreach (var rig in Rigs)
            {
                AnimationClip clip = Import(rig.clipFile);
                if (clip == null) { Debug.LogWarning("Skipping " + rig.clipFile); clips.Add(null); continue; }
                clips.Add(clip);
            }
            AnimationClip baseClip = clips[0];
            if (baseClip == null) { Debug.LogError("Thuy Tinh's climb clip is the base of the state and did not import."); return; }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Base);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            ChildAnimatorState existing = machine.states.FirstOrDefault(s => s.state.name == StateName);
            AnimatorState state = existing.state != null ? existing.state : machine.AddState(StateName, new Vector3(520f, 340f, 0f));
            state.motion = baseClip;
            state.writeDefaultValues = true;
            state.speedParameterActive = true;
            state.speedParameter = "AnimSpeed";
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            for (int r = 0; r < Rigs.Length; r++)
            {
                if (clips[r] == null) continue;
                var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Rigs[r].overrideController);
                if (aoc != null)
                {
                    var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                    aoc.GetOverrides(overrides);
                    for (int i = 0; i < overrides.Count; i++)
                        if (overrides[i].Key == baseClip) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(baseClip, clips[r]);
                    aoc.ApplyOverrides(overrides);
                    EditorUtility.SetDirty(aoc);
                }
                Enable(Rigs[r].prefab);
            }
            AssetDatabase.SaveAssets();

            BuildSandboxBlocks();
            Debug.Log($"Climb set up for {clips.Count(c => c != null)} character(s); {baseClip.length:F2} s; test blocks in Sandbox_Combat.");
        }

        static AnimationClip Import(string file)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(file);
            if (importer == null) { Debug.LogError("Missing animation file: " + file); return null; }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.SaveAndReimport();   // the default clip list only exists after a first import as Humanoid

            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults.Length == 0) { Debug.LogError("No animation in " + file); return null; }
            ModelImporterClipAnimation clip = defaults[0];
            float start = clip.firstFrame, end = clip.lastFrame;
            clip.name = ClipName;
            clip.firstFrame = start + FirstFrame;
            clip.lastFrame = Mathf.Min(end, start + LastFrame);
            clip.loopTime = false;
            clip.loopPose = false;
            clip.lockRootRotation = true;          // keep facing
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = false;          // the rise comes from code, along the measured curve in ClimbSettings
            clip.keepOriginalPositionY = false;     // root height = height of the feet (the transform stands on what the feet stand on), not the original body height
            clip.heightFromFeet = true;
            clip.lockRootPositionXZ = false;       // Animator.applyRootMotion is off on the players: travel comes from code
            clip.keepOriginalPositionXZ = false;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>().FirstOrDefault(c => c.name == ClipName);
        }

        // Switches the climb on in a player prefab (it is off by default, so a character without the clip just jumps).
        static void Enable(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var controller = root.GetComponent<SonTinhThuyTinh.Player.PlayerController>();
                var so = new SerializedObject(controller);
                so.FindProperty("climb.enabled").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // A few plain blocks next to the training dummies to try the climb on: 0.8 m, 1.15 m (what the clip was made for), 1.6 m.
        static void BuildSandboxBlocks()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Sandbox_Combat.unity");
            var old = GameObject.Find("ClimbBlocks");
            if (old != null) Object.DestroyImmediate(old);

            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Combat/Mat_ClimbBlock.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Mat_ClimbBlock" };
                material.SetColor("_BaseColor", new Color(0.55f, 0.5f, 0.45f));
                AssetDatabase.CreateAsset(material, "Assets/Art/Combat/Mat_ClimbBlock.mat");
            }

            var parent = new GameObject("ClimbBlocks");
            float[] heights = { 0.8f, 1.15f, 1.6f };
            for (int i = 0; i < heights.Length; i++)
            {
                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"ClimbBlock_{heights[i]:0.00}m";
                block.transform.SetParent(parent.transform, false);
                block.transform.localScale = new Vector3(3f, heights[i], 3f);
                block.transform.position = new Vector3(-9f, heights[i] * 0.5f, 3f + 5f * i);
                block.GetComponent<Renderer>().sharedMaterial = material;
                block.isStatic = true;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
    }
}
