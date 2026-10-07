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
    // Hung Vuong (NPC for the judgement cutscene). Input: Assets/Art/Characters/HungVuong/hung_vuong.fbx (armature + HungVuong_Body, the
    // clothed Meshy body, + HungVuong_Cape with the Cape_<column>_<segment> chains under Spine2, made by tools/fit_hung_vuong.py) and the
    // four Mixamo clips in Assets/Animations/HungVuong.
    // Builds: Humanoid import of the model and the clips (each clip with its own avatar), one URP/Lit material per mesh (cape Render Face:
    // Both), AC_HungVuong (Idle by default; triggers Talk / Point / Nod, plus Idle to stop talking) and the prefab
    // Assets/Prefabs/Characters/HungVuong.prefab (root + Model child at local Y = 0, scaled to 1.9 m at the head-top bone, OutfitSpringBones
    // swinging the cape chains).
    // Safe to run again.
    public static class HungVuongBuilder
    {
        const string ArtDir = "Assets/Art/Characters/HungVuong";
        const string ModelPath = ArtDir + "/hung_vuong.fbx";
        const string AnimDir = "Assets/Animations/HungVuong";
        const string ControllerPath = AnimDir + "/AC_HungVuong.controller";
        const string PrefabPath = "Assets/Prefabs/Characters/HungVuong.prefab";
        const string MaterialTemplate = "Assets/Art/Characters/ThuyTinh_v2/Materials/M_ThuyTinh_v2_Body.mat";
        const float HeadTopHeight = 1.9f;   // same as the two players, measured at mixamorig:HeadTop_End

        // state / trigger name, file, loops
        static readonly (string Name, string File, bool Loop)[] Clips =
        {
            ("Idle", "hung_vuong_idle.fbx", true),
            ("Talk", "hung_vuong_talking.fbx", true),
            ("Point", "hung_vuong_pointing.fbx", false),
            ("Nod", "hung_vuong_nod.fbx", false),
        };

        // mesh name suffix -> texture prefix, double sided
        static readonly (string Mesh, string Tex, bool TwoSided)[] Parts =
        {
            ("Body", "body", false),
            ("Cape", "cape", true),
        };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Hung Vuong NPC")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            ConfigureTextures();
            ConfigureModel();
            foreach (var c in Clips) ConfigureClip($"{AnimDir}/{c.File}", c.Name, c.Loop);
            AssetDatabase.Refresh();
            var materials = BuildMaterials();
            var controller = BuildController();
            BuildPrefab(materials, controller);
            AssetDatabase.SaveAssets();
            Debug.Log("HungVuongBuilder: done.");
        }

        // batch entry: unity run <project> -- -executeMethod SonTinhThuyTinh.EditorTools.HungVuongBuilder.BuildBatch [-hvShots <dir>]
        public static void BuildBatch()
        {
            Build();
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-hvShots");
            if (i >= 0 && i + 1 < args.Length) RenderChecks(args[i + 1]);
        }

        static void ConfigureTextures()
        {
            foreach (var p in Parts)
            {
                SetTexture($"{ArtDir}/Textures/hung_vuong_{p.Tex}_basecolor.png", TextureImporterType.Default, true);
                SetTexture($"{ArtDir}/Textures/hung_vuong_{p.Tex}_normal.png", TextureImporterType.NormalMap, false);
                SetTexture($"{ArtDir}/Textures/hung_vuong_{p.Tex}_metallic_smoothness.png", TextureImporterType.Default, false);
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

        static void ConfigureModel()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            Debug.Log($"HungVuongBuilder: model avatar valid={avatar != null && avatar.isValid} human={avatar != null && avatar.isHuman}");
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

        static AnimationClip LoadClip(string file) =>
            AssetDatabase.LoadAllAssetsAtPath($"{AnimDir}/{file}").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        static Material[] BuildMaterials()
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>(MaterialTemplate);
            var result = new Material[Parts.Length];
            for (int i = 0; i < Parts.Length; i++)
            {
                var p = Parts[i];
                string path = $"{ArtDir}/Materials/M_HungVuong_{p.Mesh}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = Shader.Find("Universal Render Pipeline/Lit");
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/Textures/hung_vuong_{p.Tex}_basecolor.png"));
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/Textures/hung_vuong_{p.Tex}_normal.png"));
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/Textures/hung_vuong_{p.Tex}_metallic_smoothness.png"));
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

        static AnimatorController BuildController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);   // rebuilt from scratch every run
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (string t in new[] { "Talk", "Point", "Nod", "Idle" }) ac.AddParameter(t, AnimatorControllerParameterType.Trigger);
            var sm = ac.layers[0].stateMachine;
            AnimatorState idle = null;
            for (int i = 0; i < Clips.Length; i++)
            {
                var s = sm.AddState(Clips[i].Name, new Vector3(300f, 60f * i, 0f));
                s.motion = LoadClip(Clips[i].File);
                s.writeDefaultValues = true;
                if (i == 0) idle = s;
            }
            sm.defaultState = idle;
            foreach (var st in sm.states.Select(x => x.state))
            {
                string trigger = st.name;
                var any = sm.AddAnyStateTransition(st);
                any.AddCondition(AnimatorConditionMode.If, 0, trigger);
                any.canTransitionToSelf = false;
                any.duration = 0.25f; any.hasFixedDuration = true; any.hasExitTime = false;
                if (st.name == "Point" || st.name == "Nod")   // one-shot gestures go back to Idle by themselves
                {
                    var back = st.AddTransition(idle);
                    back.hasExitTime = true; back.exitTime = 0.9f;
                    back.duration = 0.25f; back.hasFixedDuration = true;
                }
            }
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static void BuildPrefab(Material[] materials, AnimatorController controller)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = new GameObject("HungVuong");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;   // Y must stay 0: Humanoid puts the feet on the model's root plane
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int i = System.Array.FindIndex(Parts, p => smr.name.EndsWith("_" + p.Mesh));
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
            float scale = HeadTopHeight / h;
            model.transform.localScale = Vector3.one * scale;
            Debug.Log($"HungVuongBuilder: unscaled head-top to sole {h:F4}, model scale {scale:F4}");

            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // cutscene: keep animating when the camera cuts away

            // cape chains: the Animator never writes them (rigid on Spine2 in every clip); a gentle spring adds the sway
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

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
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

        // check images: the prefab in a temporary scene, each clip sampled at its middle, front + three-quarter view
        static void RenderChecks(string dir)
        {
            Directory.CreateDirectory(dir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Sun"); var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.6f; lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
            var npc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var model = npc.transform.Find("Model").gameObject;
            var camGo = new GameObject("Cam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.36f, 0.37f, 0.42f);
            cam.fieldOfView = 30f;
            var rt = new RenderTexture(640, 900, 24); cam.targetTexture = rt;
            foreach (var c in Clips)
            {
                var clip = LoadClip(c.File);
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
                    var bakedGos = new System.Collections.Generic.List<GameObject>();
                    foreach (var smr in smrs)
                    {
                        var mesh = SkinToWorld(smr);
                        var go = new GameObject("baked_" + smr.name);
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        go.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                        smr.enabled = false; bakedGos.Add(go);
                    }
                    // full body front / three-quarter / back, then close-ups of the collar and shoulders (front three-quarter and back)
                    foreach (var (tag, yaw, dist, y) in new[] { ("front", 0f, 5.2f, 1.0f), ("q", 40f, 5.2f, 1.0f), ("back", 160f, 5.2f, 1.0f),
                                                                ("closeq", 35f, 1.9f, 1.45f), ("closeb", 145f, 1.9f, 1.45f) })
                    {
                        Vector3 dir3 = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        camGo.transform.position = new Vector3(0f, y, 0f) + dir3 * dist;
                        camGo.transform.LookAt(new Vector3(0f, y, 0f));
                        cam.Render();
                        RenderTexture.active = rt;
                        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                        File.WriteAllBytes(Path.Combine(dir, $"unity_{c.Name}_{(int)(f * 100)}_{tag}.png"), tex.EncodeToPNG());
                        Object.DestroyImmediate(tex);
                        RenderTexture.active = null;
                    }
                    foreach (var go in bakedGos) { Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(go); }
                    foreach (var smr in smrs) smr.enabled = true;
                    AnimationMode.StopAnimationMode();
                }
            }
            Debug.Log("HungVuongBuilder: check images written to " + dir);
        }
    }
}
