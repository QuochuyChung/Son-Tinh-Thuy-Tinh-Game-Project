"""Sấu Chín Đuôi (the nine-tailed crocodile boss of the Thủy Tinh map): rig a Meshy quadruped by hand, weight it, animate ten clips,
export for Unity (Generic).

    blender -b --python tools/rig_sau.py -- <gray_generate.fbx> <textured.fbx> <out_skinned.fbx> <preview_dir> [key=value ...]

* gray_generate.fbx : the untextured Meshy export: the weights are computed on it
* textured.fbx      : the Meshy textured export of the same model (same vertices): it gets the weights copied 1:1
* out_skinned.fbx   : armature + SauChinDuoi_Body with ten actions, bind pose, no materials
* preview_dir       : rest.png, weights.png (one colour per tail / region), clip_<name>.png (key poses) and stretch.txt
* key=value         : blend=<abs path> also saves the result as a .blend

Same recipe as rig_ngua.py (the horse). The model faces -Y; its nine tails fan out in the x-z plane from one hub behind the hips.
Bones (Generic): Root > Hips > Spine1 > Chest > Neck > Head > Jaw; four sprawled legs <F|B><L|R>_Upper > _Lower > _Foot;
Hips > TailBase (to the hub) > for each tail k (0 = lowest on the right, 8 = lowest on the left): Tail<k>A > Tail<k>B (keyed in the
clips) > Tail_<k>_0 > Tail_<k>_1 (never keyed: OutfitSpringBones in Unity, it overrides whatever a clip writes on its chains).
Tails are found by clustering the fan's vertices by angle around the hub (k-means, 9 clusters); the hub is the least-squares meeting
point of the nine tail axes. Each vertex of the fan follows only its own tail (nearest tail axis), so one tail never pulls another.
Poses are written in world axes (pitch about X, yaw about Z, roll about Y) and turned into each bone's local rotation, so a sign means
the same thing on every bone: +pitch tips a forward-pointing bone down and an upward tail forward (towards the head); +yaw turns the
head to +X (the crocodile's left).
Clips (30 fps): Idle (loop 4 s), Walk (loop 1.2 s), Charge (loop 0.6 s), Bite (1.2 s, snap at 0.55 s), TailSweep (1.6 s, sweep
0.65-1.0 s), WaterSpit (1.4 s, spit at 0.6 s), TailSlam (2 s, impact at 1.1 s), Hit (0.5 s), Roar (2 s), Death (2.5 s, held).
"""
import bpy, bmesh, sys, math, os
import mathutils
from mathutils import Vector, Quaternion
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
pos = [a for a in argv if '=' not in a]
opt = dict(a.split('=', 1) for a in argv if '=' in a)
gray_fbx, tex_fbx, out_fbx, out_dir = pos[:4]
os.makedirs(out_dir, exist_ok=True)
NAME = "SauChinDuoi"
FPS = 30
NT = 9
report = []
def log(s): print("SAU", s); report.append(s)

def import_new(path):
    pre = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.context.scene.objects if o not in pre]

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps = FPS

# ---- 1. meshes ----
G = [o for o in import_new(gray_fbx) if o.type == 'MESH'][0]
T = [o for o in import_new(tex_fbx) if o.type == 'MESH'][0]
gw = [G.matrix_world @ v.co for v in G.data.vertices]
tw = [T.matrix_world @ v.co for v in T.data.vertices]
z0 = min(p.z for p in gw); z1 = max(p.z for p in gw); H = z1 - z0
Y0 = min(p.y for p in gw); Y1 = max(p.y for p in gw)
def Z(r): return z0 + r                                      # heights below are absolute offsets above the lowest vertex
log(f"gray {len(gw)} verts, textured {len(tw)} verts, height {H:.4f}, length y {Y0:+.3f}..{Y1:+.3f}")
kd = mathutils.kdtree.KDTree(len(gw))
for i, p in enumerate(gw): kd.insert(p, i)
kd.balance()
twin = []; worst = 0.0
for p in tw:
    co, idx, d = kd.find(p); twin.append(idx); worst = max(worst, d)
log(f"textured -> gray twins: worst distance {worst / H:.6f} heights")

def centroid(pts):
    return sum(pts, Vector()) / len(pts) if pts else None
def lerp(a, b, t): return a + (b - a) * t

# ---- 2. landmarks ----
FAN_Y = 0.30                                                  # the tail fan is behind this
def center_z(y, half=0.012):
    s = [p.z for p in gw if abs(p.y - y) < half and abs(p.x) < 0.05]
    return (min(s) + max(s)) / 2 if s else Z(0.22)
# legs: quadrant clusters at three heights (front legs y < 0, hind legs 0 < y < FAN_Y)
def leg_c(front, side, lo, hi):
    return centroid([p for p in gw if lo <= p.z - z0 < hi and abs(p.x) > 0.09 and (p.y < 0 if front else 0 < p.y < FAN_Y - 0.05)
                     and (p.x > 0) == (side == 'L')])
legs = {}
for front in (True, False):
    for side in ('L', 'R'):
        k = ('F' if front else 'B') + side
        toe = leg_c(front, side, 0.0, 0.03); ankle = leg_c(front, side, 0.06, 0.10); knee = leg_c(front, side, 0.12, 0.16)
        ankle = Vector((ankle.x, ankle.y, Z(0.045)))
        legs[k] = (Vector((toe.x, toe.y, z0)), ankle, knee)
FRONT_Y = (legs['FL'][2].y + legs['FR'][2].y) / 2; BACK_Y = (legs['BL'][2].y + legs['BR'][2].y) / 2
log(f"legs: front y {FRONT_Y:+.3f}, back y {BACK_Y:+.3f}; " + ", ".join(f"{k} toe ({v[0].x:+.3f},{v[0].y:+.3f})" for k, v in legs.items()))
snout = min(gw, key=lambda p: p.y)
HINGE_Y = -0.255                                              # the corner of the (modelled open) mouth, measured on side slices
def jaw_cut(y):                                               # lower jaw below this height (the mouth is open, the gap is clear)
    return Z(0.135) if y < -0.30 else Z(lerp(0.135, 0.19, (y + 0.30) / (HINGE_Y + 0.30)))
jaw_pts = [p for p in gw if p.y < HINGE_Y and p.z < jaw_cut(p.y) and abs(p.x) < 0.07]
jaw_tip = min(jaw_pts, key=lambda p: p.y)
log(f"snout y {snout.y:+.3f} z {snout.z - z0:.3f}; lower jaw: {len(jaw_pts)} verts, tip y {jaw_tip.y:+.3f} z {jaw_tip.z - z0:.3f}")

# tails: cluster the fan by angle around a first hub guess, fit each tail's axis, the hub is where the axes meet
fan = [i for i, p in enumerate(gw) if p.y > FAN_Y]
hub = Vector((0.0, 0.0, Z(0.27)))
def ang(p, h):                                                # angle in the fan plane, 0 = +X (left), 90 = up, continuous over the bottom-right
    a = math.degrees(math.atan2(p.z - h.z, p.x - h.x))
    return a + 360 if a < -90 else a
far = [i for i in fan if Vector((gw[i].x - hub.x, 0, gw[i].z - hub.z)).length > 0.18]
A_ = np.array([ang(gw[i], hub) for i in far])
cent = np.percentile(A_, np.linspace(100 / (2 * NT), 100 - 100 / (2 * NT), NT))
for _ in range(50):
    lab = np.argmin(np.abs(A_[:, None] - cent[None, :]), axis=1)
    cent = np.array([A_[lab == k].mean() if (lab == k).any() else cent[k] for k in range(NT)])
order = np.argsort(cent); cent = cent[order]
lab = np.argmin(np.abs(A_[:, None] - cent[None, :]), axis=1)
clusters = [[far[j] for j in range(len(far)) if lab[j] == k] for k in range(NT)]
M2 = np.zeros((2, 2)); b2 = np.zeros(2)
for cl in clusters:
    P = np.array([(gw[i].x, gw[i].z) for i in cl]); c = P.mean(0)
    u = np.linalg.svd(P - c)[2][0]; Pm = np.eye(2) - np.outer(u, u)
    M2 += Pm; b2 += Pm @ c
hx, hz = np.linalg.solve(M2, b2)
hub = Vector((0.0, sum(gw[i].y for i in fan if abs(gw[i].x) < 0.1 and abs(gw[i].z - hz) < 0.1) / max(1, sum(1 for i in fan if abs(gw[i].x) < 0.1 and abs(gw[i].z - hz) < 0.1)), hz))
log(f"hub: x {hx:+.3f} (forced 0), y {hub.y:+.3f}, z {hub.z - z0:.3f}; tail angles " + ", ".join(f"{c:.0f}" for c in cent) + "; sizes " + ", ".join(str(len(c)) for c in clusters))
tails = []                                                    # per tail: polyline from the hub to the tip
for cl in clusters:
    pts = [gw[i] for i in cl]
    rr = [(p - hub).length for p in pts]
    rmax = max(rr)
    line = [hub.copy()]
    for b in range(5):
        lo = 0.14 + (rmax - 0.14) * b / 5; hi = 0.14 + (rmax - 0.14) * (b + 1) / 5
        s = [p for p, r in zip(pts, rr) if lo <= r < hi]
        if s: line.append(centroid(s))
    tip = centroid([p for p, r in zip(pts, rr) if r > rmax - 0.02])
    line.append(tip)
    tails.append(line)
def along(line, t):
    L = [0.0]
    for a, b in zip(line, line[1:]): L.append(L[-1] + (b - a).length)
    s = t * L[-1]
    for j in range(len(line) - 1):
        if L[j + 1] >= s: return lerp(line[j], line[j + 1], (s - L[j]) / max(L[j + 1] - L[j], 1e-9))
    return line[-1].copy()
log("tail lengths: " + ", ".join(f"{sum((b - a).length for a, b in zip(l, l[1:])):.3f}" for l in tails))

# ---- 3. armature ----
arm_data = bpy.data.armatures.new("Armature")
A = bpy.data.objects.new("Armature", arm_data); sc.collection.objects.link(A)
bpy.context.view_layer.objects.active = A; A.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
def bone(name, head, tail, parent=None, connect=False, deform=True):
    b = eb.new(name); b.head = head; b.tail = tail; b.roll = 0.0
    if parent: b.parent = eb[parent]; b.use_connect = connect
    b.use_deform = deform
    return b
def sp(y): return Vector((0.0, y, center_z(y)))
bone("Root", Vector((0, 0, z0)), Vector((0, -0.1 * H, z0)), deform=False)
hips = sp(BACK_Y + 0.03); spine1 = sp((BACK_Y + FRONT_Y) / 2); chest = sp(FRONT_Y - 0.02); neck = sp(-0.20)
head_base = Vector((0.0, HINGE_Y + 0.02, Z(0.215)))
bone("Hips", hips, spine1, "Root")
bone("Spine1", spine1, chest, "Hips", True)
bone("Chest", chest, neck, "Spine1", True)
bone("Neck", neck, head_base, "Chest", True)
bone("Head", head_base, Vector((0.0, snout.y + 0.01, snout.z)), "Neck", True)
hinge = Vector((0.0, HINGE_Y + 0.01, Z(0.165)))
bone("Jaw", hinge, Vector((0.0, jaw_tip.y + 0.005, jaw_tip.z + 0.01)), "Head")
for k, (toe, ankle, knee) in legs.items():
    top = Vector((math.copysign(0.075, knee.x), knee.y, Z(0.235)))
    par = "Chest" if k[0] == 'F' else "Hips"
    bone(f"{k}_Upper", top, knee, par)
    bone(f"{k}_Lower", knee, ankle, f"{k}_Upper", True)
    bone(f"{k}_Foot", ankle, toe, f"{k}_Lower", True)
bone("TailBase", hips, hub, "Hips")
for k, line in enumerate(tails):
    cuts = [0.0, 0.30, 0.55, 0.78, 1.0]
    names = [f"Tail{k}A", f"Tail{k}B", f"Tail_{k}_0", f"Tail_{k}_1"]
    for j, n in enumerate(names):
        bone(n, along(line, cuts[j]), along(line, cuts[j + 1]), "TailBase" if j == 0 else names[j - 1], j > 0)
bpy.ops.object.mode_set(mode='OBJECT')
bones = [b for b in arm_data.bones if b.use_deform]
log(f"armature: {len(arm_data.bones)} bones")

# ---- 4. weights on the gray mesh ----
def seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12))); return (p - (a + ab * t)).length
bmw = A.matrix_world
segs = {b.name: (bmw @ b.head_local, bmw @ b.tail_local) for b in bones}
LEG = {k: [f"{k}_Upper", f"{k}_Lower", f"{k}_Foot"] for k in legs}
TAILK = [[f"Tail{k}A", f"Tail{k}B", f"Tail_{k}_0", f"Tail_{k}_1"] for k in range(NT)]
BODY = ["Hips", "Spine1", "Chest", "Neck"]
def line_dist(p, line): return min(seg_dist(p, a, b) for a, b in zip(line, line[1:]))
tail_of = {}
def region(p):
    zr = p.z - z0
    if p.y > FAN_Y:
        r = (p - hub).length
        if r < 0.07: return ["TailBase"] + [TAILK[k][0] for k in range(NT)]
        k = min(range(NT), key=lambda k: line_dist(p, tails[k]))
        return TAILK[k] + (["TailBase"] if r < 0.13 else [])
    if p.y > BACK_Y + 0.07: return ["TailBase", "Hips"]
    if zr < 0.24 and abs(p.x) > 0.075:
        # the nearest leg; the toes spread far from the foot bone, so everything near the ground belongs to a leg
        dk, k = min((min(seg_dist(p, *segs[n]) for n in names), k) for k, names in LEG.items())
        if dk < 0.06 or zr < 0.07:
            return LEG[k] + (["Chest"] if k[0] == 'F' else ["Hips"])
    if p.y < HINGE_Y + 0.02:
        if p.y < HINGE_Y and p.z < jaw_cut(p.y): return ["Jaw", "Head"]
        return ["Head", "Neck"]
    return BODY + ["Head"]
def weigh(p, cands, k=3):
    ds = sorted(((seg_dist(p, *segs[n]), n) for n in cands), key=lambda t: t[0])[:k]
    w = {n: 1.0 / (d + 0.01 * H) ** 4 for d, n in ds}
    s = sum(w.values()); return {n: x / s for n, x in w.items()}
gweights = [weigh(p, region(p)) for p in gw]
bm = bmesh.new(); bm.from_mesh(G.data); bm.verts.ensure_lookup_table()
seen = set(); comps = []
for v in bm.verts:
    if v.index in seen: continue
    st = [v]; seen.add(v.index); mem = []
    while st:
        c = st.pop(); mem.append(c.index)
        for e in c.link_edges:
            o_ = e.other_vert(c)
            if o_.index not in seen: seen.add(o_.index); st.append(o_)
    comps.append(mem)
big = max(comps, key=len)
adj = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]
bm.free()
rigid = 0
for mem in comps:                                             # spikes / claws: one weight set for the whole piece
    if mem is big or len(mem) > 600: continue
    acc = {}
    for i in mem:
        for n, x in gweights[i].items(): acc[n] = acc.get(n, 0) + x / len(mem)
    for i in mem: gweights[i] = dict(acc)
    rigid += 1
for _ in range(2):
    gweights = [(lambda nb: {n: sum(gweights[j].get(n, 0) for j in nb) / len(nb) for n in set().union(*(gweights[j] for j in nb))})([i, i] + adj[i]) for i in range(len(gw))]
log(f"weights: {len(comps)} pieces, {rigid} small pieces rigid, main piece {len(big)} verts")

def apply_weights(obj, weights):
    obj.vertex_groups.clear()
    for b in arm_data.bones: obj.vertex_groups.new(name=b.name)
    for i, w in enumerate(weights):
        top4 = sorted(w.items(), key=lambda t: -t[1])[:4]; s = sum(x for _, x in top4) or 1.0
        for n, x in top4: obj.vertex_groups[n].add([i], x / s, 'REPLACE')
    m = obj.modifiers.new("Armature", 'ARMATURE'); m.object = A
    obj.parent = A; obj.matrix_parent_inverse = A.matrix_world.inverted()
apply_weights(G, gweights)
apply_weights(T, [gweights[twin[i]] for i in range(len(tw))])
T.name = f"{NAME}_Body"; T.data.name = f"{NAME}_Body"

# weight preview colours on the gray mesh: one hue per tail, legs / jaw / head / body in fixed colours
PAL = {"Jaw": (1, 0.3, 0.3), "Head": (1, 0.8, 0.3), "Neck": (0.9, 0.9, 0.5), "Chest": (0.5, 0.5, 0.5), "Spine1": (0.65, 0.65, 0.65),
       "Hips": (0.8, 0.8, 0.8), "TailBase": (1, 1, 1)}
def colour(n):
    if n in PAL: return PAL[n]
    if n[1] == 'L' or n[1] == 'R':
        return {"FL": (0.2, 0.4, 1), "FR": (0.2, 1, 0.4), "BL": (0.6, 0.2, 1), "BR": (0.1, 0.8, 0.8)}[n[:2]]
    k = int(n[4]) if n[4].isdigit() else int(n[5])
    import colorsys
    return colorsys.hsv_to_rgb(k / NT, 0.85, 1.0)
ca = G.data.color_attributes.new("w", 'FLOAT_COLOR', 'POINT')
for i, w in enumerate(gweights):
    c = Vector((0, 0, 0))
    for n, x in w.items(): c += Vector(colour(n)) * x
    ca.data[i].color = (c.x, c.y, c.z, 1)

# ---- 5. actions ----
pb = A.pose.bones
for p_ in pb: p_.rotation_mode = 'QUATERNION'
RQ = {b.name: b.matrix_local.to_quaternion() for b in arm_data.bones}
def wq(pitch=0.0, yaw=0.0, roll=0.0):
    return (Quaternion((0, 0, 1), math.radians(yaw)) @ Quaternion((1, 0, 0), math.radians(pitch)) @ Quaternion((0, 1, 0), math.radians(roll)))
def set_pose(pose):
    for p_ in pb: p_.rotation_quaternion = (1, 0, 0, 0); p_.location = (0, 0, 0)
    for n, v in pose.items():
        if n.endswith("@loc"):
            n = n[:-4]; pb[n].location = RQ[n].inverted() @ Vector(v)
        else:
            q = RQ[n]; pb[n].rotation_quaternion = q.inverted() @ wq(*v) @ q
KEYED = [b.name for b in arm_data.bones if b.name != "Root" and not b.name.startswith("Tail_")]
def key_pose(frame, pose):
    set_pose(pose)
    for n in KEYED:
        pb[n].keyframe_insert("rotation_quaternion", frame=frame)
    pb["Hips"].keyframe_insert("location", frame=frame)
def make_action(name, frames, pose_at):
    act = bpy.data.actions.new(name); A.animation_data_create(); A.animation_data.action = act
    for f in frames: key_pose(f, pose_at(f))
    act.use_fake_user = True
    return act
TA = [math.radians(c) for c in cent]                          # each tail's direction in the fan plane (0 = +X, 90 = up)
def ss(t): t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)
def env(t, keys):                                             # piecewise smooth curve through (time, value) keys
    if t <= keys[0][0]: return keys[0][1]
    for (ta, va), (tb, vb) in zip(keys, keys[1:]):
        if t <= tb: return va + (vb - va) * ss((t - ta) / (tb - ta))
    return keys[-1][1]
def add(d, n, pitch=0.0, yaw=0.0, roll=0.0):
    a = d.get(n, (0.0, 0.0, 0.0)); d[n] = (a[0] + pitch, a[1] + yaw, a[2] + roll)
def tails_pose(d, pitch=0.0, spread=0.0, wave=0.0, t=0.0, freq=1.0, pitchB=None):
    """pitch: all tails lean forward (+) / back (-); spread: each tail turns away from vertical in the fan plane (+ = fan opens);
    wave: sideways sway (degrees) with a phase per tail."""
    for k in range(NT):
        sgn = 1 if math.cos(TA[k]) >= 0 else -1               # tails on +X open towards +X
        sw = wave * math.sin(2 * math.pi * freq * t + k * 0.7)
        add(d, f"Tail{k}A", pitch, 0, sgn * spread + sw)
        add(d, f"Tail{k}B", pitch * 0.5 if pitchB is None else pitchB, 0, sw * 0.8)
LEGS = ["FL", "FR", "BL", "BR"]
SIDE = {"FL": 1, "BL": 1, "FR": -1, "BR": -1}
def gait(d, t, freq, swing, lift, phase_off=0.0):
    ph = {"FL": 0.0, "BR": 0.0, "FR": 0.5, "BL": 0.5}           # diagonal pairs
    for k in LEGS:
        a = 2 * math.pi * (freq * t + ph[k] + phase_off)
        s = SIDE[k]
        add(d, f"{k}_Upper", 0, -s * swing * math.sin(a), -s * lift * max(0.0, math.cos(a)))
        add(d, f"{k}_Lower", 0, 0, s * 0.5 * lift * max(0.0, math.cos(a)))
        add(d, f"{k}_Foot", 0, s * swing * 0.5 * math.sin(a), 0)

def idle(f):           # 120 frames: breathing, tails sway, head drifts
    t = (f - 1) / FPS; d = {}
    br = math.sin(2 * math.pi * t / 2)
    add(d, "Chest", -1.2 * br); add(d, "Spine1", 0.8 * br)
    add(d, "Neck", 1.5 * math.sin(2 * math.pi * t / 4), 3 * math.sin(2 * math.pi * t / 4 + 1))
    add(d, "Jaw", 2 + 2 * math.sin(2 * math.pi * t / 4))
    tails_pose(d, 3 * math.sin(2 * math.pi * t / 4), 0, 6, t, 0.5)
    return d
def walk(f):           # 36 frames: sprawled walk, spine swings side to side, tails sway
    t = (f - 1) / FPS; d = {}; fr = 1 / 1.2
    gait(d, t, fr, 22, 18)
    sw = math.sin(2 * math.pi * fr * t)
    add(d, "Hips", 0, 6 * sw); add(d, "Spine1", 0, -4 * sw); add(d, "Chest", 0, -5 * sw); add(d, "Neck", 0, 4 * sw)
    add(d, "TailBase", 0, -8 * sw)
    d["Hips@loc"] = (0, 0, 0.004 * abs(math.cos(2 * math.pi * fr * t)))
    tails_pose(d, 4, 0, 5, t, fr)
    return d
def charge(f):         # 18 frames: fast run, head low, jaw a little open, tails swept back
    t = (f - 1) / FPS; d = {}; fr = 1 / 0.6
    gait(d, t, fr, 32, 26)
    sw = math.sin(2 * math.pi * fr * t)
    add(d, "Hips", 0, 8 * sw); add(d, "Chest", 2, -7 * sw); add(d, "Neck", 6, 4 * sw); add(d, "Head", 2); add(d, "Jaw", 6)
    add(d, "TailBase", 0, -10 * sw)
    d["Hips@loc"] = (0, 0, 0.008 * abs(math.cos(2 * math.pi * fr * t)))
    tails_pose(d, -28, -10, 6, t, fr * 2)
    return d
def bite(f):           # 36 frames: rear back + open wide (0-0.4 s), lunge + snap (0.55 s), recover
    t = (f - 1) / FPS; d = {}
    up = env(t, [(0, 0), (0.4, 1), (0.5, -1), (0.75, -1), (1.2, 0)])     # 1 = reared back, -1 = lunged
    jaw = env(t, [(0, 0), (0.35, 1), (0.48, 1), (0.56, -1), (0.8, -1), (1.2, 0)])
    add(d, "Chest", -6 * up); add(d, "Neck", -10 * up); add(d, "Head", -6 * up)
    add(d, "Jaw", 26 * jaw if jaw > 0 else 14 * jaw)
    d["Hips@loc"] = (0, 0.03 * max(0, -up) * -1 + 0.012 * max(0, up), 0)
    for k in ("FL", "FR"): add(d, f"{k}_Upper", 0, -SIDE[k] * 12 * max(0, -up))
    tails_pose(d, 6 * up, 0, 3, t, 1.5)
    return d
def tail_sweep(f):     # 48 frames: wind-up to the left (0-0.6 s), sweep round to the right (0.65-1.0 s), recover
    t = (f - 1) / FPS; d = {}
    s = env(t, [(0, 0), (0.6, -1), (1.0, 1), (1.6, 0)])
    low = env(t, [(0, 0), (0.45, 1), (1.15, 1), (1.6, 0)])
    add(d, "Hips", 0, 14 * s); add(d, "Spine1", 0, -6 * s); add(d, "Chest", 0, -10 * s); add(d, "Neck", 0, -8 * s)
    add(d, "TailBase", -10 * low, 55 * s)
    tails_pose(d, -55 * low, 15 * low, 4, t, 2)
    for k in LEGS: add(d, f"{k}_Upper", 0, 0, -SIDE[k] * 6 * low)
    return d
def water_spit(f):     # 42 frames: rear up, throat swells, spit at 0.6 s, recover
    t = (f - 1) / FPS; d = {}
    up = env(t, [(0, 0), (0.45, 1), (0.58, -0.4), (0.85, -0.4), (1.4, 0)])
    jaw = env(t, [(0, 0), (0.45, 0.5), (0.58, 1), (0.95, 1), (1.4, 0)])
    add(d, "Chest", -10 * up); add(d, "Neck", -12 * up); add(d, "Head", -8 * up); add(d, "Jaw", 24 * jaw)
    for k in ("FL", "FR"): add(d, f"{k}_Upper", 0, 0, -SIDE[k] * 8 * max(0, up))
    tails_pose(d, -8 * max(0, up), 6 * jaw, 3, t, 1.5)
    return d
def tail_slam(f):      # 60 frames: the fan gathers straight up, front rears (0-0.85 s); the nine tails crash down open all round (1.1 s):
    t = (f - 1) / FPS; d = {}   # the side tails hit the ground left and right, the middle ones behind (the hub is low behind the hips,
    # so slamming them forward would pass through the back)
    spread = env(t, [(0, 0), (0.85, -22), (1.1, 55), (1.5, 55), (2.0, 0)])
    back = env(t, [(0, 0), (0.85, 10), (1.1, -45), (1.5, -45), (2.0, 0)])
    rear = env(t, [(0, 0), (0.85, 1), (1.1, -0.6), (1.4, -0.6), (2.0, 0)])
    add(d, "Chest", -8 * rear); add(d, "Neck", -8 * rear); add(d, "Head", -4 * rear); add(d, "Jaw", 14 * max(0, rear))
    d["Hips@loc"] = (0, 0, -0.012 * max(0, -rear))
    for k in ("FL", "FR"): add(d, f"{k}_Upper", 0, 0, -SIDE[k] * 10 * max(0, rear))
    tails_pose(d, back, spread, 2, t, 3, pitchB=back * 0.3)
    return d
def hit(f):            # 15 frames: flinch back and to the side
    t = (f - 1) / FPS; d = {}
    h = env(t, [(0, 0), (0.08, 1), (0.5, 0)])
    add(d, "Chest", -6 * h, 5 * h, -4 * h); add(d, "Neck", -8 * h, 8 * h); add(d, "Head", -4 * h); add(d, "Jaw", 10 * h)
    d["Hips@loc"] = (0, 0.015 * h, 0)
    tails_pose(d, -12 * h, 8 * h)
    return d
def roar(f):           # 60 frames: head up, jaw wide, tails flare open and shake
    t = (f - 1) / FPS; d = {}
    r = env(t, [(0, 0), (0.4, 1), (1.6, 1), (2.0, 0)])
    shake = math.sin(2 * math.pi * 7 * t) * env(t, [(0, 0), (0.45, 1), (1.5, 1), (1.8, 0)])
    add(d, "Chest", -8 * r); add(d, "Neck", -14 * r, 3 * shake); add(d, "Head", -12 * r); add(d, "Jaw", 34 * r + 2 * shake)
    for k in ("FL", "FR"): add(d, f"{k}_Upper", 0, 0, -SIDE[k] * 8 * r)
    tails_pose(d, -14 * r, 14 * r, 4 * r, t, 3.5)
    return d
def death(f):          # 75 frames: staggers, legs give way, belly on the ground, head down, tails fall open to the ground; held
    t = (f - 1) / FPS; d = {}
    s = env(t, [(0, 0), (0.3, 0.3), (1.2, 1), (2.5, 1)])
    st = env(t, [(0, 0), (0.25, 1), (0.6, 0)])
    add(d, "Hips", 0, 0, 12 * s); add(d, "Chest", 6 * s - 10 * st, 6 * st); add(d, "Neck", 12 * s - 8 * st); add(d, "Head", 6 * s)
    add(d, "Jaw", 18 * s)
    d["Hips@loc"] = (0, 0, -0.085 * s)
    for k in LEGS: add(d, f"{k}_Upper", 0, 0, -SIDE[k] * 40 * s); add(d, f"{k}_Lower", 0, 0, SIDE[k] * 25 * s)   # legs splay out flat
    tails_pose(d, -25 * s, 45 * env(t, [(0, 0), (0.6, 0), (2.0, 1)]), 0, t, 1, pitchB=-10 * s)
    return d
CLIPS = [("Idle", 120, idle), ("Walk", 36, walk), ("Charge", 18, charge), ("Bite", 36, bite), ("TailSweep", 48, tail_sweep),
         ("WaterSpit", 42, water_spit), ("TailSlam", 60, tail_slam), ("Hit", 15, hit), ("Roar", 60, roar), ("Death", 75, death)]
acts = {}
for name, n, fn in CLIPS:
    step = 1 if n <= 48 else 2
    acts[name] = make_action(name, list(range(1, n + 2, step)) + ([n + 1] if n % step else []), fn)
log("actions: " + ", ".join(f"{n} {int(a.frame_range[1] - a.frame_range[0])} frames" for n, a in acts.items()))

# ---- 6. previews + stretch ----
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'
sc.view_settings.view_transform = 'Standard'
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.3, 0.3, 0.33)
cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = 1.3
cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
VIEWS = {'side': Vector((1, 0, 0)), 'front': Vector((0, -1, 0)), 'q': Vector((0.75, -0.65, 0.25)).normalized(), 'back': Vector((-0.6, 0.8, 0.3)).normalized(),
         'top': Vector((0, 0.001, 1))}
def shot(path, view, color='TEXTURE', w=700, h=560):
    sc.render.resolution_x = w; sc.render.resolution_y = h; sc.display.shading.color_type = color
    c = Vector((0.0, 0.0, z0 + 0.36))
    d = VIEWS[view]
    cam.location = c + d * 4; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)
def sheet(paths, out, cols):
    imgs = []
    for p in paths:
        im = bpy.data.images.load(p); imgs.append(np.array(im.pixels[:]).reshape(im.size[1], im.size[0], 4)); bpy.data.images.remove(im); os.remove(p)
    rows = [np.concatenate(imgs[i:i + cols] + [np.zeros_like(imgs[0])] * (cols - len(imgs[i:i + cols])), axis=1) for i in range(0, len(imgs), cols)]
    s_ = np.concatenate(rows[::-1], axis=0)
    o = bpy.data.images.new("sheet", s_.shape[1], s_.shape[0], alpha=True)
    o.pixels.foreach_set(s_.astype(np.float32).ravel()); o.filepath_raw = out; o.file_format = 'PNG'; o.save(); bpy.data.images.remove(o)
def eval_pts(obj):
    dg = bpy.context.evaluated_depsgraph_get(); ev = obj.evaluated_get(dg); m = ev.to_mesh()
    p = [obj.matrix_world @ v.co for v in m.vertices]; ev.to_mesh_clear(); return p
E = [tuple(e.vertices) for e in T.data.edges]
E = [(a, b) for a, b in E if (tw[a] - tw[b]).length > 0.003 * H]
L0 = np.array([(tw[a] - tw[b]).length for a, b in E])
def stretch():
    p = eval_pts(T); L = np.array([(p[a] - p[b]).length for a, b in E]); r = L / L0
    j = int(np.argmax(r)); a, b = E[j]
    return float(r.max()), int((r > 1.3).sum()), tw[a]

A.animation_data.action = None
for p_ in pb: p_.rotation_quaternion = (1, 0, 0, 0); p_.location = (0, 0, 0)
bpy.context.view_layer.update()
P_ = []
for v in ('side', 'front', 'q', 'back'):
    p = os.path.join(out_dir, f"_r_{v}.png"); shot(p, v); P_.append(p)
sheet(P_, os.path.join(out_dir, "rest.png"), 4)
# weights: the gray mesh in its colour attribute, the textured one hidden
T.hide_render = True; G.hide_render = False
G.data.color_attributes.active_color = ca
P_ = []
for v in ('front', 'q', 'top', 'side'):
    p = os.path.join(out_dir, f"_w_{v}.png"); shot(p, v, color='VERTEX'); P_.append(p)
sheet(P_, os.path.join(out_dir, "weights.png"), 4)
T.hide_render = False; G.hide_render = True
A.show_in_front = True; arm_data.display_type = 'STICK'
SHOW = {"Idle": ('q', 'front'), "Walk": ('top', 'q'), "Charge": ('top', 'side'), "Bite": ('side', 'q'), "TailSweep": ('back', 'top'),
        "WaterSpit": ('side', 'q'), "TailSlam": ('side', 'q'), "Hit": ('side', 'q'), "Roar": ('side', 'front'), "Death": ('q', 'front')}
for act_name, act in acts.items():
    A.animation_data.action = act
    try: A.animation_data.action_slot = act.slots[0]
    except Exception: pass
    first, last = int(act.frame_range[0]), int(act.frame_range[1])
    worst = (0, 0, None, 0)
    for f in range(first, last + 1, 3):
        sc.frame_set(f); bpy.context.view_layer.update()
        m, n, where = stretch()
        if m > worst[0]: worst = (m, n, where, f)
    log(f"stretch {act_name}: max {worst[0]:.3f} at f{worst[3]} (>1.3: {worst[1]}), worst edge near y {worst[2].y:+.3f} z {worst[2].z - z0:.3f} x {worst[2].x:+.3f}")
    P_ = []
    n = last - first
    for f in sorted(set([first, first + n // 3, first + (2 * n) // 3, last] if n > 20 else [first, first + n // 2, last])):
        sc.frame_set(f); bpy.context.view_layer.update()
        for v in SHOW[act_name]:
            p = os.path.join(out_dir, f"_c_{v}_{f}.png"); shot(p, v, w=560, h=450); P_.append(p)
    sheet(P_, os.path.join(out_dir, f"clip_{act_name}.png"), 4)
with open(os.path.join(out_dir, "stretch.txt"), "w", encoding="utf-8") as fh: fh.write("\n".join(report) + "\n")

# ---- 7. export: armature + textured body + all actions ----
A.animation_data.action = None
for p_ in pb: p_.rotation_quaternion = (1, 0, 0, 0); p_.location = (0, 0, 0)
sc.frame_set(1); bpy.context.view_layer.update()
bpy.data.objects.remove(G, do_unlink=True)
if opt.get('blend'):
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(opt['blend']), copy=True); log(f"saved {opt['blend']}")
T.data.materials.clear()
bpy.ops.object.select_all(action='DESELECT')
for o in (A, T): o.select_set(True)
bpy.context.view_layer.objects.active = A
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                         bake_anim_simplify_factor=0.0, embed_textures=False, path_mode='COPY', use_armature_deform_only=False)
log(f"exported {out_fbx}")
