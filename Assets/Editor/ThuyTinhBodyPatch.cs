
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SonTinhThuyTinh.EditorTools
{
    // Thủy Tinh v2's body mesh has two open pockets in the sides of the torso under the arms (the vest panels hide them in the rest pose); with an
    // arm raised in a cutscene the pocket shows as a missing piece of flesh. The boundary loops of the pockets (the two loops of 28 and 29 edges, found by welding the
    // vertices of the mesh) are closed with a fan around a new centre vertex (its skin weights, uv and normal are the average of the loop's).
    // The patched mesh is saved next to the model (ThuyTinh_Body_Filled.asset) and put on the body renderer of the player prefab.
    public static class ThuyTinhBodyPatch
    {
        const string PlayerPrefab = "Assets/Prefabs/Player/Player_ThuyTinh_v2.prefab";
        const string OutMesh = "Assets/Art/Characters/ThuyTinh_v2/ThuyTinh_Body_Filled.asset";
        public static float RingTorso = 0f;   // how much of the cap ring follows the torso instead of the arm (0 = the loop's own weights, 1 = fully the torso)
        static string[] BoneNames = new string[0];
        static Color BellyColour = new Color(0.85f, 0.65f, 0.55f);
        static readonly int[] LoopSizes = { 29, 28 };

        [MenuItem("Tools/Son Tinh Thuy Tinh/Thuy Tinh: Fill Armpit Holes")]
        public static void Menu() => Debug.Log("ThuyTinhBodyPatch: " + Apply());

        public static string Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var smr = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name.Contains("Body"));
                if (smr == null) return "no body renderer";
                Mesh src = smr.sharedMesh;
                string srcPath = AssetDatabase.GetAssetPath(src);
                if (srcPath.EndsWith("ThuyTinh_Body_Filled.asset")) src = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Characters/ThuyTinh_v2/thuy_tinh_v2.fbx").OfType<Mesh>().First(m => m.name.Contains("Body"));
                var mesh = Object.Instantiate(src); mesh.name = "ThuyTinh_Body_Filled";
                BoneNames = smr.bones.Select(b => b != null ? b.name : "").ToArray();
                string report = Fill(mesh);
                if (AssetDatabase.LoadAssetAtPath<Mesh>(OutMesh) != null) AssetDatabase.DeleteAsset(OutMesh);
                AssetDatabase.CreateAsset(mesh, OutMesh);
                smr.sharedMesh = mesh;
                // the cap is the second submesh: a plain matte material in the belly's colour (the texture around the opening is the vest's, a patch of it
                // would not look like skin)
                Material Cap(string file, Color colour)
                {
                    string path = "Assets/Art/Characters/ThuyTinh_v2/Materials/" + file;
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
                    m.SetColor("_BaseColor", colour); m.SetFloat("_Smoothness", 0.28f); m.SetFloat("_Metallic", 0f); m.SetFloat("_Cull", 0f);
                    EditorUtility.SetDirty(m); return m;
                }
                // both sides: the belly colour a little brighter
                var mats = smr.sharedMaterials.Take(1).ToList();
                mats.Add(Cap("M_ThuyTinh_v2_Cap.mat", new Color(Mathf.Min(1f, BellyColour.r * 1.22f), Mathf.Min(1f, BellyColour.g * 1.08f), Mathf.Min(1f, BellyColour.b * 0.98f))));
                mats.Add(Cap("M_ThuyTinh_v2_CapRight.mat", new Color(Mathf.Min(1f, BellyColour.r * 1.22f), Mathf.Min(1f, BellyColour.g * 1.08f), Mathf.Min(1f, BellyColour.b * 0.98f))));   // (the same as the left: the two sides must look alike)
                smr.sharedMaterials = mats.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                return report;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Gaussian elimination of a 3x3 system (the fit's normal equations)
        static double[] Solve3(double[,] m, double[] b)
        {
            var a = new double[3, 4]; for (int i = 0; i < 3; i++) { for (int j = 0; j < 3; j++) a[i, j] = m[i, j]; a[i, 3] = b[i]; }
            for (int c = 0; c < 3; c++)
            {
                int piv = c; for (int r = c + 1; r < 3; r++) if (System.Math.Abs(a[r, c]) > System.Math.Abs(a[piv, c])) piv = r;
                if (System.Math.Abs(a[piv, c]) < 1e-30) return new double[] { 0, 0, b[2] / System.Math.Max(m[2, 2], 1e-30) };
                for (int j = 0; j < 4; j++) { double t = a[c, j]; a[c, j] = a[piv, j]; a[piv, j] = t; }
                for (int r = 0; r < 3; r++) { if (r == c) continue; double f = a[r, c] / a[c, c]; for (int j = c; j < 4; j++) a[r, j] -= f * a[c, j]; }
            }
            return new[] { a[0, 3] / a[0, 0], a[1, 3] / a[1, 1], a[2, 3] / a[2, 2] };
        }

        static string Fill(Mesh mesh)
        {
            var verts = mesh.vertices.ToList(); var normals = mesh.normals.ToList(); var uvs = mesh.uv.ToList();
            var weights = mesh.boneWeights.ToList(); var tris = mesh.GetTriangles(0).ToList(); var capTris = new List<int>(); var capTrisRight = new List<int>();
            // weld
            var weld = new int[verts.Count]; var map = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < verts.Count; i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(verts[i].x * 1e7f), Mathf.RoundToInt(verts[i].y * 1e7f), Mathf.RoundToInt(verts[i].z * 1e7f));
                if (!map.TryGetValue(key, out int id)) { id = i; map[key] = id; }
                weld[i] = id;
            }
            var edgeCount = new Dictionary<(int, int), int>(); var edgeDir = new Dictionary<(int, int), (int, int)>();
            for (int t = 0; t < tris.Count; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = weld[tris[t + k]], b = weld[tris[t + (k + 1) % 3]]; var key = a < b ? (a, b) : (b, a);
                    edgeCount[key] = edgeCount.TryGetValue(key, out int c) ? c + 1 : 1;
                    edgeDir[key] = (a, b);   // the direction the (single) triangle runs
                }
            var boundary = edgeCount.Where(kv => kv.Value == 1).Select(kv => edgeDir[kv.Key]).ToList();
            var next = new Dictionary<int, List<int>>(); foreach (var (a, b) in boundary) { if (!next.ContainsKey(a)) next[a] = new(); next[a].Add(b); }
            var used = new HashSet<(int, int)>(); var loops = new List<List<int>>();
            foreach (var (a0, b0) in boundary)
            {
                if (used.Contains((a0, b0))) continue;
                // walk the boundary; where it comes back to a vertex it already passed (a pinch), that stretch is its own loop
                var path = new List<int> { a0 }; int cur = b0; used.Add((a0, b0)); int guard = 0;
                while (guard++ < 20000)
                {
                    int at = path.IndexOf(cur);
                    if (at >= 0)
                    {
                        loops.Add(path.GetRange(at, path.Count - at)); path.RemoveRange(at + 1, path.Count - at - 1);
                        if (at == 0) break;
                        cur = path[at]; path.RemoveAt(at);   // continue from the pinch vertex
                        path.Add(cur); path.RemoveAt(path.Count - 1);
                        // re-enter the walk at the pinch vertex with whatever boundary edge is left
                        if (!next.TryGetValue(cur, out var o1) || !o1.Any(o => !used.Contains((cur, o)))) break;
                        int n1 = o1.First(o => !used.Contains((cur, o))); used.Add((cur, n1)); path.Add(cur); cur = n1; continue;
                    }
                    path.Add(cur);
                    if (!next.TryGetValue(cur, out var outs) || !outs.Any(o => !used.Contains((cur, o)))) break;
                    int nx = outs.First(o => !used.Contains((cur, o))); used.Add((cur, nx)); cur = nx;
                }
            }
            var sb = new System.Text.StringBuilder();
            foreach (var l in loops.Where(l => l.Count >= 8))
            {
                var pts = l.Select(v => verts[v] * 100f).ToList(); Vector3 mn = pts[0], mx = pts[0]; foreach (var q in pts) { mn = Vector3.Min(mn, q); mx = Vector3.Max(mx, q); }
                sb.Append("loop " + l.Count + " centre " + ((mn + mx) * 0.5f).ToString("F3") + " size " + (mx - mn).ToString("F3") + "; ");
            }
            Debug.Log("ThuyTinhBodyPatch loops: " + sb);
            sb.Clear();
            int filled = 0;
            // the body texture (a readable copy), and the colour of the belly: what the cap should look like
            Texture2D tex = null; Color belly = default;
            var bodyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/ThuyTinh_v2/Materials/M_ThuyTinh_v2_Body.mat");
            var baseTex = bodyMat != null ? bodyMat.GetTexture("_BaseMap") as Texture2D : null;
            if (baseTex != null)
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(baseTex)));
                Color sum = Color.black; int n0 = 0;
                for (int v = 0; v < verts.Count; v++)
                {
                    Vector3 q = verts[v] * 100f;
                    if (q.z > 0.10f && q.z < 0.20f && Mathf.Abs(q.x) < 0.05f) { sum += tex.GetPixelBilinear(uvs[v].x, uvs[v].y); n0++; }
                }
                belly = n0 > 0 ? sum / n0 : Color.gray;
            }
            foreach (var loop in loops)
            {
                if (!LoopSizes.Contains(loop.Count)) continue;
                Vector3 centre = Vector3.zero; Vector3 n = Vector3.zero; Vector2 uv = Vector2.zero; var acc = new Dictionary<int, float>();
                foreach (int v in loop)
                {
                    centre += verts[v]; n += normals[v]; uv += uvs[v];
                    var w = weights[v];
                    void Add(int bone, float wt) { if (wt <= 0f) return; acc[bone] = acc.TryGetValue(bone, out float o) ? o + wt : wt; }
                    Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                }
                centre /= loop.Count; n = n.normalized; uv /= loop.Count;
                var topW = acc.OrderByDescending(kv => kv.Value).Take(4).ToList(); float sum = topW.Sum(kv => kv.Value);
                var bw = new BoneWeight();
                if (topW.Count > 0) { bw.boneIndex0 = topW[0].Key; bw.weight0 = topW[0].Value / sum; }
                if (topW.Count > 1) { bw.boneIndex1 = topW[1].Key; bw.weight1 = topW[1].Value / sum; }
                if (topW.Count > 2) { bw.boneIndex2 = topW[2].Key; bw.weight2 = topW[2].Value / sum; }
                if (topW.Count > 3) { bw.boneIndex3 = topW[3].Key; bw.weight3 = topW[3].Value / sum; }
                // the cap is skin: its uv is the uv of the skin vertex next to the opening (the third vertex of a triangle on the loop, nearest to its
                // middle: boundary vertices carry the vest's uv), the ring of the cap is new vertices (same place and weights as the loop's, that uv, the
                // loop's own normals so it shades like the flank around it) and the middle bulges outwards like flesh instead of a flat plate
                var loopSet = new HashSet<int>(loop);
                Vector3 mn0 = verts[loop[0]], mx0 = mn0; foreach (int v in loop) { mn0 = Vector3.Min(mn0, verts[v]); mx0 = Vector3.Max(mx0, verts[v]); }
                float reach = Mathf.Max(mx0.x - mn0.x, mx0.y - mn0.y, mx0.z - mn0.z) * 1.6f;
                // the skin around the opening gives the cap its uv: an affine fit uv = f(y, z) over the skin-coloured vertices next to it (the opening lies in
                // the side plane of the torso, and the flank around it is mapped roughly flat), so the cap continues the texture instead of showing one texel
                var near = new List<int>();
                for (int v = 0; v < weld.Length; v++)
                {
                    if (loopSet.Contains(weld[v]) || (verts[v] - centre).magnitude > reach || Mathf.Sign(verts[v].x) != Mathf.Sign(centre.x)) continue;
                    Color col = tex != null ? tex.GetPixelBilinear(uvs[v].x, uvs[v].y) : belly;
                    float d = (col.r - belly.r) * (col.r - belly.r) + (col.g - belly.g) * (col.g - belly.g) + (col.b - belly.b) * (col.b - belly.b);
                    if (d < 0.02f) near.Add(v);
                }
                System.Func<Vector3, Vector2> mapUv; float fitRms = 1f;
                if (near.Count >= 8)
                {
                    // normal equations of  u = a*y + b*z + c  (and v)
                    double[,] m = new double[3, 3]; double[] bu = new double[3], bv = new double[3];
                    foreach (int v in near)
                    {
                        double[] r = { verts[v].y, verts[v].z, 1.0 };
                        for (int a = 0; a < 3; a++) { for (int b2 = 0; b2 < 3; b2++) m[a, b2] += r[a] * r[b2]; bu[a] += r[a] * uvs[v].x; bv[a] += r[a] * uvs[v].y; }
                    }
                    double[] su = Solve3(m, bu), sv = Solve3(m, bv);
                    mapUv = p => new Vector2((float)(su[0] * p.y + su[1] * p.z + su[2]), (float)(sv[0] * p.y + sv[1] * p.z + sv[2]));
                    double err = 0; foreach (int v in near) { var q = mapUv(verts[v]); err += (q - uvs[v]).sqrMagnitude; }
                    fitRms = (float)System.Math.Sqrt(err / near.Count);
                }
                else { Vector2 flat = uvs[loop[0]]; mapUv = p => flat; }
                Vector3 mn = verts[loop[0]], mx = mn; foreach (int v in loop) { mn = Vector3.Min(mn, verts[v]); mx = Vector3.Max(mx, verts[v]); }
                float extent = Mathf.Max(mx.x - mn.x, mx.y - mn.y, mx.z - mn.z);
                Vector3 outward = new Vector3(centre.x >= 0f ? 1f : -1f, 0f, 0f);
                // a texture-continuing cap (uv fit good) uses the body material, so the skin and the patterns around it carry on; otherwise a plain skin colour
                bool textured = false;
                bool right = centre.x > 0f;   // the character's right (the mesh's +x): its own submesh, so its colour can be tuned alone
                float h = extent * 0.22f;
                var torso = acc.Where(kv => kv.Key < BoneNames.Length && (BoneNames[kv.Key].ToLower().Contains("spine") || BoneNames[kv.Key].ToLower().Contains("chest"))).OrderByDescending(kv => kv.Value).Select(kv => (int?)kv.Key).FirstOrDefault();
                int torsoBone = torso ?? topW[0].Key;
                BoneWeight Blend(BoneWeight w, float share)
                {
                    var d = new Dictionary<int, float>();
                    void A(int b, float x) { if (x > 0f) d[b] = d.TryGetValue(b, out float o) ? o + x : x; }
                    A(w.boneIndex0, w.weight0 * (1f - share)); A(w.boneIndex1, w.weight1 * (1f - share)); A(w.boneIndex2, w.weight2 * (1f - share)); A(w.boneIndex3, w.weight3 * (1f - share)); A(torsoBone, share);
                    var t4 = d.OrderByDescending(kv => kv.Value).Take(4).ToList(); float sm = t4.Sum(kv => kv.Value); var r = new BoneWeight();
                    if (t4.Count > 0) { r.boneIndex0 = t4[0].Key; r.weight0 = t4[0].Value / sm; }
                    if (t4.Count > 1) { r.boneIndex1 = t4[1].Key; r.weight1 = t4[1].Value / sm; }
                    if (t4.Count > 2) { r.boneIndex2 = t4[2].Key; r.weight2 = t4[2].Value / sm; }
                    if (t4.Count > 3) { r.boneIndex3 = t4[3].Key; r.weight3 = t4[3].Value / sm; }
                    return r;
                }
                var ring0 = new List<int>();
                foreach (int v in loop)
                {
                    ring0.Add(verts.Count); verts.Add(verts[v]); normals.Add(Vector3.Lerp(normals[v], outward, 0.5f).normalized); uvs.Add(mapUv(verts[v])); weights.Add(Blend(weights[v], RingTorso));
                }
                Vector3 top = centre + outward * h;
                // the middle of the cap is pinned to the torso (the strongest spine / chest bone of the loop), not blended with the arm: it keeps its shape
                // when the arm moves instead of stretching with it; only the ring, which joins the surrounding skin, follows the loop's own weights
                var pinned = new BoneWeight { boneIndex0 = torsoBone, weight0 = 1f };
                int c = verts.Count; verts.Add(top); normals.Add(outward); uvs.Add(mapUv(top)); weights.Add(pinned);
                sb.Append("pinned to " + (torso.HasValue ? BoneNames[torso.Value] : "?") + "; ");
                // the loop runs the way the existing triangles run along the boundary edge; the cap must run the opposite way
                var capList = right ? capTrisRight : capTris;
                for (int i2 = 0; i2 < loop.Count; i2++) { int a = ring0[i2], b = ring0[(i2 + 1) % loop.Count]; capList.Add(b); capList.Add(a); capList.Add(c); }
                sb.Append("fit rms " + fitRms.ToString("F4") + (textured ? " (textured)" : " (plain)") + "; ");
                sb.Append("filled " + loop.Count + " (centre " + centre.ToString("F3") + "); ");
                filled++;
            }
            mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.boneWeights = weights.ToArray();
            mesh.subMeshCount = 3; mesh.SetTriangles(tris, 0); mesh.SetTriangles(capTris, 1); mesh.SetTriangles(capTrisRight, 2); mesh.RecalculateBounds(); BellyColour = belly;
            return sb + "(" + filled + " loops closed, mesh " + mesh.vertexCount + " vertices, " + tris.Count / 3 + " + " + capTris.Count / 3 + " cap triangles, belly " + belly + ")";
        }
    }
}
