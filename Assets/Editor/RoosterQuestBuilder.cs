using System.Collections.Generic;
using System.IO;
using System.Linq;
using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Quest;
using SonTinhThuyTinh.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UImage = UnityEngine.UI.Image;

namespace SonTinhThuyTinh.EditorTools
{
    // Gà Chín Cựa and the Ninja thieves on Map_SonTinh (docs/ke-hoach-ga-chin-cua.md).
    //  - Build Ninja (enemy prefab): the Ninja NPC through NpcBuilder (Assets/Art/Characters/Ninja/ninja.fbx made by tools/skin_ninja.py, the
    //    Mixamo clips in Assets/Animations/Ninja, the hold clip from tools/pose_ninja_hold.py; AC_Ninja gets a mirrored StrafeR), then
    //    Assets/Prefabs/Characters/Ninja_Enemy.prefab: capsule, Health 120, NinjaEnemy, the sword (hand socket from the hand bones, a socket
    //    on the back), a small health bar over the head, a chest socket for the rooster.
    //  - Build Rooster Quest (Son Tinh map): everything under the root "RoosterQuest" in the stone ring (the map's second stop, z 175):
    //    two Ninja + the leader, the rooster, the marks, the cutscene camera and its shots, the dialogue, RoosterQuestDirector. Switches off
    //    the old walk-in Pickup_GaChinCua. Safe to run again (the root is rebuilt). Rebuilding the map with Build Son Tinh Map turns the old
    //    pickup back on: run this again afterwards.
    // Batch: unity run . -- -executeMethod SonTinhThuyTinh.EditorTools.RoosterQuestBuilder.BuildAllBatch [-gaShots <dir>]
    public static class RoosterQuestBuilder
    {
        const string ScenePath = "Assets/Scenes/Map_SonTinh.unity";
        const string EnemyPrefabPath = "Assets/Prefabs/Characters/Ninja_Enemy.prefab";
        const string SwordModel = "Assets/Art/Weapons/NinjaSword/ninja_sword_mesh.fbx";
        const string SwordMaterialPath = "Assets/Art/Weapons/NinjaSword/M_NinjaSword.mat";
        const string RoosterPrefab = "Assets/Prefabs/Gifts/Rooster_GaChinCua.prefab";
        const string GiftPath = "Assets/Data/Gifts/Gift_GaChinCua.asset";
        const string DialogueDir = "Assets/Data/Dialogue";
        const string WhiteSprite = "Assets/Art/UI/HUD/hud_white.png";
        const string Root = "RoosterQuest";
        const float SwordLength = 1.05f;
        const float NinjaHealth = 120f;
        const float RingRadius = 9.5f;   // inside the boulder ring (11.5 m)
        const string Leader = "Thủ lĩnh Ninja", Ninja = "Ninja", Hero = "Sơn Tinh";

        public static readonly NpcBuilder.Spec Spec = new()
        {
            Name = "Ninja",
            Prefix = "ninja",
            ArtDir = "Assets/Art/Characters/Ninja",
            AnimDir = "Assets/Animations/Ninja",
            HeadTopHeight = 1.78f,
            Clips = new[]
            {
                ("Idle", "ninja_sword_and_shield_idle.fbx", true, false),
                ("Hold", "ninja_hold_rooster.fbx", true, false),
                ("Draw", "ninja_draw_a_great_sword_1.fbx", false, false),
                ("Run", "ninja_sword_and_shield_run.fbx", true, false),
                ("Strafe", "ninja_sword_and_shield_strafe.fbx", true, false),
                ("Kick", "ninja_sword_and_shield_kick.fbx", false, false),
                ("Attack", "ninja_sword_and_shield_attack.fbx", false, false),
                ("Combo", "ninja_two_hand_sword_combo.fbx", false, false),
                ("Slash", "ninja_sword_and_shield_slash.fbx", false, false),
                ("Impact", "ninja_sword_and_shield_impact.fbx", false, false),
                ("Block", "ninja_great_sword_impact.fbx", false, false),
                ("Death", "ninja_sword_and_shield_death.fbx", false, false),
            },
            Parts = new[] { ("Body", "body", true) },
        };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Ninja (enemy prefab)")]
        public static void BuildNinja()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            NpcBuilder.Build(Spec);
            UnbakeTravel();
            AddMirroredStrafe();
            AddHoldArmsLayer();
            BuildEnemyPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("RoosterQuestBuilder: Ninja_Enemy done.");
        }

        public static void BuildAllBatch()
        {
            BuildNinja();
            BuildQuest();
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-gaShots");
            if (i >= 0 && i + 1 < args.Length) { RenderSceneShots(args[i + 1]); RenderNinjaShots(args[i + 1]); }
        }

        // NpcBuilder bakes the clips' travel into the pose (an NPC stands on its spot): a Ninja running, leaping or stepping then drifts away
        // from its root and snaps back when the clip starts again. Here the travel is not baked: it becomes root motion, which the Animator
        // throws away (applyRootMotion off), so the body stays on the root and NinjaEnemy does the moving. Death keeps it (it ends lying down).
        static void UnbakeTravel()
        {
            foreach (var c in Spec.Clips.Where(c => c.State != "Death").Select(c => c.File).Distinct())
            {
                var mi = (ModelImporter)AssetImporter.GetAtPath($"{Spec.AnimDir}/{c}");
                var clips = mi.clipAnimations;
                foreach (var clip in clips) { clip.lockRootPositionXZ = false; clip.loopPose = clip.loopTime; }
                mi.clipAnimations = clips;
                mi.SaveAndReimport();
            }
        }

        // A second layer, arms only, playing Hold: the leader walks / runs (base layer) with the rooster still in his arms. Weight 0 until
        // NinjaEnemy.SetHolding(true).
        static void AddHoldArmsLayer()
        {
            const string maskPath = "Assets/Animations/Ninja/AM_Ninja_Arms.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, maskPath); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                                                     || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers);
            }
            EditorUtility.SetDirty(mask);

            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Spec.ControllerPath);
            for (int i = ac.layers.Length - 1; i > 0; i--) ac.RemoveLayer(i);
            ac.AddLayer("HoldArms");
            var layers = ac.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 0f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            var hold = layers[1].stateMachine.AddState("Hold");
            hold.motion = NpcBuilder.LoadClip(Spec, "ninja_hold_rooster.fbx");
            layers[1].stateMachine.defaultState = hold;
            ac.layers = layers;
            EditorUtility.SetDirty(ac);
        }

        // the Strafe clip only goes to the Ninja's left: a mirrored copy goes right
        static void AddMirroredStrafe()
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Spec.ControllerPath);
            var sm = ac.layers[0].stateMachine;
            var strafe = sm.states.First(s => s.state.name == "Strafe").state;
            var right = sm.AddState("StrafeR", new Vector3(560f, 240f, 0f));
            right.motion = strafe.motion; right.mirror = true; right.writeDefaultValues = true;
            EditorUtility.SetDirty(ac);
        }

        // ---------------------------------------------------------------- enemy prefab

        static Material SwordMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(SwordMaterialPath);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, SwordMaterialPath); }
            string t = "Assets/Art/Weapons/NinjaSword/Textures/ninja_sword_";
            foreach (var (path, type, srgb) in new[] { (t + "basecolor.png", TextureImporterType.Default, true), (t + "normal.png", TextureImporterType.NormalMap, false),
                                                       (t + "metallic_smoothness.png", TextureImporterType.Default, false) })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (ti == null) continue;
                ti.textureType = type; ti.sRGBTexture = srgb; ti.maxTextureSize = 1024; ti.SaveAndReimport();
            }
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "basecolor.png"));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "normal.png")); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "metallic_smoothness.png")); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void BuildEnemyPrefab()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(SwordModel);
            mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
            mi.SaveAndReimport();
            var swordMat = SwordMaterial();

            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Spec.PrefabPath));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = "Ninja_Enemy";
            var model = root.transform.Find("Model");
            Object.DestroyImmediate(model.GetComponent<OutfitSpringBones>());
            Transform Bone(string n) => model.GetComponentsInChildren<Transform>().First(t => t.name == "mixamorig:" + n);
            Transform BoneOr(string a, string b) => model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "mixamorig:" + a) ?? Bone(b);

            // hand socket: +Y along the blade, origin in the fist. Bind pose (T-pose, palms down): the fingers point out along the arm, the
            // thumb forward; a fist holds the blade coming out on the thumb side
            Transform hand = Bone("RightHand");
            Transform fingers = BoneOr("RightHandMiddle1", "RightHandIndex1");
            Transform thumb = Bone("RightHandThumb1");
            Vector3 along = (fingers.position - hand.position).normalized;
            Vector3 blade = Vector3.ProjectOnPlane(thumb.position - hand.position, along).normalized;
            Vector3 palm = Vector3.Cross(along, blade).normalized;
            var handSocket = new GameObject("SwordHand").transform;
            handSocket.SetParent(hand, false);
            handSocket.position = hand.position + along * (fingers.position - hand.position).magnitude * 0.75f + palm * 0.02f;
            handSocket.rotation = Quaternion.LookRotation(palm, blade);

            // back socket: the handle over the right shoulder, the blade down to the left hip, behind the back
            Transform spine = Bone("Spine2");
            var backSocket = new GameObject("SwordBack").transform;
            backSocket.SetParent(spine, false);
            Vector3 fwd = root.transform.forward, right = root.transform.right;
            backSocket.position = spine.position - fwd * 0.2f + Vector3.up * 0.25f + right * 0.18f;
            backSocket.rotation = Quaternion.LookRotation(-fwd, (-Vector3.up * 1f - right * 0.55f).normalized);

            // the sword: the model's long axis is its blade; the narrower end is the handle
            var swordAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SwordModel);
            var swordPivot = new GameObject("Sword").transform;
            var swordModel = (GameObject)PrefabUtility.InstantiatePrefab(swordAsset, swordPivot);
            PrefabUtility.UnpackPrefabInstance(swordModel, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var r in swordModel.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = swordMat; r.shadowCastingMode = ShadowCastingMode.On; }
            var mf = swordModel.GetComponentInChildren<MeshFilter>();
            var mesh = mf.sharedMesh;
            Matrix4x4 toPivot = swordPivot.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            var pts = mesh.vertices.Select(v => toPivot.MultiplyPoint3x4(v)).ToArray();
            var b = new Bounds(pts[0], Vector3.zero); foreach (var p in pts) b.Encapsulate(p);
            int ax = b.size.x > b.size.y ? (b.size.x > b.size.z ? 0 : 2) : (b.size.y > b.size.z ? 1 : 2);
            float len = b.size[ax];
            float Width(float lo, float hi) { var s = pts.Where(p => p[ax] >= lo && p[ax] <= hi).ToArray(); if (s.Length == 0) return 0f; int o = (ax + 1) % 3, q = (ax + 2) % 3; return s.Max(p => p[o]) - s.Min(p => p[o]) + s.Max(p => p[q]) - s.Min(p => p[q]); }
            // the guard is the widest part near one end; the handle is the short narrow piece past it
            float lowEnd = Width(b.min[ax], b.min[ax] + len * 0.3f), highEnd = Width(b.max[ax] - len * 0.3f, b.max[ax]);
            bool handleLow = lowEnd >= highEnd;
            Vector3 axis = Vector3.zero; axis[ax] = handleLow ? 1f : -1f;   // handle -> tip
            Vector3 grip = b.center; grip[ax] = handleLow ? b.min[ax] + len * 0.09f : b.max[ax] - len * 0.09f;
            // turn about the pivot so the blade runs along +Y and the grip sits on the origin (keeping the model's own import rotation)
            Quaternion q = Quaternion.FromToRotation(axis, Vector3.up);
            swordModel.transform.localRotation = q * swordModel.transform.localRotation;
            swordModel.transform.localPosition = q * swordModel.transform.localPosition - q * grip;
            Debug.Log($"RoosterQuestBuilder: sword long axis {ax}, length {len:F3}, handle at the {(handleLow ? "low" : "high")} end (end widths {lowEnd:F3} / {highEnd:F3})");
            swordPivot.SetParent(backSocket, false);
            swordPivot.localPosition = Vector3.zero; swordPivot.localRotation = Quaternion.identity;
            swordPivot.localScale = Vector3.one * (SwordLength / len) / backSocket.lossyScale.x;

            // the rooster's place in the leader's arms
            var chest = new GameObject("RoosterChest").transform;
            chest.SetParent(spine, false);
            chest.position = spine.position + fwd * 0.38f - Vector3.up * 0.18f;
            chest.rotation = Quaternion.LookRotation(right, Vector3.up);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f); capsule.radius = 0.35f; capsule.height = 1.8f;
            var health = root.AddComponent<Health>();
            var hso = new SerializedObject(health); hso.FindProperty("max").floatValue = NinjaHealth; hso.ApplyModifiedPropertiesWithoutUndo();
            var bar = BuildBar(root.transform, health);

            var ninja = root.AddComponent<NinjaEnemy>();
            var so = new SerializedObject(ninja);
            so.FindProperty("animator").objectReferenceValue = model.GetComponent<Animator>();
            so.FindProperty("sword").objectReferenceValue = swordPivot;
            so.FindProperty("handSocket").objectReferenceValue = handSocket;
            so.FindProperty("backSocket").objectReferenceValue = backSocket;
            so.FindProperty("healthBar").objectReferenceValue = bar;
            var rs = so.FindProperty("renderers");
            var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            rs.arraySize = smrs.Length;
            for (int i = 0; i < smrs.Length; i++) rs.GetArrayElementAtIndex(i).objectReferenceValue = smrs[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);
        }

        static WorldHealthBar BuildBar(Transform parent, Health health)
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSprite);
            var go = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.localPosition = new Vector3(0f, 2.15f, 0f); rt.sizeDelta = new Vector2(110f, 13f); rt.localScale = Vector3.one * 0.01f;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            UImage Img(string n, Color c, Vector2 inset, bool filled)
            {
                var o = new GameObject(n, typeof(RectTransform), typeof(UImage));
                o.transform.SetParent(go.transform, false);
                var r = (RectTransform)o.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = -inset; r.offsetMax = inset;
                var img = o.GetComponent<UImage>(); img.sprite = white; img.color = c; img.raycastTarget = false;
                if (filled) { img.type = UImage.Type.Filled; img.fillMethod = UImage.FillMethod.Horizontal; img.fillOrigin = 0; }
                return img;
            }
            Img("Frame", new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, 2f), false);
            var trail = Img("Trail", new Color(1f, 0.8f, 0.45f), Vector2.zero, true);
            var fill = Img("Fill", new Color(0.85f, 0.18f, 0.15f), Vector2.zero, true);
            var bar = go.AddComponent<WorldHealthBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("group").objectReferenceValue = go.GetComponent<CanvasGroup>();
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("trail").objectReferenceValue = trail;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.GetComponent<CanvasGroup>().alpha = 0f;
            return bar;
        }

        // ---------------------------------------------------------------- dialogue

        static string[] BuildDialogues()
        {
            var sets = new (string name, (string speaker, string text)[] lines)[]
            {
                ("Dialogue_RoosterQuest_Intro", new[]
                {
                    ("", "Giữa vòng đá cổ trên đường núi, Gà Chín Cựa đang thong thả mổ thóc..."),
                    (Leader, "Gà chín cựa đây rồi! Bắt lấy, mang về cho chủ nhân."),
                    (Ninja, "Dễ như trở bàn tay!"),
                    (Hero, "Dừng tay! Gà chín cựa là sính lễ dâng Vua Hùng. Bỏ nó xuống!"),
                    (Ninja, "Một mình ngươi mà đòi cản bọn ta sao?"),
                    (Leader, "Hai đứa bay, xử nó! Ta giữ con gà."),
                    (Hero, "Vậy thì để núi rừng phân xử!"),
                }),
                ("Dialogue_RoosterQuest_Flee", new[]
                {
                    (Leader, "Chết tiệt... hai đứa vô dụng! Rút!"),
                    (Hero, "Con gà chạy mất rồi! Phải bắt nó lại thôi."),
                }),
            };
            var paths = new List<string>();
            foreach (var (name, lines) in sets)
            {
                string path = $"{DialogueDir}/{name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
                if (asset == null) { asset = ScriptableObject.CreateInstance<DialogueSequence>(); AssetDatabase.CreateAsset(asset, path); }
                var so = new SerializedObject(asset);
                var arr = so.FindProperty("lines");
                arr.arraySize = lines.Length;
                for (int i = 0; i < lines.Length; i++)
                {
                    var e = arr.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("speaker").stringValue = lines[i].speaker;
                    e.FindPropertyRelative("text").stringValue = lines[i].text;
                    e.FindPropertyRelative("illustration").objectReferenceValue = null;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                paths.Add(path);
            }
            AssetDatabase.SaveAssets();
            return paths.ToArray();
        }

        // ---------------------------------------------------------------- the quest in the scene

        static Terrain terrain;
        static Vector3 Ground(Vector3 p) { p.y = terrain.SampleHeight(p) + terrain.transform.position.y; return p; }

        static Transform Mark(Transform parent, string name, Vector3 pos, Vector3 look)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = Ground(pos);
            Vector3 d = look - pos; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(d);
            return t;
        }

        static Transform Shot(Transform parent, string name, Vector3 pos, Vector3 look)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = pos; t.rotation = Quaternion.LookRotation(look - pos);
            return t;
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Rooster Quest (Son Tinh map)")]
        public static void BuildQuest()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var dialoguePaths = BuildDialogues();
            var giftSo = new SerializedObject(AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath));
            giftSo.FindProperty("receivedMessage").stringValue = "Bạn đã nhận được {0}";
            giftSo.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var dialogues = dialoguePaths.Select(AssetDatabase.LoadAssetAtPath<DialogueSequence>).ToArray();
            var gift = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            terrain = Object.FindFirstObjectByType<Terrain>();
            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            // the old walk-in pickup: the rooster now comes from the thieves
            foreach (var pickup in Object.FindObjectsByType<GiftPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (pickup.Gift == gift) pickup.gameObject.SetActive(false);

            // the stone ring (SonTinhMapBuilder: the second stop, a ring of boulders 11.5 m round it, the path coming in from lower z)
            Vector2 s = SonTinhMapBuilder.Stop(1);
            float z = s.y;
            Vector3 t = new Vector3((SonTinhMapBuilder.PathX(z + 1f) - SonTinhMapBuilder.PathX(z - 1f)) / 2f, 0f, 1f).normalized;
            Vector3 side = new(t.z, 0f, -t.x);
            Vector3 c = Ground(new Vector3(s.x, 0f, s.y));

            var root = new GameObject(Root).transform;
            var marks = new GameObject("Marks").transform; marks.SetParent(root, false);
            var centre = Mark(marks, "RingCentre", c, c + t);
            var playerMark = Mark(marks, "SonTinhMark", c - t * 8f, c);
            var spots = new[] { Mark(marks, "NinjaSpot1", c + side * 2.2f + t * 1.4f, c - t * 8f), Mark(marks, "NinjaSpot2", c - side * 2.2f + t * 1.4f, c - t * 8f) };
            var entries = new[] { Mark(marks, "NinjaEntry1", c + t * 21f + side * 2.5f, c), Mark(marks, "NinjaEntry2", c + t * 21f - side * 2.5f, c) };
            var leaderEntry = Mark(marks, "LeaderEntry", c + t * 23f, c);
            var leaderGrab = Mark(marks, "LeaderGrab", c + t * 1.1f, c);
            var leaderEdge = Mark(marks, "LeaderEdge", c + t * 7.5f + side * 3f, c - t * 8f);
            var leaderExit = Mark(marks, "LeaderExit", c + t * 34f, c + t * 40f);

            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            NinjaEnemy Spawn(string name, Transform at)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(enemy, root);
                go.name = name;
                go.transform.SetPositionAndRotation(at.position, at.rotation);
                return go.GetComponent<NinjaEnemy>();
            }
            var fighters = new[] { Spawn("Ninja_1", spots[0]), Spawn("Ninja_2", spots[1]) };
            var leader = Spawn("Ninja_Leader", leaderEdge);
            leader.transform.Find("HealthBar").gameObject.SetActive(false);   // the leader does not fight (invulnerable: RoosterQuestDirector)
            var leaderChest = leader.GetComponentsInChildren<Transform>().First(x => x.name == "RoosterChest");

            // the rooster: a holder with the runner + E prompt, the animated model inside
            var roosterGo = new GameObject("GaChinCua");
            roosterGo.transform.SetParent(root, false);
            roosterGo.transform.position = c;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RoosterPrefab), roosterGo.transform);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one * 1.1f;
            var interact = roosterGo.AddComponent<Interactable>();
            var iso = new SerializedObject(interact); iso.FindProperty("radius").floatValue = 1.6f; iso.FindProperty("prompt").stringValue = "Nhấn E để bắt gà"; iso.ApplyModifiedPropertiesWithoutUndo();
            var runner = roosterGo.AddComponent<RoosterRunner>();
            var rso = new SerializedObject(runner);
            rso.FindProperty("animator").objectReferenceValue = model.GetComponentInChildren<Animator>();
            rso.FindProperty("gift").objectReferenceValue = gift;
            rso.FindProperty("arenaCenter").objectReferenceValue = centre;
            rso.FindProperty("arenaRadius").floatValue = RingRadius - 1f;
            rso.ApplyModifiedPropertiesWithoutUndo();

            // cutscene camera + one shot per intro line
            var camGo = new GameObject("RoosterCutsceneCam");
            camGo.transform.SetParent(root, false);
            var cam = camGo.AddComponent<CinemachineCamera>();
            cam.Priority = 100;
            cam.Lens.FieldOfView = 45f;
            camGo.SetActive(false);
            var shotRoot = new GameObject("Shots").transform; shotRoot.SetParent(root, false);
            Vector3 up = Vector3.up;
            Vector3 mid = (spots[0].position + spots[1].position) / 2f;
            var shots = new[]
            {
                Shot(shotRoot, "0_Ring", c - t * 6.5f + up * 2.2f + side * 2f, c + up * 0.4f),
                Shot(shotRoot, "1_ThievesComeIn", c + side * 12f + up * 8f - t * 3f, c + t * 6f),
                Shot(shotRoot, "2_LeaderGrabs", leaderGrab.position + side * 2.6f - t * 2.4f + up * 1.6f, leaderGrab.position + up * 1.1f),
                Shot(shotRoot, "3_SonTinh", playerMark.position - t * 3.6f + side * 1.2f + up * 2.1f, c + up * 1.2f),
                Shot(shotRoot, "4_TwoNinja", c - t * 3.2f - side * 1.8f + up * 1.5f, mid + up * 1.3f),
                Shot(shotRoot, "5_LeaderBacksOff", leaderEdge.position - t * 6.5f - side * 4f + up * 2.2f, leaderEdge.position + up * 1.1f),
                Shot(shotRoot, "6_Draw", c - side * 9f - t * 3f + up * 2.4f, mid + up * 1f),
            };
            foreach (var shot in shots) shot.position = new Vector3(shot.position.x, Mathf.Max(shot.position.y, Ground(shot.position).y + 1.2f), shot.position.z);

            var runnerUi = Object.FindObjectsByType<DialogueRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (runnerUi == null) Debug.LogError("RoosterQuestBuilder: no DialogueRunner in Map_SonTinh (Build Horse Quest copies one in).");

            var director = root.gameObject.AddComponent<RoosterQuestDirector>();
            var d = new SerializedObject(director);
            void Arr(string prop, Object[] items) { var p = d.FindProperty(prop); p.arraySize = items.Length; for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i]; }
            Arr("fighters", fighters);
            d.FindProperty("leader").objectReferenceValue = leader;
            d.FindProperty("rooster").objectReferenceValue = runner;
            d.FindProperty("leaderChest").objectReferenceValue = leaderChest;
            d.FindProperty("arenaCenter").objectReferenceValue = centre;
            d.FindProperty("arenaRadius").floatValue = RingRadius;
            Arr("fighterEntries", entries);
            Arr("fighterSpots", spots);
            d.FindProperty("leaderEntry").objectReferenceValue = leaderEntry;
            d.FindProperty("leaderGrabSpot").objectReferenceValue = leaderGrab;
            d.FindProperty("leaderEdge").objectReferenceValue = leaderEdge;
            d.FindProperty("leaderExit").objectReferenceValue = leaderExit;
            d.FindProperty("playerMark").objectReferenceValue = playerMark;
            d.FindProperty("runner").objectReferenceValue = runnerUi;
            d.FindProperty("intro").objectReferenceValue = dialogues[0];
            d.FindProperty("flee").objectReferenceValue = dialogues[1];
            d.FindProperty("cutsceneCamera").objectReferenceValue = cam;
            Arr("shots", shots);
            d.FindProperty("gift").objectReferenceValue = gift;
            d.ApplyModifiedPropertiesWithoutUndo();
            ArenaBoundaryBuilder.AddRoosterBoundary(director);   // the player stays in the ring during the fight

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"RoosterQuestBuilder: quest built at the stone ring {c}");
        }

        // ---------------------------------------------------------------- check images (batch)

        static void Snap(Camera cam, string path)
        {
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        }

        // the cutscene shots as built (Ninja standing on their marks, the leader holding the rooster)
        static void RenderSceneShots(string dir)
        {
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find(Root).transform;
            var leader = root.Find("Ninja_Leader");
            var rooster = root.Find("GaChinCua");
            rooster.SetParent(leader.GetComponentsInChildren<Transform>().First(x => x.name == "RoosterChest"), false);
            rooster.localPosition = Vector3.zero; rooster.localRotation = Quaternion.identity;
            var camGo = new GameObject("ShotCam"); var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 45f; cam.farClipPlane = 600f;
            foreach (Transform shot in root.Find("Shots"))
            {
                camGo.transform.SetPositionAndRotation(shot.position, shot.rotation);
                Snap(cam, Path.Combine(dir, $"shot_{shot.name}.png"));
            }
            Object.DestroyImmediate(camGo);
        }

        // only the Ninja check images (after BuildNinja): -gaShots <dir>
        public static void NinjaShotsBatch()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-gaShots");
            RenderNinjaShots(args[i + 1]);
        }

        // the enemy prefab with its sword: each clip at 30 / 60 % (CPU-skinned, edit mode skins once per editor frame)
        static void RenderNinjaShots(string dir)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Sun"); var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.6f; lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);
            var ninja = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath));
            PrefabUtility.UnpackPrefabInstance(ninja, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);   // edit mode: no reparenting inside a prefab instance
            var model = ninja.transform.Find("Model").gameObject;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var swordT = ninja.GetComponentsInChildren<Transform>(true).First(x => x.name == "Sword");
            var hand = ninja.GetComponentsInChildren<Transform>(true).First(x => x.name == "SwordHand");
            var camGo = new GameObject("Cam"); var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.36f, 0.37f, 0.42f); cam.fieldOfView = 35f;
            foreach (var clipSpec in Spec.Clips)
            {
                var clip = NpcBuilder.LoadClip(Spec, clipSpec.File);
                bool armed = clipSpec.State != "Idle" && clipSpec.State != "Hold" && clipSpec.State != "Draw";
                foreach (float f in new[] { 0.3f, 0.6f })
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(model, clip, clip.length * f);
                    AnimationMode.EndSampling();
                    // after sampling: animation mode puts the hierarchy back the way it found it
                    if (armed) { swordT.SetParent(hand, false); swordT.localPosition = Vector3.zero; swordT.localRotation = Quaternion.identity; }
                    var mesh = NpcBuilder.SkinToWorld(smr);
                    var baked = new GameObject("baked");
                    baked.AddComponent<MeshFilter>().sharedMesh = mesh;
                    baked.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                    smr.enabled = false;
                    foreach (var (tag, yaw) in new[] { ("q", 35f), ("side", 95f) })
                    {
                        Vector3 dir3 = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        Vector3 aimAt = mesh.bounds.center; aimAt.y = Mathf.Clamp(aimAt.y, 0.6f, 1.3f);
                        camGo.transform.position = aimAt + Vector3.up * 0.2f + dir3 * 3.4f;
                        camGo.transform.LookAt(aimAt);
                        Snap(cam, Path.Combine(dir, $"ninja_{clipSpec.State}_{(int)(f * 100)}_{tag}.png"));
                    }
                    Object.DestroyImmediate(mesh); Object.DestroyImmediate(baked);
                    smr.enabled = true;
                    AnimationMode.StopAnimationMode();
                }
            }
            Debug.Log("RoosterQuestBuilder: Ninja check images written to " + dir);
        }
    }
}
