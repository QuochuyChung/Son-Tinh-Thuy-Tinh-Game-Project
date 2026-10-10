import bpy
import math
import sys
from collections import deque
from mathutils import Vector


OUT = sys.argv[sys.argv.index("--") + 1]


def bone(edit_bones, name, head, tail, parent=None, deform=True):
    b = edit_bones.new(name)
    b.head = head
    b.tail = tail
    b.roll = 0.0
    b.use_deform = deform
    if parent:
        b.parent = parent
    return b


def components(mesh):
    adj = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adj[a].add(b)
        adj[b].add(a)
    seen = set()
    result = []
    for start in range(len(mesh.vertices)):
        if start in seen:
            continue
        todo = [start]
        seen.add(start)
        comp = []
        while todo:
            cur = todo.pop()
            comp.append(cur)
            for nxt in adj[cur]:
                if nxt not in seen:
                    seen.add(nxt)
                    todo.append(nxt)
        result.append(comp)
    return result


def add_weight(groups, index, name, value=1.0):
    if value > 0.0001:
        groups[name].add([index], value, "REPLACE")


def nearest_leg(x, y):
    centers = {
        "fore.L": (-0.06, 0.11),
        "fore.R": (-0.14, -0.11),
        "hind.L": (0.25, 0.11),
        "hind.R": (0.32, -0.11),
    }
    return min(centers, key=lambda name: (x - centers[name][0]) ** 2 + (y - centers[name][1]) ** 2)


def reset_pose(rig):
    rig.location = (0.0, 0.0, 0.0)
    rig.rotation_euler = (0.0, 0.0, 0.0)
    rig.scale = (1.0, 1.0, 1.0)
    for pb in rig.pose.bones:
        pb.location = (0.0, 0.0, 0.0)
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)


def new_action(rig, name, description, loop=False):
    reset_pose(rig)
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    action["description"] = description
    action["loop"] = loop
    rig.animation_data_create()
    rig.animation_data.action = action
    return action


def key_object(rig, frame, location=None, rotation=None):
    if location is not None:
        rig.location = location
        rig.keyframe_insert(data_path="location", frame=frame)
    if rotation is not None:
        rig.rotation_euler = rotation
        rig.keyframe_insert(data_path="rotation_euler", frame=frame)


def key_bone(rig, name, frame, rot=None, loc=None):
    pb = rig.pose.bones[name]
    if rot is not None:
        pb.rotation_euler = rot
        pb.keyframe_insert(data_path="rotation_euler", frame=frame, group=name)
    if loc is not None:
        pb.location = loc
        pb.keyframe_insert(data_path="location", frame=frame, group=name)


def walk_action(rig, name, direction):
    desc = "Root-motion walk forward" if direction < 0 else "Root-motion walk backward"
    action = new_action(rig, name, desc, loop=True)
    travel = 0.30 * direction
    frames = [1, 7, 13, 19, 25]
    phase = [1.0, 0.0, -1.0, 0.0, 1.0]
    for frame, ph in zip(frames, phase):
        t = (frame - 1) / 24.0
        bounce = 0.009 if frame in (7, 19) else 0.0
        key_object(rig, frame, (travel * t, 0.0, bounce), (0.0, 0.0, 0.0))
        key_bone(rig, "body", frame, (0.0, 0.0, math.radians(1.8 * ph)))
        key_bone(rig, "chest", frame, (0.0, 0.0, math.radians(-1.2 * ph)))
        key_bone(rig, "head", frame, (0.0, 0.0, math.radians(-2.8 * ph)))
        key_bone(rig, "trunk.01", frame, (0.0, 0.0, math.radians(4.0 * ph)))
        key_bone(rig, "trunk.02", frame, (0.0, 0.0, math.radians(-6.0 * ph)))
        key_bone(rig, "tail", frame, (0.0, math.radians(3.0 * ph), math.radians(-7.0 * ph)))
        for leg, sign in (("fore.L", 1), ("hind.R", 1), ("fore.R", -1), ("hind.L", -1)):
            swing = 14.0 * ph * sign * (-direction)
            lift = max(0.0, ph * sign) * 13.0
            key_bone(rig, leg + ".upper", frame, (0.0, 0.0, math.radians(swing)))
            key_bone(rig, leg + ".lower", frame, (0.0, 0.0, math.radians(-lift)))
            key_bone(rig, leg + ".foot", frame, (0.0, 0.0, math.radians(lift * 0.45)))
    for marker_name, marker_frame in (("Contact_A", 1), ("Passing_A", 7), ("Contact_B", 13), ("Passing_B", 19)):
        action.pose_markers.new(marker_name).frame = marker_frame
    return action


mesh = next((o for o in bpy.context.scene.objects if o.type == "MESH"), None)
if mesh is None:
    raise RuntimeError("No mesh object found in source scene")
mesh.name = "War_Elephant_Mesh"

# Remove any old armature remnants without touching the source mesh.
for obj in list(bpy.context.scene.objects):
    if obj != mesh and obj.type == "ARMATURE":
        bpy.data.objects.remove(obj, do_unlink=True)

arm_data = bpy.data.armatures.new("War_Elephant_Rig")
rig = bpy.data.objects.new("War_Elephant_Rig", arm_data)
bpy.context.collection.objects.link(rig)
rig.show_in_front = True
rig.display_type = "WIRE"
rig["forward_axis"] = "-X"
rig["animation_notes"] = "Walk_Forward and Walk_Backward contain root motion; Attack and Death are one-shot actions."

bpy.context.view_layer.objects.active = rig
rig.select_set(True)
mesh.select_set(False)
bpy.ops.object.mode_set(mode="EDIT")
eb = arm_data.edit_bones
root = bone(eb, "root", (0.0, 0.0, -0.32), (0.0, 0.0, -0.22), deform=False)
body = bone(eb, "body", (0.18, 0.0, -0.01), (-0.04, 0.0, 0.07), root)
pelvis = bone(eb, "pelvis", (0.08, 0.0, 0.00), (0.28, 0.0, 0.01), body)
chest = bone(eb, "chest", (-0.04, 0.0, 0.07), (-0.21, 0.0, 0.09), body)
head = bone(eb, "head", (-0.21, 0.0, 0.09), (-0.34, 0.0, 0.02), chest)
trunk1 = bone(eb, "trunk.01", (-0.34, 0.0, 0.02), (-0.41, 0.0, -0.06), head)
trunk2 = bone(eb, "trunk.02", (-0.41, 0.0, -0.06), (-0.42, 0.0, -0.16), trunk1)
trunk3 = bone(eb, "trunk.03", (-0.42, 0.0, -0.16), (-0.38, 0.0, -0.24), trunk2)
tail = bone(eb, "tail", (0.34, 0.0, -0.01), (0.43, 0.0, -0.19), pelvis)

leg_specs = {
    "fore.L": ((-0.16, 0.11, -0.02), (-0.08, 0.11, -0.17), (-0.01, 0.11, -0.30), (-0.06, 0.11, -0.34), chest),
    "fore.R": ((-0.16, -0.11, -0.02), (-0.15, -0.11, -0.17), (-0.13, -0.11, -0.30), (-0.19, -0.11, -0.34), chest),
    "hind.L": ((0.26, 0.11, -0.02), (0.25, 0.11, -0.17), (0.25, 0.11, -0.30), (0.20, 0.11, -0.34), pelvis),
    "hind.R": ((0.27, -0.11, -0.02), (0.30, -0.11, -0.17), (0.34, -0.11, -0.30), (0.28, -0.11, -0.34), pelvis),
}
for prefix, (hip, knee, ankle, toe, parent) in leg_specs.items():
    upper = bone(eb, prefix + ".upper", hip, knee, parent)
    lower = bone(eb, prefix + ".lower", knee, ankle, upper)
    bone(eb, prefix + ".foot", ankle, toe, lower)

bpy.ops.object.mode_set(mode="OBJECT")

# Parent and skin the original mesh with deterministic, anatomy-aware weights.
for vg in list(mesh.vertex_groups):
    mesh.vertex_groups.remove(vg)
deform_names = [b.name for b in arm_data.bones if b.use_deform]
groups = {name: mesh.vertex_groups.new(name=name) for name in deform_names}

parts = components(mesh.data)
large_part = max(parts, key=len)
large_set = set(large_part)

for comp in parts:
    coords = [mesh.matrix_world @ mesh.data.vertices[i].co for i in comp]
    lo = Vector(tuple(min(v[k] for v in coords) for k in range(3)))
    hi = Vector(tuple(max(v[k] for v in coords) for k in range(3)))
    center = (lo + hi) * 0.5
    if comp is not large_part:
        # Rigid accessories: keep tusks/ears with head, feet with their leg,
        # tail ornaments with tail, and the howdah/armor with the torso.
        if hi.z < -0.26:
            target = nearest_leg(center.x, center.y) + ".foot"
        elif hi.x < -0.18:
            target = "head"
        elif lo.x > 0.34 and abs(center.y) < 0.07 and center.z < 0.02:
            target = "tail"
        elif center.z < -0.17 and abs(center.y) < 0.15:
            leg = nearest_leg(center.x, center.y)
            target = leg + (".lower" if center.z < -0.20 else ".upper")
        else:
            target = "body" if center.x < 0.16 else "pelvis"
        for idx in comp:
            add_weight(groups, idx, target)
        continue

    for idx in comp:
        co = mesh.matrix_world @ mesh.data.vertices[idx].co
        x, y, z = co
        if x > 0.36 and abs(y) < 0.065 and z < 0.06:
            add_weight(groups, idx, "tail")
        elif x < -0.365 and z < 0.075:
            if z > -0.07:
                add_weight(groups, idx, "trunk.01")
            elif z > -0.17:
                add_weight(groups, idx, "trunk.02")
            else:
                add_weight(groups, idx, "trunk.03")
        elif x < -0.245:
            # Gentle head/chest blend around the neck.
            blend = min(1.0, max(0.0, (-x - 0.245) / 0.055))
            add_weight(groups, idx, "head", 0.70 + 0.30 * blend)
            add_weight(groups, idx, "chest", 0.30 * (1.0 - blend))
        elif z < -0.105 and -0.26 < x < 0.40 and abs(y) < 0.16:
            leg = nearest_leg(x, y)
            if z < -0.285:
                add_weight(groups, idx, leg + ".foot")
            elif z < -0.205:
                blend = min(1.0, max(0.0, (-z - 0.205) / 0.08))
                add_weight(groups, idx, leg + ".lower", 1.0 - 0.25 * blend)
                add_weight(groups, idx, leg + ".foot", 0.25 * blend)
            elif z < -0.13:
                blend = min(1.0, max(0.0, (-z - 0.13) / 0.075))
                add_weight(groups, idx, leg + ".upper", 1.0 - 0.45 * blend)
                add_weight(groups, idx, leg + ".lower", 0.45 * blend)
            else:
                add_weight(groups, idx, leg + ".upper", 0.72)
                add_weight(groups, idx, "body" if x < 0.12 else "pelvis", 0.28)
        elif x < -0.06:
            add_weight(groups, idx, "chest", 0.78)
            add_weight(groups, idx, "body", 0.22)
        elif x > 0.18:
            add_weight(groups, idx, "pelvis", 0.75)
            add_weight(groups, idx, "body", 0.25)
        else:
            add_weight(groups, idx, "body")

modifier = mesh.modifiers.new("War Elephant Armature", "ARMATURE")
modifier.object = rig
modifier.use_vertex_groups = True
mesh.parent = rig

# Actions -------------------------------------------------------------------
idle = new_action(rig, "Idle", "Subtle breathing idle", loop=True)
for frame, amount in ((1, 0.0), (16, 1.0), (31, 0.0), (46, -0.65), (61, 0.0)):
    key_object(rig, frame, (0.0, 0.0, 0.003 * max(0.0, amount)), (0.0, 0.0, 0.0))
    key_bone(rig, "body", frame, (0.0, 0.0, math.radians(0.8 * amount)))
    key_bone(rig, "head", frame, (0.0, 0.0, math.radians(-1.2 * amount)))
    key_bone(rig, "trunk.02", frame, (0.0, 0.0, math.radians(2.2 * amount)))
    key_bone(rig, "tail", frame, (0.0, math.radians(2.5 * amount), math.radians(-2.0 * amount)))

walk_action(rig, "Walk_Forward", -1)
walk_action(rig, "Walk_Backward", 1)

attack = new_action(rig, "Attack", "Anticipation, forward lunge, tusk/head strike, recovery", loop=False)
attack_keys = [
    (1, (0.0, 0.0, 0.0), 0.0, 0.0, 0.0),
    (8, (0.045, 0.0, 0.006), 7.0, -8.0, -18.0),
    (15, (-0.14, 0.0, -0.012), -9.0, 19.0, 34.0),
    (22, (-0.10, 0.0, -0.004), -3.0, 8.0, 12.0),
    (34, (0.0, 0.0, 0.0), 0.0, 0.0, 0.0),
]
for frame, loc, body_ang, head_ang, trunk_ang in attack_keys:
    key_object(rig, frame, loc, (0.0, 0.0, 0.0))
    key_bone(rig, "body", frame, (0.0, 0.0, math.radians(body_ang)))
    key_bone(rig, "chest", frame, (0.0, 0.0, math.radians(body_ang * 0.45)))
    key_bone(rig, "head", frame, (0.0, 0.0, math.radians(head_ang)))
    key_bone(rig, "trunk.01", frame, (0.0, 0.0, math.radians(-trunk_ang * 0.35)))
    key_bone(rig, "trunk.02", frame, (0.0, 0.0, math.radians(trunk_ang)))
    key_bone(rig, "trunk.03", frame, (0.0, 0.0, math.radians(-trunk_ang * 0.45)))
    # Brace the legs during the lunge.
    brace = 9.0 if frame == 15 else (4.0 if frame in (8, 22) else 0.0)
    for leg in ("fore.L", "fore.R"):
        key_bone(rig, leg + ".upper", frame, (0.0, 0.0, math.radians(-brace)))
        key_bone(rig, leg + ".lower", frame, (0.0, 0.0, math.radians(brace * 0.7)))

death = new_action(rig, "Death", "Stagger, knees buckle, fall to the elephant's left side, settle", loop=False)
death_keys = [
    (1, (0.0, 0.0, 0.0), (0.0, 0.0, 0.0), 0.0, 0.0),
    (11, (0.025, 0.0, -0.01), (math.radians(7), 0.0, 0.0), -4.0, 7.0),
    (22, (0.035, 0.0, -0.055), (math.radians(22), 0.0, 0.0), -9.0, 15.0),
    (36, (0.02, 0.0, -0.115), (math.radians(64), 0.0, math.radians(2)), -13.0, 24.0),
    (50, (0.015, 0.0, -0.14), (math.radians(88), 0.0, math.radians(2)), -15.0, 31.0),
    (58, (0.015, 0.0, -0.142), (math.radians(91), 0.0, math.radians(1)), -16.0, 33.0),
]
for frame, loc, rot, body_ang, head_ang in death_keys:
    key_object(rig, frame, loc, rot)
    key_bone(rig, "body", frame, (0.0, 0.0, math.radians(body_ang)))
    key_bone(rig, "head", frame, (0.0, 0.0, math.radians(head_ang)))
    key_bone(rig, "trunk.01", frame, (0.0, 0.0, math.radians(12.0 if frame >= 22 else 0.0)))
    key_bone(rig, "trunk.02", frame, (0.0, 0.0, math.radians(28.0 if frame >= 36 else 8.0 if frame >= 22 else 0.0)))
    fold = 0.0 if frame == 1 else min(30.0, frame * 0.6)
    for leg in ("fore.L", "fore.R", "hind.L", "hind.R"):
        key_bone(rig, leg + ".upper", frame, (0.0, 0.0, math.radians(-fold * 0.35)))
        key_bone(rig, leg + ".lower", frame, (0.0, 0.0, math.radians(fold)))
        key_bone(rig, leg + ".foot", frame, (0.0, 0.0, math.radians(-fold * 0.4)))

# Friendly documentation inside the .blend file.
readme = bpy.data.texts.get("README_Animations") or bpy.data.texts.new("README_Animations")
readme.clear()
readme.write(
    "WAR ELEPHANT RIG\n\n"
    "Forward axis: -X\n"
    "Actions:\n"
    "  Idle            frames 1-61, loop\n"
    "  Walk_Forward    frames 1-25, loop, root motion toward -X\n"
    "  Walk_Backward   frames 1-25, loop, root motion toward +X\n"
    "  Attack          frames 1-34, one-shot\n"
    "  Death           frames 1-58, one-shot\n\n"
    "Choose War_Elephant_Rig in the Dope Sheet > Action Editor to switch actions.\n"
    "The imported low-poly mesh is a single object with many disconnected armor pieces;"
    " weights were assigned procedurally so the armor stays stable while the legs, head, trunk and tail move.\n"
)

# Leave a useful default view/action when opened.
rig.animation_data.action = idle
reset_pose(rig)
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 61
bpy.context.scene.frame_set(1)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
mesh.select_set(False)

bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("SAVED", OUT)
print("ACTIONS", [(a.name, tuple(round(x, 2) for x in a.frame_range)) for a in bpy.data.actions])
