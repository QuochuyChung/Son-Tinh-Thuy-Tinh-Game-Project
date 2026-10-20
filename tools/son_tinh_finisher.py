"""Sơn Tinh's own combo finisher, hand-keyed (the third light attack): Địa Chấn Quyền, the earth-shaking fist. He sinks and winds the
right fist up high behind him, springs up, and drives the fist down into the ground in front of him in a deep lunge (impact at
0.55 s), holds, and comes back to his fighting stance (1.4 s).

Keyed the same way as tools/cutscene_gestures.py (rotations in the character's frame: x = its left, y = up, z = forward; positive x
bends forward, positive y turns to its left), laid over one still frame of his own fighting stance (the first frame of the cut Punch
clip), baked at 30 fps.

    blender -b --python tools/son_tinh_finisher.py -- <son_tinh_v2_punch.fbx> <out.fbx> [preview_dir]

Unity: Humanoid, avatar from this model (Assets/Animations/SonTinh_v2/son_tinh_v2_earth_fist.fbx, clip EarthFist), plays in the Attack3
state through AOC_SonTinh_v2 (SonTinhCombatBuilder).
"""
import bpy, sys, math, os
from mathutils import Matrix, Quaternion, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
IDLE, OUT = argv[0], argv[1]
PREVIEW = argv[2] if len(argv) > 2 and not argv[2].startswith("only=") else None
BASE_FRAME = 14   # the fighting stance: first frame of the cut Punch clip (SonTinhCombatBuilder.Cuts)
ONLY = next((a[5:].split(",") for a in argv if a.startswith("only=")), None)
FPS = 30

B = {k: "mixamorig:" + v for k, v in dict(
    H="Hips", S="Spine", S1="Spine1", S2="Spine2", N="Neck", Hd="Head",
    LS="LeftShoulder", LA="LeftArm", LF="LeftForeArm", LH="LeftHand",
    RS="RightShoulder", RA="RightArm", RF="RightForeArm", RH="RightHand",
    LU="LeftUpLeg", LL="LeftLeg", LFt="LeftFoot", RU="RightUpLeg", RL="RightLeg", RFt="RightFoot").items()}
ORDER = ["H", "S", "S1", "S2", "N", "Hd", "LS", "LA", "LF", "LH", "RS", "RA", "RF", "RH", "LU", "LL", "LFt", "RU", "RL", "RFt"]
AXES = {"x": Vector((1, 0, 0)), "y": Vector((0, 1, 0)), "z": Vector((0, 0, 1))}


def merge(*poses):
    out = {}
    for p in poses:
        for k, v in p.items():
            out[k] = v if k == "drop" else out.get(k, []) + list(v)
    return out


NEUTRAL = []

WIND_UP = {"drop": (0.07, 0.02),
           "S": [("y", -10)], "S1": [("x", -6), ("y", -12)], "S2": [("x", -6), ("y", -12)], "Hd": [("y", 18)],
           "RA": [("x", -160), ("z", -15)], "RF": [("x", -40)], "LA": [("x", -55), ("y", -10)], "LF": [("x", -45)],
           "LU": [("x", -28)], "RU": [("x", -22)], "LL": [("x", 45)], "RL": [("x", 40)], "LFt": [("x", -12)], "RFt": [("x", -12)]}
AIR = {"drop": (-0.10, 0.0),
       "S1": [("x", -10), ("y", -6)], "S2": [("x", -12), ("y", -6)], "Hd": [("x", 8), ("y", 8)],
       "RA": [("x", -175), ("z", -5)], "RF": [("x", -15)], "LA": [("x", -45), ("z", 25)], "LF": [("x", -30)],
       "LU": [("x", -55)], "LL": [("x", 75)], "RU": [("x", -30)], "RL": [("x", 65)], "LFt": [("x", 20)], "RFt": [("x", 20)]}
IMPACT = {"drop": (0.30, -0.02),
          "S": [("x", 18), ("y", 8)], "S1": [("x", 18), ("y", 6)], "S2": [("x", 14)], "N": [("x", -10)], "Hd": [("x", -20)],
          "RA": [("x", 10), ("y", 10)], "RF": [("x", 75)], "LA": [("x", 35), ("z", 30)], "LF": [("x", -25)],
          "LU": [("x", -75)], "LL": [("x", 80)], "RU": [("x", 10)], "RL": [("x", 55)], "LFt": [("x", -5)], "RFt": [("x", -35)]}
HOLD = merge(IMPACT, {"drop": (0.29, -0.02), "S2": [("x", -2)], "Hd": [("x", -4)]})

CLIPS = {
    "EarthFist": (1.4, False, [(0.0, {}), (0.28, WIND_UP), (0.42, AIR), (0.55, IMPACT), (0.85, HOLD), (1.4, {})]),
}


# ---------------------------------------------------------------- baking

def smooth(k):
    k = max(0.0, min(1.0, k))
    return k * k * (3 - 2 * k)


def pose_at(keys, t):
    """Blend of the two keys around t: bone -> list of (axis, degrees) with weights folded in, plus the hips drop."""
    for (t0, p0), (t1, p1) in zip(keys, keys[1:]):
        if t0 <= t <= t1:
            w = smooth((t - t0) / max(t1 - t0, 1e-6))
            return p0, p1, w
    return keys[-1][1], keys[-1][1], 0.0


FACING = Quaternion()   # armature-space yaw of the stance's hips: the fighting stance stands turned, the keys are in its own frame


def rotation_of(entries, scale):
    q = Quaternion()
    for axis, deg in entries:
        q = Quaternion(FACING @ AXES[axis], math.radians(deg * scale)) @ q
    return q


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=IDLE)
    arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    idle = arm.animation_data.action
    idle_start, idle_end = int(idle.frame_range[0]), int(idle.frame_range[1])
    pbs = {k: arm.pose.bones[v] for k, v in B.items() if v in arm.pose.bones}
    height = (arm.pose.bones[B["Hd"]].head - arm.pose.bones[B["LFt"]].head).length
    scene = bpy.context.scene
    scene.render.fps = FPS
    made = []

    for name, (duration, loop, keys) in CLIPS.items():
        if ONLY and name not in ONLY:
            continue
        frames = int(round(duration * FPS))
        baked = []   # per frame: {bone: (quaternion basis, location basis)}
        for f in range(frames + 1):
            t = f / FPS
            arm.animation_data.action = idle
            scene.frame_set(idle_start + BASE_FRAME)
            for nk in NEUTRAL:
                if nk in pbs:
                    pbs[nk].rotation_mode = "QUATERNION"
                    pbs[nk].rotation_quaternion = Quaternion()
            bpy.context.view_layer.update()
            p0, p1, w = pose_at(keys, t)
            breath = 0.0   # a combat move: no breathing
            for k in ORDER:
                if k not in pbs:
                    continue
                pb = pbs[k]
                entries0, entries1 = p0.get(k, []), p1.get(k, [])
                if k == "H" and f == 0:
                    global FACING
                    fwd = pb.matrix.to_3x3().col[2]
                    FACING = Quaternion(AXES["y"], math.atan2(fwd.x, fwd.z))
                q = rotation_of(entries1, w) @ rotation_of(entries0, 1 - w)   # the two keys' rotations, each weighted
                if k == "S1":
                    q = Quaternion(AXES["x"], math.radians(1.6 * breath)) @ q
                drop0, drop1 = p0.get("drop", (0, 0)), p1.get("drop", (0, 0))
                offset = FACING @ Vector((0, -(drop0[0] * (1 - w) + drop1[0] * w), -(drop0[1] * (1 - w) + drop1[1] * w))) * height
                if q == Quaternion() and (k != "H" or offset.length == 0):
                    continue
                m = pb.matrix.copy()
                head = m.translation.copy()
                rot = q.to_matrix().to_4x4()
                m = Matrix.Translation(head) @ rot @ Matrix.Translation(-head) @ m
                if k == "H":
                    m.translation += offset
                pb.matrix = m
                bpy.context.view_layer.update()
            baked.append({k: (pb.rotation_quaternion.copy(), pb.location.copy()) for k, pb in pbs.items()})

        action = bpy.data.actions.new(name)
        arm.animation_data.action = action
        for f, pose in enumerate(baked):
            for k, (q, loc) in pose.items():
                pb = pbs[k]
                pb.rotation_mode = "QUATERNION"
                pb.rotation_quaternion = q
                pb.keyframe_insert("rotation_quaternion", frame=f + 1, group=pb.name)
                if k == "H":
                    pb.location = loc
                    pb.keyframe_insert("location", frame=f + 1, group=pb.name)
        action.use_fake_user = True
        made.append((name, action, frames, keys))
        print(f"CLIP {name}: {frames} frames, loop={loop}")

    if PREVIEW:
        render_previews(arm, made)

    # export: armature only, one take per clip (the idle take left out)
    arm.animation_data.action = None
    idle.use_fake_user = False
    bpy.data.actions.remove(idle)
    for o in list(scene.objects):
        if o.type == "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"ARMATURE"}, add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_simplify_factor=0.0, armature_nodetype="NULL")
    print("EXPORTED", OUT, [m[0] for m in made])


def render_previews(arm, made):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 360, 420
    scene.display.shading.light = "STUDIO"
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    feet = arm.matrix_world @ arm.pose.bones[B["LFt"]].head
    head = arm.matrix_world @ arm.pose.bones[B["Hd"]].tail
    centre = (feet + head) / 2
    size = (head - feet).length * 1.9
    os.makedirs(PREVIEW, exist_ok=True)
    for name, action, frames, keys in made:
        arm.animation_data.action = action
        moments = sorted({min(frames, int(round(t * FPS))) + 1 for t, _ in keys})
        # every key moment
        for m in moments:
            scene.frame_set(m)
            hips = arm.pose.bones[B["H"]]
            fwd = arm.matrix_world.to_3x3() @ hips.matrix.to_3x3().col[2]   # the hips' forward, so the views follow the stance
            fwd.z = 0
            fwd.normalize()
            right = fwd.cross(Vector((0, 0, 1)))
            for view, d in (("front", fwd + Vector((0, 0, 0.1))), ("side", right + Vector((0, 0, 0.1))), ("three", fwd + right + Vector((0, 0, 0.3)))):
                cam.location = centre + d.normalized() * size
                cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
                scene.render.filepath = os.path.join(PREVIEW, f"{name}_{m:03d}_{view}.png")
                bpy.ops.render.render(write_still=True)


main()
