using System.Linq;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Environment;
using SonTinhThuyTinh.Quest;
using UnityEditor;
using UnityEngine;
using static SonTinhThuyTinh.EditorTools.SonTinhMapBuilder;

namespace SonTinhThuyTinh.EditorTools
{
    // The story events of Map_ThuyTinh (docs/ke-hoach-thuy-tinh-su-kien.md), built under Generated/SeaEvents by Build Thuy Tinh Map after
    // the sea and the old gift setups. The dialogue box is copied from the Prologue (once per build, under Generated), the dialogue
    // texts are assets Assets/Data/Dialogue/Dialogue_Sea_*.asset.
    //  - Shark Bay: Mập Chín Vây (SeaBossArenaEvent), a net across the way out that goes when it is beaten.
    public static partial class ThuyTinhMapBuilder
    {
        const string DialogueDir = "Assets/Data/Dialogue";
        const string SharkPrefabPath = "Assets/Prefabs/Characters/ManyFinnedShark.prefab";
        const string SharkGiftPath = "Assets/Data/Gifts/Gift_Map9Vay.asset";
        const string Hero = "Thủy Tinh";

        static void BuildEvents(Transform root)
        {
            var scene = root.gameObject.scene;
            Transform events = Group(root, "SeaEvents");
            DialogueRunner runner = EnsureDialogueUi(scene, root);
            root.gameObject.AddComponent<DeepWaterGuard>();   // the open sea is not for wading (nets and barriers cannot be waded round)
            BuildSharkEvent(events, runner);
            BuildTurtleEvent(events, runner);
            BuildStoryEvents(events, runner);
        }

        // ---------------------------------------------------------------- the flooded shrine: Rùa Chín Chân

        const string TurtlePrefabPath = "Assets/Prefabs/Characters/NineLeggedTurtle.prefab";
        const string TurtleGiftPath = "Assets/Data/Gifts/Gift_Rua9Chan.asset";

        static void BuildTurtleEvent(Transform events, DialogueRunner runner)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TurtlePrefabPath);
            if (prefab == null) { Debug.LogError("Turtle prefab missing: " + TurtlePrefabPath); return; }
            Transform ev = Group(events, "TurtleEvent");

            // the old walk-in turtle on the shrine's slab (touching it used to load Battle_Rua9Chan): switched off
            foreach (var trigger in Object.FindObjectsByType<NineLeggedTurtleEncounterTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                trigger.gameObject.SetActive(false);

            Vector2 s = Stop(2), t = Tangent(StopZ[2]);
            Vector3 forward = new(t.x, 0f, t.y);
            float floorY = GroundHeight(s.x, s.y);
            Transform centre = Mark(ev, "ArenaCentre", new Vector3(s.x, floorY, s.y), forward);
            Vector2 bossAt = s + t * 6f;
            Transform bossMark = Mark(ev, "BossMark", new Vector3(bossAt.x, GroundHeight(bossAt.x, bossAt.y) + 0.55f, bossAt.y), -forward);
            Vector2 back = s - t * 24f;   // on the causeway, outside the arena
            Transform playerMark = Mark(ev, "PlayerMark", new Vector3(back.x, GroundHeight(back.x, back.y) + 0.3f, back.y), forward);

            var bossGo = new GameObject("Rua9Chan_Boss");
            bossGo.transform.SetParent(ev, false);
            bossGo.transform.SetPositionAndRotation(bossMark.position, bossMark.rotation);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, bossGo.transform);
            visual.name = "Rua9Chan_Model";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var capsule = bossGo.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.center = new Vector3(0f, 0f, 0.05f);
            capsule.height = 5.2f;
            capsule.radius = 1.35f;
            var body = bossGo.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            bossGo.AddComponent<Health>();
            var ai = bossGo.AddComponent<NineLeggedTurtleBoss>();
            ai.enabled = false;

            var intro = SaveDialogue("Turtle_Intro",
                ("", "Giữa đền chìm, mặt nước phẳng như gương. Linh Quy chín chân từ từ nhô lên khỏi nước, mai phủ rêu ngọc sáng mờ."),
                ("Rùa Chín Chân", "Sóng đã kể cho ta nghe: ngư tặc tan, hải đăng sáng, nước các vịnh lắng lại. Ngươi làm được nhiều điều rồi, Thủy Tinh."),
                (Hero, "Ta đến xin Rùa Chín Chân làm sính lễ cuối cùng dâng Vua Hùng."),
                ("Rùa Chín Chân", "Người trị thủy không chỉ biết đánh. Phải biết giữ, biết né, biết đợi sóng lên. Hãy nếm vòng xoáy của mai ta."),
                ("", "Linh Quy hạ thấp thân mình. Ngọc trên mai rực sáng, nước quanh đền bắt đầu rung."),
                (Hero, "Ta sẵn sàng. Xin hãy ra tay!"));

            var evt = ev.gameObject.AddComponent<SeaBossArenaEvent>();
            var so = new SerializedObject(evt);
            so.FindProperty("boss").objectReferenceValue = ai;
            so.FindProperty("gift").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GiftItem>(TurtleGiftPath);
            so.FindProperty("arenaCentre").objectReferenceValue = centre;
            so.FindProperty("bossMark").objectReferenceValue = bossMark;
            so.FindProperty("playerMark").objectReferenceValue = playerMark;
            so.FindProperty("runner").objectReferenceValue = runner;
            so.FindProperty("intro").objectReferenceValue = intro;
            so.FindProperty("key").stringValue = "Turtle";
            so.FindProperty("title").stringValue = "RÙA CHÍN CHÂN";
            so.FindProperty("accent").colorValue = new Color(0.45f, 1f, 0.6f);
            so.FindProperty("triggerRadius").floatValue = 15f;
            so.FindProperty("arenaRadius").floatValue = 12f;
            so.FindProperty("bossMaxHealth").floatValue = 260f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The Prologue's dialogue box and the "Nhấn E" prompt, parented under Generated so the next build removes them with it.
        static DialogueRunner EnsureDialogueUi(UnityEngine.SceneManagement.Scene scene, Transform root)
        {
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == "DialogueCanvas" || g.name == "Dialogue")) Object.DestroyImmediate(old);
            DialogueRunner runner = HorseQuestBuilder.CopyDialogueUi(scene);
            scene.GetRootGameObjects().First(g => g.name == "DialogueCanvas").transform.SetParent(root, true);
            runner.transform.SetParent(root, true);
            HorseQuestBuilder.BuildPrompt(root, runner);
            return runner;
        }

        // Writes (or rewrites) Assets/Data/Dialogue/Dialogue_Sea_<name>.asset and returns it.
        static DialogueSequence SaveDialogue(string name, params (string who, string text)[] lines)
        {
            string path = $"{DialogueDir}/Dialogue_Sea_{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<DialogueSequence>(); AssetDatabase.CreateAsset(asset, path); }
            var so = new SerializedObject(asset);
            var arr = so.FindProperty("lines");
            arr.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("speaker").stringValue = lines[i].who;
                e.FindPropertyRelative("text").stringValue = lines[i].text;
                e.FindPropertyRelative("illustration").objectReferenceValue = null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static Transform Mark(Transform parent, string name, Vector3 at, Vector3 forward)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(at, forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : Quaternion.identity);
            return t;
        }

        // ---------------------------------------------------------------- Shark Bay

        static void BuildSharkEvent(Transform events, DialogueRunner runner)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharkPrefabPath);
            if (prefab == null) { Debug.LogError("Shark prefab missing: " + SharkPrefabPath); return; }
            Transform ev = Group(events, "SharkEvent");

            // the old walk-in shark on the first bank (touching it used to load Battle_Map9Vay): switched off
            foreach (var trigger in Object.FindObjectsByType<ManyFinnedSharkEncounterTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                trigger.gameObject.SetActive(false);

            float floorY = WaterY - PoolDepth;
            Vector2 c = PoolCentre, enter = PoolEntranceDir, exit = PoolExitDir;
            Transform centre = Mark(ev, "ArenaCentre", new Vector3(c.x, floorY, c.y), new Vector3(enter.x, 0f, enter.y));
            Vector2 bossAt = c + exit * 7f;
            Transform bossMark = Mark(ev, "BossMark", new Vector3(bossAt.x, floorY + 1.15f, bossAt.y), new Vector3(enter.x, 0f, enter.y));
            Vector2 back = PoolEntrance;   // the end of the pier outside the ring: where the player comes back after a defeat
            Transform playerMark = Mark(ev, "PlayerMark", new Vector3(back.x, PierDeck + 0.1f, back.y), new Vector3(-enter.x, 0f, -enter.y));

            // the boss: root (the AI moves it, forward = +Z) with the model inside, like Battle_Map9Vay (ManyFinnedSharkBattleBuilder.BuildBoss)
            var bossGo = new GameObject("Map9Vay_Boss");
            bossGo.transform.SetParent(ev, false);
            bossGo.transform.SetPositionAndRotation(bossMark.position, bossMark.rotation);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, bossGo.transform);
            visual.name = "Map9Vay_Model";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var swimmer = visual.GetComponent<ManyFinnedSharkSwimmer>();
            if (swimmer != null) Object.DestroyImmediate(swimmer);
            var capsule = bossGo.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.center = new Vector3(0f, 0f, 0.15f);
            capsule.height = 8.4f;
            capsule.radius = 2.1f;
            var body = bossGo.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            bossGo.AddComponent<Health>();
            var ai = bossGo.AddComponent<ManyFinnedSharkBoss>();
            ai.enabled = false;

            // the net across the way out (a gap of about 11 m in the ring): a collider and a grid of ropes, gone when the shark is beaten
            var net = BuildBarrier(ev, "SharkNet", PoolCentre + exit * (PoolRingRadius - 1.5f), exit, 13f, 6f, new Color(0.78f, 0.70f, 0.50f));

            var intro = SaveDialogue("Shark_Intro",
                ("", "Giữa vịnh kín nước xanh thẫm, một bóng vây khổng lồ lướt chậm dọc vách đá. Chín chiếc vây xé nước như chín lưỡi dao."),
                ("Mập Chín Vây", "Lại thêm một kẻ nữa tới quấy vịnh của ta! Bọn ngư tặc đốt thuyền, xé lưới, giờ đến lượt ngươi sao?"),
                (Hero, "Ta là Thủy Tinh, chúa vùng nước. Ta đến để dẹp bọn ngư tặc, và xin Mập Chín Vây làm sính lễ dâng Vua Hùng."),
                ("Mập Chín Vây", "Chúa vùng nước mà để vịnh nhà ra nông nỗi này ư? Muốn ta tin, hãy cho ta thấy ngươi giữ được dòng nước."),
                ("", "Chín chiếc vây dựng đứng. Nước trong vịnh cuộn thành xoáy quanh bóng cá khổng lồ."),
                (Hero, "Ta chấp nhận thử thách. Hãy ra tay!"));

            var evt = ev.gameObject.AddComponent<SeaBossArenaEvent>();
            var so = new SerializedObject(evt);
            so.FindProperty("boss").objectReferenceValue = ai;
            so.FindProperty("gift").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GiftItem>(SharkGiftPath);
            so.FindProperty("arenaCentre").objectReferenceValue = centre;
            so.FindProperty("bossMark").objectReferenceValue = bossMark;
            so.FindProperty("playerMark").objectReferenceValue = playerMark;
            so.FindProperty("runner").objectReferenceValue = runner;
            so.FindProperty("intro").objectReferenceValue = intro;
            so.FindProperty("key").stringValue = "Shark";
            so.FindProperty("title").stringValue = "MẬP CHÍN VÂY";
            so.FindProperty("accent").colorValue = new Color(0.55f, 0.92f, 1f);
            so.FindProperty("triggerRadius").floatValue = PoolRadius + 1f;
            so.FindProperty("arenaRadius").floatValue = PoolRadius - 2.5f;
            so.FindProperty("bossMaxHealth").floatValue = 220f;
            var openOnWin = so.FindProperty("openOnWin");
            openOnWin.arraySize = 1;
            openOnWin.GetArrayElementAtIndex(0).objectReferenceValue = net;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Fishing net / chain hung across a gap or a pier: ropes on posts with floats, standing on the sea floor, with a box collider
        // (the whole width and a good deal higher than the deck). `forward` is the direction the net faces (across which one passes).
        static GameObject BuildBarrier(Transform parent, string name, Vector2 at, Vector2 forward, float width, float height, Color ropeColor)
        {
            var net = new GameObject(name).transform;
            net.SetParent(parent, false);
            net.SetPositionAndRotation(new Vector3(at.x, GroundHeight(at.x, at.y), at.y), Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.y)));
            var wall = net.gameObject.AddComponent<BoxCollider>();
            wall.size = new Vector3(width, height + 1.5f, 0.8f);
            wall.center = new Vector3(0f, (height + 1.5f) * 0.5f, 0f);

            Material rope = MapDecor.Lit("Mat_NetRope_" + ColorUtility.ToHtmlStringRGB(ropeColor), ropeColor, 0f, 0.1f);
            Material wood = MapDecor.Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            Material buoy = MapDecor.Lit("Mat_Buoy", new Color(0.95f, 0.45f, 0.15f), 0f, 0.4f);
            foreach (float x in new[] { -width * 0.5f, 0f, width * 0.5f })
                MapDecor.Part(PrimitiveType.Cylinder, "Post", net, new Vector3(x, height * 0.5f + 0.7f, 0f), new Vector3(0.3f, height * 0.5f + 0.7f, 0.3f), Quaternion.identity, wood);
            for (float x = -width * 0.5f; x <= width * 0.5f + 0.01f; x += 0.8f)
                MapDecor.Part(PrimitiveType.Cube, "NetV", net, new Vector3(x, height * 0.5f + 0.5f, 0f), new Vector3(0.05f, height + 1f, 0.05f), Quaternion.identity, rope);
            for (float y = 0.4f; y <= height + 0.9f; y += 0.8f)
                MapDecor.Part(PrimitiveType.Cube, "NetH", net, new Vector3(0f, y, 0f), new Vector3(width, 0.05f, 0.05f), Quaternion.identity, rope);
            for (float x = -width * 0.5f + 0.8f; x < width * 0.5f; x += 1.6f)
                MapDecor.Part(PrimitiveType.Sphere, "Float", net, new Vector3(x, height + 0.9f, 0f), Vector3.one * 0.4f, Quaternion.identity, buoy);
            return net.gameObject;
        }
    }
}
