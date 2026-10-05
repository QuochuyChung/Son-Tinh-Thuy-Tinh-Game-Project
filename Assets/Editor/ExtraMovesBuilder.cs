using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Hooks the slide, the two jumps and the knock-down (death) animations from ArtSource/Son-Tinh-vs-Thuy-Tinh_Animation into the shared
    // animator (docs/progress.md 9.11): imports the eight FBX files as Humanoid clips, adds the states Slide / JumpUp / RunJump / Death
    // to AC_Humanoid_Base (the code in Player/States starts them with CrossFade by name) and fills each character's override controller
    // with its own clips. Safe to run again.
    public static class ExtraMovesBuilder
    {
        const string Base = "Assets/Animations/Shared/AC_Humanoid_Base.controller";
        const string SonOverride = "Assets/Animations/Shared/AOC_SonTinh_v2.overrideController";
        const string ThuyOverride = "Assets/Animations/Shared/AOC_ThuyTinh_v2.overrideController";

        // state name = clip name; where the body goes when the clip moves it ("bake" = the clip's own height change stays visible in the pose)
        sealed class Move
        {
            public string Name, SonFile, ThuyFile;
            public bool BakeHeight;   // jumps: false, the jump height comes from physics (character controller), not from the clip
        }

        static readonly Move[] Moves =
        {
            new() { Name = "Slide", SonFile = "SonTinh_v2/son_tinh_v2_slide.fbx", ThuyFile = "ThuyTinh_v2/thuy_tinh_v2_slide.fbx", BakeHeight = true },
            new() { Name = "JumpUp", SonFile = "SonTinh_v2/son_tinh_v2_jump_up.fbx", ThuyFile = "ThuyTinh_v2/thuy_tinh_v2_jump_up.fbx", BakeHeight = false },
            new() { Name = "RunJump", SonFile = "SonTinh_v2/son_tinh_v2_run_jump.fbx", ThuyFile = "ThuyTinh_v2/thuy_tinh_v2_run_jump.fbx", BakeHeight = false },
            new() { Name = "Death", SonFile = "SonTinh_v2/son_tinh_v2_death.fbx", ThuyFile = "ThuyTinh_v2/thuy_tinh_v2_death.fbx", BakeHeight = true },
        };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Set Up Slide, Jump and Death Animations")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }

            foreach (Move m in Moves)
            {
                ConfigureClip("Assets/Animations/" + m.SonFile, m);
                ConfigureClip("Assets/Animations/" + m.ThuyFile, m);
            }
            AssetDatabase.Refresh();

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Base);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (Move m in Moves)
            {
                AnimationClip clip = LoadClip("Assets/Animations/" + m.SonFile);   // the base clip; every character overrides it below
                ChildAnimatorState existing = machine.states.FirstOrDefault(s => s.state.name == m.Name);
                AnimatorState state = existing.state != null ? existing.state : machine.AddState(m.Name, new Vector3(520f, 40f + 70f * System.Array.IndexOf(Moves, m), 0f));
                state.motion = clip;
                state.writeDefaultValues = true;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Override(SonOverride, true);
            Override(ThuyOverride, false);
            AssetDatabase.SaveAssets();
            Debug.Log("Slide / jump / death animations set up.");
        }

        static void ConfigureClip(string path, Move move)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) { Debug.LogWarning("Missing animation file: " + path); return; }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.SaveAndReimport();   // the default clip list only exists after a first import as Humanoid

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips.Length == 0) { Debug.LogWarning("No animation in " + path); return; }
            ModelImporterClipAnimation clip = clips[0];
            clip.name = move.Name;
            clip.loopTime = false;
            clip.loopPose = false;
            clip.lockRootRotation = true;          // keep facing
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = move.BakeHeight;   // bake the up/down motion of the body into the pose (slide, lying down) or leave it to physics (jumps)
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = false;       // Animator.applyRootMotion is off on the players: forward motion comes from code
            clip.keepOriginalPositionXZ = false;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }

        static AnimationClip LoadClip(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        static void Override(string path, bool son)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (controller == null) { Debug.LogWarning("Missing override controller: " + path); return; }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                foreach (Move m in Moves)
                {
                    AnimationClip original = LoadClip("Assets/Animations/" + m.SonFile);
                    if (overrides[i].Key != original) continue;
                    AnimationClip mine = LoadClip("Assets/Animations/" + (son ? m.SonFile : m.ThuyFile));
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, mine);
                }
            }
            controller.ApplyOverrides(overrides);
            EditorUtility.SetDirty(controller);
        }
    }
}
