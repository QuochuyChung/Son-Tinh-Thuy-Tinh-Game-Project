using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // The Meshy bridge (Assets/Art/Props/bridge.fbx) as the real thing on Map_ThuyTinh's walkways: copies of the model are laid end to end
    // along every straight stretch (each copy stretched a little so a whole number of them fits the stretch exactly), instead of the
    // block-built boards, posts and rails. Used by MapDecor.PlankBridge / NewProps.BuildStraight (the decorative plank bridges) and by
    // ThuyTinhMapSea.Pier (the long piers). The piers keep their own box colliders (the "Deck" children) and their piles under the water; the
    // plank bridges get the model's own box collider on every copy. The menu does the same to the saved Map_ThuyTinh.
    //
    // The model is not square to its own axes: the FBX is turned about 15 degrees on the vertical, so NewProps' WoodenBridge prefab (which
    // only turns it by a quarter turn) made the copies zig-zag. WoodenBridgeSegment.prefab is the same model with its planks lined up with
    // +Z (found from the plank vertices), the base at the origin, and a box collider for the deck.
    public static class BridgeModelSwap
    {
        const string HolderName = "BridgeModel";
        const string SegmentPath = "Assets/Prefabs/Props/WoodenBridgeSegment.prefab";

        static GameObject segment;
        static Vector3 segmentSize;     // x = width, z = length, y = height of one copy at scale 1
        static float segmentDeckTop;    // height of the planks' top over the origin

        static bool LoadSegment()
        {
            if (segment != null) return true;
            segment = AssetDatabase.LoadAssetAtPath<GameObject>(SegmentPath);
            if (segment == null) segment = MakeSegmentPrefab();
            if (segment == null) return false;
            Bounds b = VillageModelSetup.LocalBounds(segment, segment.transform);
            segmentSize = b.size;
            var box = segment.GetComponent<BoxCollider>();
            segmentDeckTop = box != null ? box.center.y + box.size.y * 0.5f : b.size.y * 0.5f;
            return true;
        }

        // Direction (degrees, from +X towards +Z, 0..180) of the long side of the smallest rectangle that holds the plank vertices of `model`
        // (seen from above, in the frame of `root`): the deck is a rectangle, so its edges give the direction, not the spread of the vertices.
        static float DeckAxisAngle(GameObject root, GameObject model)
        {
            var pts = new List<Vector2>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                {
                    Vector3 w = root.transform.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (w.y >= 0.04f && w.y <= 0.16f) pts.Add(new Vector2(w.x, w.z));
                }
            }
            if (pts.Count < 10) return 90f;
            float bestArea = float.MaxValue, bestAngle = 0f; bool longIsU = true;
            for (float a = 0f; a < 90f; a += 0.25f)
            {
                float rad = a * Mathf.Deg2Rad;
                Vector2 u = new(Mathf.Cos(rad), Mathf.Sin(rad)), v = new(-Mathf.Sin(rad), Mathf.Cos(rad));
                float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
                foreach (Vector2 p in pts)
                {
                    float pu = Vector2.Dot(p, u), pv = Vector2.Dot(p, v);
                    minU = Mathf.Min(minU, pu); maxU = Mathf.Max(maxU, pu); minV = Mathf.Min(minV, pv); maxV = Mathf.Max(maxV, pv);
                }
                float area = (maxU - minU) * (maxV - minV);
                if (area < bestArea) { bestArea = area; bestAngle = a; longIsU = (maxU - minU) >= (maxV - minV); }
            }
            return longIsU ? bestAngle : bestAngle + 90f;
        }

        static GameObject MakeSegmentPrefab()
        {
            GameObject source = NewProps.Prefab(NewProps.Kind.Bridge);   // (makes the material too)
            if (source == null) return null;
            Transform sourceModel = source.transform.GetChild(0);

            var root = new GameObject("WoodenBridgeSegment");
            GameObject fbx = PrefabUtility.GetCorrespondingObjectFromSource(sourceModel.gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx != null ? fbx : sourceModel.gameObject, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = sourceModel.localRotation;
            model.transform.localScale = sourceModel.localScale;

            // turn the deck to run along +Z: the long side of its rectangle goes to 90 degrees
            float angle = DeckAxisAngle(root, model);
            float yaw = angle - 90f;
            if (yaw > 90f) yaw -= 180f; else if (yaw < -90f) yaw += 180f;
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;
            float after = DeckAxisAngle(root, model);

            Bounds b = VillageModelSetup.LocalBounds(model, root.transform);
            model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            b = VillageModelSetup.LocalBounds(model, root.transform);

            Material material = source.GetComponentInChildren<Renderer>().sharedMaterial;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();

            float deckTop = source.GetComponent<BoxCollider>() != null ? source.GetComponent<BoxCollider>().size.y : 0.14f;
            var col = root.AddComponent<BoxCollider>();   // the deck, walked on
            col.size = new Vector3(b.size.x, deckTop, b.size.z);
            col.center = new Vector3(0f, deckTop * 0.5f, 0f);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, SegmentPath);
            Object.DestroyImmediate(root);
            Debug.Log("[BridgeModel] WoodenBridgeSegment.prefab made: size " + b.size + ", plank axis " + angle.ToString("F1") + " -> " + after.ToString("F1") + " degrees");
            return saved;
        }

        // `from` / `to`: the deck top at both ends of a straight stretch. Returns the number of copies (0: the model is not in the project).
        public static int BuildModelRun(Transform holder, Vector3 from, Vector3 to, float width, bool colliders)
        {
            if (!LoadSegment()) return 0;
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.3f) return 0;
            Quaternion rotation = Quaternion.LookRotation(d / length);
            float scaleX = width / segmentSize.x;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / (segmentSize.z * scaleX)));
            for (int i = 0; i < count; i++)
                PlaceSegment(holder, from + d * ((i + 0.5f) / count), rotation, width, length / count, colliders);
            return count;
        }

        // `deckCentre`: the middle of the copy's deck top.
        static void PlaceSegment(Transform holder, Vector3 deckCentre, Quaternion rotation, float width, float length, bool colliders)
        {
            float scaleX = width / segmentSize.x;
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(segment, holder);
            copy.name = "BridgeSegment";
            copy.transform.SetPositionAndRotation(deckCentre - rotation * Vector3.up * (segmentDeckTop * scaleX), rotation);
            copy.transform.localScale = new Vector3(scaleX, scaleX, length / segmentSize.z);
            if (!colliders)
                foreach (Collider c in copy.GetComponents<Collider>()) Object.DestroyImmediate(c);
        }

        // A pier of ThuyTinhMapSea: one "Deck" collider per straight stretch (centre, forward, length + 0.5, width). The boards and rails go, the
        // model takes their place; the posts stay as piles, cut off under the deck.
        public static bool DressPier(Transform pier, float deckTopY)
        {
            if (pier == null) return false;
            if (!LoadSegment()) return false;
            Transform old = pier.Find(HolderName);
            if (old != null) Object.DestroyImmediate(old.gameObject);   // laid again from the Deck colliders
            var decks = pier.Cast<Transform>().Where(c => c.name == "Deck" && c.GetComponent<BoxCollider>() != null).ToList();
            if (decks.Count == 0) return false;

            var holder = new GameObject(HolderName).transform;
            holder.SetParent(pier, false);
            foreach (Transform deck in decks)
            {
                var box = deck.GetComponent<BoxCollider>();
                float length = box.size.z - 0.5f, width = box.size.x;
                Vector3 centre = new Vector3(deck.position.x, deckTopY, deck.position.z);
                Vector3 half = deck.forward * (length * 0.5f);
                BuildModelRun(holder, centre - half, centre + half, width, false);
            }

            foreach (Transform child in pier.Cast<Transform>().ToList())
            {
                if (child.name == "Board" || child.name == "Rail") Object.DestroyImmediate(child.gameObject);
                else if (child.name == "Post")
                {
                    // a cylinder of height 2 * scale.y centred at position.y: keep the part under the deck as a pile
                    float bottom = child.position.y - child.localScale.y, top = deckTopY - 0.12f;
                    if (top - bottom < 0.3f) { Object.DestroyImmediate(child.gameObject); continue; }
                    child.position = new Vector3(child.position.x, (top + bottom) * 0.5f, child.position.z);
                    child.localScale = new Vector3(child.localScale.x, (top - bottom) * 0.5f, child.localScale.z);
                }
            }
            return true;
        }

        // Copies already laid with an older segment prefab: each is swapped for the current one at the same place, width and length.
        static void Rebuild(Transform holder)
        {
            GameObject legacy = NewProps.Prefab(NewProps.Kind.Bridge);
            Bounds lb = VillageModelSetup.LocalBounds(legacy, legacy.transform);
            var lbox = legacy.GetComponent<BoxCollider>();
            float legacyTop = lbox != null ? lbox.center.y + lbox.size.y * 0.5f : lb.size.y * 0.5f;
            foreach (Transform old in holder.Cast<Transform>().ToList())
            {
                if (old.name != "BridgeSegment") continue;
                bool colliders = old.GetComponent<Collider>() != null;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject);
                bool isNew = src != null && AssetDatabase.GetAssetPath(src) == SegmentPath;
                if (isNew) continue;
                // copies of the first aligned prefab (deleted since, so their prefab link is gone): that one measured 1.78 x 2.06, deck top 0.14
                bool lost = src == null;
                float width = old.localScale.x * (lost ? 1.78f : lb.size.x), length = old.localScale.z * (lost ? 2.06f : lb.size.z);
                Vector3 deckCentre = old.position + old.up * ((lost ? 0.14f : legacyTop) * old.localScale.y);
                Quaternion rotation = old.rotation;
                Object.DestroyImmediate(old.gameObject);
                PlaceSegment(holder, deckCentre, rotation, width, length, colliders);
            }
        }

        // A saved PlankBridge made of "Straight" boards (NewProps.BuildStraight before the model): redone with the model.
        public static bool ReStraight(Transform bridge)
        {
            Transform holder = bridge.Find("Straight");
            if (holder == null || !LoadSegment()) return false;
            if (holder.Find("BridgeSegment") != null) { Rebuild(holder); return true; }
            var boards = holder.Cast<Transform>().Where(c => c.name == "Board").ToList();
            if (boards.Count < 2) return false;
            Vector3 first = boards.OrderBy(b => b.position.x * 7f + b.position.z * 13f + b.position.y).First().position;
            Vector3 last = boards.OrderByDescending(b => (b.position - first).sqrMagnitude).First().position;
            boards = boards.OrderBy(b => Vector3.Dot(b.position - first, last - first)).ToList();
            Vector3 step = (last - first) / (boards.Count - 1);
            Transform b0 = boards[0];
            float width = b0.localScale.x, thickness = b0.localScale.y;
            Vector3 from = first - step * 0.5f + Vector3.up * (thickness * 0.5f), to = last + step * 0.5f + Vector3.up * (thickness * 0.5f);
            foreach (Transform c in holder.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
            return BuildModelRun(holder, from, to, width, true) > 0;
        }

        [MenuItem("Tools/Son Tinh Thuy Tinh/New Props/Bridges To Model (open Map_ThuyTinh)")]
        public static void Swap()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Stop Play mode first."); return; }
            var scene = EditorSceneManager.GetActiveScene();
            int piers = 0, bridges = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList())
            {
                if (t == null) continue;
                if (t.name.StartsWith("Pier_") && DressPier(t, PierDeckY)) piers++;
                else if (t.name == "PlankBridge" && ReStraight(t)) bridges++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[BridgeModel] " + scene.name + ": " + piers + " piers, " + bridges + " plank bridges now use the model");
        }

        // ThuyTinhMapSea.PierDeck = WaterY + 0.95 with WaterY 2.4.
        const float PierDeckY = 2.4f + 0.95f;
    }
}
