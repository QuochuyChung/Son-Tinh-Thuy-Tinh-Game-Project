"""Mị Nương NPC: clothed Meshy body rigged by Mixamo + a cloth panel hanging over the front of the skirt.

    blender -b --python tools/fit_mi_nuong.py -- <idle.fbx> <body_textured.fbx> <drape_textured.fbx> <out_skinned.fbx> <preview_dir> [<clip.fbx> ...] [key=value ...]

* idle.fbx           : Mixamo download WITH SKIN of the untextured Meshy body (no UVs): gives the rig + weights
* body_textured.fbx  : the Meshy textured FBX of the same body: it gets the rig's weights copied on
* drape_textured.fbx : the Meshy textured FBX of the cloth (a long narrow panel with a cord loop and two tassels at the top)
* out_skinned.fbx    : armature + MiNuong_Body + MiNuong_Drape, no materials, bind pose
* preview_dir        : fit_tpose.png, pose_<clip>.png, close_<clip>.png (around the hips) and stretch.txt
* clips              : extra Mixamo clips used only for the previews and the measurements
* key=value          : alen    panel length as a fraction of the body height (default 0.44)
                       atop    height of its top (the cord loop on the belt) as a fraction of the body height (default 0.585)
                       afwd    optional forward lean of the hem, in body heights (default 0: straight down)
                       apct    which percentile of the rows' clearance sets the panel's single forward offset (default 0.75)
                       agap    extra gap in front of that, in body heights (default 0.012)
                       cmargin clearance between panel and body, in body heights (default 0.012)
                       sfar / ssoft / sthigh / swrad / swaist: skirt weights, see the comment in the script
                       blend=<path> also saves the fit (rest pose) as a .blend

Same body steps as tools/fit_hung_vuong.py (copied: weights 1:1 from the rig mesh, finger weights averaged) plus the skirt rule below. The
panel hangs straight down from the belt over the front of the skirt, decorated face out, keeping its own flat shape: the whole panel is moved
forward by one distance (rays, rest pose and the idle's first pose), never bent along the skirt. Weights: top band rigid on Hips, below one chain of 3 bones (Cape_0_* under Hips) for OutfitSpringBones.
"""
import bpy, bmesh, sys, math, os
import mathutils
from mathutils import Vector
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
pos = [a for a in argv if '=' not in a]
opt = dict(a.split('=', 1) for a in argv if '=' in a)
idle_fbx, body_fbx, drape_fbx, out_fbx, out_dir = pos[:5]
clip_fbx = [idle_fbx] + pos[5:]
ALEN = float(opt.get('alen', 0.44)); ATOP = float(opt.get('atop', 0.585)); AFWD = float(opt.get('afwd', 0.0))
APCT = float(opt.get('apct', 0.75)); AGAP = float(opt.get('agap', 0.012))
CMARGIN = float(opt.get('cmargin', 0.012))
os.makedirs(out_dir, exist_ok=True)
NAME = "MiNuong"
report = []
def log(s): print("MN", s); report.append(s)


def import_new(path):
    pre = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.context.scene.objects if o not in pre]


bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

# ---- 1. rig (from the idle) ----
new = import_new(idle_fbx)
A = [o for o in new if o.type == 'ARMATURE'][0]
R = [o for o in new if o.type == 'MESH'][0]
A.name = "Armature"
A.animation_data_clear()
A.data.pose_position = 'REST'
bpy.context.view_layer.update()
rnames = {g.index: g.name for g in R.vertex_groups}
rw = [R.matrix_world @ v.co for v in R.data.vertices]
rpolys = [tuple(p.vertices) for p in R.data.polygons]
rweights = [{rnames[g.group]: g.weight for g in v.groups if g.weight > 1e-4} for v in R.data.vertices]
bz0 = min(p.z for p in rw); bz1 = max(p.z for p in rw); BH = bz1 - bz0
def rel(z): return (z - bz0) / BH
log(f"rig mesh verts={len(rw)} height={BH:.4f} groups={len(rnames)}")

# ---- 2. textured body: copy the weights ----
new = import_new(body_fbx)
B = [o for o in new if o.type == 'MESH'][0]
for o in new:
    if o is not B: bpy.data.objects.remove(o, do_unlink=True)
B.name = f"{NAME}_Body"; B.data.name = f"{NAME}_Body"
bw_ = [B.matrix_world @ v.co for v in B.data.vertices]
kd = mathutils.kdtree.KDTree(len(rw))
for i, p in enumerate(rw): kd.insert(p, i)
kd.balance()
rbvh = mathutils.bvhtree.BVHTree.FromPolygons(rw, rpolys)
bweights = []; exact = 0; far = 0.0
for q in bw_:
    co, idx, d = kd.find(q)
    if d < 1e-4 * BH:
        bweights.append(dict(rweights[idx])); exact += 1; continue
    loc, nrm, fi, dist = rbvh.find_nearest(q)
    far = max(far, dist)
    ws = []; tot = 0.0
    for vi in rpolys[fi]:
        k = 1.0 / ((rw[vi] - loc).length + 1e-6) ** 2; ws.append((vi, k)); tot += k
    w = {}
    for vi, k in ws:
        for b, x in rweights[vi].items(): w[b] = w.get(b, 0) + x * k / tot
    bweights.append(w)
log(f"body: {len(bw_)} verts, {exact} with an exact twin in the rig mesh, worst distance of the others {far / BH:.5f} body heights")
# fused Meshy fingers: Mixamo weights each finger separately, so the webbing between them tears when the hand opens.
# Average the finger weights over the hand (radius `frad` body heights), keeping the hand / forearm part as it was.
FRAD = float(opt.get('frad', 0.012))
def is_finger(b): return any(k in b for k in ('Thumb', 'Index', 'Middle', 'Ring', 'Pinky'))
kdb = mathutils.kdtree.KDTree(len(bw_))
for i, p in enumerate(bw_): kdb.insert(p, i)
kdb.balance()
hand = [i for i, w in enumerate(bweights) if sum(x for b, x in w.items() if is_finger(b)) > 0.01]
for _ in range(3):
    newf = {}
    for i in hand:
        nb = [j for (_, j, _) in kdb.find_range(bw_[i], FRAD * BH)]
        tot_f = sum(x for b, x in bweights[i].items() if is_finger(b))
        acc = {}
        for j in nb:
            fj = sum(x for b, x in bweights[j].items() if is_finger(b)) or 1.0
            for b, x in bweights[j].items():
                if is_finger(b): acc[b] = acc.get(b, 0) + x / fj / len(nb)
        w = {b: x for b, x in bweights[i].items() if not is_finger(b)}
        for b, x in acc.items(): w[b] = w.get(b, 0) + x * tot_f
        newf[i] = w
    for i, w in newf.items(): bweights[i] = w
# skirt: Mixamo gives the long skirt the weights of the leg it happens to be nearest, so the panels tear apart as soon as a leg moves.
# Everything below the waist that stands away from the legs (`sfar` body heights from the thigh / shin bones) follows the hips plus an
# equal share of both thighs that grows towards the hem (`sthigh` at the hem), blended in over `ssoft`; then spatially averaged
# (`swrad`) so the skirt's layers move together. Legs, boots and the skin of the thighs keep Mixamo's weights.
SFAR = float(opt.get('sfar', 0.035)); SSOFT = float(opt.get('ssoft', 0.06)); STHIGH = float(opt.get('sthigh', 0.35)); SWRAD = float(opt.get('swrad', 0.03))
SWAIST = float(opt.get('swaist', 0.61))
def seg_dist(p, a_, b_):
    ab = b_ - a_; t = max(0.0, min(1.0, (p - a_).dot(ab) / max(ab.length_squared, 1e-12))); return (p - (a_ + ab * t)).length
def bw0(n): return A.matrix_world @ A.data.bones['mixamorig:' + n].head_local
legs = [(bw0(f'{s_}UpLeg'), bw0(f'{s_}Leg'), bw0(f'{s_}Foot')) for s_ in ('Left', 'Right')]
skirt_f = [0.0] * len(bw_)
for i, q in enumerate(bw_):
    zr = rel(q.z)
    if zr > SWAIST: continue
    d = min(min(seg_dist(q, u, k), seg_dist(q, k, f_)) for u, k, f_ in legs)
    g = min(max((d / BH - SFAR) / SSOFT, 0.0), 1.0)
    g *= min(max((SWAIST - zr) / 0.04, 0.0), 1.0)             # fade in under the waistband
    if g <= 0: continue
    skirt_f[i] = g
    hem = min(max((SWAIST - zr) / SWAIST, 0.0), 1.0)
    th = STHIGH * hem
    rule = {'mixamorig:Hips': 1.0 - th, 'mixamorig:LeftUpLeg': th / 2, 'mixamorig:RightUpLeg': th / 2}
    w = {}
    fold = min(1.0, 2.0 * g)                                   # near the legs the cloth first stops following the shins / feet (knees bend)
    for k_, x in bweights[i].items():
        sn = k_.split(':')[-1]
        for side in ('Left', 'Right'):
            if sn in (f'{side}Leg', f'{side}Foot', f'{side}ToeBase'):
                w[f'mixamorig:{side}UpLeg'] = w.get(f'mixamorig:{side}UpLeg', 0) + x * fold; x *= 1 - fold
        w[k_] = w.get(k_, 0) + x
    bweights[i] = {k_: w.get(k_, 0) * (1 - g) + rule.get(k_, 0) * g for k_ in set(w) | set(rule)}
sk = [i for i in range(len(bw_)) if skirt_f[i] > 0]
for _ in range(2):
    newsk = {}
    for i in sk:
        nb = [j for (_, j, _) in kdb.find_range(bw_[i], SWRAD * BH) if skirt_f[j] > 0]
        acc = {}
        for j in nb:
            for k_, x in bweights[j].items(): acc[k_] = acc.get(k_, 0) + x / len(nb)
        newsk[i] = acc
    for i, w in newsk.items(): bweights[i] = w
panel = len(sk)
log(f"body: finger weights averaged on {len(hand)} hand verts, {panel} skirt verts follow the hips + both thighs")
# put the body under the armature with the same transform the rig mesh had
B.parent = A; B.matrix_parent_inverse = A.matrix_world.inverted()
B.vertex_groups.clear()
for b in A.data.bones: B.vertex_groups.new(name=b.name)
for i, w in enumerate(bweights):
    top4 = sorted(w.items(), key=lambda t: -t[1])[:4]; s = sum(x for _, x in top4) or 1.0
    for b, x in top4: B.vertex_groups[b].add([i], x / s, 'REPLACE')
mod = B.modifiers.new("Armature", 'ARMATURE'); mod.object = A
bpy.data.objects.remove(R, do_unlink=True)
bpy.context.view_layer.update()

def bone_w(n): return A.matrix_world @ A.data.bones['mixamorig:' + n].head_local
# facing: toes in front of the ankles
fwd = (bone_w('LeftToeBase') + bone_w('RightToeBase') - bone_w('LeftFoot') - bone_w('RightFoot')); fwd.z = 0; fwd.normalize()
log(f"body faces ({fwd.x:+.2f},{fwd.y:+.2f})")

# ---- helpers (as in fit_hung_vuong.py) ----
def posed_body():
    dg = bpy.context.evaluated_depsgraph_get()
    ev = B.evaluated_get(dg); m = ev.to_mesh()
    pts = [B.matrix_world @ v.co for v in m.vertices]; ev.to_mesh_clear()
    return pts
bpolys = [tuple(p.vertices) for p in B.data.polygons]
def inside_test(bvh, pts):
    # body normals may point either way after the import: decide with a point that is surely inside (Spine1)
    ref = A.matrix_world @ A.pose.bones['mixamorig:Spine1'].head   # current pose (= rest when the pose is reset)
    l0, n0, i0, d0 = bvh.find_nearest(ref)
    return 1.0 if (ref - l0).dot(n0) < 0 else -1.0

# body faces without the arms (the cape is not widened for the arms: in T-pose they are up, in the clips they move in front of it)
ARMS = ('Arm', 'Hand', 'Shoulder')
def is_arm(i):
    w = bweights[i]
    if not w: return False
    top = max(w.items(), key=lambda t: t[1])[0].split(':')[-1]
    return any(k in top for k in ARMS) and sum(x for b_, x in w.items() if any(k in b_ for k in ARMS)) > 0.5
arm_v = [is_arm(i) for i in range(len(bw_))]
torso_polys = [p for p in bpolys if not any(arm_v[i] for i in p)]
def set_action(act):
    A.animation_data_create(); A.animation_data.action = act
    try: A.animation_data.action_slot = act.slots[0]
    except Exception: pass
def reset_pose():
    A.animation_data_clear()
    for p_ in A.pose.bones:
        p_.location = (0, 0, 0); p_.rotation_quaternion = (1, 0, 0, 0); p_.rotation_euler = (0, 0, 0); p_.scale = (1, 1, 1)
    bpy.context.view_layer.update()

def push_back(cpts, body_pts, back):
    """Move each row of a drape straight back (along `back`) until it clears the body + hair by CMARGIN, smoothed over the height so the
    cloth keeps its shape; whatever still pokes in afterwards (a few folds) goes just outside along the body normal."""
    bvh = mathutils.bvhtree.BVHTree.FromPolygons(body_pts, torso_polys)
    outward = inside_test(bvh, body_pts)
    NB = 50; zt = max(q.z for q in cpts); zb = min(q.z for q in cpts); span = max(zt - zb, 1e-6)
    band = [min(int((zt - q.z) / span * NB), NB - 1) for q in cpts]
    need = [0.0] * NB
    for i, q in enumerate(cpts):
        # the body surface behind which this point should stay: cast from far in front, along `back`, and take the last exit
        start = q - back * 0.5 * BH; p = start; last = None
        for _ in range(40):
            hit = bvh.ray_cast(p + back * 1e-5, back, 1.0 * BH)
            if hit[0] is None: break
            p = hit[0]
            last = p                                             # the outermost surface along the ray (Meshy normals are not reliable)
        if last is None: continue
        d = (last - q).dot(back) + CMARGIN * BH                     # how far behind q the clear position is
        if d > 0: need[band[i]] = max(need[band[i]], d)
    raw = list(need)
    for _ in range(3): need = [max(need[j], (need[max(j - 1, 0)] + need[j] + need[min(j + 1, NB - 1)]) / 3) for j in range(NB)]
    for _ in range(4): need = [(need[max(j - 1, 0)] + 2 * need[j] + need[min(j + 1, NB - 1)]) / 4 for j in range(NB)]
    need = [max(n_, r_) for n_, r_ in zip(need, raw)]          # smoothing must never leave a row inside the body
    out = [q + back * need[band[i]] for i, q in enumerate(cpts)]
    # no snapping along the body normal afterwards (it buried the drapes): the long hair's normals are scrambled
    return out, 0, max(need) / BH

# ---- 3. apron: the cloth hangs over the front of the skirt, from the belt down between the legs ----
# (07/10: in the owner's reference the Meshy "banner" with its cord loop and two tassels is the front panel of the skirt, not a cape)
ANCHOR = 'mixamorig:Hips'
back = fwd                                                  # the direction the cloth is pushed away from the body: forwards
side_axis = Vector((-fwd.y, fwd.x, 0))                      # the character's left
new = import_new(drape_fbx)
C = [o for o in new if o.type == 'MESH'][0]
for o in new:
    if o is not C: bpy.data.objects.remove(o, do_unlink=True)
C.name = f"{NAME}_Drape"; C.data.name = f"{NAME}_Drape"
pts0 = [C.matrix_world @ v.co for v in C.data.vertices]
z1 = max(p.z for p in pts0); z0 = min(p.z for p in pts0); H = z1 - z0
cx_ = sum(p.x for p in pts0) / len(pts0); cy_ = sum(p.y for p in pts0) / len(pts0)
s = ALEN * BH / H
hips = bone_w('Hips')
top = Vector((hips.x, hips.y, bz0 + ATOP * BH))
fitted = []
for p in pts0:
    t = (z1 - p.z) / H
    # the Meshy file faces -Y like the body (decorated face towards the viewer): keep x / y as they are, relative to the panel centre
    q = top + Vector(((p.x - cx_) * s, (p.y - cy_) * s, -(z1 - p.z) * s))
    q += fwd * (AFWD * BH * t)                               # optional lean of the hem (default 0: hangs straight down)
    fitted.append(q)
log(f"apron: {len(pts0)} verts, scale {s:.4f}, top at {rel(top.z):.3f}, hem at {rel(min(q.z for q in fitted)):.3f}, "
    f"width {(max(q.dot(side_axis) for q in fitted) - min(q.dot(side_axis) for q in fitted)) / BH:.3f} BH")
# Kept flat and straight (07/10, "the panel curls into the skirt"): the whole panel moves forward by ONE distance, never row by row, so it
# never follows the shape of the skirt / legs. The distance: how far each row would have to move to clear the outermost surface in front of
# the body (rays), taken in T-pose and in the idle's first pose; the `apct` percentile of the rows (the body's own thin cords and tassels
# hanging from the belt would otherwise push it far out), plus `agap`.
def row_needs(cpts, body_pts, d):
    bvh = mathutils.bvhtree.BVHTree.FromPolygons(body_pts, torso_polys)
    NB = 40; zt = max(q.z for q in cpts); zb = min(q.z for q in cpts); span = max(zt - zb, 1e-6)
    need = [0.0] * NB
    for q in cpts:
        k = min(int((zt - q.z) / span * NB), NB - 1)
        p = q - d * 0.5 * BH; last = None
        for _ in range(40):
            hit = bvh.ray_cast(p + d * 1e-5, d, 1.0 * BH)
            if hit[0] is None: break
            p = hit[0]; last = p
        if last is not None: need[k] = max(need[k], (last - q).dot(d))
    return need
def robust(need): v = sorted(need); return v[min(int(len(v) * APCT), len(v) - 1)]
n_rest = row_needs(fitted, bw_, back)
new = import_new(idle_fbx)
idle_act = [o for o in new if o.type == 'ARMATURE'][0].animation_data.action
for o in new: bpy.data.objects.remove(o, do_unlink=True)
A.data.pose_position = 'POSE'; set_action(idle_act); sc.frame_set(1); bpy.context.view_layer.update()
pb = A.pose.bones[ANCHOR]
M = A.matrix_world @ pb.matrix @ pb.bone.matrix_local.inverted() @ A.matrix_world.inverted()
n_idle = row_needs([M @ q for q in fitted], posed_body(), (M.to_3x3() @ back).normalized())
off = max(robust(n_rest), robust(n_idle)) + AGAP * BH
fitted = [q + back * off for q in fitted]
log("apron: rows' clearance (T-pose, BH): " + " ".join(f"{x / BH:.3f}" for x in n_rest[::4]) + " | idle: " + " ".join(f"{x / BH:.3f}" for x in n_idle[::4]))
log(f"apron: whole panel moved forward {off / BH:.3f} BH (percentile {APCT:.2f} of the rows + gap {AGAP:.3f})")
reset_pose(); A.data.pose_position = 'REST'; bpy.context.view_layer.update()
for v, q in zip(C.data.vertices, fitted): v.co = C.matrix_world.inverted() @ q
C.data.update()
cfit = fitted

# ---- 4. weights: top band rigid on Hips, below one spring chain (Cape_0_*) under Hips ----
C.parent = A; C.matrix_parent_inverse = A.matrix_world.inverted()
NSEG = 3; T_LEV = [0.16, 0.44, 0.72, 1.0]
zt = max(q.z for q in cfit); zb = min(q.z for q in cfit); Hd = zt - zb
joints = []
for tk in T_LEV:
    sel = [q for q in cfit if abs((zt - q.z) / Hd - tk) < 0.05] or [min(cfit, key=lambda q: abs((zt - q.z) / Hd - tk))]
    joints.append(Vector((sum(q.x for q in sel) / len(sel), sum(q.y for q in sel) / len(sel), zt - tk * Hd)))
bpy.context.view_layer.objects.active = A; A.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
inv = A.matrix_world.inverted(); eb = A.data.edit_bones
names = {}; prev = eb[ANCHOR]
for j in range(NSEG):
    nm = f"Cape_0_{j}"; b_ = eb.new(nm)
    b_.head = inv @ joints[j]; b_.tail = inv @ joints[j + 1]
    b_.parent = prev; b_.use_connect = False; b_.use_deform = True
    prev = b_; names[j] = nm
bpy.ops.object.mode_set(mode='OBJECT')
cweights = []
for q in cfit:
    t = (zt - q.z) / Hd
    if t <= T_LEV[0]: cweights.append({ANCHOR: 1.0}); continue
    j = 0
    while j < NSEG - 1 and t > T_LEV[j + 1]: j += 1
    sgm = j + min((t - T_LEV[j]) / (T_LEV[j + 1] - T_LEV[j]), 1.0)
    wj = [max(0.0, 1.0 - abs(sgm - (n + 0.5))) for n in range(NSEG)]
    if sgm > NSEG - 0.5: wj = [0.0] * (NSEG - 1) + [1.0]
    fade = min((t - T_LEV[0]) / 0.08, 1.0)
    w = {}
    anchor = max(0.0, 1.0 - sum(wj) * fade)
    if anchor > 0: w[ANCHOR] = anchor
    for n in range(NSEG):
        if wj[n] > 0: w[names[n]] = wj[n] * fade
    cweights.append(w)
log(f"apron bones: 1 chain x {NSEG} under Hips, joints at {[round(rel(j_.z), 3) for j_ in joints]} of body height")
C.vertex_groups.clear()
for b_ in A.data.bones: C.vertex_groups.new(name=b_.name)
for i, w in enumerate(cweights):
    top4 = sorted(w.items(), key=lambda t: -t[1])[:4]; s_ = sum(x for _, x in top4) or 1.0
    for b_, x in top4: C.vertex_groups[b_].add([i], x / s_, 'REPLACE')
mod = C.modifiers.new("Armature", 'ARMATURE'); mod.object = A
bpy.context.view_layer.update()

# ---- 5. previews + measurements ----
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'
sc.view_settings.view_transform = 'Standard'
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.3, 0.3, 0.33)
cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'
cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
B.color = (0.86, 0.68, 0.58, 1); C.color = (0.75, 0.15, 0.15, 1)
cz = bz0 + BH / 2
def shot(path, view, w=600, h=900, scale=None, center=None, flat=False):
    sc.render.resolution_x = w; sc.render.resolution_y = h
    sc.display.shading.color_type = 'OBJECT' if flat else 'TEXTURE'
    cd.ortho_scale = scale or BH * 1.15
    if center is None:
        bp = eval_pts(B); center = Vector(((min(p.x for p in bp) + max(p.x for p in bp)) / 2, (min(p.y for p in bp) + max(p.y for p in bp)) / 2, (min(p.z for p in bp) + max(p.z for p in bp)) / 2))
    c = center
    a = math.radians({'front': 0, 'back': 180, 'side': 90, 'q': 35, 'bq': 145, 'bq2': -145}[view])
    d = Vector((fwd.x * math.cos(a) - fwd.y * math.sin(a), fwd.x * math.sin(a) + fwd.y * math.cos(a), 0))   # character -> camera
    cam.location = c + d * 4; cam.rotation_euler = (math.radians(90), 0, math.atan2(d.x, -d.y))
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)
def sheet(paths, out, cols=None):
    imgs = []
    for p in paths:
        im = bpy.data.images.load(p); imgs.append(np.array(im.pixels[:]).reshape(im.size[1], im.size[0], 4)); bpy.data.images.remove(im); os.remove(p)
    cols = cols or len(imgs)
    rows = [np.concatenate(imgs[i:i + cols] + [np.zeros_like(imgs[0])] * (cols - len(imgs[i:i + cols])), axis=1) for i in range(0, len(imgs), cols)]
    s_ = np.concatenate(rows[::-1], axis=0)                  # Blender images are stored bottom row first: first row on top
    o = bpy.data.images.new("sheet", s_.shape[1], s_.shape[0], alpha=True)
    o.pixels.foreach_set(s_.astype(np.float32).ravel()); o.filepath_raw = out; o.file_format = 'PNG'; o.save(); bpy.data.images.remove(o)

# bind lengths
def edges_of(obj): return [tuple(e.vertices) for e in obj.data.edges]
bind = {}
for obj in (B, C):
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    E = [(a, b) for a, b in edges_of(obj) if (pts[a] - pts[b]).length > 0.002 * BH]
    bind[obj.name] = (E, np.array([(pts[a] - pts[b]).length for a, b in E]))
def eval_pts(obj):
    dg = bpy.context.evaluated_depsgraph_get(); ev = obj.evaluated_get(dg); m = ev.to_mesh()
    p = [obj.matrix_world @ v.co for v in m.vertices]; ev.to_mesh_clear(); return p
def measure():
    res = {}
    for obj in (B, C):
        E, L0 = bind[obj.name]; p = eval_pts(obj)
        L = np.array([(p[a] - p[b]).length for a, b in E]); r = L / L0   # stretch (tears); squashing at the armpits is normal
        res[obj.name] = (float(r.max()), int((r > 1.3).sum()))
        if obj is B:
            bad = np.argsort(-r)[:8]
            res['where'] = [(round(float(r[j]), 2), round(rel((p[E[j][0]] + p[E[j][1]]).z / 2 - (min(q.z for q in p) - bz0)), 3), round(bw_[E[j][0]].x / BH, 3), round(rel(bw_[E[j][0]].z), 3),
                             max(bweights[E[j][0]].items(), key=lambda t: t[1])[0].split(':')[-1], max(bweights[E[j][1]].items(), key=lambda t: t[1])[0].split(':')[-1]) for j in bad]
    # CapeIn: drape vertices in front of the body's back surface (body + hair, not the arms), measured like the fit: a ray along the
    # current outward direction (turned with the anchor bone) from behind the surface, last exit = the surface (normals are not trusted)
    bp = eval_pts(B); cp = eval_pts(C)
    bvh = mathutils.bvhtree.BVHTree.FromPolygons(bp, torso_polys); outward = inside_test(bvh, bp)
    pb_ = A.pose.bones[ANCHOR]
    Mp = A.matrix_world @ pb_.matrix @ pb_.bone.matrix_local.inverted() @ A.matrix_world.inverted()
    bk = (Mp.to_3x3() @ back).normalized()
    n_in = 0
    for q in cp[::4]:
        p = q - bk * 0.5 * BH; last = None
        for _ in range(40):
            hit = bvh.ray_cast(p + bk * 1e-5, bk, 1.0 * BH)
            if hit[0] is None: break
            p = hit[0]
            if (hit[1].dot(bk) * outward) > 0: last = p
        if last is not None and (last - q).dot(bk) > 0.002 * BH: n_in += 4
    res['CapeIn'] = n_in
    return res

A.data.pose_position = 'POSE'; reset_pose()
m = measure()
log(f"stretch T-pose: body max {m[B.name][0]:.3f} (>1.3: {m[B.name][1]}), cape max {m[C.name][0]:.3f} (>1.3: {m[C.name][1]}), CapeIn {m['CapeIn']}")
P = []
for v in ('front', 'q', 'side', 'bq', 'back'):
    p = os.path.join(out_dir, f"_t_{v}.png"); shot(p, v); P.append(p)
sheet(P, os.path.join(out_dir, "fit_tpose.png"))

for path in clip_fbx:
    clip = os.path.splitext(os.path.basename(path))[0].replace('Mi-Nuong_', '')
    new = import_new(path)
    act = [o for o in new if o.type == 'ARMATURE'][0].animation_data.action
    for o in new: bpy.data.objects.remove(o, do_unlink=True)
    set_action(act); last = int(act.frame_range[1])
    worst = {B.name: (0, 0, 0), C.name: (0, 0, 0)}; worst_in = (0, 0)
    for f in range(1, last + 1, 3):
        sc.frame_set(f); bpy.context.view_layer.update(); m = measure()
        for k in (B.name, C.name):
            if m[k][0] > worst[k][0]: worst[k] = (m[k][0], m[k][1], f)
            if k == B.name and m[k][0] >= worst[k][0]: wh = m['where']
        if m['CapeIn'] > worst_in[0]: worst_in = (m['CapeIn'], f)
    log(f"stretch {clip} ({last} frames): body max {worst[B.name][0]:.3f} at f{worst[B.name][2]} (>1.3: {worst[B.name][1]}), "
        f"cape max {worst[C.name][0]:.3f} at f{worst[C.name][2]} (>1.3: {worst[C.name][1]}), CapeIn max {worst_in[0]} at f{worst_in[1]}")
    log(f"  worst body edges (ratio, -, bind x, bind z, bones): {[(w[0], w[2], w[3], w[4], w[5]) for w in wh]}")
    frames = sorted(set([1, max(1, last // 3), max(1, (2 * last) // 3), last]))
    P = []; Q = []
    for f in frames:
        sc.frame_set(f); bpy.context.view_layer.update()
        for v in ('front', 'bq'):
            p = os.path.join(out_dir, f"_p_{v}_{f}.png"); shot(p, v, w=450, h=800); P.append(p)
        # close-ups: collar / shoulders / back
        hc = A.matrix_world @ A.pose.bones['mixamorig:Hips'].head
        for v in ('q', 'bq', 'bq2'):
            p = os.path.join(out_dir, f"_c_{v}_{f}.png"); shot(p, v, w=420, h=420, scale=BH * 0.42, center=hc); Q.append(p)
    sheet(P, os.path.join(out_dir, f"pose_{clip}.png"), cols=4)
    sheet(Q, os.path.join(out_dir, f"close_{clip}.png"), cols=6)
A.animation_data_clear()
with open(os.path.join(out_dir, "stretch.txt"), "w", encoding="utf-8") as fh: fh.write("\n".join(report) + "\n")

# ---- 6. export (bind pose) ----
for p_ in A.pose.bones:
    p_.location = (0, 0, 0); p_.rotation_quaternion = (1, 0, 0, 0); p_.rotation_euler = (0, 0, 0); p_.scale = (1, 1, 1)
A.data.pose_position = 'REST'; A.location = (0, 0, 0)
sc.frame_set(1); bpy.context.view_layer.update()
if opt.get('blend'):
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(opt['blend']), copy=True)
    log(f"saved {opt['blend']}")
for o in (B, C): o.data.materials.clear()
bpy.ops.object.select_all(action='DESELECT')
for o in (A, B, C): o.select_set(True)
bpy.context.view_layer.objects.active = A
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         add_leaf_bones=False, bake_anim=False, embed_textures=False, path_mode='COPY',
                         use_armature_deform_only=False)
log(f"exported {out_fbx}")
