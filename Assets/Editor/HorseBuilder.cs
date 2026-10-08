using System.IO;
using System.Linq;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Quest;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonTinhThuyTinh.EditorTools
{
    // Ngựa Chín Hồng Mao: Assets/Art/Characters/NguaChinHongMao/ngua_chin_hong_mao.fbx (armature + NguaChinHongMao_Body + the actions
    // EatGrass / Idle / LookUp, made by tools/rig_ngua.py). Builds: Generic import (a horse is not Humanoid) with the three clips (EatGrass
    // and Idle loop), one URP/Lit material (Render Face: Both, the mane and tail are single-sided strands), AC_NguaChinHongMao
    // (EatGrass by default; trigger LookUp -> LookUp -> Idle; trigger Eat -> back to EatGrass) and Assets/Prefabs/Characters/NguaChinHongMao.prefab
    // (root on the ground, Model child scaled to Height at the ear tips, a box collider on the body, OutfitSpringBones on the Tail_ and
    // Mane_ chains, HorseStall). Safe to run again.
    public static class HorseBuilder
    {
        const string ArtDir = "Assets/Art/Characters/NguaChinHongMao";
        const string ModelPath = ArtDir + "/ngua_chin_hong_mao.fbx";
        const string MaterialPath = ArtDir + "/Materials/M_NguaChinHongMao.mat";
        const string AnimDir = "Assets/Animations/NguaChinHongMao";
        const string ControllerPath = AnimDir + "/AC_NguaChinHongMao.controller";
        public const string PrefabPath = "Assets/Prefabs/Characters/NguaChinHongMao.prefab";
        const string MaterialTemplate = "Assets/Art/Characters/ThuyTinh_v2/Materials/M_ThuyTinh_v2_Body.mat";
        const float Height = 2.1f;   // ground to the ear tips (withers ~1.45 m)
        static readonly (string Name, bool Loop)[] Clips = { ("EatGrass", true), ("Idle", true), ("LookUp", false) };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Ngua Chin Hong Mao")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            foreach (var (path, type, srgb) in new[] {
                         (ArtDir + "/Textures/ngua_basecolor.png", TextureImporterType.Default, true),
                         (ArtDir + "/Textures/ngua_normal.png", TextureImporterType.NormalMap, false),
                         (ArtDir + "/Textures/ngua_metallic_smoothness.png", TextureImporterType.Default, false) })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (ti == null) { Debug.LogError("Missing texture: " + path); continue; }
                ti.textureType = type; ti.sRGBTexture = srgb; ti.maxTextureSize = 2048; ti.SaveAndReimport();
            }
            ConfigureModel();
            var material = BuildMaterial();
            var controller = BuildController();
            BuildPrefab(material, controller);
            AssetDatabase.SaveAssets();
            Debug.Log("HorseBuilder: done.");
        }

        static void ConfigureModel()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false; mi.importLights = false;
            mi.SaveAndReimport();
            var clips = mi.defaultClipAnimations.ToList();
            var result = new System.Collections.Generic.List<ModelImporterClipAnimation>();
            foreach (var (name, loop) in Clips)
            {
                var c = clips.FirstOrDefault(x => x.takeName.EndsWith(name) || x.name.EndsWith(name));
                if (c == null) { Debug.LogError($"HorseBuilder: no take for {name} in " + string.Join(", ", clips.Select(x => x.takeName))); continue; }
                c.name = name; c.loopTime = loop; c.loopPose = false;
                c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
                result.Add(c);
            }
            mi.clipAnimations = result.ToArray();
            mi.SaveAndReimport();
        }

        static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);

        static Material BuildMaterial()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                var template = AssetDatabase.LoadAssetAtPath<Material>(MaterialTemplate);
                m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Textures/ngua_basecolor.png"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Textures/ngua_normal.png"));
            m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Textures/ngua_metallic_smoothness.png"));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_WorkflowMode", 1f); m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off); m.doubleSidedGI = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static AnimatorController BuildController()
        {
            Directory.CreateDirectory(AnimDir);
            // rebuilt in place so the controller keeps its GUID
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var p in ac.parameters) ac.RemoveParameter(p);
            var sm = ac.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (var st in sm.states) sm.RemoveState(st.state);
            ac.AddParameter("LookUp", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Eat", AnimatorControllerParameterType.Trigger);
            var eat = sm.AddState("EatGrass", new Vector3(300f, 0f, 0f)); eat.motion = Clip("EatGrass");
            var look = sm.AddState("LookUp", new Vector3(300f, 80f, 0f)); look.motion = Clip("LookUp");
            var idle = sm.AddState("Idle", new Vector3(300f, 160f, 0f)); idle.motion = Clip("Idle");
            sm.defaultState = eat;
            AnimatorStateTransition T(AnimatorState from, AnimatorState to, string trigger, float duration)
            {
                var t = from.AddTransition(to);
                if (trigger != null) t.AddCondition(AnimatorConditionMode.If, 0, trigger);
                t.hasExitTime = trigger == null; t.exitTime = 0.95f; t.duration = duration; t.hasFixedDuration = true;
                return t;
            }
            T(eat, look, "LookUp", 0.3f);
            T(idle, look, "LookUp", 0.3f);
            T(look, idle, null, 0.4f);           // after looking up it stands and watches
            T(idle, eat, "Eat", 0.8f);
            T(look, eat, "Eat", 0.8f);
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static void BuildPrefab(Material material, AnimatorController controller)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = new GameObject("NguaChinHongMao");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            smr.sharedMaterial = material; smr.updateWhenOffscreen = true;

            // scale to Height and stand the hooves on the root's ground plane (bind pose: the stored mesh sits under its renderer)
            var toWorld = smr.localToWorldMatrix;
            var verts = smr.sharedMesh.vertices.Select(v => toWorld.MultiplyPoint3x4(v)).ToArray();
            float minY = verts.Min(v => v.y), maxY = verts.Max(v => v.y);
            float scale = Height / (maxY - minY);
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = new Vector3(0f, -minY * scale, 0f);
            Debug.Log($"HorseBuilder: unscaled height {maxY - minY:F4}, scale {scale:F3}");

            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            // a solid box on the body (not the mane / tail strands), so the player walks around the horse
            toWorld = smr.localToWorldMatrix;
            verts = smr.sharedMesh.vertices.Select(v => toWorld.MultiplyPoint3x4(v)).ToArray();
            var body = verts.Where(v => v.y > 0.55f && v.y < 1.6f).ToArray();
            var b = new Bounds(body[0], Vector3.zero); foreach (var v in body) b.Encapsulate(v);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(b.center.x, 0.85f, b.center.z);
            box.size = new Vector3(Mathf.Min(b.size.x, 0.7f), 1.5f, b.size.z * 0.8f);

            var springs = model.AddComponent<OutfitSpringBones>();
            var so = new SerializedObject(springs);
            var groups = so.FindProperty("groups");
            groups.arraySize = 2;
            void Group(int i, string name, string prefix, float frequency, float maxAngle)
            {
                var g = groups.GetArrayElementAtIndex(i);
                g.FindPropertyRelative("name").stringValue = name;
                g.FindPropertyRelative("bonePrefix").stringValue = prefix;
                g.FindPropertyRelative("frequency").floatValue = frequency;
                g.FindPropertyRelative("dampingRatio").floatValue = 0.6f;
                g.FindPropertyRelative("drag").floatValue = 3f;
                g.FindPropertyRelative("gravity").floatValue = 0.5f;
                g.FindPropertyRelative("maxAngle").floatValue = maxAngle;
                g.FindPropertyRelative("inwardLimit").floatValue = 0.1f;
            }
            Group(0, "Tail", "Tail_", 3.5f, 10f);
            Group(1, "Mane", "Mane_", 4.5f, 8f);
            so.ApplyModifiedPropertiesWithoutUndo();

            var stall = root.AddComponent<HorseStall>();
            var sso = new SerializedObject(stall);
            sso.FindProperty("animator").objectReferenceValue = animator;
            sso.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }
    }
}
