using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using SonTinhThuyTinh.Characters;

namespace SonTinhThuyTinh.EditorTools
{
    // Story NPCs (Hùng Vương, Mị Nương): a Mixamo-rigged model made by tools/fit_<npc>.py (armature + <Name>_<Part> skinned meshes, the cloth
    // part with Cape_<column>_<segment> chains under Spine2) plus its Mixamo clips. Builds: Humanoid import of the model and the clips (each
    // clip file with its own avatar), one URP/Lit material per mesh (two-sided parts Render Face: Both), AC_<Name> (first state is the default;
    // one trigger per state, one-shot states go back to the first one) and Assets/Prefabs/Characters/<Name>.prefab (root + Model child at
    // local Y = 0, scaled to the spec's height at the head-top bone, OutfitSpringBones swinging the Cape_ chains). Safe to run again.
    public static class NpcBuilder
    {
        public class Spec
        {
            public string Name;           // HungVuong
            public string Prefix;         // hung_vuong: model <ArtDir>/<Prefix>.fbx, textures <ArtDir>/Textures/<Prefix>_<tex>_*.png
            public string ArtDir;
            public string AnimDir;
            public float HeadTopHeight;   // metres, measured at mixamorig:HeadTop_End above the soles
            // state / trigger name, clip file in AnimDir, loops, one-shot (goes back to the first state by itself). Several states may share a file.
            public (string State, string File, bool Loop, bool OneShot)[] Clips;
            // mesh name suffix -> texture prefix, double sided
            public (string Mesh, string Tex, bool TwoSided)[] Parts;

            public string ModelPath => $"{ArtDir}/{Prefix}.fbx";
            public string ControllerPath => $"{AnimDir}/AC_{Name}.controller";
            public string PrefabPath => $"Assets/Prefabs/Characters/{Name}.prefab";
        }

        const string MaterialTemplate = "Assets/Art/Characters/ThuyTinh_v2/Materials/M_ThuyTinh_v2_Body.mat";

        public static void Build(Spec spec)
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            ConfigureTextures(spec);
            ConfigureModel(spec);
            var configured = new HashSet<string>();
            foreach (var c in spec.Clips)
                if (configured.Add(c.File)) ConfigureClip($"{spec.AnimDir}/{c.File}", c.State, c.Loop);
            AssetDatabase.Refresh();
            var materials = BuildMaterials(spec);
            var controller = BuildController(spec);
            BuildPrefab(spec, materials, controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"NpcBuilder: {spec.Name} done.");
        }

        static void ConfigureTextures(Spec spec)
        {
            foreach (var p in spec.Parts)
            {
                SetTexture($"{spec.ArtDir}/Textures/{spec.Prefix}_{p.Tex}_basecolor.png", TextureImporterType.Default, true);
                SetTexture($"{spec.ArtDir}/Textures/{spec.Prefix}_{p.Tex}_normal.png", TextureImporterType.NormalMap, false);
                SetTexture($"{spec.ArtDir}/Textures/{spec.Prefix}_{p.Tex}_metallic_smoothness.png", TextureImporterType.Default, false);
            }
        }

        static void SetTexture(string path, TextureImporterType type, bool srgb)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) { Debug.LogError("Missing texture: " + path); return; }
            ti.textureType = type;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }

        static void ConfigureModel(Spec spec)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(spec.ModelPath);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(spec.ModelPath).OfType<Avatar>().FirstOrDefault();
            Debug.Log($"NpcBuilder: {spec.Name} avatar valid={avatar != null && avatar.isValid} human={avatar != null && avatar.isHuman}");
        }

        static void ConfigureClip(string path, string name, bool loop)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            if (mi == null) { Debug.LogError("Missing clip file: " + path); return; }
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;   // own avatar per file (docs/progress.md section 2)
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.SaveAndReimport();
            var clips = mi.defaultClipAnimations;
            if (clips.Length == 0) { Debug.LogError("No animation in " + path); return; }
            var c = clips[0];
            c.name = name;
            c.loopTime = loop;
            c.loopPose = false;
            c.lockRootRotation = true; c.keepOriginalOrientation = true;
            c.lockRootHeightY = true; c.keepOriginalPositionY = true;
            c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;   // an NPC stands on its spot: all body motion stays in the pose
            mi.clipAnimations = new[] { c };
            mi.SaveAndReimport();
        }

        public static AnimationClip LoadClip(Spec spec, string file) =>
            AssetDatabase.LoadAllAssetsAtPath($"{spec.AnimDir}/{file}").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        static Material[] BuildMaterials(Spec spec)
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>(MaterialTemplate);
            var result = new Material[spec.Parts.Length];
            Directory.CreateDirectory($"{spec.ArtDir}/Materials");
            for (int i = 0; i < spec.Parts.Length; i++)
            {
                var p = spec.Parts[i];
                string path = $"{spec.ArtDir}/Materials/M_{spec.Name}_{p.Mesh}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(m, path);
                }
                string tex = $"{spec.ArtDir}/Textures/{spec.Prefix}_{p.Tex}";
                m.shader = Shader.Find("Universal Render Pipeline/Lit");
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_basecolor.png"));
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_normal.png"));
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_metallic_smoothness.png"));
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_WorkflowMode", 1f);
                m.SetFloat("_Smoothness", 1f);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.SetFloat("_Cull", p.TwoSided ? (float)CullMode.Off : (float)CullMode.Back);
                m.doubleSidedGI = p.TwoSided;
                EditorUtility.SetDirty(m);
                result[i] = m;
            }
            return result;
        }

        static AnimatorController BuildController(Spec spec)
        {
            // rebuilt from scratch every run, but in the same asset so its GUID (and the prefab's reference) stays
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(spec.ControllerPath)
                     ?? AnimatorController.CreateAnimatorControllerAtPath(spec.ControllerPath);
            foreach (var p in ac.parameters) ac.RemoveParameter(p);
            var old = ac.layers[0].stateMachine;
            foreach (var t in old.anyStateTransitions) old.RemoveAnyStateTransition(t);
            foreach (var st in old.states) old.RemoveState(st.state);
            foreach (var c in spec.Clips) ac.AddParameter(c.State, AnimatorControllerParameterType.Trigger);
            var sm = ac.layers[0].stateMachine;
            AnimatorState first = null;
            var states = new List<(AnimatorState state, bool oneShot)>();
            for (int i = 0; i < spec.Clips.Length; i++)
            {
                var s = sm.AddState(spec.Clips[i].State, new Vector3(300f, 60f * i, 0f));
                s.motion = LoadClip(spec, spec.Clips[i].File);
                s.writeDefaultValues = true;
                if (i == 0) first = s;
                states.Add((s, spec.Clips[i].OneShot));
            }
            sm.defaultState = first;
            foreach (var (st, oneShot) in states)
            {
                var any = sm.AddAnyStateTransition(st);
                any.AddCondition(AnimatorConditionMode.If, 0, st.name);
                any.canTransitionToSelf = false;
                any.duration = 0.25f; any.hasFixedDuration = true; any.hasExitTime = false;
                if (oneShot)   // gestures go back to the first state (Idle) by themselves
                {
                    var back = st.AddTransition(first);
                    back.hasExitTime = true; back.exitTime = 0.9f;
                    back.duration = 0.25f; back.hasFixedDuration = true;
                }
            }
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static void BuildPrefab(Spec spec, Material[] materials, AnimatorController controller)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
            var root = new GameObject(spec.Name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;   // Y must stay 0: Humanoid puts the feet on the model's root plane
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int i = System.Array.FindIndex(spec.Parts, p => smr.name.EndsWith("_" + p.Mesh));
                if (i < 0) { Debug.LogWarning("Unknown mesh " + smr.name); continue; }
                smr.sharedMaterial = materials[i];
                smr.updateWhenOffscreen = true;
            }

            // scale: head-top bone to boot sole = HeadTopHeight
            Transform head = model.GetComponentsInChildren<Transform>().First(t => t.name == "mixamorig:HeadTop_End");
            var body = model.GetComponentsInChildren<SkinnedMeshRenderer>().First(s => s.name.EndsWith("_Body"));
            var toWorld = body.localToWorldMatrix;   // bind pose (nothing sampled yet): the stored mesh sits under its renderer
            float sole = body.sharedMesh.vertices.Min(v => toWorld.MultiplyPoint3x4(v).y);
            float h = head.position.y - sole;
            float scale = spec.HeadTopHeight / h;
            model.transform.localScale = Vector3.one * scale;
            Debug.Log($"NpcBuilder: {spec.Name} unscaled head-top to sole {h:F4}, model scale {scale:F4}");

            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(spec.ModelPath).OfType<Avatar>().FirstOrDefault();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // cutscene: keep animating when the camera cuts away

            // cloth chains: the Animator never writes them (rigid on Spine2 in every clip); a gentle spring adds the sway
            var springs = model.AddComponent<OutfitSpringBones>();
            var so = new SerializedObject(springs);
            var groups = so.FindProperty("groups");
            groups.arraySize = 1;
            var g = groups.GetArrayElementAtIndex(0);
            g.FindPropertyRelative("name").stringValue = "Cape";
            g.FindPropertyRelative("bonePrefix").stringValue = "Cape_";
            g.FindPropertyRelative("frequency").floatValue = 4f;
            g.FindPropertyRelative("dampingRatio").floatValue = 0.7f;
            g.FindPropertyRelative("drag").floatValue = 3f;
            g.FindPropertyRelative("gravity").floatValue = 0.5f;
            g.FindPropertyRelative("maxAngle").floatValue = 6f;
            g.FindPropertyRelative("inwardLimit").floatValue = 0.04f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(spec.PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, spec.PrefabPath);
            Object.DestroyImmediate(root);
        }

        static Mesh SkinToWorld(SkinnedMeshRenderer smr)
        {
            Mesh src = smr.sharedMesh;
            Matrix4x4[] bind = src.bindposes;
            var m = new Matrix4x4[bind.Length];
            for (int b = 0; b < bind.Length; b++) m[b] = smr.bones[b].localToWorldMatrix * bind[b];
            Vector3[] v = src.vertices, n = src.normals;
            BoneWeight[] w = src.boneWeights;
            var ov = new Vector3[v.Length]; var on = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                BoneWeight bw = w[i];
                ov[i] = m[bw.boneIndex0].MultiplyPoint3x4(v[i]) * bw.weight0 + m[bw.boneIndex1].MultiplyPoint3x4(v[i]) * bw.weight1
                      + m[bw.boneIndex2].MultiplyPoint3x4(v[i]) * bw.weight2 + m[bw.boneIndex3].MultiplyPoint3x4(v[i]) * bw.weight3;
                on[i] = (m[bw.boneIndex0].MultiplyVector(n[i]) * bw.weight0 + m[bw.boneIndex1].MultiplyVector(n[i]) * bw.weight1
                       + m[bw.boneIndex2].MultiplyVector(n[i]) * bw.weight2 + m[bw.boneIndex3].MultiplyVector(n[i]) * bw.weight3).normalized;
            }
            var mesh = new Mesh { indexFormat = src.indexFormat, vertices = ov, normals = on, uv = src.uv, tangents = src.tangents };
            mesh.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) mesh.SetTriangles(src.GetTriangles(s), s);
            mesh.RecalculateBounds();
            return mesh;
        }

        // check images: the prefab in a temporary scene, each clip sampled at 25 % and 60 %, front / three-quarter / back plus two close-ups
        // of the shoulders. Opens a new empty scene: run it in batch mode (BuildBatch of the NPC's builder, -npcShots <dir>).
        public static void RenderChecks(Spec spec, string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Sun"); var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.6f; lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
            var npc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath));
            var model = npc.transform.Find("Model").gameObject;
            var camGo = new GameObject("Cam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.36f, 0.37f, 0.42f);
            cam.fieldOfView = 30f;
            var rt = new RenderTexture(640, 900, 24); cam.targetTexture = rt;
            float k = spec.HeadTopHeight / 1.9f;
            foreach (var file in spec.Clips.Select(c => c.File).Distinct())
            {
                var clip = LoadClip(spec, file);
                foreach (float f in new[] { 0.25f, 0.6f })
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(model, clip, clip.length * f);
                    AnimationMode.EndSampling();
                    // edit mode skins a SkinnedMeshRenderer only once per editor frame, so every pose would render like the first one:
                    // skin the sampled pose on the CPU into plain world-space meshes and render those instead (BakeMesh returns
                    // nothing usable in batch mode)
                    var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                    var bakedGos = new List<GameObject>();
                    foreach (var smr in smrs)
                    {
                        var mesh = SkinToWorld(smr);
                        var go = new GameObject("baked_" + smr.name);
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        go.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                        smr.enabled = false; bakedGos.Add(go);
                    }
                    foreach (var (tag, yaw, dist, y) in new[] { ("front", 0f, 5.2f, 1.0f), ("q", 40f, 5.2f, 1.0f), ("back", 160f, 5.2f, 1.0f),
                                                                ("closeq", 35f, 1.9f, 1.45f), ("closeb", 145f, 1.9f, 1.45f) })
                    {
                        Vector3 dir3 = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        camGo.transform.position = new Vector3(0f, y * k, 0f) + dir3 * dist * k;
                        camGo.transform.LookAt(new Vector3(0f, y * k, 0f));
                        cam.Render();
                        RenderTexture.active = rt;
                        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                        File.WriteAllBytes(Path.Combine(dir, $"unity_{Path.GetFileNameWithoutExtension(file)}_{(int)(f * 100)}_{tag}.png"), tex.EncodeToPNG());
                        Object.DestroyImmediate(tex);
                        RenderTexture.active = null;
                    }
                    foreach (var go in bakedGos) { Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(go); }
                    foreach (var smr in smrs) smr.enabled = true;
                    AnimationMode.StopAnimationMode();
                }
            }
            Debug.Log($"NpcBuilder: {spec.Name} check images written to " + dir);
        }

        // batch entry helper: -npcShots <dir> (also accepts the old -hvShots)
        public static void BuildBatch(Spec spec)
        {
            Build(spec);
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.FindIndex(args, a => a == "-npcShots" || a == "-hvShots");
            if (i >= 0 && i + 1 < args.Length) RenderChecks(spec, args[i + 1]);
        }
    }
}
