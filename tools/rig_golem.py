"""Người đá (Sơn Tinh's Z skill): rig the Meshy rock golem by hand, weight it and animate it, export for Unity (Legacy clips).

    blender -b --python tools/rig_golem.py -- <in.fbx> <out.fbx> <preview_dir>

* in.fbx      : the prepared golem (tools/prep_new_assets.py: rock_golem.fbx, decimated, no material, same UVs as the textures)
* out.fbx     : armature + RockGolem_Body with the clips below, bind pose, no material (overwrite rock_golem.fbx with it)
* preview_dir : rest.png, weights.png, run.png / emerge.png (key poses side by side)

The model is one piece of stone, 1.9 units tall, facing -Y, its left at +X, long ape arms with the fists at knee height.
Bones (Generic): Root > Hips > Spine > Chest > Neck > Head; Chest > Shoulder_<L|R> > UpperArm > LowerArm > Hand;
Hips > Thigh_<L|R> > Shin > Foot. Each vertex follows the two or three nearest bones of its region (arm, leg or trunk), weights
smoothed over the surface, so the rocks bend at the joints only.
Poses are rotations about the armature axes applied top-down on the posed bones (pitch about X: +pitch tips the top of a bone
forward and the bottom of it back; roll about Y; yaw about Z), so a sign means the same on every bone.
Clips (30 fps): Run (loop 0.6 s: heavy gorilla run, knuckles swinging, body bobbing), Emerge (0.5 s: crouched, bursts up with
the arms thrown out, ends in the run's first pose).
"""
import bpy, bmesh, sys, math, os
from mathutils import Vector, Matrix, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:]
src, out_fbx, out_dir = argv[:3]
os.makedirs(out_dir, exist_ok=True)
FPS = 30

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps = FPS
bpy.ops.import_scene.fbx(filepath=src)
body = [o for o in sc.objects if o.type == 'MESH'][0]
body.name = "RockGolem_Body"
# bake the import transform into the vertices: the armature and the body share the identity frame
body.data.transform(body.matrix_world)
body.matrix_world = Matrix.Identity(4)
W = [v.co.copy() for v in body.data.vertices]
z0 = min(p.z for p in W)
print("GOLEM verts", len(W), "z", round(z0, 3), round(max(p.z for p in W), 3))

# ---- landmarks (measured on the front / side views; z from -0.95 at the soles to 0.95 at the top of the back spikes) ----
def P(x, y, z): return Vector((x, y, z))
J = {
    "Root": P(0, 0.05, z0), "Hips": P(0, 0.12, -0.30), "Spine": P(0, 0.10, -0.05), "Chest": P(0, 0.02, 0.22),
    "Neck": P(0, -0.22, 0.40), "Head": P(0, -0.42, 0.42), "HeadTop": P(0, -0.50, 0.66),
}
for s, sx in (("L", 1), ("R", -1)):
    J["Shoulder_" + s] = P(sx * 0.22, 0.05, 0.42)
    J["UpperArm_" + s] = P(sx * 0.56, 0.02, 0.46)
    J["LowerArm_" + s] = P(sx * 0.70, -0.08, -0.02)
    J["Hand_" + s] = P(sx * 0.74, -0.18, -0.48)
    J["HandEnd_" + s] = P(sx * 0.76, -0.24, -0.70)
    J["Thigh_" + s] = P(sx * 0.22, 0.14, -0.36)
    J["Shin_" + s] = P(sx * 0.27, 0.08, -0.64)
    J["Foot_" + s] = P(sx * 0.31, 0.16, -0.86)
    J["Toe_" + s] = P(sx * 0.32, -0.12, -0.93)

arm_data = bpy.data.armatures.new("RockGolem_Rig")
rig = bpy.data.objects.new("RockGolem", arm_data)
sc.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
def bone(name, head, tail, parent=None):
    b = eb.new(name); b.head = head; b.tail = tail
    if parent: b.parent = eb[parent]
    return b
bone("Root", J["Root"], J["Root"] + Vector((0, 0, 0.2)))
bone("Hips", J["Hips"], J["Spine"], "Root")
bone("Spine", J["Spine"], J["Chest"], "Hips")
bone("Chest", J["Chest"], J["Neck"], "Spine")
bone("Neck", J["Neck"], J["Head"], "Chest")
bone("Head", J["Head"], J["HeadTop"], "Neck")
for s in ("L", "R"):
    bone("Shoulder_" + s, J["Shoulder_" + s], J["UpperArm_" + s], "Chest")
    bone("UpperArm_" + s, J["UpperArm_" + s], J["LowerArm_" + s], "Shoulder_" + s)
    bone("LowerArm_" + s, J["LowerArm_" + s], J["Hand_" + s], "UpperArm_" + s)
    bone("Hand_" + s, J["Hand_" + s], J["HandEnd_" + s], "LowerArm_" + s)
    bone("Thigh_" + s, J["Thigh_" + s], J["Shin_" + s], "Hips")
    bone("Shin_" + s, J["Shin_" + s], J["Foot_" + s], "Thigh_" + s)
    bone("Foot_" + s, J["Foot_" + s], J["Toe_" + s], "Shin_" + s)
for b in eb: b.roll = 0.0
bpy.ops.object.mode_set(mode='OBJECT')
segs = {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in arm_data.bones}

# ---- weights ----
def seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length

def region(p):
    side = "L" if p.x > 0 else "R"
    ax = abs(p.x)
    arm = ax > 0.47 and p.z < 0.40 and (p.z > -0.55 or ax > 0.60)  # below the knees only the fists reach that far out
    if arm:                                                        # the long arms hang clear of the trunk
        return ["UpperArm_" + side, "LowerArm_" + side, "Hand_" + side]
    if ax > 0.38 and p.z >= 0.25:                                  # shoulder mass
        return ["Shoulder_" + side, "UpperArm_" + side, "Chest"]
    if p.z < -0.42 or (p.z < -0.30 and ax > 0.12):                 # legs
        return ["Thigh_" + side, "Shin_" + side, "Foot_" + side, "Hips"]
    if p.y < -0.25 and p.z > 0.22:                                 # the head juts forward
        return ["Head", "Neck", "Chest"]
    return ["Hips", "Spine", "Chest", "Neck", "Shoulder_" + side]

def weigh(p):
    cands = region(p)
    d = sorted((seg_dist(p, *segs[n]), n) for n in cands)[:3]
    w = {n: 1.0 / max(x, 0.02) ** 4 for x, n in d}
    s = sum(w.values())
    return {n: x / s for n, x in w.items()}

weights = [weigh(p) for p in W]

# Meshy welded the fists to the thighs and the elbows to the flanks: faces that join an arm to anything else are cut, so a
# swinging arm does not drag a sheet of stone behind it
def chain(i):
    n = region(W[i])[0]
    return n.split("_")[-1] + ("arm" if "Arm" in n else "leg" if "Thigh" in n else "") if "_" in n else "trunk"
bm = bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
def torn(f):
    kinds = {chain(v.index) for v in f.verts}
    if not any(k.endswith("arm") for k in kinds) or len(kinds) == 1: return False
    if any(k.endswith("leg") for k in kinds): return True              # fist on a thigh
    return max(W[v.index].z for v in f.verts) < 0.15                    # elbow on a flank (the shoulders stay joined)
cut = [f for f in bm.faces if torn(f)]
bmesh.ops.delete(bm, geom=cut, context='FACES_ONLY')
bm.to_mesh(body.data); body.data.update()
print("GOLEM cut", len(cut), "faces joining an arm to the rest")
bm.verts.ensure_lookup_table()
adj = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]
bm.free()
for _ in range(4):                                                 # smooth over the surface: soft joints, no torn rocks
    new = []
    for i in range(len(W)):
        nb = [i, i] + adj[i]
        acc = {}
        for j in nb:
            for n, x in weights[j].items(): acc[n] = acc.get(n, 0.0) + x / len(nb)
        new.append(acc)
    weights = new
body.vertex_groups.clear()
for b in arm_data.bones: body.vertex_groups.new(name=b.name)
for i, w in enumerate(weights):
    top = sorted(w.items(), key=lambda kv: -kv[1])[:4]
    s = sum(x for _, x in top)
    for n, x in top:
        if x / s > 0.002: body.vertex_groups[n].add([i], x / s, 'REPLACE')
body.parent = rig
mod = body.modifiers.new("Armature", 'ARMATURE'); mod.object = rig

# ---- poses ----
def rot(pitch=0.0, roll=0.0, yaw=0.0):
    return (Quaternion((0, 0, 1), math.radians(yaw)) @ Quaternion((0, 1, 0), math.radians(roll)) @ Quaternion((1, 0, 0), math.radians(pitch)))

ORDER = ["Root", "Hips", "Spine", "Chest", "Neck", "Head"] + [f"{b}_{s}" for s in ("L", "R") for b in ("Shoulder", "UpperArm", "LowerArm", "Hand", "Thigh", "Shin", "Foot")]
ORDER.sort(key=lambda n: len(arm_data.bones[n].parent_recursive))

def apply_pose(pose, lift=0.0, fwd=0.0):
    """pose: bone -> (pitch, roll, yaw) about the armature axes, applied on the posed bone around its head, parents first."""
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = Quaternion(); pb.location = Vector()
    bpy.context.view_layer.update()
    for n in ORDER:
        pb = rig.pose.bones[n]
        if n == "Hips":
            pb.matrix = Matrix.Translation(Vector((0, -fwd, lift))) @ pb.matrix
            bpy.context.view_layer.update()
        if n not in pose: continue
        q = rot(*pose[n])
        head = pb.matrix.translation.copy()
        pb.matrix = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head) @ pb.matrix
        bpy.context.view_layer.update()

def key(frame):
    for pb in rig.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        pb.keyframe_insert("location", frame=frame)

def run_pose(t):
    """t in [0, 1): one full stride (two steps). Left leg forward at t = 0."""
    a = math.sin(2 * math.pi * t)                 # +1: left leg forward, right arm forward
    c = math.cos(2 * math.pi * t)
    bob = abs(math.sin(2 * math.pi * t))          # 0 at the passing moment... 1 at full stride
    pose = {
        "Hips": (6, 4 * a, 8 * a),
        "Spine": (10, -2 * a, -6 * a),
        "Chest": (8 + 3 * bob, -3 * a, -10 * a),
        "Neck": (-14, 0, 6 * a),
        "Head": (-6, 0, 4 * a),
    }
    for s, sg in (("L", 1), ("R", -1)):
        leg = a * sg                               # +1: this leg forward
        knee = max(0.0, -c * sg) * 55 + 12         # the back leg folds as it comes through
        pose["Thigh_" + s] = (-30 * leg - 6, 0, 0)
        pose["Shin_" + s] = (knee, 0, 0)
        pose["Foot_" + s] = (-knee * 0.4 + 14 * leg, 0, 0)
        arm = -leg                                 # arms swing against the legs
        pose["Shoulder_" + s] = (-6 * arm, 0, 0)
        pose["UpperArm_" + s] = (-30 * arm + 4, -6 * sg, 0)
        pose["LowerArm_" + s] = (-18 - 22 * max(0.0, arm), 0, 0)
        pose["Hand_" + s] = (-10 * arm, 0, 0)
    return pose, -0.07 * (1 - bob) + 0.0, 0.0

def emerge_pose(t):
    """t in [0, 1]: crouched with the fists on the ground, bursts up, arms thrown wide, into the run's first pose."""
    e = t * t * (3 - 2 * t)
    burst = math.sin(math.pi * min(1.0, t * 1.3))
    r0, l0, _ = run_pose(0.0)
    pose = {}
    for n in set(r0) | {"Thigh_L", "Thigh_R", "Shin_L", "Shin_R", "UpperArm_L", "UpperArm_R"}:
        target = r0.get(n, (0, 0, 0))
        crouch = {"Hips": (25, 0, 0), "Spine": (20, 0, 0), "Chest": (15, 0, 0), "Neck": (-30, 0, 0),
                  "Thigh_L": (-60, 0, 0), "Thigh_R": (-60, 0, 0), "Shin_L": (80, 0, 0), "Shin_R": (80, 0, 0),
                  "Foot_L": (-25, 0, 0), "Foot_R": (-25, 0, 0),
                  "UpperArm_L": (-30, 0, 0), "UpperArm_R": (-30, 0, 0), "LowerArm_L": (-10, 0, 0), "LowerArm_R": (-10, 0, 0)}.get(n, (0, 0, 0))
        pose[n] = tuple(crouch[i] + (target[i] - crouch[i]) * e for i in range(3))
    for s, sg in (("L", 1), ("R", -1)):                      # the arms fly out to the sides at the top of the burst
        p = pose["UpperArm_" + s]
        pose["UpperArm_" + s] = (p[0] - 35 * burst, p[1] - 55 * burst * sg, p[2])
    pose["Chest"] = (pose["Chest"][0] - 25 * burst, 0, 0)
    pose["Neck"] = (pose["Neck"][0] - 20 * burst, 0, 0)
    return pose, -0.28 * (1 - e) + 0.05 * burst, 0.0

def make_action(name, frames, fn, loop):
    act = bpy.data.actions.new(name)
    rig.animation_data_create(); rig.animation_data.action = act
    for f in range(frames + (1 if not loop else 1)):
        t = f / frames
        pose, lift, fwd = fn(t % 1.0 if loop else min(t, 1.0))
        apply_pose(pose, lift, fwd)
        key(f)
    act.use_fake_user = True
    print("GOLEM action", name, frames, "frames")
    return act

run = make_action("Run", 18, run_pose, True)
emerge = make_action("Emerge", 15, emerge_pose, False)

# ---- previews ----
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'
sc.render.resolution_x = 360; sc.render.resolution_y = 400
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.3
def shoot(path, side=True):
    cam.location = (5, -0.2, 0) if side else (0, -5, 0)
    cam.rotation_euler = (math.pi / 2, 0, math.pi / 2) if side else (math.pi / 2, 0, 0)
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)
rig.animation_data.action = None
apply_pose({})
shoot(os.path.join(out_dir, "rest_side.png")); shoot(os.path.join(out_dir, "rest_front.png"), False)
for act, frames in ((run, (0, 4, 9, 13)), (emerge, (0, 5, 10, 15))):
    rig.animation_data.action = act
    for f in frames:
        sc.frame_set(f)
        shoot(os.path.join(out_dir, f"{act.name}_{f:02d}_side.png"))
        shoot(os.path.join(out_dir, f"{act.name}_{f:02d}_front.png"), False)

# ---- export: armature + body + both clips ----
rig.animation_data.action = run
sc.frame_set(0)
for o in sc.objects: o.select_set(o in (rig, body))
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                         bake_anim_simplify_factor=0.0, embed_textures=False, path_mode='COPY', use_armature_deform_only=False)   # same axes as prep_prop.py
print("GOLEM exported", out_fbx)
