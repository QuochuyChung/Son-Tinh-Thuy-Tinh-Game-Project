"""Hand-keyed cutscene gestures for the Mixamo-rigged humanoids (Hùng Vương, Mị Nương, Sơn Tinh v2, Thủy Tinh v2 all share the
mixamorig: skeleton and are Humanoid in Unity, so one set of clips retargets to all four).

Each clip is laid over the Mixamo idle (so the body keeps breathing and shifting its weight) as rotations given in the character's own
frame: x = its left, y = up, z = forward (Mixamo armature space; positive x bends forward, positive y turns to the character's left).
Poses are keyed at a few times and eased with smoothstep in between; everything is baked at 30 fps.

    blender -b --python tools/cutscene_gestures.py -- <idle.fbx> <out.fbx> [preview_dir] [only=Clip1,Clip2]

The output holds the armature and one action (FBX take) per clip, named after the clip. Previews: front + side workbench renders of
each clip's key moments.
"""
import bpy, sys, math, os
from mathutils import Matrix, Quaternion, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
IDLE, OUT = argv[0], argv[1]
PREVIEW = argv[2] if len(argv) > 2 and not argv[2].startswith("only=") else None
ONLY = next((a[5:].split(",") for a in argv if a.startswith("only=")), None)
FPS = 30

B = {k: "mixamorig:" + v for k, v in dict(
    H="Hips", S="Spine", S1="Spine1", S2="Spine2", N="Neck", Hd="Head",
    LS="LeftShoulder", LA="LeftArm", LF="LeftForeArm", LH="LeftHand",
    RS="RightShoulder", RA="RightArm", RF="RightForeArm", RH="RightHand",
    LU="LeftUpLeg", LL="LeftLeg", LFt="LeftFoot", RU="RightUpLeg", RL="RightLeg", RFt="RightFoot").items()}
ORDER = ["H", "S", "S1", "S2", "N", "Hd", "LS", "LA", "LF", "LH", "RS", "RA", "RF", "RH", "LU", "LL", "LFt", "RU", "RL", "RFt"]
AXES = {"x": Vector((1, 0, 0)), "y": Vector((0, 1, 0)), "z": Vector((0, 0, 1))}


def mirror(pose):
    """Left <-> right, turning and rolling reversed."""
    out = {}
    for k, v in pose.items():
        if k == "drop":
            out[k] = v
            continue
        m = k.replace("L", "#").replace("R", "L").replace("#", "R") if k[0] in "LR" else k
        out[m] = [(a, d if a == "x" else -d) for a, d in v]
    return out


# ---------------------------------------------------------------- the clips: (duration s, loop, [(time, pose)])
# pose: bone -> [(axis, degrees), ...] applied in that order; "drop": (down, back) hips offset in metres of the character's height

HANDS_JOINED = {"LA": [("x", -25), ("y", -20)], "RA": [("x", -25), ("y", 20)],
                "LF": [("x", -48), ("y", -60)], "RF": [("x", -48), ("y", 60)]}
ARMS_CROSSED = {"LA": [("x", -20), ("y", -18)], "RA": [("x", -20), ("y", 18)],
                "LF": [("x", -58), ("y", -85)], "RF": [("x", -62), ("y", 80)], "Hd": [("x", -4)]}
SIT = {"drop": (0.40, 0.10), "LU": [("x", -88)], "RU": [("x", -88)], "LL": [("x", 92)], "RL": [("x", 92)],
       "LA": [("x", -35), ("y", -8)], "RA": [("x", -35), ("y", 8)], "LF": [("x", 15)], "RF": [("x", 15)], "S": [("x", 4)]}


def merge(*poses):
    out = {}
    for p in poses:
        for k, v in p.items():
            out[k] = v if k == "drop" else out.get(k, []) + list(v)
    return out


# a gentle upright stance, hands joined low in front (loops): the neutral pose for Mị Nương, whose own idle leans to one side
POISE = {"LA": [("x", -8), ("y", -14), ("z", 4)], "RA": [("x", -8), ("y", 14), ("z", -4)],
         "S": [("x", 7)], "S1": [("x", 8)], "S2": [("x", 8)],
         "LF": [("x", -44), ("y", -50)], "RF": [("x", -44), ("y", 50)], "Hd": [("x", 4)]}
# hands held low in front of the waist, forearms forward with a little turn inwards (a bigger turn pushed the forearms through the
# belly and the front panel of her dress)

# Bones that take the rest (T-pose) values instead of the idle's: the idle is Hùng Vương's own stance (weight on one leg, a slight lean),
# and on a different skeleton that stance comes out as a lean (Mị Nương leaned back). The arms still come from the idle (hanging).
NEUTRAL = ["H", "S", "S1", "S2", "N", "Hd"]

CLIPS = {
    "Poise": (4.0, True, [(0.0, POISE), (2.0, merge(POISE, {"Hd": [("y", 3)]})), (4.0, POISE)]),
    # a deep bow to the king, hands joined before the chest
    "Bow": (2.8, False, [
        (0.0, {}),
        (0.5, HANDS_JOINED),
        (1.0, merge(HANDS_JOINED, {"S": [("x", 14)], "S1": [("x", 14)], "S2": [("x", 10)], "Hd": [("x", 12)]})),
        (1.9, merge(HANDS_JOINED, {"S": [("x", 14)], "S1": [("x", 14)], "S2": [("x", 10)], "Hd": [("x", 12)]})),
        (2.4, HANDS_JOINED),
        (2.8, {})]),
    # arms folded, chin up (loops)
    "CrossArms": (4.0, True, [(0.0, ARMS_CROSSED), (2.0, merge(ARMS_CROSSED, {"Hd": [("y", 6)]})), (4.0, ARMS_CROSSED)]),
    # points ahead with the right hand
    "Point": (2.4, False, [
        (0.0, {}),
        (0.45, {"RA": [("x", -82), ("y", 8)], "RF": [("x", 10)], "S2": [("y", -8)], "Hd": [("y", -5)]}),
        (1.7, {"RA": [("x", -80), ("y", 8)], "RF": [("x", 10)], "S2": [("y", -8)], "Hd": [("y", -5)]}),
        (2.4, {})]),
    # raises the right hand high: an order, an announcement
    "Command": (2.6, False, [
        (0.0, {}),
        (0.55, {"RA": [("x", -150), ("y", 10)], "RF": [("x", -10)], "S2": [("x", -6)], "Hd": [("x", -8)]}),
        (1.9, {"RA": [("x", -148), ("y", 10)], "RF": [("x", -10)], "S2": [("x", -6)], "Hd": [("x", -8)]}),
        (2.6, {})]),
    # turns the head and chest to its left, holds, turns back
    "LookLeft": (2.4, False, [
        (0.0, {}), (0.5, {"S2": [("y", 12)], "N": [("y", 15)], "Hd": [("y", 22)]}),
        (1.8, {"S2": [("y", 12)], "N": [("y", 15)], "Hd": [("y", 22)]}), (2.4, {})]),
    "HeadShake": (1.6, False, [
        (0.0, {}), (0.3, {"Hd": [("y", 20)]}), (0.6, {"Hd": [("y", -20)]}), (0.9, {"Hd": [("y", 16)]}),
        (1.2, {"Hd": [("y", -10)]}), (1.6, {})]),
    # leans in, arms out, fists shaking: anger, defiance
    "Angry": (2.2, False, [
        (0.0, {}),
        (0.35, {"S2": [("x", 10)], "Hd": [("x", -6)], "LA": [("x", -20), ("z", 10)], "RA": [("x", -20), ("z", -10)],
                "LF": [("x", -40)], "RF": [("x", -40)]}),
        (0.6, {"S2": [("x", 13)], "Hd": [("x", -4)], "LA": [("x", -24), ("z", 12)], "RA": [("x", -24), ("z", -12)],
               "LF": [("x", -48)], "RF": [("x", -48)]}),
        (0.85, {"S2": [("x", 10)], "Hd": [("x", -6)], "LA": [("x", -20), ("z", 10)], "RA": [("x", -20), ("z", -10)],
                "LF": [("x", -40)], "RF": [("x", -40)]}),
        (1.6, {"S2": [("x", 10)], "Hd": [("x", -6)], "LA": [("x", -20), ("z", 10)], "RA": [("x", -20), ("z", -10)],
               "LF": [("x", -40)], "RF": [("x", -40)]}),
        (2.2, {})]),
    # Mị Nương: head lowered and turned away, hands joined low
    "Shy": (3.2, False, [
        (0.0, {}),
        (0.7, {"Hd": [("x", 18), ("y", -22)], "N": [("x", 8)], "S2": [("y", -8)], "LA": [("x", -12), ("y", -15)],
               "RA": [("x", -12), ("y", 15)], "LF": [("x", -45), ("y", -40)], "RF": [("x", -45), ("y", 40)]}),
        (2.5, {"Hd": [("x", 20), ("y", -25)], "N": [("x", 8)], "S2": [("y", -8)], "LA": [("x", -12), ("y", -15)],
               "RA": [("x", -12), ("y", 15)], "LF": [("x", -45), ("y", -40)], "RF": [("x", -45), ("y", 40)]}),
        (3.2, {})]),
    # speaks with the hands, one then the other (loops)
    "Talk": (4.0, True, [
        (0.0, {}),
        (0.7, {"RA": [("x", -25), ("y", 10)], "RF": [("x", -55), ("y", 20)], "Hd": [("x", 4)]}),
        (1.5, {"RA": [("x", -30), ("y", 14)], "RF": [("x", -60), ("y", 30)], "Hd": [("x", -2)]}),
        (2.0, {}),
        (2.7, {"LA": [("x", -25), ("y", -10)], "LF": [("x", -55), ("y", -20)], "Hd": [("x", 3), ("y", 4)]}),
        (3.4, {"LA": [("x", -28), ("y", -14)], "LF": [("x", -58), ("y", -30)]}),
        (4.0, {})]),
    # lifts the weapon hand to the sky
    "RaiseSword": (2.6, False, [
        (0.0, {}),
        (0.6, {"RA": [("x", -165), ("y", 5)], "RF": [("x", 5)], "S2": [("x", -8)], "Hd": [("x", -12)], "LA": [("z", 12)]}),
        (2.0, {"RA": [("x", -168), ("y", 5)], "RF": [("x", 5)], "S2": [("x", -8)], "Hd": [("x", -12)], "LA": [("z", 12)]}),
        (2.6, {})]),
    # "come on": the left hand held out, beckoning twice
    "Beckon": (2.4, False, [
        (0.0, {}),
        (0.4, {"LA": [("x", -60), ("y", -10)], "LF": [("x", -20)], "S2": [("x", -5)]}),
        (0.75, {"LA": [("x", -60), ("y", -10)], "LF": [("x", -55)], "S2": [("x", -5)]}),
        (1.1, {"LA": [("x", -60), ("y", -10)], "LF": [("x", -20)], "S2": [("x", -5)]}),
        (1.45, {"LA": [("x", -60), ("y", -10)], "LF": [("x", -55)], "S2": [("x", -5)]}),
        (1.8, {"LA": [("x", -60), ("y", -10)], "LF": [("x", -20)], "S2": [("x", -5)]}),
        (2.4, {})]),
    # seated (a throne): hands on the knees (loops); SitTalk adds a gesture of the right hand
    "SitIdle": (4.0, True, [(0.0, SIT), (2.0, merge(SIT, {"Hd": [("y", 5)]})), (4.0, SIT)]),
    "SitTalk": (4.0, True, [
        (0.0, SIT),
        (0.8, merge(SIT, {"RA": [("x", -15), ("y", 10)], "RF": [("x", -45), ("y", 25)], "Hd": [("x", 4)]})),
        (2.2, merge(SIT, {"RA": [("x", -20), ("y", 15)], "RF": [("x", -50), ("y", 35)], "Hd": [("x", -3)]})),
        (3.0, SIT), (4.0, SIT)]),
    "StandUp": (1.6, False, [(0.0, SIT), (0.6, merge(SIT, {"S": [("x", 22)], "drop": (0.30, 0.04)})), (1.6, {})]),
}
CLIPS["LookRight"] = (2.4, False, [(t, mirror(p)) for t, p in CLIPS["LookLeft"][2]])


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


def rotation_of(entries, scale):
    q = Quaternion()
    for axis, deg in entries:
        q = Quaternion(AXES[axis], math.radians(deg * scale)) @ q
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
            scene.frame_set(idle_start + (f % (idle_end - idle_start)))
            for nk in NEUTRAL:
                if nk in pbs:
                    pbs[nk].rotation_mode = "QUATERNION"
                    pbs[nk].rotation_quaternion = Quaternion()
            bpy.context.view_layer.update()
            p0, p1, w = pose_at(keys, t)
            breath = math.sin(2 * math.pi * t / 3.6)
            for k in ORDER:
                if k not in pbs:
                    continue
                pb = pbs[k]
                entries0, entries1 = p0.get(k, []), p1.get(k, [])
                q = rotation_of(entries1, w) @ rotation_of(entries0, 1 - w)   # the two keys' rotations, each weighted
                if k == "S1":
                    q = Quaternion(AXES["x"], math.radians(1.6 * breath)) @ q
                drop0, drop1 = p0.get("drop", (0, 0)), p1.get("drop", (0, 0))
                offset = Vector((0, -(drop0[0] * (1 - w) + drop1[0] * w), -(drop0[1] * (1 - w) + drop1[1] * w))) * height
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
        moments = [m for i, m in enumerate(moments) if i in (0, len(moments) // 2, len(moments) - 2)] or moments
        for m in moments:
            scene.frame_set(m)
            for view, d in (("front", Vector((0, -1, 0.1))), ("side", Vector((1, 0, 0.1)))):
                cam.location = centre + d.normalized() * size
                cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
                scene.render.filepath = os.path.join(PREVIEW, f"{name}_{m:03d}_{view}.png")
                bpy.ops.render.render(write_still=True)


main()
