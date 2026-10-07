using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Hooks Thuy Tinh's Great Sword animations (ArtSource/Mixamo/ThuyTinh_v2/GreatSword, copied to Assets/Animations/ThuyTinh_v2/thuy_tinh_v2_gs_*)
    // into the shared animator (docs/progress.md 9.12): imports them as Humanoid clips cut to the useful part (frame numbers measured in
    // Blender from the limb speeds), adds the combat states to AC_Humanoid_Base, makes Dodge use its own clip (the roll) and fills the
    // override controllers. Run it after ExtraMovesBuilder. Safe to run again.
    public static class CombatAnimationsBuilder
    {
        const string Dir = "Assets/Animations/ThuyTinh_v2/";
        const string Base = "Assets/Animations/Shared/AC_Humanoid_Base.controller";
        const string SonOverride = "Assets/Animations/Shared/AOC_SonTinh_v2.overrideController";
        const string ThuyOverride = "Assets/Animations/Shared/AOC_ThuyTinh_v2.overrideController";

        // clip name, first frame, last frame (-1 = to the end), loops, body height baked into the pose
        struct ClipCut
        {
            public string Name;
            public int First, Last;
            public bool Loop, BakeHeight;
        }

        sealed class Source
        {
            public string File;
            public ClipCut[] Clips;
        }

        static ClipCut Cut(string name, int first = 0, int last = -1, bool loop = false, bool bakeHeight = true) =>
            new() { Name = name, First = first, Last = last, Loop = loop, BakeHeight = bakeHeight };

        static readonly Source[] Sources =
        {
            new() { File = "thuy_tinh_v2_gs_idle.fbx", Clips = new[] { Cut("GS_Idle", loop: true) } },
            new() { File = "thuy_tinh_v2_gs_walk.fbx", Clips = new[] { Cut("GS_Walk", loop: true) } },
            new() { File = "thuy_tinh_v2_gs_run.fbx", Clips = new[] { Cut("GS_Run", loop: true) } },
            new() { File = "thuy_tinh_v2_gs_jump.fbx", Clips = new[] { Cut("GS_Jump", bakeHeight: false) } },
            new() { File = "thuy_tinh_v2_gs_run_jump.fbx", Clips = new[] { Cut("GS_RunJump", bakeHeight: false) } },
            new() { File = "thuy_tinh_v2_gs_roll.fbx", Clips = new[] { Cut("GS_Roll", 20, 52) } },
            new() { File = "thuy_tinh_v2_gs_slash.fbx", Clips = new[] { Cut("GS_Slash", 0, 44) } },
            new() { File = "thuy_tinh_v2_gs_kick.fbx", Clips = new[] { Cut("GS_Kick", 0, 40) } },
            new() { File = "thuy_tinh_v2_gs_spin.fbx", Clips = new[] { Cut("GS_Spin", 0, 48) } },
            new() { File = "thuy_tinh_v2_gs_slide_attack.fbx", Clips = new[] { Cut("GS_SlideAttack", 0, 52) } },
            new() { File = "thuy_tinh_v2_gs_casting.fbx", Clips = new[] { Cut("GS_CastHeavy", 46, 96), Cut("GS_CastWind", 52, 84) } },
            new() { File = "thuy_tinh_v2_gs_power_up.fbx", Clips = new[] { Cut("GS_PowerUp", 6, 46) } },
            new() { File = "thuy_tinh_v2_gs_jump_attack.fbx", Clips = new[] { Cut("GS_JumpAttack", 0, 48) } },
            new() { File = "thuy_tinh_v2_gs_impact.fbx", Clips = new[] { Cut("GS_Impact", 0, 32) } },
        };

        // animator state, clip it plays (the base controller points at Thuy Tinh's clips for the combat states)
        static readonly (string state, string clip)[] CombatStates =
        {
            ("Attack1", "GS_Slash"), ("Attack2", "GS_Kick"), ("Attack3", "GS_Spin"), ("HeavyAttack", "GS_CastHeavy"), ("JumpAttack", "GS_JumpAttack"),
            ("SpellWind", "GS_CastWind"), ("SpellRain", "GS_PowerUp"), ("SpellWave", "GS_SlideAttack"), ("Hit", "GS_Impact"),
        };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Set Up Great Sword Combat Animations")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }

            foreach (Source s in Sources) Configure(Dir + s.File, s);
            AssetDatabase.Refresh();

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Base);
            if (!controller.parameters.Any(p => p.name == "AnimSpeed"))
                controller.AddParameter(new AnimatorControllerParameter { name = "AnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            int row = 0;
            foreach (var (stateName, clipName) in CombatStates)
            {
                AnimatorState state = State(machine, stateName, new Vector3(760f, 40f + 60f * row++, 0f));
                state.motion = Clip(clipName);
                state.writeDefaultValues = true;
                state.speedParameterActive = true;
                state.speedParameter = "AnimSpeed";
            }

            // Dodge now plays the roll (before it borrowed the Run clip, so the override of Run changed both); Son Tinh keeps the old look
            AnimatorState dodge = State(machine, "Dodge", new Vector3(300f, 120f, 0f));
            dodge.motion = Clip("GS_Roll");
            dodge.speedParameterActive = true;
            dodge.speedParameter = "AnimSpeed";
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Override(ThuyOverride, thuy: true);
            Override(SonOverride, thuy: false);
            AssetDatabase.SaveAssets();
            Debug.Log("Great Sword combat animations set up.");
        }

        static AnimatorState State(AnimatorStateMachine machine, string name, Vector3 position)
        {
            ChildAnimatorState existing = machine.states.FirstOrDefault(s => s.state.name == name);
            return existing.state != null ? existing.state : machine.AddState(name, position);
        }

        static void Configure(string path, Source source)
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

            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults.Length == 0) { Debug.LogWarning("No animation in " + path); return; }
            float start = defaults[0].firstFrame, end = defaults[0].lastFrame;

            var clips = new List<ModelImporterClipAnimation>();
            foreach (ClipCut cut in source.Clips)
            {
                ModelImporterClipAnimation clip = importer.defaultClipAnimations[0];   // a fresh object each time (it is a class, not a struct)
                clip.name = cut.Name;
                clip.firstFrame = start + cut.First;
                clip.lastFrame = cut.Last < 0 ? end : Mathf.Min(end, start + cut.Last);
                clip.loopTime = cut.Loop;
                clip.loopPose = false;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = cut.BakeHeight;
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = false;   // Animator.applyRootMotion is off on the players: travel comes from code
                clip.keepOriginalPositionXZ = false;
                clips.Add(clip);
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        static AnimationClip Clip(string name)
        {
            foreach (Source s in Sources)
                if (s.Clips.Any(c => c.Name == name))
                    return AssetDatabase.LoadAllAssetsAtPath(Dir + s.File).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
            return null;
        }

        static AnimationClip Existing(string path, string clip) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == clip);

        static void Override(string path, bool thuy)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (controller == null) { Debug.LogWarning("Missing override controller: " + path); return; }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip original = overrides[i].Key, mine = overrides[i].Value;
                string name = original.name;
                if (thuy)
                {
                    // armed locomotion and jumps; slide, sprint and death stay the unarmed clips
                    if (name == "Idle") mine = Clip("GS_Idle");
                    else if (name == "Walk") mine = Clip("GS_Walk");
                    else if (name == "Run") mine = Clip("GS_Run");
                    else if (name == "JumpUp") mine = Clip("GS_Jump");
                    else if (name == "RunJump") mine = Clip("GS_RunJump");
                    else if (name == "GS_Roll") mine = Clip("GS_Roll");
                    else if (CombatStates.Any(c => c.clip == name)) mine = original;
                }
                else if (name == "GS_Roll")
                {
                    // his own roll (docs 9.14) once SonTinhCombatBuilder has imported it, else the old dodge (the run clip)
                    mine = Existing("Assets/Animations/SonTinh_v2/son_tinh_v2_roll.fbx", "Roll");
                    if (mine == null) mine = Existing("Assets/Animations/SonTinh_v2/son_tinh_v2_run.fbx", "Run");
                    if (mine == null) mine = AssetDatabase.LoadAllAssetsAtPath("Assets/Animations/SonTinh_v2/son_tinh_v2_run.fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                }
                else if (CombatStates.Any(c => c.clip == name) && mine != null && mine != original && mine.name != name) { /* already his own clip: keep */ }
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, mine);
            }
            controller.ApplyOverrides(overrides);
            EditorUtility.SetDirty(controller);
        }
    }
}
