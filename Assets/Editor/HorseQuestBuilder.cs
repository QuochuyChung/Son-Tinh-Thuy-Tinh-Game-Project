using System.Linq;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Quest;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SonTinhThuyTinh.EditorTools
{
    // The horse keeper's errand in Assets/Scenes/Map_SonTinh.unity (the existing map, not a new scene): everything goes under one root
    // "HorseQuest" that is deleted and rebuilt on every run.
    //  - In the hill village (bay 0 of SonTinhMapBuilder) beside the west stilt house: the Meshy stable (VillageModelSetup) with a hay trough
    //    (block stable without the model) with Ngựa Chín Hồng Mao (prefab NguaChinHongMao, EatGrass at the trough), a haystack, a rail fence and Idyllic plants round
    //    it (DressYard), and the keeper (the old Sơn Tinh v1 model) at the
    //    house's ladder with Interactable + HorseQuestVillager.
    //  - On the mountain path at the old horse stop (z = 85): the heirloom bell (Meshy model + light beam, HeirloomPickup),
    //    shown once the keeper asks for it. The old walk-in pickup Pickup_NguaChinHongMao is switched off: the horse now comes from the keeper.
    //  - UI: the Prologue's dialogue box (copied in, if the map has none) and an "Nhấn E để …" prompt (InteractionPrompt).
    //  - Data: Dialogue_HorseQuest_Offer / _Reminder / _Thanks / _After, and the horse gift's banner text "Bạn đã nhận được {0}".
    // Rebuilding the whole map with Build Son Tinh Map turns Pickup_NguaChinHongMao back on: run this menu again afterwards.
    public static class HorseQuestBuilder
    {
        const string ScenePath = "Assets/Scenes/Map_SonTinh.unity";
        const string ProloguePath = "Assets/Scenes/Prologue.unity";
        const string VillagerPrefab = "Assets/Prefabs/Characters/SonTinh.prefab";   // the v1 Sơn Tinh model, not used by the player any more
        const string GiftPath = "Assets/Data/Gifts/Gift_NguaChinHongMao.asset";
        const string DialogueDir = "Assets/Data/Dialogue";
        const string Keeper = "Người chăn ngựa", Hero = "Sơn Tinh";

        // must match SonTinhMapBuilder: the village is bay 0 (z 45, 36 m left of the path), the horse stop is the first stop (z 85)
        static float PathX(float z)
        {
            float t = Mathf.Clamp01(z / 50f); float ramp = t * t * (3f - 2f * t);
            return ramp * (15f * Mathf.Sin(z * 0.021f) + 6f * Mathf.Sin(z * 0.058f + 1.3f));
        }
        static Vector2 Village => new(PathX(45f) - 36f, 45f);
        static Vector2 HorseStop => new(PathX(85f), 85f);

        static Terrain terrain;
        static Vector3 Ground(Vector2 p) => new(p.x, terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y, p.y);

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Horse Quest (Son Tinh map)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var dialoguePaths = BuildDialogues();
            var gift = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            var gso = new SerializedObject(gift);
            gso.FindProperty("receivedMessage").stringValue = "Bạn đã nhận được {0}";
            gso.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // opening the scene unloads the in-memory copies of the assets made above: load them again by path
            var dialogues = dialoguePaths.Select(AssetDatabase.LoadAssetAtPath<DialogueSequence>).ToArray();
            gift = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            terrain = Object.FindFirstObjectByType<Terrain>();
            // previous build (the dialogue UI used to sit at the scene root)
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == "HorseQuest" || g.name == "DialogueCanvas" || g.name == "Dialogue")) Object.DestroyImmediate(old);
            var root = new GameObject("HorseQuest").transform;

            // the old walk-in pickup: off (the keeper gives the horse now)
            foreach (var pickup in Object.FindObjectsByType<GiftPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (pickup.Gift == gift) pickup.gameObject.SetActive(false);

            // ---- the stable and the horse, west of the village yard beside the west stilt house ----
            Vector2 village = Village;
            Vector2 house = village + new Vector2(-6.8f, 2.5f);          // SonTinhMapBuilder.BuildBays, sign -1
            Vector2 stableAt = village + new Vector2(-6.2f, -6.0f);
            float stableYaw = Mathf.Atan2(village.x - stableAt.x, village.y - stableAt.y) * Mathf.Rad2Deg;   // open front towards the yard
            var stable = BuildStable(root, Ground(stableAt), stableYaw, out Vector3 horseSpot, out Vector2 half);
            DressYard(root, stable.transform, half);
            var horseGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HorseBuilder.PrefabPath));
            horseGo.transform.SetParent(root);
            horseGo.transform.SetPositionAndRotation(horseSpot, Quaternion.Euler(0f, stableYaw, 0f));
            var horse = horseGo.GetComponent<HorseStall>();

            // ---- the keeper at the foot of his house's ladder, facing the yard ----
            Vector2 keeperAt = house + (village - house).normalized * 4.6f + new Vector2(0f, -1.4f);   // in the yard, clear of the stilts
            var keeper = new GameObject("HorseKeeper");
            keeper.transform.SetParent(root);
            keeper.transform.position = Ground(keeperAt);
            var keeperModel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VillagerPrefab));
            keeperModel.transform.SetParent(keeper.transform, false);
            keeperModel.transform.localPosition = Vector3.zero;
            keeperModel.transform.rotation = Quaternion.LookRotation(new Vector3(village.x - keeperAt.x, 0f, village.y - keeperAt.y));
            var capsule = keeper.AddComponent<CapsuleCollider>(); capsule.center = new Vector3(0f, 0.95f, 0f); capsule.height = 1.9f; capsule.radius = 0.35f;
            var talk = keeper.AddComponent<Interactable>();
            var tso = new SerializedObject(talk);
            tso.FindProperty("prompt").stringValue = "Nhấn E để nói chuyện";
            tso.FindProperty("radius").floatValue = 2.8f;
            tso.ApplyModifiedPropertiesWithoutUndo();

            // ---- the bronze bell at the old horse stop on the mountain path ----
            Vector2 bellAt = HorseStop + new Vector2(2.5f, 1.5f);
            var bell = BuildBell(root, Ground(bellAt));
            float distance = Vector2.Distance(keeperAt, bellAt);

            // ---- UI ----
            var runner = CopyDialogueUi(scene);
            scene.GetRootGameObjects().First(g => g.name == "DialogueCanvas").transform.SetParent(root, true);
            runner.transform.SetParent(root, true);
            BuildPrompt(root, runner);

            var villager = keeper.AddComponent<HorseQuestVillager>();
            var vso = new SerializedObject(villager);
            vso.FindProperty("runner").objectReferenceValue = runner;
            vso.FindProperty("offer").objectReferenceValue = dialogues[0];
            vso.FindProperty("reminder").objectReferenceValue = dialogues[1];
            vso.FindProperty("thanks").objectReferenceValue = dialogues[2];
            vso.FindProperty("afterwards").objectReferenceValue = dialogues[3];
            vso.FindProperty("horseGift").objectReferenceValue = gift;
            vso.FindProperty("horse").objectReferenceValue = horse;
            vso.FindProperty("body").objectReferenceValue = keeperModel.transform;
            vso.ApplyModifiedPropertiesWithoutUndo();

            int relinked = RelinkDialogueRunner(scene, runner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"HorseQuestBuilder: {relinked} link(s) to the dialogue runner restored.");
            Debug.Log($"HorseQuestBuilder: built. Village yard {village}, stable {stableAt}, keeper {keeperAt}, bell {bellAt} ({distance:F0} m from the keeper)");
        }

        // ---------- stable: the Meshy thatched bamboo stable (VillageModelSetup), the gate side to the yard, a hay trough inside its front
        // fence; without the model: 4.6 m deep, 3.4 m wide, open front with the hay trough, roof on four posts ----------

        static GameObject BuildStable(Transform parent, Vector3 ground, float yaw, out Vector3 horseSpot, out Vector2 half)
        {
            Transform s = MapDecor.Empty("Stable", parent, ground, yaw);
            var model = VillageModelSetup.Prefab(VillageModelSetup.Kind.Stable);
            if (model != null) return BuildModelStable(s, model, out horseSpot, out half);
            half = new Vector2(1.7f, 2.3f);
            Material wood = MapDecor.Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            Material plank = MapDecor.Lit("Mat_Plank", new Color(0.45f, 0.31f, 0.18f), 0f, 0.1f);
            Material thatch = MapDecor.Lit("Mat_Thatch", new Color(0.50f, 0.40f, 0.20f), 0f, 0.05f, true);
            Material hay = MapDecor.Lit("Mat_Hay", new Color(0.78f, 0.68f, 0.30f), 0f, 0.05f);
            Material grass = MapDecor.Lit("Mat_FreshGrass", new Color(0.36f, 0.62f, 0.22f), 0f, 0.1f);
            const float w = 3.4f, d = 4.6f, postH = 2.7f;
            // +Z local = the open front (towards the yard)
            MapDecor.Part(PrimitiveType.Cube, "Floor", s, new Vector3(0f, 0.06f, 0f), new Vector3(w, 0.16f, d), Quaternion.identity, plank, true);
            foreach (float x in new[] { -w / 2f, w / 2f })
                foreach (float z in new[] { -d / 2f, 0f, d / 2f })
                    MapDecor.Part(PrimitiveType.Cylinder, "Post", s, new Vector3(x, postH / 2f, z), new Vector3(0.18f, postH / 2f, 0.18f), Quaternion.identity, wood, true);
            // fences: back and both sides, two rails each, plus a solid board at the bottom (keeps the horse in, blocks the player)
            foreach (float y in new[] { 0.55f, 1.15f })
            {
                MapDecor.Part(PrimitiveType.Cube, "RailBack", s, new Vector3(0f, y, -d / 2f), new Vector3(w, 0.12f, 0.08f), Quaternion.identity, wood, true);
                foreach (float x in new[] { -w / 2f, w / 2f })
                    MapDecor.Part(PrimitiveType.Cube, "RailSide", s, new Vector3(x, y, 0f), new Vector3(0.08f, 0.12f, d), Quaternion.identity, wood, true);
            }
            MapDecor.Part(PrimitiveType.Cube, "BoardBack", s, new Vector3(0f, 0.3f, -d / 2f), new Vector3(w, 0.4f, 0.06f), Quaternion.identity, plank, true);
            foreach (float x in new[] { -w / 2f, w / 2f })
                MapDecor.Part(PrimitiveType.Cube, "BoardSide", s, new Vector3(x, 0.3f, 0f), new Vector3(0.06f, 0.4f, d), Quaternion.identity, plank, true);
            // roof: one slab sloping to the back, overhanging
            MapDecor.Part(PrimitiveType.Cube, "Roof", s, new Vector3(0f, postH + 0.35f, 0.1f), new Vector3(w + 0.9f, 0.14f, d + 1.0f), Quaternion.Euler(-12f, 0f, 0f), thatch);
            MapDecor.Part(PrimitiveType.Cube, "Beam", s, new Vector3(0f, postH, d / 2f), new Vector3(w + 0.2f, 0.16f, 0.16f), Quaternion.identity, wood);
            // the hay trough across the open front: the horse eats from it facing the yard (its muzzle ~0.9 m up while grazing)
            MapDecor.Part(PrimitiveType.Cube, "TroughBox", s, new Vector3(0f, 0.55f, d / 2f - 0.35f), new Vector3(w - 0.4f, 0.5f, 0.55f), Quaternion.identity, plank, true);
            MapDecor.Part(PrimitiveType.Cube, "TroughHay", s, new Vector3(0f, 0.82f, d / 2f - 0.35f), new Vector3(w - 0.6f, 0.08f, 0.42f), Quaternion.identity, grass);
            // fresh-cut grass heaped in the trough: Idyllic grass tufts if the pack is there, green blobs otherwise
            var tufts = new[] { "Grass_01", "Grass_02", "Grass_03" }.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(IdyllicPack + n + ".prefab")).Where(p => p != null).ToArray();
            for (int i = 0; i < 5; i++)
            {
                var at = new Vector3(-1.1f + i * 0.55f, 0.86f, d / 2f - 0.35f + ((i % 2) - 0.5f) * 0.12f);
                if (tufts.Length > 0) Prop(tufts[i % tufts.Length], s, at, i * 37f, 0.42f);
                else MapDecor.Part(PrimitiveType.Sphere, "FreshGrass", s, at + Vector3.up * 0.02f, new Vector3(0.45f, 0.16f, 0.32f), Quaternion.Euler(0f, i * 37f, 0f), grass);
            }
            // hay bales and loose hay in the back corner
            MapDecor.Part(PrimitiveType.Cube, "HayBale", s, new Vector3(-w / 2f + 0.55f, 0.4f, -d / 2f + 0.55f), new Vector3(0.8f, 0.5f, 0.6f), Quaternion.Euler(0f, 12f, 0f), hay, true);
            MapDecor.Part(PrimitiveType.Cube, "HayBale", s, new Vector3(-w / 2f + 0.6f, 0.85f, -d / 2f + 0.5f), new Vector3(0.75f, 0.45f, 0.55f), Quaternion.Euler(0f, -8f, 0f), hay, true);
            MapDecor.Part(PrimitiveType.Sphere, "LooseHay", s, new Vector3(0.6f, 0.15f, -0.9f), new Vector3(1.6f, 0.25f, 1.2f), Quaternion.identity, hay);
            // the horse's root is mid-body: 0.75 m in front of the middle puts its muzzle in the trough while grazing (measured on EatGrass)
            horseSpot = s.TransformPoint(new Vector3(0f, 0.14f, 0.75f));
            return s.gameObject;
        }

        static GameObject BuildModelStable(Transform s, GameObject prefab, out Vector3 horseSpot, out Vector2 half)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, s);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            Bounds b = VillageModelSetup.LocalBounds(go, s);
            go.transform.localPosition = Vector3.up * (VillageModelSetup.LowestGround(terrain, s, b) - s.position.y - 0.08f);
            half = new Vector2(b.extents.x, b.extents.z);

            // the horse grazes facing the gate: fresh grass in a trough just inside the front fence, its root 1.2 m behind it (EatGrass)
            Material plank = MapDecor.Lit("Mat_Plank", new Color(0.45f, 0.31f, 0.18f), 0f, 0.1f);
            Material grass = MapDecor.Lit("Mat_FreshGrass", new Color(0.36f, 0.62f, 0.22f), 0f, 0.1f);
            float troughZ = b.max.z - 1.3f, floor = FloorHeight(s, go, new Vector3(0f, 0f, troughZ - 1.2f));
            MapDecor.Part(PrimitiveType.Cube, "TroughBox", s, new Vector3(0f, floor + 0.3f, troughZ), new Vector3(2.4f, 0.5f, 0.5f), Quaternion.identity, plank, true);
            MapDecor.Part(PrimitiveType.Cube, "TroughHay", s, new Vector3(0f, floor + 0.57f, troughZ), new Vector3(2.2f, 0.08f, 0.38f), Quaternion.identity, grass);
            var tufts = new[] { "Grass_01", "Grass_02", "Grass_03" }.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(IdyllicPack + n + ".prefab")).Where(p => p != null).ToArray();
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(-0.8f + i * 0.55f, floor + 0.61f, troughZ + ((i % 2) - 0.5f) * 0.12f);
                if (tufts.Length > 0) Prop(tufts[i % tufts.Length], s, at, i * 37f, 0.42f);
                else MapDecor.Part(PrimitiveType.Sphere, "FreshGrass", s, at + Vector3.up * 0.02f, new Vector3(0.45f, 0.16f, 0.32f), Quaternion.Euler(0f, i * 37f, 0f), grass);
            }
            horseSpot = s.TransformPoint(new Vector3(0f, floor, troughZ - 1.2f));
            return s.gameObject;
        }

        // Top of the stable's floor under a local point (a ray down on its mesh collider), or the terrain if the ray misses.
        static float FloorHeight(Transform s, GameObject model, Vector3 local)
        {
            Physics.SyncTransforms();
            Vector3 top = s.TransformPoint(local) + Vector3.up * 1.5f;
            float best = float.MinValue;
            foreach (var c in model.GetComponentsInChildren<MeshCollider>())
                if (c.Raycast(new Ray(top, Vector3.down), out RaycastHit hit, 3f) && hit.point.y < top.y - 0.4f) best = Mathf.Max(best, hit.point.y);
            if (best == float.MinValue) best = Ground(new Vector2(top.x, top.z)).y;
            return best - s.position.y;
        }

        // ---------- around the stable: a haystack, a rail fence behind, a flowering tree, bushes, flowers and grass tufts ----------
        // No pack has a stilt house, a stable or a fence, so those stay built from blocks; the plants come from Idyllic Fantasy Nature
        // (skipped if it is not imported). Everything stands beside or behind the stable: the yard in front of it stays clear.

        const string IdyllicPack = "Assets/Idyllic Fantasy Nature/Prefabs/";

        static GameObject Prop(GameObject prefab, Transform parent, Vector3 localPosition, float localYaw, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, localYaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            SonTinhMapDressing.Retint(go);   // the pack's lime-yellow greens, tinted to the forest
            return go;
        }

        // `half` = the stable's half width (x) and half depth (z): everything keeps the same gap to it whatever its size.
        static void DressYard(Transform root, Transform stable, Vector2 half)
        {
            float wx = half.x - 1.7f, dz = half.y - 2.3f;   // growth over the old 3.4 x 4.6 m block stable
            var yard = new GameObject("StableYard").transform;
            yard.SetParent(root);
            var rng = new System.Random(845);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            // a point beside the stable given in the stable's frame (+z = its open front), dropped on the ground
            Vector3 At(float x, float z) { Vector3 p = stable.TransformPoint(new Vector3(x, 0f, z)); return Ground(new Vector2(p.x, p.z)); }
            GameObject[] Load(params string[] names) => names.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(IdyllicPack + n + ".prefab")).Where(p => p != null).ToArray();
            GameObject Put(GameObject prefab, Vector3 world, float scale)
            {
                var go = Prop(prefab, yard, Vector3.zero, Rand(0f, 360f), scale);
                go.transform.position = world;
                return go;
            }

            Material wood = MapDecor.Lit("Mat_Wood", new Color(0.33f, 0.22f, 0.12f), 0f, 0.15f);
            Material hay = MapDecor.Lit("Mat_Hay", new Color(0.78f, 0.68f, 0.30f), 0f, 0.05f);

            // the haystack (đống rơm) on the house side: a round stack on a pole, its top rounded off
            Vector3 stack = At(-3.4f - wx, -0.6f);
            var stackRoot = MapDecor.Empty("Haystack", yard, stack, 0f);
            MapDecor.Part(PrimitiveType.Cylinder, "Stack", stackRoot, new Vector3(0f, 0.65f, 0f), new Vector3(1.8f, 0.65f, 1.8f), Quaternion.identity, hay, true);
            MapDecor.Part(PrimitiveType.Sphere, "Top", stackRoot, new Vector3(0f, 1.3f, 0f), new Vector3(1.8f, 1.1f, 1.8f), Quaternion.identity, hay);
            MapDecor.Part(PrimitiveType.Cylinder, "Pole", stackRoot, new Vector3(0f, 1.6f, 0f), new Vector3(0.07f, 0.6f, 0.07f), Quaternion.identity, wood);

            // a rail fence behind the stable, from the haystack to the far side (posts every 1.6 m, two rails)
            var fence = new GameObject("Fence").transform; fence.SetParent(yard);
            float fenceZ = -3.6f - dz, fenceFrom = -5.2f - wx, fenceTo = 5.2f + wx;
            for (float x = fenceFrom; x <= fenceTo + 0.01f; x += 1.6f)
            {
                Vector3 p = At(x, fenceZ);
                MapDecor.Part(PrimitiveType.Cylinder, "Post", fence, p + Vector3.up * 0.6f, new Vector3(0.12f, 0.6f, 0.12f), stable.rotation, wood, true);
                if (x + 1.6f > fenceTo + 0.01f) break;
                Vector3 q = At(x + 1.6f, fenceZ), mid = (p + q) / 2f;
                Quaternion along = Quaternion.LookRotation(q - p) * Quaternion.Euler(0f, 90f, 0f);
                foreach (float y in new[] { 0.45f, 0.95f })
                    MapDecor.Part(PrimitiveType.Cube, "Rail", fence, mid + Vector3.up * y, new Vector3(Vector3.Distance(p, q) + 0.1f, 0.09f, 0.07f), along, wood, true);
            }

            // plants
            GameObject[] bushes = Load("Bush_02_01", "Bush_02_02", "Bush_02_01"), blossom = Load("BlossomTree_02", "BlossomTree_04");
            GameObject[] flowers = Load("Flower_Red", "Flower_Yellow", "Flower_Pink", "Flower_White", "Flower_YellowRed");
            GameObject[] patches = Load("FlowerMeadow_RedOrange", "FlowerMeadow_White", "FlowerMeadow_PurpleRedPink");
            GameObject[] grass = Load("Grass_01", "Grass_02", "Grass_03"), stones = Load("Stones_01", "Stones_02");
            if (blossom.Length > 0) Put(blossom[0], At(4.6f + wx, -2.2f) - Vector3.up * 0.1f, 1.25f);   // a peach tree shading the stable's far side
            foreach (var (x, z) in new[] { (-3.8f - wx, -4.8f - dz), (-0.6f, -4.9f - dz), (2.6f + wx, -4.8f - dz) })
                if (bushes.Length > 0) Put(bushes[rng.Next(bushes.Length)], At(x, z), Rand(0.9f, 1.15f));
            for (int i = 0; i < 12; i++)   // flowers along the fence and the sides of the stable
            {
                float x = i < 7 ? Rand(-4.8f - wx, 4.8f + wx) : (i % 2 == 0 ? -2.3f - wx : 2.3f + wx), z = i < 7 ? fenceZ + Rand(0.25f, 0.6f) : Rand(-1.8f - dz, 1.8f + dz);
                if (i % 3 == 0 && patches.Length > 0) Put(patches[rng.Next(patches.Length)], At(x, z), Rand(2f, 2.8f));
                else if (flowers.Length > 0) Put(flowers[rng.Next(flowers.Length)], At(x, z), Rand(0.9f, 1.3f));
            }
            for (int i = 0; i < 10; i++)   // grass tufts at the posts and round the haystack
            {
                float a = Rand(0f, Mathf.PI * 2f);
                (float x, float z) = i < 5 ? (-3.4f - wx + Mathf.Sin(a) * 1.15f, -0.6f + Mathf.Cos(a) * 1.15f) : (i % 2 == 0 ? -1.85f - wx : 1.85f + wx, Rand(-2.3f - dz, 2.3f + dz));
                if (grass.Length > 0) Put(grass[rng.Next(grass.Length)], At(x, z), Rand(0.7f, 1.1f));
            }
            if (stones.Length > 0) { Put(stones[0], At(2.6f + wx, 3.1f + dz), 0.9f); Put(stones[stones.Length - 1], At(-4.6f - wx, 1.4f), 0.8f); }
        }

        // ---------- the bell: the Meshy heirloom bell (VillageModelSetup; a glowing cube without it), bobbing and turning, a beam of light
        // over it ----------

        static GameObject BuildBell(Transform parent, Vector3 ground)
        {
            var root = new GameObject("BronzeBell").transform;
            root.SetParent(parent); root.position = ground;
            var content = new GameObject("Content").transform; content.SetParent(root, false);
            Material bronze = MapDecor.Lit("Mat_BellBronze", new Color(0.78f, 0.52f, 0.20f), 0.8f, 0.6f);
            bronze.EnableKeyword("_EMISSION"); bronze.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.2f) * 0.8f); EditorUtility.SetDirty(bronze);
            Transform visual;
            var bellModel = VillageModelSetup.Prefab(VillageModelSetup.Kind.Bell);
            if (bellModel != null)
            {
                visual = new GameObject("Visual").transform; visual.SetParent(content, false); visual.localPosition = new Vector3(0f, 0.9f, 0f);
                var bellGo = (GameObject)PrefabUtility.InstantiatePrefab(bellModel, visual);
                bellGo.transform.localPosition = new Vector3(0f, -0.3f, 0f);   // the 0.6 m bell centred on the bob point
            }
            else visual = MapDecor.Part(PrimitiveType.Cube, "Visual", content, new Vector3(0f, 0.9f, 0f), new Vector3(0.35f, 0.35f, 0.35f), Quaternion.Euler(0f, 45f, 0f), bronze).transform;
            var lightGo = new GameObject("Glow"); lightGo.transform.SetParent(content, false); lightGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1f, 0.75f, 0.35f); light.range = 8f; light.intensity = 4f;
            Material beam = MapDecor.Lit("Mat_QuestBeam", new Color(1f, 0.82f, 0.4f), 0f, 0f);
            beam.EnableKeyword("_EMISSION"); beam.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.3f) * 2f); EditorUtility.SetDirty(beam);
            MapDecor.Part(PrimitiveType.Cylinder, "Beam", content, new Vector3(0f, 7f, 0f), new Vector3(0.12f, 6f, 0.12f), Quaternion.identity, beam);
            var interactable = root.gameObject.AddComponent<Interactable>();
            var iso = new SerializedObject(interactable);
            iso.FindProperty("prompt").stringValue = "Nhấn E để nhặt";
            iso.FindProperty("radius").floatValue = 2.2f;
            iso.ApplyModifiedPropertiesWithoutUndo();
            var pickup = root.gameObject.AddComponent<HeirloomPickup>();
            var pso = new SerializedObject(pickup);
            pso.FindProperty("content").objectReferenceValue = content.gameObject;
            pso.FindProperty("visual").objectReferenceValue = visual;
            pso.FindProperty("itemName").stringValue = "Chuông cổ gia truyền";
            pso.ApplyModifiedPropertiesWithoutUndo();
            return root.gameObject;
        }

        // The dialogue runner is rebuilt with the quest, so the other quests of the map (the rooster's director, encounter triggers...) would
        // still point at the deleted one: every empty DialogueRunner field in the scene gets the new runner.
        static int RelinkDialogueRunner(Scene scene, DialogueRunner runner)
        {
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var so = new SerializedObject(mb);
                    var it = so.GetIterator();
                    bool changed = false;
                    while (it.Next(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.type == "PPtr<$DialogueRunner>" && it.objectReferenceValue == null)
                        { it.objectReferenceValue = runner; changed = true; count++; }
                    if (changed) so.ApplyModifiedPropertiesWithoutUndo();
                }
            return count;
        }

        // ---------- UI ----------

        // The Prologue's dialogue canvas + runner, copied into this map (the runner is moved under the canvas while copying so their links survive).
        static DialogueRunner CopyDialogueUi(Scene target)
        {
            var prologue = EditorSceneManager.OpenScene(ProloguePath, OpenSceneMode.Additive);
            var roots = prologue.GetRootGameObjects();
            var canvas = roots.First(g => g.name == "PrologueCanvas");
            var runnerGo = roots.First(g => g.GetComponent<DialogueRunner>() != null);
            runnerGo.transform.SetParent(canvas.transform, false);
            var copy = Object.Instantiate(canvas);
            SceneManager.MoveGameObjectToScene(copy, target);
            EditorSceneManager.CloseScene(prologue, true);   // not saved: the Prologue keeps its own objects
            copy.name = "DialogueCanvas";
            var title = copy.transform.Find("TitleCard"); if (title != null) Object.DestroyImmediate(title.gameObject);
            // the key hint sits outside the dialogue panel in the Prologue (always on there); here it must only show with the panel
            var hint = copy.transform.Find("Hint"); var panel = copy.transform.Find("DialoguePanel");
            if (hint != null && panel != null) hint.SetParent(panel, true);
            var runner = copy.GetComponentInChildren<DialogueRunner>(true);
            var director = runner.GetComponent<PrologueDirector>(); if (director != null) Object.DestroyImmediate(director);
            runner.transform.SetParent(null, false); runner.gameObject.name = "Dialogue";
            SceneManager.MoveGameObjectToScene(runner.gameObject, target);
            copy.GetComponent<Canvas>().sortingOrder = 50;   // above the gameplay HUD
            return runner;
        }

        static void BuildPrompt(Transform parent, DialogueRunner runner)
        {
            var canvasGo = new GameObject("InteractionCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(parent);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var box = new GameObject("Prompt", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            box.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)box.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 230f); rt.sizeDelta = new Vector2(420f, 56f);
            box.GetComponent<Image>().color = new Color(0.02f, 0.015f, 0.01f, 0.72f);
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(box.transform, false);
            var trt = (RectTransform)textGo.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var label = textGo.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center; label.fontSize = 28; label.color = new Color(0.98f, 0.93f, 0.80f);
            // the font that already shows Vietnamese in the gift banner
            var banner = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.font != null && t.GetComponentInParent<GiftTrackerHUD>() != null);
            if (banner != null) label.font = banner.font;
            label.text = "Nhấn E để nói chuyện";
            var prompt = canvasGo.AddComponent<InteractionPrompt>();
            var so = new SerializedObject(prompt);
            so.FindProperty("group").objectReferenceValue = box.GetComponent<CanvasGroup>();
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("dialogue").objectReferenceValue = runner;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- dialogue ----------

        static string[] BuildDialogues()
        {
            var sets = new (string name, (string who, string text)[] lines)[]
            {
                ("Offer", new[]
                {
                    (Keeper, "Chào chàng trai! Dáng đi vững như núi thế kia, chắc chàng là Sơn Tinh, chúa vùng non cao phải không?"),
                    (Hero, "Đúng vậy. Ta đang tìm ngựa chín hồng mao để dâng vua Hùng làm sính lễ, cầu hôn Mị Nương."),
                    (Keeper, "Thế thì chàng đến đúng nhà rồi! Con ngựa chín hồng mao đang ăn cỏ trong chuồng nhà tôi đấy. Tôi nuôi nó từ khi nó còn là con ngựa non."),
                    (Keeper, "Có điều mấy hôm trước núi lở, tôi chạy vội nên đánh rơi chiếc chuông đồng ông nội để lại trên đường núi phía trên. Không có tiếng chuông ấy, nó chẳng chịu theo ai."),
                    (Keeper, "Chàng tìm giúp tôi chiếc chuông nhé. Mang được về đây, tôi xin gửi chàng con ngựa quý."),
                    ("", "Nhiệm vụ: tìm chuông đồng gia truyền của người chăn ngựa. Nơi chuông rơi đã có cột sáng đánh dấu trên đường núi."),
                }),
                ("Reminder", new[]
                {
                    (Keeper, "Chiếc chuông rơi ở đoạn đường núi phía trên kia, chỗ có cột sáng ấy. Đường dốc lắm, chàng đi cẩn thận nhé!"),
                }),
                ("Thanks", new[]
                {
                    (Hero, "Chiếc chuông đồng đây. Ta tìm thấy nó bên đường núi."),
                    (Keeper, "Đúng nó rồi! Tiếng chuông này tôi nghe từ thuở bé... Cảm ơn chàng nhiều lắm!"),
                    (Keeper, "Nghe kìa, con ngựa ngẩng đầu lên rồi. Nó nhận ra tiếng chuông nhà tôi đấy."),
                    (Keeper, "Ngựa chín hồng mao xin gửi chàng. Tôi cứ để nó ở chuồng, ăn no cỏ non, đến ngày chàng mang sính lễ lên kinh thì nó theo về."),
                }),
                ("After", new[]
                {
                    (Keeper, "Con ngựa vẫn khoẻ lắm, ngày nào tôi cũng cắt cỏ non cho nó. Chúc chàng sớm cưới được Mị Nương!"),
                }),
            };
            var result = new string[sets.Length];
            for (int i = 0; i < sets.Length; i++)
            {
                string path = $"{DialogueDir}/Dialogue_HorseQuest_{sets[i].name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
                if (asset == null) { asset = ScriptableObject.CreateInstance<DialogueSequence>(); AssetDatabase.CreateAsset(asset, path); }
                var so = new SerializedObject(asset);
                var arr = so.FindProperty("lines");
                arr.arraySize = sets[i].lines.Length;
                for (int j = 0; j < sets[i].lines.Length; j++)
                {
                    var e = arr.GetArrayElementAtIndex(j);
                    e.FindPropertyRelative("speaker").stringValue = sets[i].lines[j].who;
                    e.FindPropertyRelative("text").stringValue = sets[i].lines[j].text;
                    e.FindPropertyRelative("illustration").objectReferenceValue = null;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                result[i] = path;
            }
            AssetDatabase.SaveAssets();
            return result;
        }
    }
}

