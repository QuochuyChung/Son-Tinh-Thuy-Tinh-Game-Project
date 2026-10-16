using System.Collections.Generic;
using System.IO;
using System.Linq;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Flow;
using SonTinhThuyTinh.Quest;
using SonTinhThuyTinh.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UImage = UnityEngine.UI.Image;

namespace SonTinhThuyTinh.EditorTools
{
    // Sấu Chín Đuôi, the boss of the Thủy Tinh map (docs/task-sau-chin-duoi.md).
    //  - Build Sau Chin Duoi (boss model): Assets/Art/Characters/SauChinDuoi/sau_chin_duoi.fbx (armature + SauChinDuoi_Body + ten actions, made
    //    by tools/rig_sau.py): Generic import, URP/Lit material, AC_SauChinDuoi (one state per clip, SauChinDuoiBoss cross-fades by name),
    //    Assets/Prefabs/Characters/SauChinDuoi.prefab (head towards +Z, Length long, feet on the root's ground plane, capsule on the body,
    //    OutfitSpringBones on the nine Tail_ chains, Health + SauChinDuoiBoss), plus the gift Minh châu đáy vực and Quest_SinhLe_ThuyTinh.
    //  - BuildArena is called by ThuyTinhMapBuilder (Build Thuy Tinh Map): the boss in the water beside the lake island, its lair, the
    //    player's respawn point and the boss bar. The island itself (bigger, ring of boulders, crystals) is shaped by ThuyTinhMapBuilder.
    // Batch: unity run . -- -executeMethod SonTinhThuyTinh.EditorTools.SauChinDuoiBuilder.BuildAllBatch [-sauShots <dir>]
    public static class SauChinDuoiBuilder
    {
        const string ArtDir = "Assets/Art/Characters/SauChinDuoi";
        const string ModelPath = ArtDir + "/sau_chin_duoi.fbx";
        const string MaterialPath = ArtDir + "/Materials/M_SauChinDuoi.mat";
        const string WarningMaterialPath = ArtDir + "/Materials/Mat_BossWarning.mat";
        const string WarningTexturePath = ArtDir + "/Textures/vfx_warning_disc.png";
        const string SpitMaterialPath = ArtDir + "/Materials/Mat_WaterSpit.mat";
        const string AnimDir = "Assets/Animations/SauChinDuoi";
        const string ControllerPath = AnimDir + "/AC_SauChinDuoi.controller";
        public const string PrefabPath = "Assets/Prefabs/Characters/SauChinDuoi.prefab";
        public const string GiftPath = "Assets/Data/Gifts/Gift_MinhChauDayVuc.asset";
        public const string QuestPath = "Assets/Data/Gifts/Quest_SinhLe_ThuyTinh.asset";
        const string MaterialTemplate = "Assets/Art/Characters/ThuyTinh_v2/Materials/M_ThuyTinh_v2_Body.mat";
        const string WhiteSprite = "Assets/Art/UI/HUD/hud_white.png";
        const float Length = 6.5f;           // snout to the hips' back end (the tails fan out above that)
        const float MaxHealth = 600f;
        static readonly (string Name, bool Loop)[] Clips =
        {
            ("Idle", true), ("Walk", true), ("Charge", true), ("Bite", false), ("TailSweep", false), ("WaterSpit", false),
            ("TailSlam", false), ("Hit", false), ("Roar", false), ("Death", false),
        };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Sau Chin Duoi (boss model)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            foreach (var (path, type, srgb) in new[] {
                         (ArtDir + "/Textures/sau_basecolor.png", TextureImporterType.Default, true),
                         (ArtDir + "/Textures/sau_normal.png", TextureImporterType.NormalMap, false),
                         (ArtDir + "/Textures/sau_metallic_smoothness.png", TextureImporterType.Default, false) })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (ti == null) { Debug.LogError("Missing texture: " + path); continue; }
                ti.textureType = type; ti.sRGBTexture = srgb; ti.maxTextureSize = 2048; ti.SaveAndReimport();
            }
            ConfigureModel();
            var material = BuildMaterial();
            var controller = BuildController();
            EnsureGift();
            BuildPrefab(material, controller);
            AssetDatabase.SaveAssets();
            Debug.Log("SauChinDuoiBuilder: model + prefab done.");
        }

        // Model, then the whole Thủy Tinh map (which builds the arena), then optional check images.
        public static void BuildAllBatch()
        {
            Build();
            ThuyTinhMapBuilder.Build(true);
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-sauShots");
            if (i >= 0 && i + 1 < args.Length)
            {
                RenderArenaShots(args[i + 1]);
                RenderClipShots(args[i + 1]);
            }
        }

        // Only the map (and the arena shots): unity run . -- -executeMethod SonTinhThuyTinh.EditorTools.SauChinDuoiBuilder.MapBatch -sauShots <dir>
        public static void MapBatch()
        {
            ThuyTinhMapBuilder.Build(true);
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-sauShots");
            if (i >= 0 && i + 1 < args.Length) RenderArenaShots(args[i + 1]);
        }

        // ---------------------------------------------------------------- model, material, controller

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
            var result = new List<ModelImporterClipAnimation>();
            foreach (var (name, loop) in Clips)
            {
                var c = clips.FirstOrDefault(x => x.takeName.EndsWith(name) || x.name.EndsWith(name));
                if (c == null) { Debug.LogError($"SauChinDuoiBuilder: no take for {name} in " + string.Join(", ", clips.Select(x => x.takeName))); continue; }
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
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Textures/sau_basecolor.png"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Textures/sau_normal.png"));
            m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtDir + "/Textures/sau_metallic_smoothness.png"));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_WorkflowMode", 1f); m.SetFloat("_Smoothness", 0.8f); m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off); m.doubleSidedGI = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // Red disc for the warning circles: a soft fill with a bright rim, transparent outside.
        static Material WarningMaterial()
        {
            if (!File.Exists(WarningTexturePath))
            {
                const int n = 256;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float r = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude / (n / 2f);
                        float fill = 0.45f * Mathf.Clamp01((1f - r) * 40f);
                        float rim = Mathf.Exp(-Mathf.Pow((r - 0.95f) / 0.03f, 2f));
                        float a = Mathf.Clamp01(Mathf.Max(fill, rim));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                Directory.CreateDirectory(Path.GetDirectoryName(WarningTexturePath));
                File.WriteAllBytes(WarningTexturePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(WarningTexturePath);
                var ti = (TextureImporter)AssetImporter.GetAtPath(WarningTexturePath);
                ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = false; ti.SaveAndReimport();
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(WarningMaterialPath);
            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, WarningMaterialPath); }
            m.shader = shader;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WarningTexturePath);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(1f, 0.15f, 0.1f, 0.5f));
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material SpitMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(SpitMaterialPath);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, SpitMaterialPath); }
            m.SetColor("_BaseColor", new Color(0.35f, 0.75f, 0.95f));
            m.SetFloat("_Smoothness", 0.95f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.05f, 0.35f, 0.55f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        static AnimatorController BuildController()
        {
            Directory.CreateDirectory(AnimDir);
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var p in ac.parameters) ac.RemoveParameter(p);
            var sm = ac.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (var st in sm.states) sm.RemoveState(st.state);
            for (int i = 0; i < Clips.Length; i++)
            {
                var state = sm.AddState(Clips[i].Name, new Vector3(300f, i * 60f, 0f));
                state.motion = Clip(Clips[i].Name);
                if (i == 0) sm.defaultState = state;
            }
            EditorUtility.SetDirty(ac);
            return ac;
        }

        // ---------------------------------------------------------------- gift + quest

        static void EnsureGift()
        {
            var gift = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            if (gift == null) { gift = ScriptableObject.CreateInstance<GiftItem>(); AssetDatabase.CreateAsset(gift, GiftPath); }
            var so = new SerializedObject(gift);
            so.FindProperty("displayName").stringValue = "Minh châu đáy vực";
            so.FindProperty("description").stringValue = "Viên ngọc sáng Sấu Chín Đuôi giữ dưới đáy vực sâu, sính lễ Thủy Tinh dâng Vua Hùng.";
            so.FindProperty("receivedMessage").stringValue = "Bạn đã nhận được {0}";
            so.ApplyModifiedPropertiesWithoutUndo();

            var quest = AssetDatabase.LoadAssetAtPath<GiftQuest>(QuestPath);
            if (quest == null) { quest = ScriptableObject.CreateInstance<GiftQuest>(); AssetDatabase.CreateAsset(quest, QuestPath); }
            var qso = new SerializedObject(quest);
            qso.FindProperty("title").stringValue = "Sính lễ";
            var list = qso.FindProperty("requiredGifts");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = gift;
            qso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gift); EditorUtility.SetDirty(quest);
        }

        // ---------------------------------------------------------------- prefab

        static void BuildPrefab(Material material, AnimatorController controller)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = new GameObject("SauChinDuoi");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            smr.sharedMaterial = material; smr.updateWhenOffscreen = true;

            // turn the head towards +Z
            Transform Bone(string n) => model.GetComponentsInChildren<Transform>().First(t => t.name == n);
            Vector3 along = Bone("Head").position - Bone("Hips").position; along.y = 0f;
            model.transform.localRotation = Quaternion.FromToRotation(along.normalized, Vector3.forward);

            // scale: snout to the back of the hind legs = Length; feet on the ground; pivot under the middle of the body
            Vector3[] World() { var m = smr.localToWorldMatrix; return smr.sharedMesh.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray(); }
            var verts = World();
            float snout = verts.Max(v => v.z), back = verts.Where(v => v.y < Bone("Hips").position.y).Min(v => v.z);
            float scale = Length / (snout - back);
            model.transform.localScale = Vector3.one * scale;
            verts = World();
            float minY = verts.Min(v => v.y);
            float midZ = (Bone("Hips").position.z + Bone("Chest").position.z) / 2f;
            model.transform.localPosition = new Vector3(0f, -minY, -midZ);
            verts = World();
            snout = verts.Max(v => v.z);
            float top = verts.Max(v => v.y);
            Debug.Log($"SauChinDuoiBuilder: scale {scale:F3}, snout {snout:F2} m ahead of the pivot, top of the tails {top:F2} m, hub {Bone("TailBase").position}");

            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // the body (not the tails): the player cannot walk through it and the player's attacks find it
            var body = verts.Where(v => v.y < 2.6f && v.z > Bone("TailBase").position.z).ToArray();
            var b = new Bounds(body[0], Vector3.zero); foreach (var v in body) b.Encapsulate(v);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = Mathf.Min(b.size.x * 0.5f, 1.3f);
            capsule.height = b.size.z * 0.95f;
            capsule.center = new Vector3(0f, capsule.radius + 0.1f, b.center.z);

            var springs = model.AddComponent<OutfitSpringBones>();
            var so = new SerializedObject(springs);
            var groups = so.FindProperty("groups");
            groups.arraySize = 1;
            var g = groups.GetArrayElementAtIndex(0);
            g.FindPropertyRelative("name").stringValue = "Tails";
            g.FindPropertyRelative("bonePrefix").stringValue = "Tail_";
            g.FindPropertyRelative("frequency").floatValue = 3f;
            g.FindPropertyRelative("dampingRatio").floatValue = 0.5f;
            g.FindPropertyRelative("drag").floatValue = 2f;
            g.FindPropertyRelative("gravity").floatValue = 0.3f;
            g.FindPropertyRelative("maxAngle").floatValue = 14f;
            g.FindPropertyRelative("inwardLimit").floatValue = 0.3f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var health = root.AddComponent<Health>();
            var hso = new SerializedObject(health);
            hso.FindProperty("max").floatValue = MaxHealth;
            hso.ApplyModifiedPropertiesWithoutUndo();

            var boss = root.AddComponent<SauChinDuoiBoss>();
            var bso = new SerializedObject(boss);
            bso.FindProperty("animator").objectReferenceValue = animator;
            var rs = bso.FindProperty("renderers"); rs.arraySize = 1; rs.GetArrayElementAtIndex(0).objectReferenceValue = smr;
            bso.FindProperty("reward").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            bso.FindProperty("warningMaterial").objectReferenceValue = WarningMaterial();
            bso.FindProperty("spitMaterial").objectReferenceValue = SpitMaterial();
            bso.FindProperty("biteReach").floatValue = Mathf.Round((snout - 0.8f) * 10f) / 10f;
            bso.FindProperty("biteRange").floatValue = Mathf.Round((snout + 0.8f) * 10f) / 10f;
            bso.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        // ---------------------------------------------------------------- arena (called by ThuyTinhMapBuilder.Build, scene already open)

        public const string ArenaRoot = "SauChinDuoiArena";

        // centre: island centre at ground level; entry: direction the player arrives from (along the causeway, pointing into the island);
        // lairSide: horizontal direction from the centre to the deep water where the crocodile waits; waterY: the water surface.
        public static void BuildArena(Transform parent, Vector3 centre, Vector3 entry, Vector3 lairSide, float waterY, float arenaRadius)
        {
            var old = GameObject.Find(ArenaRoot);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(ArenaRoot).transform;
            root.SetParent(parent, false);

            var centreT = new GameObject("ArenaCenter").transform;
            centreT.SetParent(root, false); centreT.position = centre;
            var lair = new GameObject("Lair").transform;
            lair.SetParent(root, false);
            Vector3 lp = centre + lairSide.normalized * (arenaRadius + 7f);
            var terrain = Object.FindFirstObjectByType<Terrain>();
            float bed = terrain != null ? terrain.SampleHeight(lp) + terrain.transform.position.y : waterY - 2.2f;
            lair.position = new Vector3(lp.x, Mathf.Min(waterY - 1.8f, bed + 0.1f), lp.z);   // standing on the lake bed: only the back and the tails show
            lair.rotation = Quaternion.LookRotation(-lairSide.normalized);   // facing the island
            var respawn = new GameObject("PlayerRespawn").transform;
            respawn.SetParent(root, false);
            respawn.position = centre - entry.normalized * (arenaRadius + 6f) + Vector3.up * 0.3f;
            respawn.rotation = Quaternion.LookRotation(entry.normalized);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("SauChinDuoiBuilder: build the boss model first (" + PrefabPath + ")."); return; }
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            boss.transform.SetPositionAndRotation(lair.position, lair.rotation);

            var bar = BuildBar(root);
            var so = new SerializedObject(boss.GetComponent<SauChinDuoiBoss>());
            so.FindProperty("bar").objectReferenceValue = bar;
            so.FindProperty("arenaCenter").objectReferenceValue = centreT;
            so.FindProperty("lair").objectReferenceValue = lair;
            so.FindProperty("playerRespawn").objectReferenceValue = respawn;
            so.FindProperty("arenaRadius").floatValue = arenaRadius;
            so.FindProperty("wakeRadius").floatValue = arenaRadius - 1.5f;
            so.FindProperty("leashRadius").floatValue = arenaRadius + 12f;
            so.ApplyModifiedPropertiesWithoutUndo();
            ArenaBoundaryBuilder.AddCrocIntro(boss.GetComponent<SauChinDuoiBoss>());   // island limit + intro cutscene camera and cards

            // the map's gift list and exit now use the Thủy Tinh quest (one gift: the pearl)
            var quest = AssetDatabase.LoadAssetAtPath<GiftQuest>(QuestPath);
            foreach (var hud in Object.FindObjectsByType<GiftTrackerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var h = new SerializedObject(hud); h.FindProperty("quest").objectReferenceValue = quest; h.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var exit in Object.FindObjectsByType<SceneTransitionTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var e = new SerializedObject(exit);
                var p = e.FindProperty("requiredQuest");
                if (p != null && p.objectReferenceValue != null) { p.objectReferenceValue = quest; e.ApplyModifiedPropertiesWithoutUndo(); }
            }
            Debug.Log($"SauChinDuoiBuilder: arena at {centre}, lair {lair.position}, respawn {respawn.position}");
        }

        static BossHealthBar BuildBar(Transform parent)
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSprite);
            var font = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.font != null && t.GetComponentInParent<GiftTrackerHUD>() != null)?.font;

            var canvasGo = new GameObject("BossBarCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;

            var barGo = new GameObject("BossBar", typeof(RectTransform), typeof(CanvasGroup));
            barGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)barGo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -70f); rt.sizeDelta = new Vector2(920f, 26f);

            UImage Img(string name, Color color, Vector2 inset, bool filled)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(UImage));
                go.transform.SetParent(barGo.transform, false);
                var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = -inset; r.offsetMax = inset;
                var img = go.GetComponent<UImage>(); img.sprite = white; img.color = color; img.raycastTarget = false;
                if (filled) { img.type = UImage.Type.Filled; img.fillMethod = UImage.FillMethod.Horizontal; img.fillOrigin = 0; img.fillAmount = 1f; }
                return img;
            }
            Img("Frame", new Color(0.85f, 0.68f, 0.30f), new Vector2(4f, 4f), false);
            Img("Back", new Color(0.05f, 0.06f, 0.08f, 0.9f), Vector2.zero, false);
            var trail = Img("Trail", new Color(1f, 0.62f, 0.2f), Vector2.zero, true);
            var fill = Img("Fill", new Color(0.8f, 0.16f, 0.12f), Vector2.zero, true);
            var mark = Img("HalfMark", new Color(1f, 0.95f, 0.8f, 0.9f), Vector2.zero, false);
            var mr = mark.rectTransform; mr.anchorMin = new Vector2(0.5f, 0f); mr.anchorMax = new Vector2(0.5f, 1f); mr.offsetMin = new Vector2(-1.5f, -4f); mr.offsetMax = new Vector2(1.5f, 4f);

            TMP_Text Label(string name, string text, TextAlignmentOptions align, Vector2 anchor, float size)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(barGo.transform, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(anchor.x, 0f);
                r.anchoredPosition = new Vector2(0f, 8f); r.sizeDelta = new Vector2(600f, 44f);
                var t = go.GetComponent<TextMeshProUGUI>();
                if (font != null) t.font = font;
                t.text = text; t.alignment = align; t.fontSize = size; t.color = new Color(0.98f, 0.93f, 0.80f); t.raycastTarget = false;
                t.fontStyle = FontStyles.Bold;
                return t;
            }
            var nameLabel = Label("Name", "Sấu Chín Đuôi", TextAlignmentOptions.BottomLeft, new Vector2(0f, 1f), 34f);
            var phaseLabel = Label("Phase", "", TextAlignmentOptions.BottomRight, new Vector2(1f, 1f), 26f);
            phaseLabel.color = new Color(1f, 0.55f, 0.85f);

            var bar = barGo.AddComponent<BossHealthBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("group").objectReferenceValue = barGo.GetComponent<CanvasGroup>();
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("trail").objectReferenceValue = trail;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("phaseLabel").objectReferenceValue = phaseLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            barGo.GetComponent<CanvasGroup>().alpha = 0f;
            return bar;
        }

        // ---------------------------------------------------------------- check images (batch)

        static void Snap(Camera cam, string path)
        {
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        }

        // The arena in the built map: from the causeway, from above, and the crocodile in its lair; then the crocodile posed on the island.
        static void RenderArenaShots(string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene("Assets/Scenes/Map_ThuyTinh.unity", OpenSceneMode.Single);
            var root = GameObject.Find(ArenaRoot).transform;
            Vector3 c = root.Find("ArenaCenter").position, lair = root.Find("Lair").position, spawn = root.Find("PlayerRespawn").position;
            var camGo = new GameObject("ShotCam"); var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 55f; cam.farClipPlane = 900f;
            void At(Vector3 pos, Vector3 look, string name) { camGo.transform.position = pos; camGo.transform.LookAt(look); Snap(cam, Path.Combine(dir, name)); }
            At(spawn + Vector3.up * 2.5f - (c - spawn).normalized * 2f, c + Vector3.up * 1.5f, "arena_from_causeway.png");
            At(c + Vector3.up * 38f + (spawn - c).normalized * 22f, c, "arena_above.png");
            At(Vector3.Lerp(c, lair, 0.35f) + Vector3.up * 4f, lair + Vector3.up * 1.5f, "arena_lair.png");
            // pose the boss on the island for one shot (not saved)
            var boss = root.GetComponentInChildren<SauChinDuoiBoss>().transform;
            boss.position = c + (spawn - c).normalized * 3f; boss.rotation = Quaternion.LookRotation(spawn - c);
            At(c + (spawn - c).normalized * 15f + Vector3.up * 3f, boss.position + Vector3.up * 2f, "arena_boss_on_island.png");
            Object.DestroyImmediate(camGo);
        }

        // Each clip at 25 / 55 / 85 % on the prefab in an empty scene (skinned on the CPU: edit mode skins only once per editor frame).
        static void RenderClipShots(string dir)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Sun"); var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.6f; lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var model = boss.transform.Find("Model").gameObject;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var camGo = new GameObject("Cam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.36f, 0.37f, 0.42f); cam.fieldOfView = 40f;
            camGo.transform.position = new Vector3(10f, 5f, 9f); camGo.transform.LookAt(new Vector3(0f, 1.8f, 0f));
            foreach (var (name, _) in Clips)
            {
                var clip = Clip(name);
                foreach (float f in new[] { 0.25f, 0.55f, 0.85f })
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(model, clip, clip.length * f);
                    AnimationMode.EndSampling();
                    var mesh = NpcBuilder.SkinToWorld(smr);
                    var go = new GameObject("baked");
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                    smr.enabled = false;
                    Snap(cam, Path.Combine(dir, $"unity_{name}_{(int)(f * 100)}.png"));
                    Object.DestroyImmediate(mesh); Object.DestroyImmediate(go);
                    smr.enabled = true;
                    AnimationMode.StopAnimationMode();
                }
            }
            Debug.Log("SauChinDuoiBuilder: check images written to " + dir);
        }
    }
}
