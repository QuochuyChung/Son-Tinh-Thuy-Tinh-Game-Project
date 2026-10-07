"""Transfer skin weights from the Mixamo-rigged mannequin onto the complete Meshy mesh, then test-pose it.

Run (from the repo root; see tools/README.md for the whole workflow):
    blender --background --python tools/transfer_weights.py -- <rigged_idle.fbx> <full_textured_meshy.fbx> <preview_dir> [<walk.fbx|-> <run.fbx|-> [<out_skinned.fbx|-> [<cape_push> <cape_widen> <cape_arm_follow>]]]

* the preview sheets (run_side.png, walk_front.png) show the model running/walking in place, to check deformation
* it also prints "clip-through": how far swinging arms end up behind the cape sheet (body height = 1.0), to compare settings
* the skinned-FBX argument exports armature + mesh (no materials) for Unity
* cape_push moves the lower cape backwards, cape_widen flares it, cape_arm_follow (0..1) lets its side edges follow the arms
* cape detection and the thresholds below were tuned for Son Tinh v2; check them for another model"""
import bpy, bmesh, sys, math, os, mathutils
from mathutils import Vector, Matrix
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
rigged, full, out_dir = argv[0], argv[1], argv[2]
walk_fbx = argv[3] if len(argv) > 3 and argv[3] != "-" else None
run_fbx = argv[4] if len(argv) > 4 and argv[4] != "-" else None
export_path = argv[5] if len(argv) > 5 and argv[5] != "-" else None
cape_push = float(argv[6]) if len(argv) > 6 else 0.0      # how far (mesh units) the lower cape is moved backwards so swinging arms do not go through it
cape_widen = float(argv[7]) if len(argv) > 7 else 0.0     # extra sideways flare of the lower cape (fraction)
cape_arm_follow = float(argv[8]) if len(argv) > 8 else 0.0  # 0..1: the cape's side edges at arm height follow the arm bones, so swinging arms push the cape out of the way
os.makedirs(out_dir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)

# ---- 1. rigged mannequin ----
bpy.ops.import_scene.fbx(filepath=rigged)
A = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
M = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
M.name = "Mannequin"
before = set(bpy.context.scene.objects)

# ---- 2. full mesh ----
bpy.ops.import_scene.fbx(filepath=full)
F = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o not in before][0]
F.name = "SonTinh_v2"
print(f"ASM full mesh verts={len(F.data.vertices)} polys={len(F.data.polygons)} scale={tuple(F.scale)}")

# ---- 3. BVH of the mannequin rest pose (world space) ----
mverts = [M.matrix_world @ v.co for v in M.data.vertices]
mpolys = [tuple(p.vertices) for p in M.data.polygons]
bvh = mathutils.bvhtree.BVHTree.FromPolygons(mverts, mpolys)
mw = []   # per mannequin vertex: {bone: weight}
for v in M.data.vertices:
    mw.append({M.vertex_groups[g.group].name: g.weight for g in v.groups if g.weight > 1e-4})
bones = [b.name for b in A.data.bones]

zs = [(F.matrix_world @ v.co).z for v in F.data.vertices]
zmin, zmax = min(zs), max(zs); H = zmax - zmin
fverts = [F.matrix_world @ v.co for v in F.data.vertices]

# ---- 4. components of the full mesh (to find the cape) ----
bm = bmesh.new(); bm.from_mesh(F.data); bm.verts.ensure_lookup_table()
comp = {}; comps = []
for v in bm.verts:
    if v.index in comp: continue
    cid = len(comps); st = [v]; comp[v.index] = cid; mem = []
    while st:
        cur = st.pop(); mem.append(cur.index)
        for e in cur.link_edges:
            w = e.other_vert(cur)
            if w.index not in comp: comp[w.index] = cid; st.append(w)
    comps.append(mem)
bm.free()
cape_ids = set()
for cid, mem in enumerate(comps):
    pts = [fverts[i] for i in mem]
    zlo = (min(p.z for p in pts) - zmin) / H; zhi = (max(p.z for p in pts) - zmin) / H
    width = max(p.x for p in pts) - min(p.x for p in pts)
    if len(mem) >= 300 and zlo < 0.10 and zhi > 0.80 and width > 0.45:
        cape_ids.add(cid)
print(f"ASM components={len(comps)} cape components={len(cape_ids)} verts={sum(len(comps[c]) for c in cape_ids)}")

LEG = ('LeftUpLeg', 'LeftLeg', 'LeftFoot', 'LeftToeBase', 'RightUpLeg', 'RightLeg', 'RightFoot', 'RightToeBase')
ARM = ('LeftArm', 'LeftForeArm', 'LeftHand', 'RightArm', 'RightForeArm', 'RightHand')


def key(n): return n.split(':')[-1]


def fold(w, groups, target):
    out = {}
    for b, x in w.items():
        k = key(b)
        if k in groups: b = 'mixamorig:' + target
        out[b] = out.get(b, 0) + x
    return out


# ---- 5. per-vertex weights ----
raw = []; dists = []
for i, p in enumerate(fverts):
    loc, nrm, fidx, dist = bvh.find_nearest(p)
    poly = mpolys[fidx]
    ws = []; tot = 0.0
    for vi in poly:
        d = (mverts[vi] - loc).length + 1e-5
        ws.append((vi, 1.0 / (d * d))); tot += 1.0 / (d * d)
    w = {}
    for vi, k in ws:
        for b, x in mw[vi].items():
            w[b] = w.get(b, 0) + x * k / tot
    raw.append(w); dists.append(dist)


def avg_dicts(ds):
    out = {}
    for d in ds:
        for b, x in d.items(): out[b] = out.get(b, 0) + x / len(ds)
    return out


def skirt_rules(w, cx):
    w = fold(fold(w, ('LeftLeg', 'LeftFoot', 'LeftToeBase'), 'LeftUpLeg'), ('RightLeg', 'RightFoot', 'RightToeBase'), 'RightUpLeg')
    w = fold(w, ARM, 'Hips')
    if abs(cx) < 0.05:     # centre panel: follow both thighs equally, so it does not tear
        l = w.get('mixamorig:LeftUpLeg', 0); r = w.get('mixamorig:RightUpLeg', 0); a = (l + r) / 2
        if l or r: w['mixamorig:LeftUpLeg'] = a; w['mixamorig:RightUpLeg'] = a
    return w


final = [None] * len(fverts)
kinds = {'body': 0, 'cape': 0, 'skirt': 0, 'rigid': 0}
for cid, mem in enumerate(comps):
    n = len(mem)
    pts = [fverts[i] for i in mem]
    zc = (sum(p.z for p in pts) / n - zmin) / H
    xc = sum(p.x for p in pts) / n
    mean_d = sum(dists[i] for i in mem) / n
    # only the torso piece, the bracers/fists and shoulder ornaments may follow the arm bones;
    # hip fringe that merely hangs next to a fist must not be dragged along by the swinging hand
    arm_ok = n >= 700 or (abs(xc) >= 0.13 and 0.44 <= zc <= 0.78)
    if cid not in cape_ids and n < 200 and abs(xc) >= 0.13 and 0.44 <= zc <= 0.64:
        # fists and forearm bracers: solid pieces, so only hand/forearm bones (Mixamo's left = +X here)
        side = 'Left' if xc > 0 else 'Right'
        for i in mem:
            t = min(max(((fverts[i].z - zmin) / H - 0.50) / 0.10, 0.0), 1.0)
            final[i] = {f'mixamorig:{side}Hand': 1.0 - t, f'mixamorig:{side}ForeArm': t}
        kinds['arm'] = kinds.get('arm', 0) + n
        continue
    if cid in cape_ids:
        for i in mem:
            w = fold(fold(raw[i], LEG, 'Hips'), ARM, 'Spine2')
            if cape_arm_follow > 0:
                x = fverts[i].x; zp_ = (fverts[i].z - zmin) / H
                side = 'Left' if x > 0 else 'Right'
                lat = min(max((abs(x) - 0.08) / 0.12, 0.0), 1.0); lat = lat * lat * (3 - 2 * lat)
                b1 = min(max((zp_ - 0.40) / 0.10, 0.0), 1.0); b2 = min(max((0.80 - zp_) / 0.10, 0.0), 1.0)
                wa = cape_arm_follow * lat * (b1 * b1 * (3 - 2 * b1)) * (b2 * b2 * (3 - 2 * b2))
                if wa > 0:
                    w = {b: x_ * (1 - wa) for b, x_ in w.items()}
                    w[f'mixamorig:{side}Arm'] = w.get(f'mixamorig:{side}Arm', 0) + wa * 0.6
                    w[f'mixamorig:{side}ForeArm'] = w.get(f'mixamorig:{side}ForeArm', 0) + wa * 0.4
            final[i] = w
        kinds['cape'] += n
    elif n < 100 and mean_d >= 0.010:
        # loose attachment (feather, tassel, hair strand, skirt strip): one rigid weight set for the whole piece
        w = avg_dicts([raw[i] for i in mem])
        if not arm_ok: w = fold(w, ARM, 'Hips')
        if 0.20 <= zc <= 0.60: w = skirt_rules(w, xc)
        for i in mem: final[i] = w
        kinds['rigid'] += n
    else:
        for i in mem:
            w = raw[i] if arm_ok else fold(raw[i], ARM, 'Hips')
            if 0.20 <= (fverts[i].z - zmin) / H <= 0.60 and dists[i] >= 0.004:
                w = skirt_rules(dict(w), fverts[i].x); kinds['skirt'] += 1
            else:
                kinds['body'] += 1
            final[i] = w
print("ASM vertex kinds", kinds)

# smooth the cape weights over its surface so it bends instead of showing facets
bm = bmesh.new(); bm.from_mesh(F.data); bm.verts.ensure_lookup_table()
cape_v = [i for c in cape_ids for i in comps[c]]
for it in range(4):
    new = {}
    for i in cape_v:
        nb = [e.other_vert(bm.verts[i]).index for e in bm.verts[i].link_edges]
        if nb:
            a = avg_dicts([final[j] for j in nb]); b = final[i]
            m = {}
            for k in set(a) | set(b): m[k] = 0.5 * a.get(k, 0) + 0.5 * b.get(k, 0)
            new[i] = m
    for i, m in new.items(): final[i] = m
bm.free()

fw = []
for w in final:
    top = sorted(w.items(), key=lambda t: -t[1])[:4]
    s = sum(x for _, x in top) or 1.0
    fw.append([(b, x / s) for b, x in top])

# ---- 5b. move the lower cape away from the body (weights above were computed from the original positions) ----
def smooth(t): t = min(max(t, 0.0), 1.0); return t * t * (3 - 2 * t)
if cape_push or cape_widen:
    for cid in cape_ids:
        for i in comps[cid]:
            zp = (fverts[i].z - zmin) / H
            k = smooth((0.82 - zp) / 0.22)          # 0 at the shoulders, full from about 60 % of the height down to the hem
            co = F.data.vertices[i].co
            co.y += cape_push * k                   # +Y is the back of the character
            co.x *= 1.0 + cape_widen * k
    F.data.update()
    print(f"ASM cape moved back by {cape_push} and widened by {cape_widen}")

# ---- 6. apply to the full mesh ----
F.vertex_groups.clear()
for b in bones: F.vertex_groups.new(name=b)
for i, lst in enumerate(fw):
    for b, x in lst:
        F.vertex_groups[b].add([i], x, 'REPLACE')
mod = F.modifiers.new("Armature", 'ARMATURE'); mod.object = A
F.parent = A
F.matrix_parent_inverse = A.matrix_world.inverted()
M.hide_render = True; M.hide_viewport = True

# ---- 7. test poses ----
def render_frames(action_fbx, label, frames, view):
    pre = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=action_fbx)
    new = [o for o in bpy.context.scene.objects if o not in pre]
    src_arm = [o for o in new if o.type == 'ARMATURE'][0]
    act = src_arm.animation_data.action
    A.animation_data_create(); A.animation_data.action = act
    for o in new: o.hide_render = True; o.hide_viewport = True
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x = 450; sc.render.resolution_y = 700
    sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.22, 0.22, 0.25)
    cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = 1.3
    cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
    if view == 'side':
        cam.location = (-4, 0, 0.0); cam.rotation_euler = (math.radians(90), 0, math.radians(-90))
    else:
        cam.location = (-2.2, -3.2, 0.0); cam.rotation_euler = (math.radians(90), 0, math.radians(-34))
    imgs = []
    A.location = (0, 0, 0); bpy.context.view_layer.update()
    rest_hips = (A.matrix_world @ A.data.bones['mixamorig:Hips'].head_local).copy()
    hips1 = None
    for f in frames:
        sc.frame_set(f)
        A.location = (0, 0, 0)
        bpy.context.view_layer.update()
        hips_now = (A.matrix_world @ A.pose.bones['mixamorig:Hips'].matrix.translation).copy()
        if hips1 is None:
            hips1 = hips_now.copy()
        # keep the character in place: remove the walk/run travel, keep the vertical bob
        A.location = Vector((rest_hips.x - hips_now.x, rest_hips.y - hips_now.y, rest_hips.z - hips1.z))
        bpy.context.view_layer.update()
        print(f"ASM dbg {label} f={f} hips_now=({hips_now.x:.3f},{hips_now.y:.3f},{hips_now.z:.3f}) A.loc=({A.location.x:.3f},{A.location.y:.3f},{A.location.z:.3f})")
        path = os.path.join(out_dir, f"_{label}_{f}.png")
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(path)
        px = np.array(im.pixels[:]).reshape(im.size[1], im.size[0], 4)
        imgs.append(px)
    # arm-through-cape test: how far behind the cape sheet do arm vertices end up, over the whole cycle?
    cape_set = set(i for c in cape_ids for i in comps[c])
    arm_names = {'mixamorig:' + n for n in ARM}
    arm_verts = [i for i, lst in enumerate(fw) if i not in cape_set and sum(x for b, x in lst if b in arm_names) >= 0.6 and (fverts[i].z - zmin) / H < 0.78]
    last = int(act.frame_range[1])
    worst_all = 0.0; worst_frame = 0; worst_cnt = 0
    for f in range(1, last + 1):
        sc.frame_set(f); A.location = (0, 0, 0); bpy.context.view_layer.update()
        Fe2 = F.evaluated_get(bpy.context.evaluated_depsgraph_get()); me2 = Fe2.to_mesh()
        wv = [Fe2.matrix_world @ v.co for v in me2.vertices]
        cpolys = [tuple(p.vertices) for p in me2.polygons if all(v in cape_set for v in p.vertices)]
        tree = mathutils.bvhtree.BVHTree.FromPolygons(wv, cpolys)
        worst = 0.0; cnt = 0
        for i in arm_verts:
            loc, n_, idx_, d = tree.ray_cast(wv[i], Vector((0, -1, 0)), 0.35)   # forward from the vertex: hits the cape if the vertex is behind it
            if loc is not None:
                cnt += 1; worst = max(worst, d)
        Fe2.to_mesh_clear()
        if worst > worst_all: worst_all, worst_frame, worst_cnt = worst, f, cnt
    print(f"ASM clip-through {label}: arm vertices tested={len(arm_verts)} worst depth={worst_all:.3f} (frame {worst_frame}, {worst_cnt} vertices) -- body height is 1.0")
    # stretch analysis at the last frame: which pieces grow the most compared with their rest size?
    sc.frame_set(frames[2]); bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get(); Fe = F.evaluated_get(dg); me = Fe.to_mesh()
    rows = []
    for cid, mem in enumerate(comps):
        if len(mem) < 8: continue
        rest = [fverts[i] for i in mem]
        pos = [Fe.matrix_world @ me.vertices[i].co for i in mem]
        def diag(ps):
            lo = Vector((min(p.x for p in ps), min(p.y for p in ps), min(p.z for p in ps)))
            hi = Vector((max(p.x for p in ps), max(p.y for p in ps), max(p.z for p in ps)))
            return (hi - lo).length
        r0, r1 = diag(rest), diag(pos)
        zc = (sum(p.z for p in rest) / len(rest) - zmin) / H * 100
        xc = sum(p.x for p in rest) / len(rest)
        rows.append((r1 / max(r0, 1e-6), cid, len(mem), r0, r1, zc, xc))
    Fe.to_mesh_clear()
    for r in sorted(rows, reverse=True)[:8]:
        print(f"ASM stretch {label} f={frames[2]} ratio={r[0]:.2f} comp={r[1]} n={r[2]} rest_diag={r[3]:.3f} posed_diag={r[4]:.3f} z%={r[5]:.0f} x={r[6]:+.3f} cape={r[1] in cape_ids}")
    sheet = np.concatenate(imgs, axis=1)
    out = bpy.data.images.new(f"sheet_{label}", sheet.shape[1], sheet.shape[0], alpha=True)
    out.pixels.foreach_set(sheet.astype(np.float32).ravel()); out.filepath_raw = os.path.join(out_dir, f"{label}.png"); out.file_format = 'PNG'; out.save()
    for f in frames: os.remove(os.path.join(out_dir, f"_{label}_{f}.png"))
    print("ASM sheet", out.filepath_raw, "frames", frames, "action range", tuple(act.frame_range))
    return act

if run_fbx: render_frames(run_fbx, "run_side", [1, 6, 11, 16], 'side')
if walk_fbx: render_frames(walk_fbx, "walk_front", [1, 9, 17, 25], 'front')

if export_path:
    A.animation_data_clear()
    # back to the exact bind pose and origin the Mixamo file had (the test poses moved the armature object)
    A.location = (0, 0, 0)
    for pb in A.pose.bones:
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    A.data.pose_position = 'REST'
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    F.data.materials.clear()
    for img in list(bpy.data.images): bpy.data.images.remove(img) if img.users == 0 else None
    bpy.ops.object.select_all(action='DESELECT')
    M.hide_viewport = False
    bpy.data.objects.remove(M, do_unlink=True)
    A.select_set(True); F.select_set(True); bpy.context.view_layer.objects.active = A
    bpy.ops.export_scene.fbx(filepath=export_path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             add_leaf_bones=False, bake_anim=False, embed_textures=False, path_mode='COPY',
                             use_armature_deform_only=False)
    print("ASM exported", export_path)
