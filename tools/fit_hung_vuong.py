"""Hùng Vương NPC: clothed Meshy body rigged by Mixamo + a separately generated cape.

    blender -b --python tools/fit_hung_vuong.py -- <idle.fbx> <body_textured.fbx> <cape_textured.fbx> <out_skinned.fbx> <preview_dir> [<clip.fbx> ...] [key=value ...]

* idle.fbx          : Mixamo download WITH SKIN of the untextured Meshy body (its mesh has no UVs); gives the rig + weights
* body_textured.fbx : the Meshy textured FBX of the same body (same mesh, with UVs): it gets the rig's weights copied on
* cape_textured.fbx : the Meshy textured FBX of the cape (collar ring at the top, sheet hanging behind)
* out_skinned.fbx   : armature + HungVuong_Body + HungVuong_Cape, no materials (assign them in Unity), bind pose
* preview_dir       : fit_*.png, pose_<clip>.png, close_<clip>.png and stretch.txt
* clips             : extra Mixamo clips (talking, pointing, nod ...) used only for the previews and the measurements
* key=value         : clen  cape length as a fraction of the body height (default 0.60)
                      cdrop collar top below the Neck joint, in body heights (default 0.0)
                      cmargin clearance between cape and body, in body heights (default 0.012)
                      capebones 1 = Cape_<column>_<segment> chains under Spine2 for OutfitSpringBones (default 1)
                      blend=<path> also saves the fit (rest pose) as a .blend

Body weights: the textured body is the same Meshy mesh as the rigged one, so every vertex takes the weights of the rigged vertex
at the same place (nearest surface point, interpolated, if a vertex has no exact twin).
Cape: scaled uniformly to `clen` of the body height, collar ring centred on the Neck joint, then pushed out of the body (rest
pose and the idle's first pose, arms down) along the body normal. Weights are rigid: the collar / shoulders 100 % Spine2, the
sheet below on 5 chains x 3 bones hanging from Spine2 that do nothing in Blender (= rigid on Spine2) and sway gently in Unity
with OutfitSpringBones.
Measurements (stretch.txt): edge length / bind length, T-pose and every 3rd frame of every clip (edges under 0.002 body heights
skipped), and CapeIn = cape vertices that end up inside the body.
"""
import bpy, bmesh, sys, math, os
import mathutils
from mathutils import Vector
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
pos = [a for a in argv if '=' not in a]
opt = dict(a.split('=', 1) for a in argv if '=' in a)
idle_fbx, body_fbx, cape_fbx, out_fbx, out_dir = pos[:5]
clip_fbx = [idle_fbx] + pos[5:]
CLEN = float(opt.get('clen', 0.60)); CDROP = float(opt.get('cdrop', 0.0)); CMARGIN = float(opt.get('cmargin', 0.012))
os.makedirs(out_dir, exist_ok=True)
NAME = "HungVuong"
report = []
def log(s): print("HV", s); report.append(s)


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
# front / back panel hanging between the legs: both thighs equally near the middle, so it does not tear when the legs part
PBAND = float(opt.get('pband', 0.06))
cx = (bone_w0 := (A.matrix_world @ A.data.bones['mixamorig:Hips'].head_local)).x
panel = 0
for i, q in enumerate(bw_):
    w = bweights[i]; l = w.get('mixamorig:LeftUpLeg', 0); r = w.get('mixamorig:RightUpLeg', 0)
    if l + r < 0.05 or not (0.25 < rel(q.z) < 0.56): continue
    f = 1.0 - min(abs(q.x - cx) / (PBAND * BH), 1.0)
    if f <= 0: continue
    a = (l + r) / 2
    w['mixamorig:LeftUpLeg'] = l + (a - l) * f; w['mixamorig:RightUpLeg'] = r + (a - r) * f
    for side in ('Left', 'Right'):     # shins of the panel follow the thigh, not the knee
        for b in (f'mixamorig:{side}Leg', f'mixamorig:{side}Foot'):
            if b in w:
                x = w[b] * f; w[b] -= x
                w['mixamorig:LeftUpLeg'] += x / 2; w['mixamorig:RightUpLeg'] += x / 2
    panel += 1
log(f"body: finger weights averaged on {len(hand)} hand verts, {panel} verts of the middle panel share both thighs")
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

# ---- 3. cape: scale, place on the neck ----
new = import_new(cape_fbx)
C = [o for o in new if o.type == 'MESH'][0]
for o in new:
    if o is not C: bpy.data.objects.remove(o, do_unlink=True)
C.name = f"{NAME}_Cape"; C.data.name = f"{NAME}_Cape"
cw0 = [C.matrix_world @ v.co for v in C.data.vertices]
cz0 = min(p.z for p in cw0); cz1 = max(p.z for p in cw0); CH = cz1 - cz0
ring = [p for p in cw0 if (p.z - cz0) / CH > 0.95]
rcx = sum(p.x for p in ring) / len(ring); rcy = sum(p.y for p in ring) / len(ring)
mid = [p for p in cw0 if 0.3 < (p.z - cz0) / CH < 0.7]
mcy = sum(p.y for p in mid) / len(mid)
# the sheet hangs behind the collar: if it lies on the body's front side, turn the cape round
flip = (mcy - rcy) * fwd.y > 0
s = CLEN * BH / CH
neck = bone_w('Neck'); spine2 = bone_w('Spine2')
cfit = []
for p in cw0:
    dx, dy = (p.x - rcx) * s, (p.y - rcy) * s
    if flip: dx, dy = -dx, -dy
    cfit.append(Vector((neck.x + dx, neck.y + dy, neck.z - CDROP * BH - (cz1 - p.z) * s)))
log(f"cape: {len(cw0)} verts, scale {s:.4f} (height {CLEN:.2f} of the body), turned round={flip}, collar ring width {(max(p.x for p in ring) - min(p.x for p in ring)) * s / BH:.3f} BH, "
    f"top at {rel(max(q.z for q in cfit)):.3f}, hem at {rel(min(q.z for q in cfit)):.3f} of body height")

# no collar (06/10, "drop the collar piece, just attach the cape to the robe"): the stand-up collar ring (the dense top `ccut` 0.08 of the
# Meshy cape) folded and crumpled round the neck in Unity. It is deleted with any loose bits left over, and the sheet's top edge, now at the
# base of the neck, is laid onto the shoulders / upper back by the hug step below.
CCUT = float(opt.get('ccut', 0.08))
if CCUT > 0:
    for v, q in zip(C.data.vertices, cfit): v.co = C.matrix_world.inverted() @ q
    bm = bmesh.new(); bm.from_mesh(C.data); bm.verts.ensure_lookup_table()
    kill = {v for v in bm.verts if (cz1 - cw0[v.index].z) / CH < CCUT}
    # loose pieces that are left (bits of the collar band hanging below the cut)
    seen = set(); loose = set()
    for v0 in bm.verts:
        if v0 in seen or v0 in kill: continue
        comp = [v0]; seen.add(v0); k = 0
        while k < len(comp):
            for e in comp[k].link_edges:
                o_ = e.other_vert(comp[k])
                if o_ not in seen and o_ not in kill: seen.add(o_); comp.append(o_)
            k += 1
        if len(comp) < 40: loose.update(comp)
    bmesh.ops.delete(bm, geom=list(kill | loose), context='VERTS')
    bm.to_mesh(C.data); bm.free(); C.data.update()
    cfit = [C.matrix_world @ v.co for v in C.data.vertices]
    log(f"cape: collar removed ({len(kill)} verts above {CCUT:.2f} of the cape, {len(loose)} in loose bits), {len(cfit)} left, top now at {rel(max(q.z for q in cfit)):.3f} of body height")

# push out of the body: rest pose, then the idle's first frame (arms down) - measured on the posed body, moved back by the inverse skin
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

def push(cpts, body_pts):
    """Widen the cape row by row (radially about the vertical line through the neck) until it clears the body, so the drape keeps its shape;
    what still pokes in after that (a few folds) is moved out along the body normal."""
    bvh = mathutils.bvhtree.BVHTree.FromPolygons(body_pts, torso_polys)
    outward = inside_test(bvh, body_pts)
    ax = AXIS[0]
    NB = 60; zt = max(q.z for q in cpts); zb = min(q.z for q in cpts); span = zt - zb
    band = [min(int((zt - q.z) / span * NB), NB - 1) for q in cpts]
    need = [[[] for _ in range(NB)], [[] for _ in range(NB)]]     # [0] sideways (x), [1] front/back (y)
    for i, q in enumerate(cpts):
        h = Vector((q.x - ax.x, q.y - ax.y, 0)); r = h.length
        if r < 1e-4: continue
        d = h / r; comp = 0 if abs(h.x) > abs(h.y) else 1
        loc, nrm, idx, dist = bvh.find_nearest(q)
        if loc is None or dist > 0.1 * BH: continue
        inside = ((q - loc).dot(nrm) * outward) < 0
        if not inside and dist >= CMARGIN * BH: continue
        # walk outward from the vertex until the body is left behind
        k = 1.0; p = q
        for _ in range(40):
            hit = bvh.ray_cast(p + d * 1e-5, d, 0.3 * BH)
            if hit[0] is None: break
            p = hit[0]
            if (hit[1].dot(d) * outward) > 0:             # leaving the body
                e = Vector((p.x - ax.x, p.y - ax.y, 0)); e += e.normalized() * CMARGIN * BH
                k = max(k, abs(e[comp]) / max(abs(h[comp]), 1e-4))
        need[comp][band[i]].append(min(k, 2.0))
    def profile(n):
        cnt = [len(x) for x in n]
        kb = [sorted(x)[int(len(x) * 0.9)] if len(x) >= 3 else 1.0 for x in n]
        for _ in range(4): kb = [max(kb[j], (kb[max(j - 1, 0)] + kb[j] + kb[min(j + 1, NB - 1)]) / 3) for j in range(NB)]
        for _ in range(4): kb = [(kb[max(j - 1, 0)] + 2 * kb[j] + kb[min(j + 1, NB - 1)]) / 4 for j in range(NB)]
        return kb
    kx = profile(need[0]); ky = profile(need[1])
    out = []
    for i, q in enumerate(cpts):
        out.append(Vector((ax.x + (q.x - ax.x) * kx[band[i]], ax.y + (q.y - ax.y) * ky[band[i]], q.z)))
    kb = [max(a_, b_) for a_, b_ in zip(kx, ky)]
    log("  widening sideways by row: " + " ".join(f"{x:.2f}" for x in kx[::6]) + " | backwards: " + " ".join(f"{x:.2f}" for x in ky[::6]))
    moved = 0; res = []
    for q in out:
        loc, nrm, idx, dist = bvh.find_nearest(q)
        if loc is not None and dist < 0.08 * BH and ((q - loc).dot(nrm) * outward) < 0:
            q = loc + nrm * outward * (CMARGIN * BH * 0.5); moved += 1
        res.append(q)
    return res, moved, max(kb)

AXIS = [neck]
cfit, moved, kmax = push(cfit, bw_)
log(f"cape: widened up to x{kmax:.3f} to clear the body in T-pose, {moved} verts still moved along the normal")

# arms down (idle frame 1): clear the torso/legs in that pose too; the cape is rigid on Spine2, so a move in the posed frame is the
# same move rotated back by Spine2's pose matrix
new = import_new(idle_fbx)
src = [o for o in new if o.type == 'ARMATURE'][0]
idle_act = src.animation_data.action
for o in new: bpy.data.objects.remove(o, do_unlink=True)
def set_action(act):
    A.animation_data_create(); A.animation_data.action = act
    try: A.animation_data.action_slot = act.slots[0]
    except Exception: pass
def reset_pose():
    A.animation_data_clear()
    for p_ in A.pose.bones:
        p_.location = (0, 0, 0); p_.rotation_quaternion = (1, 0, 0, 0); p_.rotation_euler = (0, 0, 0); p_.scale = (1, 1, 1)
    bpy.context.view_layer.update()
A.data.pose_position = 'POSE'; set_action(idle_act); sc.frame_set(1); bpy.context.view_layer.update()
pb = A.pose.bones['mixamorig:Spine2']
M = A.matrix_world @ pb.matrix @ pb.bone.matrix_local.inverted() @ A.matrix_world.inverted()   # rest -> posed for a Spine2-rigid point
Minv = M.inverted()
AXIS = [M @ neck]
posed2, moved2, kmax2 = push([M @ q for q in cfit], posed_body())
cfit = [Minv @ q for q in posed2]
log(f"cape: widened up to x{kmax2:.3f} more in the idle's first pose, {moved2} verts moved along the normal")
reset_pose(); A.data.pose_position = 'REST'; bpy.context.view_layer.update()
# collar hugs the body (06/10, "the collar is too stiff, make it soft and lie on him"): the top `ctop` of the cape (collar ring +
# shoulder drape) is drawn onto the neck / shoulders, `cgap` body heights off the skin, full strength above `chug` and fading out
# below; the move is smoothed along the cape's edges so the collar's layers keep their thickness. Those vertices later take the
# skin's own weights (soft: they move with neck, clavicles and spine instead of being rigid on Spine2).
CTOP = float(opt.get('ctop', 0.14)); CHUG = float(opt.get('chug', 0.05)); CGAP = float(opt.get('cgap', 0.006))
z_top0 = max(q.z for q in cfit); Hm0 = z_top0 - min(q.z for q in cfit)
def hug_of(q):
    u = min(max((CTOP - (z_top0 - q.z) / Hm0) / (CTOP - CHUG), 0.0), 1.0)   # 1 above chug, 0 below ctop
    return u * u * (3 - 2 * u)
hug = [hug_of(q) for q in cfit]
tbvh = mathutils.bvhtree.BVHTree.FromPolygons(bw_, torso_polys); tout = inside_test(tbvh, bw_)
disp = []; near_poly = []
for i, q in enumerate(cfit):
    if hug[i] <= 0: disp.append(Vector()); near_poly.append(None); continue
    loc, nrm, idx, dist = tbvh.find_nearest(q)
    disp.append((loc + nrm * tout * CGAP * BH) - q); near_poly.append(idx)
cadj = [[] for _ in cfit]
for e in C.data.edges: a_, b_ = e.vertices; cadj[a_].append(b_); cadj[b_].append(a_)
for _ in range(6):
    disp = [(disp[i] * 2 + sum((disp[j] for j in cadj[i] if hug[j] > 0), Vector())) / (2 + sum(1 for j in cadj[i] if hug[j] > 0)) if hug[i] > 0 else disp[i] for i in range(len(cfit))]
cfit = [q + disp[i] * hug[i] for i, q in enumerate(cfit)]
# whatever still ends up under the skin goes just outside it
fixed = 0
for i, q in enumerate(cfit):
    if hug[i] <= 0: continue
    loc, nrm, idx, dist = tbvh.find_nearest(q)
    if ((q - loc).dot(nrm) * tout) < CGAP * BH * 0.5:
        cfit[i] = loc + nrm * tout * CGAP * BH * 0.5; fixed += 1
# skin weights under each top-edge vertex (arm weights -> that side's clavicle, neck / head -> Spine2)
def collar_fold(w):
    out = {}
    for b, x in w.items():
        s_ = b.split(':')[-1]
        if s_.startswith('Head') or s_ == 'Neck': b = 'mixamorig:Spine2'   # the cape lies on the robe: it never follows the neck
        elif 'Arm' in s_ or 'Hand' in s_: b = 'mixamorig:' + ('LeftShoulder' if s_.startswith('Left') else 'RightShoulder')
        elif 'Leg' in s_ or 'Foot' in s_ or 'Toe' in s_: b = 'mixamorig:Hips'
        out[b] = out.get(b, 0) + x
    return out
collar_w = {}
for i, q in enumerate(cfit):
    if hug[i] <= 0: continue
    loc, nrm, idx, dist = tbvh.find_nearest(q)
    poly = torso_polys[idx]; acc = {}; tot = 0.0
    for vi in poly:
        k = 1.0 / ((bw_[vi] - loc).length + 1e-6) ** 2; tot += k
        for b, x in bweights[vi].items(): acc[b] = acc.get(b, 0) + x * k
    collar_w[i] = collar_fold({b: x / tot for b, x in acc.items()})
for _ in range(4):   # neighbours alike: no tearing between the collar's layers
    collar_w = {i: (lambda ns: {b: sum(collar_w[j].get(b, 0) for j in ns) / len(ns) for b in set().union(*(collar_w[j] for j in ns))})([i] + [j for j in cadj[i] if j in collar_w]) for i in collar_w}
log(f"cape collar: {sum(1 for h in hug if h > 0)} verts hug the neck/shoulders ({sum(1 for h in hug if h >= 1)} fully), {fixed} kept just outside the skin, skin weights on them")

for v, q in zip(C.data.vertices, cfit): v.co = C.matrix_world.inverted() @ q
C.data.update()

# ---- 4. cape weights: rigid on Spine2, sheet on spring chains ----
C.parent = A; C.matrix_parent_inverse = A.matrix_world.inverted()
cweights = [{'mixamorig:Spine2': 1.0} for _ in cfit]
if opt.get('capebones', '1') == '1':
    NCOL, NSEG = 5, 3
    side = Vector((-fwd.y, fwd.x, 0))                      # lateral axis
    z_top = max(q.z for q in cfit); z_hem = min(q.z for q in cfit); Hm = z_top - z_hem
    # chains start below the shoulders (collar + shoulder part stays 100 % Spine2)
    T_LEV = [0.22, 0.45, 0.70, 1.0]
    tv = [(z_top - q.z) / Hm for q in cfit]
    back = [i for i in range(len(cfit)) if (cfit[i] - neck).dot(fwd) < 0]   # the sheet behind the neck
    joints = []
    for k, tk in enumerate(T_LEV):
        sel = [i for i in back if abs(tv[i] - tk) < 0.05] or [min(back, key=lambda i: abs(tv[i] - tk))]
        xs = [cfit[i].dot(side) for i in sel]; xa, xb = min(xs), max(xs); span = max(xb - xa, 0.02)
        row = []
        for c in range(NCOL):
            xc = xa + span * (c + 0.5) / NCOL
            near = [cfit[i] for i in sel if abs(cfit[i].dot(side) - xc) < span / NCOL] or [cfit[i] for i in sel]
            depth = sum(p.dot(fwd) for p in near) / len(near)
            row.append(side * xc + fwd * depth + Vector((0, 0, z_top - tk * Hm)))
        joints.append(row)
    bpy.context.view_layer.objects.active = A; A.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    inv = A.matrix_world.inverted(); eb = A.data.edit_bones
    names = {}
    for c in range(NCOL):
        prev = eb['mixamorig:Spine2']
        for j in range(NSEG):
            nm = f"Cape_{c}_{j}"; b = eb.new(nm)
            b.head = inv @ joints[j][c]; b.tail = inv @ joints[j + 1][c]
            b.parent = prev; b.use_connect = False; b.use_deform = True
            prev = b; names[(c, j)] = nm
    bpy.ops.object.mode_set(mode='OBJECT')
    def lat_at(c, t):
        k = 0
        while k < NSEG - 1 and t > T_LEV[k + 1]: k += 1
        f = min(max((t - T_LEV[k]) / (T_LEV[k + 1] - T_LEV[k]), 0.0), 1.0)
        return joints[k][c].dot(side) * (1 - f) + joints[k + 1][c].dot(side) * f
    for i, q in enumerate(cfit):
        t = tv[i]
        if t <= T_LEV[0] or (q - neck).dot(fwd) > 0: continue          # collar / front of the collar: Spine2
        k = 0
        while k < NSEG - 1 and t > T_LEV[k + 1]: k += 1
        sgm = k + min((t - T_LEV[k]) / (T_LEV[k + 1] - T_LEV[k]), 1.0)
        wj = [max(0.0, 1.0 - abs(sgm - (j + 0.5))) for j in range(NSEG)]
        if sgm > NSEG - 0.5: wj = [0.0] * (NSEG - 1) + [1.0]
        fade = min((t - T_LEV[0]) / 0.08, 1.0)                           # soft start under the shoulders
        anchor = max(0.0, 1.0 - sum(wj) * fade)
        xs_c = [lat_at(c, t) for c in range(NCOL)]; x = q.dot(side)
        if x <= xs_c[0]: a, al = 0, 0.0
        elif x >= xs_c[-1]: a, al = NCOL - 2, 1.0
        else:
            a = max(c for c in range(NCOL - 1) if xs_c[c] <= x); al = (x - xs_c[a]) / max(xs_c[a + 1] - xs_c[a], 1e-6)
        w = {'mixamorig:Spine2': anchor} if anchor > 0 else {}
        for j in range(NSEG):
            if wj[j] <= 0: continue
            w[names[(a, j)]] = w.get(names[(a, j)], 0) + (1 - al) * wj[j] * fade
            w[names[(a + 1, j)]] = w.get(names[(a + 1, j)], 0) + al * wj[j] * fade
        cweights[i] = w
    log(f"cape bones: {NCOL} x {NSEG} under Spine2, joint rows at {[round(rel(joints[k][0].z), 3) for k in range(len(joints))]} of body height")
for i, cw in collar_w.items():                       # collar: the skin's weights, fading into the rigid / chain weights below
    h = hug[i]; w = {b: x * (1 - h) for b, x in cweights[i].items()}
    for b, x in cw.items(): w[b] = w.get(b, 0) + x * h
    cweights[i] = w
C.vertex_groups.clear()
for b in A.data.bones: C.vertex_groups.new(name=b.name)
for i, w in enumerate(cweights):
    top4 = sorted(w.items(), key=lambda t: -t[1])[:4]; s_ = sum(x for _, x in top4) or 1.0
    for b, x in top4: C.vertex_groups[b].add([i], x / s_, 'REPLACE')
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
    bp = eval_pts(B); cp = eval_pts(C)
    bvh = mathutils.bvhtree.BVHTree.FromPolygons(bp, bpolys); outward = inside_test(bvh, bp)
    n_in = 0
    for q in cp:
        loc, nrm, idx, dist = bvh.find_nearest(q)
        if loc is not None and dist < 0.05 * BH and ((q - loc).dot(nrm) * outward) < -0.002 * BH: n_in += 1
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
    clip = os.path.splitext(os.path.basename(path))[0].replace('Hung-Vuong_', '')
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
        hc = A.matrix_world @ A.pose.bones['mixamorig:Spine2'].head
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
