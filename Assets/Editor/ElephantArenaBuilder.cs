using System.Linq;
using SonTinhThuyTinh.Combat;
using SonTinhThuyTinh.Dialogue;
using SonTinhThuyTinh.Quest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Puts Voi Chín Ngà into the arena of Assets/Scenes/Map_SonTinh.unity (SonTinhMapBuilder.BuildElephantArena: the round floor with the
    // markers ElephantArena / ElephantSpawn / ElephantPlayerEntry) and wires the fight (VoiChinNgaArenaEvent, docs/progress.md 9.24).
    // The boss is the same one Huy built for Battle_VoiChinNga (prefab Resources/VoiChinNga, VoiChinNgaBoss, Health, capsule), built here
    // by the same recipe as VoiChinNgaBattleBootstrap.BuildBoss. The walk-in encounter on the main path (Pickup_VoiChinNga with the patrol
    // and the trigger to Battle_VoiChinNga) is switched off: the elephant now stays in the arena.
    // Run after Build Son Tinh Map and Build Horse Quest (it needs the dialogue box that one puts in the map). Safe to run again.
    public static class ElephantArenaBuilder
    {
        const string ScenePath = "Assets/Scenes/Map_SonTinh.unity";
        const string Root = "ElephantEvent";
        const string GiftPath = "Assets/Data/Gifts/Gift_VoiChinNga.asset";

        [MenuItem("Tools/Son Tinh Thuy Tinh/Build Elephant Arena Event (Son Tinh map)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Transform centre = Find("ElephantArena"), spawn = Find("ElephantSpawn"), entry = Find("ElephantPlayerEntry");
            if (centre == null || spawn == null || entry == null)
            {
                Debug.LogError("ElephantArenaBuilder: no elephant arena markers in the scene; run Build Son Tinh Map first.");
                return;
            }
            var runner = Object.FindObjectsByType<DialogueRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (runner == null) { Debug.LogError("ElephantArenaBuilder: no DialogueRunner in the scene; run Build Horse Quest first."); return; }
            var prefab = Resources.Load<GameObject>("VoiChinNga");
            if (prefab == null) { Debug.LogError("ElephantArenaBuilder: Assets/Resources/VoiChinNga.prefab is missing."); return; }

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(Root).transform;

            // marks, dropped on the ground
            Terrain terrain = Object.FindFirstObjectByType<Terrain>();
            Vector3 OnGround(Vector3 p) => new(p.x, terrain.SampleHeight(p) + terrain.transform.position.y, p.z);
            Transform Mark(string name, Transform from)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root, false);
                t.SetPositionAndRotation(OnGround(from.position), from.rotation);
                return t;
            }
            Transform centreMark = Mark("ArenaCentre", centre), bossMark = Mark("BossMark", spawn), playerMark = Mark("PlayerMark", entry);

            // the boss: a root that the AI moves (forward = +Z), the model inside it turned -90 degrees (its authored forward is +X)
            var boss = new GameObject("VoiChinNga_Boss");
            boss.transform.SetParent(root, false);
            boss.transform.SetPositionAndRotation(bossMark.position, bossMark.rotation);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, boss.transform);
            model.name = "VoiChinNga_Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var capsule = boss.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 4.3f, 0f);
            capsule.height = 8.6f;
            capsule.radius = 2.65f;
            var body = boss.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            boss.AddComponent<Health>();
            var ai = boss.AddComponent<VoiChinNgaBoss>();
            ai.enabled = false;
            // The arena fight is gentler than Huy's separate scene (the player has no healing here): lower damage, longer waits between
            // blows and longer warnings before the special attacks. Only this instance; the defaults in VoiChinNgaBoss stay for Battle_VoiChinNga.
            var bso = new SerializedObject(ai);
            foreach (var (field, value) in new (string, float)[]
            {
                ("attackDamage", 8f), ("attackCooldown", 1.8f),
                ("specialOpeningDelay", 4f), ("specialCooldown", 8f),
                ("slamDamage", 10f), ("slamWindup", 1.3f), ("slamDuration", 1.8f),
                ("chargeDamage", 13f), ("chargeWindup", 1.1f), ("chargeDuration", 1.5f),
                ("moveSpeed", 2.4f),
                ("rockDamage", 16f), ("rockWindup", 1.4f),   // the rock volley Huy added (merge 17/10): 22 -> 16, a longer warning
            })
                bso.FindProperty(field).floatValue = value;
            bso.ApplyModifiedPropertiesWithoutUndo();

            var evt = root.gameObject.AddComponent<VoiChinNgaArenaEvent>();
            var so = new SerializedObject(evt);
            so.FindProperty("boss").objectReferenceValue = ai;
            so.FindProperty("gift").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GiftItem>(GiftPath);
            so.FindProperty("arenaCentre").objectReferenceValue = centreMark;
            so.FindProperty("bossMark").objectReferenceValue = bossMark;
            so.FindProperty("playerMark").objectReferenceValue = playerMark;
            so.FindProperty("runner").objectReferenceValue = runner;
            so.ApplyModifiedPropertiesWithoutUndo();

            // the walk-in elephant on the main path is replaced by this one
            foreach (var pickup in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (pickup.name == "Pickup_VoiChinNga") pickup.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"ElephantArenaBuilder: elephant at {bossMark.position}, arena centre {centreMark.position}, player entry {playerMark.position}");
        }

        static Transform Find(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }
    }
}
