"""Builds a clenched fist with four curled fingers and a thumb, and swaps it in for Meshy's fused fist.

Meshy models the hands as a single smooth lump (fingers fused, no gaps), and the Mixamo "no fingers" rig cannot move
them anyway. This keeps the lump's place, size and orientation (taken from the old fist and the forearm bracer) but
replaces its geometry. The fist is a skin-modifier skeleton (palm + 4 curled fingers + thumb) that is voxel-remeshed,
smoothed and decimated into one clean closed piece per hand.

Normally run through tools/replace_fists.py (works on the Meshy mesh or on the already skinned FBX). Standalone check:
    blender --background --python tools/fist_builder.py -- <full.fbx> <render_prefix>
which swaps the fists and writes <render_prefix>_front.png / _medial.png / _lateral.png close-ups.

Mesh conventions (world space, after the FBX import): Z up, the character faces -Y, +X is the character's left,
body height = 1.0 (about 1.9 m). Everything here is done in world space and converted back to the mesh's own space
at the end, because the skinned FBX carries a 100x object scale.
"""
import bpy, bmesh, sys, math, os
from mathutils import Vector

CM = 1.0 / 190.0          # 1 cm in mesh units (body height 1.0 ~ 1.9 m)
VOXEL_CM = 0.36           # voxel remesh resolution that merges the finger tubes into one clean blob
DECIMATE = 0.30           # fraction of the remeshed triangles to keep

# Fist skeleton in a local frame, in cm. m = towards the palm (medial for a hanging arm), f = forward, d = down along the forearm.
# Origin = the wrist. Back of the hand is on the -m side. Fingers listed forward -> back: index, middle, ring, pinky.
FINGER_F = (3.3, 1.1, -1.1, -3.1)
FINGER_R = (1.15, 1.2, 1.1, 0.95)               # finger thickness


def skeleton():
    """Returns (points, edges, roots); a point is (m, f, d, radius_m, radius_f)."""
    pts = []; edges = []; roots = []

    def add(m, f, d, rm, rf):
        pts.append((m, f, d, rm, rf)); return len(pts) - 1

    wrist = add(0.0, 0.0, 0.0, 3.0, 3.6); roots.append(wrist)
    palm_c = add(-0.4, 0.0, 3.2, 2.5, 4.2)
    edges.append((wrist, palm_c))
    knuckles = []
    for f, r in zip(FINGER_F, FINGER_R):
        k = add(-1.1, f, 7.0, 1.3 * r, 1.3 * r)          # knuckle (MCP joint), bottom of the back of the hand
        p = add(2.0, f, 7.7, r, r)                         # proximal phalanx goes medially under the knuckle
        mid = add(3.4, f, 5.6, r, r)                       # bend: middle phalanx runs back up the palm
        tip = add(2.4, f, 4.0, 0.9 * r, 0.9 * r)           # fingertip turns into the palm
        edges += [(palm_c, k), (k, p), (p, mid), (mid, tip)]
        knuckles.append(k)
    for a, b in zip(knuckles, knuckles[1:]): edges.append((a, b))
    # thumb: from the base of the palm down the front, across the middle phalanges of index and middle finger
    t0 = add(1.2, 4.0, 2.2, 1.5, 1.4)
    t1 = add(3.0, 4.3, 4.8, 1.3, 1.25)
    t2 = add(4.4, 3.2, 6.6, 1.15, 1.1)
    edges += [(palm_c, t0), (t0, t1), (t1, t2)]
    return pts, edges, roots


def components(bm):
    seen = {}; comps = []
    for v in bm.verts:
        if v.index in seen: continue
        cid = len(comps); st = [v]; seen[v.index] = cid; mem = []
        while st:
            cur = st.pop(); mem.append(cur)
            for e in cur.link_edges:
                w = e.other_vert(cur)
                if w.index not in seen: seen[w.index] = cid; st.append(w)
        comps.append(mem)
    return comps


def find_fists_and_bracers(bm, zmin, H):
    """Sơn Tinh v2: the fused fists are small loose pieces under each forearm bracer (found by position and size)."""
    fists = {}; bracers = {}
    for mem in components(bm):
        pts = [v.co for v in mem]
        xc = sum(p.x for p in pts) / len(pts); zc = (sum(p.z for p in pts) / len(pts) - zmin) / H
        side = 'Left' if xc > 0 else 'Right'
        if abs(xc) >= 0.13 and 0.42 <= zc <= 0.50 and 100 <= len(mem) <= 300: fists[side] = mem
        elif abs(xc) >= 0.13 and 0.52 <= zc <= 0.60 and 30 <= len(mem) <= 100: bracers[side] = mem
    return fists, bracers


def _read_image(path):
    import numpy as np
    im = bpy.data.images.load(path)
    w, h = im.size
    arr = np.empty(w * h * 4, dtype=np.float32); im.pixels.foreach_get(arr)
    bpy.data.images.remove(im)
    return arr.reshape(h, w, 4)


def pick_skin_uv(loops_uv, basecolor, normal):
    """A UV point inside a uniform patch of skin colour (and flat normal map), taken from the old fist's own UVs."""
    import numpy as np
    base = _read_image(basecolor); nrm = _read_image(normal) if normal else None
    h, w = base.shape[:2]
    def px(img, uv, r):
        x = int(uv[0] * (img.shape[1] - 1)); y = int(uv[1] * (img.shape[0] - 1))
        return img[max(0, y - r):y + r + 1, max(0, x - r):x + r + 1, :3].reshape(-1, 3)
    samples = [px(base, uv, 0)[0] for uv in loops_uv]
    mean = np.mean(samples, axis=0)
    best = None
    for uv in loops_uv:
        patch = px(base, uv, 3)
        score = float(np.std(patch, axis=0).sum() + 2.0 * np.abs(patch.mean(axis=0) - mean).sum())
        if nrm is not None:
            npatch = px(nrm, uv, 3)
            score += 2.0 * float(np.abs(npatch.mean(axis=0)[:2] - 0.5).sum()) + float(np.std(npatch, axis=0).sum())
        if best is None or score < best[0]: best = (score, tuple(uv), patch.mean(axis=0))
    print(f"FIST skin uv={best[1]} colour={tuple(round(float(c), 3) for c in best[2])} (mean of old fist {tuple(round(float(c), 3) for c in mean)}) score={best[0]:.3f}")
    return best[1]


def _fist_mesh(origin, Mv, Fv, Dv, scale):
    """The fist in world space: (verts, faces)."""
    pts, edges, roots = skeleton()
    me = bpy.data.meshes.new("fist_tmp")
    me.from_pydata([(m * CM * scale, f * CM * scale, d * CM * scale) for m, f, d, _, _ in pts], edges, [])
    ob = bpy.data.objects.new("fist_tmp", me); bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.modifier_add(type='SKIN')
    skv = me.skin_vertices[0].data
    for i, (m, f, d, rm, rf) in enumerate(pts):
        skv[i].radius = (rm * CM * scale, rf * CM * scale)
        skv[i].use_root = i in roots
    ss = ob.modifiers.new("sub", 'SUBSURF'); ss.levels = 1; ss.render_levels = 1
    rm_ = ob.modifiers.new("vox", 'REMESH'); rm_.mode = 'VOXEL'; rm_.voxel_size = VOXEL_CM * CM * scale; rm_.use_smooth_shade = True
    sm = ob.modifiers.new("smooth", 'SMOOTH'); sm.factor = 0.8; sm.iterations = 6
    dc = ob.modifiers.new("dec", 'DECIMATE'); dc.ratio = DECIMATE
    bpy.context.view_layer.update()
    ev = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    tmp = bpy.data.meshes.new_from_object(ev)
    verts = [origin + Mv * v.co.x + Fv * v.co.y + Dv * v.co.z for v in tmp.vertices]
    faces = [tuple(p.vertices) for p in tmp.polygons]
    bpy.data.objects.remove(ob); bpy.data.meshes.remove(me); bpy.data.meshes.remove(tmp)
    return verts, faces


def replace_fists(F, basecolor=None, normal=None, scale=1.0):
    """Swaps the two fused fists of mesh object F for clenched fists. If F has vertex groups named mixamorig:LeftHand /
    mixamorig:RightHand, each new fist is weighted 100 % to its hand bone. Returns {'Left': [new vertex indices], 'Right': [...]}."""
    mw = F.matrix_world.copy()
    bm = bmesh.new(); bm.from_mesh(F.data)
    bmesh.ops.transform(bm, matrix=mw, verts=bm.verts)             # world space
    bm.verts.ensure_lookup_table()
    zs = [v.co.z for v in bm.verts]; zmin, zmax = min(zs), max(zs); H = zmax - zmin
    fists, bracers = find_fists_and_bracers(bm, zmin, H)
    if set(fists) != {'Left', 'Right'}:
        raise RuntimeError(f"expected to find 2 fists, found {list(fists)} -- this mesh is not Sơn Tinh v2, adjust find_fists_and_bracers()")
    uvl = bm.loops.layers.uv.verify()
    old_uvs = [tuple(l[uvl].uv) for side in fists for v in fists[side] for l in v.link_loops]
    skin_uv = pick_skin_uv(old_uvs, basecolor, normal) if basecolor else old_uvs[0]
    frames = {}
    for side, mem in fists.items():
        pts = [v.co.copy() for v in mem]
        top_z = max(p.z for p in pts)
        cx = sum(p.x for p in pts) / len(pts); cy = sum(p.y for p in pts) / len(pts)
        D = Vector((0, 0, -1))                                      # forearm direction from the bracer: bottom ring - top ring
        if side in bracers:
            bp = [v.co for v in bracers[side]]; lo = min(p.z for p in bp); hi = max(p.z for p in bp); span = hi - lo
            top = [p for p in bp if p.z > hi - 0.25 * span]; bot = [p for p in bp if p.z < lo + 0.25 * span]
            D = (sum(bot, Vector()) / len(bot) - sum(top, Vector()) / len(top)).normalized()
        Fv = Vector((0, -1, 0)); Fv = (Fv - D * Fv.dot(D)).normalized()
        sgn = -1.0 if side == 'Left' else 1.0                       # palm faces the body: -X for the left hand, +X for the right
        Mv = Vector((sgn, 0, 0)); Mv = (Mv - D * Mv.dot(D) - Fv * Mv.dot(Fv)).normalized()
        frames[side] = (Vector((cx, cy, top_z - 0.012)), Mv, Fv, D)  # wrist sits a little inside the bracer so no gap shows
        print(f"FIST {side}: old n={len(mem)} height={max(p.z for p in pts) - min(p.z for p in pts):.3f} forearm dir={tuple(round(x, 3) for x in D)}")
    bmesh.ops.delete(bm, geom=[v for s in fists for v in fists[s]], context='VERTS')
    dvl = bm.verts.layers.deform.verify() if F.vertex_groups else None
    new_idx = {}
    for side, (origin, Mv, Fv, D) in frames.items():
        verts, faces = _fist_mesh(origin, Mv, Fv, D, scale)
        bv = [bm.verts.new(v) for v in verts]
        start = len(bm.verts) - len(bv)
        fl = []
        for f in faces:
            try: fc = bm.faces.new([bv[i] for i in f])
            except ValueError: continue
            fc.smooth = True; fl.append(fc)
        bmesh.ops.recalc_face_normals(bm, faces=fl)
        edges = {e for fc in fl for e in fc.edges}
        print(f"FIST {side}: {len(bv)} verts, {len(fl)} faces of {len(faces)} built, open edges={sum(1 for e in edges if e.is_boundary)}, non-manifold edges={sum(1 for e in edges if not e.is_manifold)}")
        for fc in fl:
            for l in fc.loops: l[uvl].uv = skin_uv
        grp = F.vertex_groups.get(f'mixamorig:{side}Hand') if dvl is not None else None
        if grp is not None:
            for v in bv: v[dvl][grp.index] = 1.0
        new_idx[side] = list(range(start, start + len(bv)))
    bm.verts.ensure_lookup_table()
    bmesh.ops.transform(bm, matrix=mw.inverted(), verts=bm.verts)   # back to the mesh's own space
    bm.to_mesh(F.data); bm.free(); F.data.update()
    print("FIST new vertices", {k: len(v) for k, v in new_idx.items()}, "mesh verts", len(F.data.vertices), "tris", sum(len(p.vertices) - 2 for p in F.data.polygons))
    return new_idx


def render_closeups(F, prefix, side='Left'):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'
    sc.display.shading.single_color = (0.82, 0.62, 0.52)
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x = 800; sc.render.resolution_y = 800
    sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.2, 0.2, 0.24)
    ws = [F.matrix_world @ v.co for v in F.data.vertices]
    zmin = min(w.z for w in ws); H = max(w.z for w in ws) - zmin
    sgn = 1 if side == 'Left' else -1
    target = Vector((sgn * 0.17, -0.05, zmin + H * 0.46))
    cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = 0.22
    cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
    views = {'front': ((0, -3, 0), (90, 0, 0)),
             'lateral': ((sgn * 3, 0, 0), (90, 0, 90 * sgn)),
             'medial': ((-sgn * 3, 0, 0), (90, 0, -90 * sgn))}
    for name, (off, rot) in views.items():
        cam.location = target + Vector(off); cam.rotation_euler = tuple(math.radians(a) for a in rot)
        sc.render.filepath = f"{prefix}_{name}.png"
        bpy.ops.render.render(write_still=True)
        print("FIST rendered", sc.render.filepath)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:]
    src, prefix = argv[0], argv[1]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)
    F = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    bpy.context.view_layer.objects.active = F
    replace_fists(F)
    render_closeups(F, prefix, 'Left')
