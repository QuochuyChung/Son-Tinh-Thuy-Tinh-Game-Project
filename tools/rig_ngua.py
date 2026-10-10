"""Ngựa Chín Hồng Mao (the horse gift): rig a Meshy quadruped by hand, weight it, animate three clips, export for Unity (Generic).

    blender -b --python tools/rig_ngua.py -- <gray_generate.fbx> <textured.fbx> <out_skinned.fbx> <preview_dir> [key=value ...]

* gray_generate.fbx : the untextured Meshy export (no UVs): the weights are computed on it
* textured.fbx      : the Meshy textured export of the same model (same vertices): it gets the weights copied 1:1
* out_skinned.fbx   : armature + NguaChinHongMao_Body with three actions (EatGrass, Idle, LookUp), bind pose, no materials
* preview_dir       : rig.png (bones), weights.png (one colour per region), clip_<name>.png (key poses, side + front) and stretch.txt
* key=value         : blend=<abs path> also saves the result as a .blend

Mixamo has no quadruped rig, so the skeleton is built from landmarks measured on the mesh (height slices: four leg clusters, the
belly, the withers, the head, the tail hanging behind). Bones (Generic, not Humanoid): Root > Hips > Spine1 > Chest > Neck1 > Neck2 >
Head > Jaw / Ear_L / Ear_R; four legs <F|B><L|R>_Upper > _Lower > _Hoof; TailBase > Tail1 > Tail2 (animated) > Tail_0_0 > Tail_0_1
(spring); Mane_<c>_0 > Mane_<c>_1 hanging from Neck1 / Neck2 / Head (spring). The Tail_ / Mane_ chains are for OutfitSpringBones
in Unity (they are never keyed).
Weights: each vertex follows the nearest bone segments of its region (inverse distance, top 3), smoothed along the mesh: the four leg
regions are split by side and by front / back so a leg never pulls the other one; loose pieces (tail strands, mane strands, tassels)
take one weight set for the whole piece from the bones nearest to where it is attached.
Clips (30 fps): EatGrass (loop, 5 s: head down to the grass, jaw chewing, small lifts), Idle (loop, 4 s: breathing, tail swish, ear
flicks), LookUp (one-shot, 2 s: from grazing to head up, ears forward, held).
"""
import bpy, bmesh, sys, math, os
import mathutils
from mathutils import Vector, Euler
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
pos = [a for a in argv if '=' not in a]
opt = dict(a.split('=', 1) for a in argv if '=' in a)
gray_fbx, tex_fbx, out_fbx, out_dir = pos[:4]
os.makedirs(out_dir, exist_ok=True)
NAME = "NguaChinHongMao"
FPS = 30
report = []
def log(s): print("NG", s); report.append(s)

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
def P(y, zr, x=0.0): return Vector((x, y, z0 + zr * H))          # helper: y is absolute, z relative to the height
log(f"gray {len(gw)} verts, textured {len(tw)} verts, height {H:.4f}, length y {min(p.y for p in gw):+.3f}..{max(p.y for p in gw):+.3f}")
kd = mathutils.kdtree.KDTree(len(gw))
for i, p in enumerate(gw): kd.insert(p, i)
kd.balance()
twin = []; worst = 0.0
for p in tw:
    co, idx, d = kd.find(p); twin.append(idx); worst = max(worst, d)
log(f"textured -> gray twins: worst distance {worst / H:.6f} heights")

# ---- 2. landmarks ----
def rel(z): return (z - z0) / H
def centroid(pts):
    return sum(pts, Vector()) / len(pts) if pts else None
# leg clusters: low vertices split by front / back (gap in y) and side (x sign); tail strands (behind the hind legs) are left out
low = [p for p in gw if rel(p.z) < 0.30]
yf = [p.y for p in low if p.y < -0.1]; yb = [p.y for p in low if -0.05 < p.y < 0.21]
FRONT_Y = sum(yf) / len(yf); BACK_Y = sum(yb) / len(yb)
def leg_pts(front, side, zr0, zr1):
    return [p for p in gw if zr0 <= rel(p.z) <= zr1 and (abs(p.y - FRONT_Y) < 0.08 if front else abs(p.y - BACK_Y) < 0.08)
            and (p.x > 0) == (side == 'L')]
legs = {}
for front in (True, False):
    for side in ('L', 'R'):
        hoof = centroid(leg_pts(front, side, 0.0, 0.04)); mid = centroid(leg_pts(front, side, 0.14, 0.20))
        knee = centroid(leg_pts(front, side, 0.26, 0.32))
        legs[('F' if front else 'B') + side] = (hoof, mid, knee)
spine_y = [p.y for p in gw if 0.5 < rel(p.z) < 0.6]
BELLY = 0.42
log(f"legs: front y {FRONT_Y:+.3f}, back y {BACK_Y:+.3f}; " + ", ".join(f"{k} hoof x {v[0].x:+.3f}" for k, v in legs.items()))
head_pts = [p for p in gw if rel(p.z) > 0.62 and p.y < -0.38]
muzzle = min(head_pts, key=lambda p: p.y)
ears = [p for p in gw if rel(p.z) > 0.85 and p.y < -0.40]   # the mane's crest rises as high as the ears further back
ear_tip = max(ears, key=lambda p: p.z)
tail_pts = [p for p in gw if p.y > 0.22 and rel(p.z) < 0.66]
tail_root = max([p for p in tail_pts if rel(p.z) > 0.5], key=lambda p: p.z, default=P(0.22, 0.6))
tail_tip = centroid([p for p in tail_pts if rel(p.z) < 0.05])
log(f"muzzle y {muzzle.y:+.3f} z {rel(muzzle.z):.2f}; ear tip y {ear_tip.y:+.3f}; tail root y {tail_root.y:+.3f} z {rel(tail_root.z):.2f}, tip y {tail_tip.y:+.3f}")

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
cx = (legs['FL'][0].x + legs['FR'][0].x + legs['BL'][0].x + legs['BR'][0].x) / 4
bone("Root", Vector((cx, 0, z0)), Vector((cx, -0.1 * H, z0)), deform=False)
hips = P(BACK_Y - 0.02, 0.56, cx); spine1 = P((BACK_Y + FRONT_Y) / 2, 0.57, cx); chest = P(FRONT_Y + 0.05, 0.58, cx)
withers = P(FRONT_Y + 0.06, 0.68, cx)            # above and a little behind the front legs
bone("Hips", hips, spine1, "Root")
bone("Spine1", spine1, chest, "Hips", True)
bone("Chest", chest, withers, "Spine1", True)
head_base = Vector((cx, ear_tip.y + 0.03, z0 + 0.82 * H))
neck_mid = (withers + head_base) / 2 + Vector((0, 0, 0.02 * H))
bone("Neck1", withers, neck_mid, "Chest", True)
bone("Neck2", neck_mid, head_base, "Neck1", True)
bone("Head", head_base, Vector((cx, muzzle.y + 0.01, muzzle.z + 0.02 * H)), "Neck2", True)
jaw_hinge = head_base + (Vector((cx, muzzle.y, muzzle.z)) - head_base) * 0.35 + Vector((0, 0, -0.05 * H))
bone("Jaw", jaw_hinge, Vector((cx, muzzle.y + 0.015, muzzle.z - 0.03 * H)), "Head")
for side, sg in (('L', 1), ('R', -1)):
    eb_ = [p for p in ears if (p.x - cx) * sg > 0] or ears
    tip = max(eb_, key=lambda p: p.z)
    bone(f"Ear_{side}", Vector((tip.x - 0.01 * sg, tip.y + 0.01, z0 + 0.88 * H)), tip, "Head")
for key, (hoof, mid, knee) in legs.items():
    top = Vector((knee.x, knee.y, z0 + (BELLY + 0.08) * H))
    par = "Chest" if key[0] == 'F' else "Hips"
    bone(f"{key}_Upper", top, knee, par)
    bone(f"{key}_Lower", knee, mid, f"{key}_Upper", True)
    bone(f"{key}_Hoof", mid, Vector((hoof.x, hoof.y, z0)), f"{key}_Lower", True)
# tail: animated base + two segments, then the spring chain to the tip
tr = Vector((cx, tail_root.y, tail_root.z)); tt = Vector((cx, tail_tip.y, z0 + 0.02 * H))
def lerp(a, b, t): return a + (b - a) * t
bone("TailBase", lerp(tr, tt, -0.08), tr, "Hips")
bone("Tail1", tr, lerp(tr, tt, 0.22), "TailBase", True)
bone("Tail2", lerp(tr, tt, 0.22), lerp(tr, tt, 0.45), "Tail1", True)
bone("Tail_0_0", lerp(tr, tt, 0.45), lerp(tr, tt, 0.72), "Tail2", True)
bone("Tail_0_1", lerp(tr, tt, 0.72), tt, "Tail_0_0", True)
# mane: three short spring chains hanging down the neck's sides from the crest
mane_roots = [(withers + neck_mid) / 2, neck_mid, (neck_mid + head_base) / 2]
mane_par = ["Neck1", "Neck2", "Neck2"]
for c, (r, par) in enumerate(zip(mane_roots, mane_par)):
    r = r + Vector((0, 0, 0.05 * H))
    bone(f"Mane_{c}_0", r, r + Vector((0, 0.04 * H, -0.10 * H)), par)
    bone(f"Mane_{c}_1", r + Vector((0, 0.04 * H, -0.10 * H)), r + Vector((0, 0.08 * H, -0.20 * H)), f"Mane_{c}_0", True)
bpy.ops.object.mode_set(mode='OBJECT')
bones = [b for b in arm_data.bones if b.use_deform]
log(f"armature: {len(arm_data.bones)} bones")

# ---- 4. weights on the gray mesh ----
def seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12))); return (p - (a + ab * t)).length
bmw = A.matrix_world
segs = {b.name: (bmw @ b.head_local, bmw @ b.tail_local) for b in bones}
LEG = {k: [f"{k}_Upper", f"{k}_Lower", f"{k}_Hoof"] for k in legs}
TAIL = ["TailBase", "Tail1", "Tail2", "Tail_0_0", "Tail_0_1"]
MANE = [n for n in segs if n.startswith("Mane_")]
HEAD = ["Head", "Jaw", "Ear_L", "Ear_R", "Neck2"]
BODY = ["Hips", "Spine1", "Chest", "Neck1", "Neck2", "Head"]
def region(p):
    zr = rel(p.z)
    if p.y > tail_root.y - 0.01 and zr < rel(tail_root.z) + 0.04 and abs(p.x - cx) < 0.09: return TAIL + ["Hips"]
    if zr < BELLY + 0.02:
        for k, names in LEG.items():
            if seg_dist(p, *segs[k + "_Upper"]) < 0.06 * H or seg_dist(p, *segs[k + "_Lower"]) < 0.06 * H or seg_dist(p, *segs[k + "_Hoof"]) < 0.06 * H:
                return names + (["Chest"] if k[0] == 'F' else ["Hips"])
        return ["Hips", "Spine1", "Chest"]                            # tassels / saddle cloth hanging below the belly
    if p.y < withers.y - 0.02 or zr > 0.72: return BODY + HEAD + MANE
    return BODY + MANE + ["TailBase"]
def weigh(p, cands, k=3):
    ds = sorted(((seg_dist(p, *segs[n]), n) for n in cands), key=lambda t: t[0])[:k]
    w = {n: 1.0 / (d + 0.01 * H) ** 4 for d, n in ds}
    s = sum(w.values()); return {n: x / s for n, x in w.items()}
gweights = [weigh(p, region(p)) for p in gw]
# loose pieces (strands, tassels, ornaments): one weight set for the whole piece, the average of its vertices
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
for mem in comps:
    if mem is big or len(mem) > 600: continue
    # long strands (tail / mane) keep per-vertex weights so the spring chain bends them; small pieces are rigid
    ext = max((gw[i] - gw[j]).length for i in mem[:1] for j in mem)
    if ext > 0.12 * H and any(n in TAIL + MANE for n in gweights[mem[0]]): continue
    acc = {}
    for i in mem:
        for n, x in gweights[i].items(): acc[n] = acc.get(n, 0) + x / len(mem)
    for i in mem: gweights[i] = dict(acc)
    rigid += 1
for _ in range(2):   # smoothing along the edges (the joints bend softly)
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
G.hide_render = True; G.hide_viewport = True

# ---- 5. actions ----
pb = A.pose.bones
for p_ in pb: p_.rotation_mode = 'XYZ'
def key_pose(frame, pose):
    for n, (rx, ry, rz) in pose.items():
        pb[n].rotation_euler = Euler((math.radians(rx), math.radians(ry), math.radians(rz)))
        pb[n].keyframe_insert("rotation_euler", frame=frame)
def make_action(name, frames, pose_at):
    act = bpy.data.actions.new(name); A.animation_data_create(); A.animation_data.action = act
    for p_ in pb: p_.rotation_euler = (0, 0, 0)
    for f in frames: key_pose(f, pose_at(f))
    act.use_fake_user = True
    return act
ANIMATED = ["Hips", "Spine1", "Chest", "Neck1", "Neck2", "Head", "Jaw", "Ear_L", "Ear_R", "TailBase", "Tail1", "Tail2",
            "FL_Upper", "FL_Lower", "FR_Upper", "FR_Lower", "BL_Upper", "BR_Upper"]
def full(d): return {n: d.get(n, (0, 0, 0)) for n in ANIMATED}
# Bone local axes (roll 0): X is sideways, so +X rotation pitches a bone; Z swings it sideways.
# Grazing pose: searched, not guessed. Neck1 = a, Neck2 = 0.35 a, Head = b; the muzzle (Head's tail) must reach the food (`gz` of the
# height above the ground) in front of the front hooves, with the face pointing steeply down (about 65 degrees below horizontal).
# The model's neck is short: reaching the ground needs ~90 degrees at one joint and folds the head behind the mane, so the target is
# the hay trough in the stable (`gz` 0.40 of the height) and the neck joint stays under `gmax` 60 degrees.
GZ = float(opt.get('gz', 0.40)); GMAX = int(opt.get('gmax', 60))
def muzzle_for(a, b):
    for p_ in pb: p_.rotation_euler = (0, 0, 0)
    pb["Neck1"].rotation_euler = (math.radians(a), 0, 0); pb["Neck2"].rotation_euler = (math.radians(0.35 * a), 0, 0)
    pb["Head"].rotation_euler = (math.radians(b), 0, 0)
    bpy.context.view_layer.update()
    hb = pb["Head"]; tip = A.matrix_world @ hb.tail; base = A.matrix_world @ hb.head
    return tip, base
best = None
for a in range(-GMAX, GMAX + 1, 2):
    for b in range(-60, 61, 5):
        tip, base = muzzle_for(a, b)
        d = tip - base; down = math.degrees(math.atan2(-d.z, max(-d.y, 1e-6)))   # angle of the face below the horizontal, facing forward
        cost = ((tip.z - (z0 + GZ * H)) / H) ** 2 * 400 + max(0.0, tip.y - (FRONT_Y - 0.06)) ** 2 * 400 + ((down - 60) / 90) ** 2
        if d.y > 0: cost += 10                                                  # face turned backwards
        if best is None or cost < best[0]: best = (cost, a, b, tip)
for p_ in pb: p_.rotation_euler = (0, 0, 0)
GRAZE = {"Neck1": best[1], "Neck2": 0.35 * best[1], "Head": best[2]}
log(f"grazing pose: Neck1 {best[1]} deg, Neck2 {0.35 * best[1]:.0f} deg, Head {best[2]} deg; muzzle at z {rel(best[3].z):.3f}, y {best[3].y:+.3f} (front hooves y {FRONT_Y:+.3f})")
def graze_pose(lift=0.0, chew=0.0):
    sg = 1 if GRAZE["Neck1"] >= 0 else -1                                     # lifting = towards the rest pose
    return {"Neck1": (GRAZE["Neck1"] - 10 * lift * sg, 0, 0), "Neck2": (GRAZE["Neck2"] - 6 * lift * sg, 0, 0), "Head": (GRAZE["Head"], 0, 0),
            "Jaw": (8 * chew, 0, 0), "Chest": (4, 0, 0), "FL_Upper": (-6, 0, 0), "FR_Upper": (4, 0, 0), "FL_Lower": (6, 0, 0),
            "Ear_L": (-10, 0, 8), "Ear_R": (-10, 0, -8)}
def eat(f):            # 150 frames: chewing at ~2.4 Hz, a small lift around frame 75
    t = (f - 1) / 150
    lift = 0.5 - 0.5 * math.cos(2 * math.pi * t) if 0.35 < t < 0.65 else 0.0
    chew = 0.5 + 0.5 * math.sin(2 * math.pi * 12 * t)
    swish = 6 * math.sin(2 * math.pi * 2 * t)
    d = graze_pose(lift, chew); d["Tail1"] = (0, 0, swish); d["Tail2"] = (0, 0, swish * 1.2)
    return full(d)
def idle(f):           # 120 frames: breathing, two tail swishes, ear flicks
    t = (f - 1) / 120
    br = math.sin(2 * math.pi * 2 * t)
    swish = 18 * math.sin(2 * math.pi * 2 * t) * (0.5 + 0.5 * math.cos(2 * math.pi * t))
    d = {"Chest": (1.0 * br, 0, 0), "Spine1": (-0.6 * br, 0, 0), "Neck1": (-2 + 1.5 * br, 0, 0), "Head": (2 * math.sin(2 * math.pi * t), 0, 3 * math.sin(2 * math.pi * t)),
         "TailBase": (-4, 0, swish * 0.3), "Tail1": (0, 0, swish), "Tail2": (0, 0, swish * 1.3),
         "Ear_L": (0, 0, 25 if 0.30 < t < 0.36 else 0), "Ear_R": (0, 0, -25 if 0.68 < t < 0.74 else 0)}
    return full(d)
def lookup(f):         # 60 frames: grazing -> head high, ears forward; ease out, then hold
    t = min((f - 1) / 36, 1.0); e = t * t * (3 - 2 * t)
    g = graze_pose()
    sg = 1 if GRAZE["Neck1"] >= 0 else -1
    up = {"Neck1": (-18 * sg, 0, 0), "Neck2": (-8 * sg, 0, 0), "Head": (6 * sg, 0, 0), "Ear_L": (-20, 0, -6), "Ear_R": (-20, 0, 6), "Chest": (-2 * sg, 0, 0)}
    d = {}
    for n in set(g) | set(up):
        a = g.get(n, (0, 0, 0)); b = up.get(n, (0, 0, 0)); d[n] = tuple(a[i] + (b[i] - a[i]) * e for i in range(3))
    d["Jaw"] = (0, 0, 0)
    return full(d)
acts = {
    "EatGrass": make_action("EatGrass", range(1, 152, 2), eat),
    "Idle": make_action("Idle", range(1, 122, 2), idle),
    "LookUp": make_action("LookUp", range(1, 62, 2), lookup),
}
log("actions: " + ", ".join(f"{n} {int(a.frame_range[1] - a.frame_range[0])} frames" for n, a in acts.items()))

# ---- 6. previews + stretch ----
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'
sc.view_settings.view_transform = 'Standard'
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.3, 0.3, 0.33)
cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = 1.25
cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
def shot(path, view, color='TEXTURE', w=700, h=560):
    sc.render.resolution_x = w; sc.render.resolution_y = h; sc.display.shading.color_type = color
    c = Vector((cx, 0.0, z0 + 0.45 * H))
    d = {'side': Vector((1, 0, 0)), 'front': Vector((0, -1, 0)), 'q': Vector((0.75, -0.65, 0)).normalized(), 'back': Vector((0, 1, 0))}[view]
    cam.location = c + d * 4; cam.rotation_euler = (math.radians(90), 0, math.atan2(d.x, -d.y))
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
for p_ in pb: p_.rotation_euler = (0, 0, 0)
bpy.context.view_layer.update()
# region colours: paint the dominant bone group as vertex colours is not shown by workbench OBJECT mode; render the rest pose twice instead
P_ = []
for v in ('side', 'front', 'q', 'back'):
    p = os.path.join(out_dir, f"_r_{v}.png"); shot(p, v); P_.append(p)
sheet(P_, os.path.join(out_dir, "rest.png"), 4)
A.show_in_front = True; arm_data.display_type = 'STICK'
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
    log(f"stretch {act_name}: max {worst[0]:.3f} at f{worst[3]} (>1.3: {worst[1]}), worst edge near y {worst[2].y:+.3f} z {rel(worst[2].z):.2f} x {worst[2].x:+.3f}")
    P_ = []
    for f in sorted(set([first, (first + last) // 3, (2 * (first + last)) // 3, last])):
        sc.frame_set(f); bpy.context.view_layer.update()
        for v in ('side', 'q'):
            p = os.path.join(out_dir, f"_c_{v}_{f}.png"); shot(p, v, w=560, h=450); P_.append(p)
    sheet(P_, os.path.join(out_dir, f"clip_{act_name}.png"), 4)
with open(os.path.join(out_dir, "stretch.txt"), "w", encoding="utf-8") as fh: fh.write("\n".join(report) + "\n")

# ---- 7. export: armature + textured body + all three actions ----
A.animation_data.action = None
for p_ in pb: p_.rotation_euler = (0, 0, 0)
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
