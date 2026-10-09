import bpy
import math
import sys
from pathlib import Path
from mathutils import Vector


args = sys.argv[sys.argv.index("--") + 1:]
SOURCE = str(Path(args[0]).resolve())
BLEND_OUT = str(Path(args[1]).resolve())
FBX_OUT = str(Path(args[2]).resolve())


def add_bone(edit_bones, name, head, tail, parent=None, deform=True):
    bone = edit_bones.new(name)
    bone.head = head
    bone.tail = tail
    bone.roll = 0.0
    bone.use_deform = deform
    if parent:
        bone.parent = parent
    return bone


def reset_pose(rig):
    rig.location = (0.0, 0.0, 0.0)
    rig.rotation_mode = "XYZ"
    rig.rotation_euler = (0.0, 0.0, 0.0)
    rig.scale = (1.0, 1.0, 1.0)
    for pose_bone in rig.pose.bones:
        pose_bone.location = (0.0, 0.0, 0.0)
        pose_bone.rotation_mode = "XYZ"
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        pose_bone.scale = (1.0, 1.0, 1.0)


def new_action(rig, name, description, loop, frame_end):
    reset_pose(rig)
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    action["description"] = description
    action["loop"] = loop
    action["frame_start"] = 1
    action["frame_end"] = frame_end
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


def key_bone(rig, name, frame, rotation=None, location=None):
    pose_bone = rig.pose.bones[name]
    if rotation is not None:
        pose_bone.rotation_euler = rotation
        pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=name)
    if location is not None:
        pose_bone.location = location
        pose_bone.keyframe_insert(data_path="location", frame=frame, group=name)


def add_weight(groups, vertex_index, bone_name, weight=1.0):
    if weight > 0.0001:
        groups[bone_name].add([vertex_index], weight, "REPLACE")


def add_marker(action, name, frame):
    marker = action.pose_markers.new(name)
    marker.frame = frame


bpy.ops.wm.read_factory_settings(use_empty=True)
if hasattr(bpy.ops.wm, "fbx_import"):
    bpy.ops.wm.fbx_import(filepath=SOURCE)
else:
    bpy.ops.import_scene.fbx(filepath=SOURCE)

mesh = next((obj for obj in bpy.context.scene.objects if obj.type == "MESH"), None)
if mesh is None:
    raise RuntimeError("Source FBX does not contain a mesh")
mesh.name = "Dragon_Turtle_Mesh"
mesh.data.name = "Dragon_Turtle_Geometry"

# Remove any imported armature remnants, while preserving the Meshy mesh/materials.
for obj in list(bpy.context.scene.objects):
    if obj != mesh and obj.type == "ARMATURE":
        bpy.data.objects.remove(obj, do_unlink=True)

armature_data = bpy.data.armatures.new("Dragon_Turtle_Rig")
rig = bpy.data.objects.new("Dragon_Turtle_Rig", armature_data)
bpy.context.collection.objects.link(rig)
rig.show_in_front = True
rig.display_type = "WIRE"
rig["forward_axis"] = "-Y"
rig["unit_scale"] = "meters"
rig["animation_clips"] = "Move 1-25 loop/root motion; Attack 1-36; Defeated 1-58"

bpy.context.view_layer.objects.active = rig
rig.select_set(True)
mesh.select_set(False)
bpy.ops.object.mode_set(mode="EDIT")
eb = armature_data.edit_bones

root = add_bone(eb, "root", (0.0, 0.0, -0.095), (0.0, 0.0, -0.035), deform=False)
body = add_bone(eb, "body", (0.0, 0.03, -0.015), (0.0, 0.03, 0.055), root)
chest = add_bone(eb, "chest", (0.0, 0.01, 0.0), (0.0, -0.19, 0.005), body)
pelvis = add_bone(eb, "pelvis", (0.0, 0.04, 0.0), (0.0, 0.23, -0.01), body)
neck = add_bone(eb, "neck", (0.0, -0.18, 0.0), (0.0, -0.29, -0.005), chest)
head = add_bone(eb, "head", (0.0, -0.28, -0.005), (0.0, -0.405, -0.02), neck)
jaw = add_bone(eb, "jaw", (0.0, -0.325, -0.035), (0.0, -0.445, -0.055), head)

tail_01 = add_bone(eb, "tail.01", (0.0, 0.20, -0.015), (0.0, 0.33, -0.02), pelvis)
tail_02 = add_bone(eb, "tail.02", (0.0, 0.33, -0.02), (0.0, 0.42, -0.03), tail_01)
add_bone(eb, "tail.03", (0.0, 0.42, -0.03), (0.0, 0.48, -0.04), tail_02)

leg_specs = {
    "fore.L": ((-0.085, -0.16, -0.005), (-0.135, -0.17, -0.045), (-0.16, -0.19, -0.082), (-0.185, -0.225, -0.095), chest),
    "fore.R": ((0.085, -0.16, -0.005), (0.135, -0.17, -0.045), (0.16, -0.19, -0.082), (0.185, -0.225, -0.095), chest),
    "hind.L": ((-0.085, 0.16, -0.005), (-0.135, 0.17, -0.045), (-0.16, 0.19, -0.082), (-0.185, 0.225, -0.095), pelvis),
    "hind.R": ((0.085, 0.16, -0.005), (0.135, 0.17, -0.045), (0.16, 0.19, -0.082), (0.185, 0.225, -0.095), pelvis),
}
for prefix, (hip, knee, ankle, toe, parent) in leg_specs.items():
    upper = add_bone(eb, prefix + ".upper", hip, knee, parent)
    lower = add_bone(eb, prefix + ".lower", knee, ankle, upper)
    add_bone(eb, prefix + ".foot", ankle, toe, lower)

bpy.ops.object.mode_set(mode="OBJECT")

# Deterministic weights based on the known silhouette of this specific Meshy model.
for group in list(mesh.vertex_groups):
    mesh.vertex_groups.remove(group)
deform_names = [bone.name for bone in armature_data.bones if bone.use_deform]
groups = {name: mesh.vertex_groups.new(name=name) for name in deform_names}

for vertex in mesh.data.vertices:
    world = mesh.matrix_world @ vertex.co
    x, y, z = world
    ax = abs(x)

    if y < -0.285:
        # Dragon head and articulated lower jaw.
        if z < -0.035 and y < -0.335:
            add_weight(groups, vertex.index, "jaw", 0.82)
            add_weight(groups, vertex.index, "head", 0.18)
        else:
            add_weight(groups, vertex.index, "head", 0.88)
            add_weight(groups, vertex.index, "neck", 0.12)
    elif y < -0.205:
        blend = min(1.0, max(0.0, (-y - 0.205) / 0.08))
        add_weight(groups, vertex.index, "neck", 0.55 + 0.35 * blend)
        add_weight(groups, vertex.index, "chest", 0.45 - 0.35 * blend)
    elif y > 0.285 and ax < 0.105:
        if y < 0.355:
            add_weight(groups, vertex.index, "tail.01")
        elif y < 0.435:
            add_weight(groups, vertex.index, "tail.02")
        else:
            add_weight(groups, vertex.index, "tail.03")
    elif ax > 0.085 and z < -0.025:
        region = "fore" if y < 0.0 else "hind"
        side = "L" if x < 0.0 else "R"
        prefix = region + "." + side
        if ax < 0.132:
            add_weight(groups, vertex.index, prefix + ".upper", 0.84)
            add_weight(groups, vertex.index, "chest" if region == "fore" else "pelvis", 0.16)
        elif ax < 0.162:
            add_weight(groups, vertex.index, prefix + ".lower", 0.86)
            add_weight(groups, vertex.index, prefix + ".upper", 0.14)
        else:
            add_weight(groups, vertex.index, prefix + ".foot", 0.90)
            add_weight(groups, vertex.index, prefix + ".lower", 0.10)
    elif y < -0.045:
        add_weight(groups, vertex.index, "chest", 0.82)
        add_weight(groups, vertex.index, "body", 0.18)
    elif y > 0.10:
        add_weight(groups, vertex.index, "pelvis", 0.82)
        add_weight(groups, vertex.index, "body", 0.18)
    else:
        add_weight(groups, vertex.index, "body")

modifier = mesh.modifiers.new("Dragon Turtle Armature", "ARMATURE")
modifier.object = rig
modifier.use_vertex_groups = True
mesh.parent = rig

# MOVE: one-second root-motion locomotion cycle at 24 fps.
move = new_action(
    rig,
    "Move",
    "Looping forward locomotion with alternating feet, body bob, head counter-motion and root motion toward -Y.",
    True,
    25,
)
frames = [1, 7, 13, 19, 25]
phases = [1.0, 0.0, -1.0, 0.0, 1.0]
for frame, phase in zip(frames, phases):
    t = (frame - 1) / 24.0
    bob = 0.005 if frame in (7, 19) else 0.0
    key_object(rig, frame, (0.0, -0.28 * t, bob), (0.0, 0.0, 0.0))
    key_bone(rig, "body", frame, (math.radians(1.8 * phase), 0.0, math.radians(0.8 * phase)))
    key_bone(rig, "chest", frame, (math.radians(-1.3 * phase), 0.0, 0.0))
    key_bone(rig, "head", frame, (math.radians(3.5 * phase), 0.0, math.radians(-1.3 * phase)))
    key_bone(rig, "jaw", frame, (math.radians(-2.0 if frame in (7, 19) else 0.0), 0.0, 0.0))
    key_bone(rig, "tail.01", frame, (0.0, 0.0, math.radians(-7.0 * phase)))
    key_bone(rig, "tail.02", frame, (0.0, 0.0, math.radians(10.0 * phase)))
    key_bone(rig, "tail.03", frame, (0.0, 0.0, math.radians(13.0 * phase)))
    for prefix, sign in (("fore.L", 1.0), ("hind.R", 1.0), ("fore.R", -1.0), ("hind.L", -1.0)):
        swing = 18.0 * phase * sign
        lift = max(0.0, phase * sign) * 16.0
        key_bone(rig, prefix + ".upper", frame, (math.radians(swing), 0.0, 0.0))
        key_bone(rig, prefix + ".lower", frame, (math.radians(-lift), 0.0, 0.0))
        key_bone(rig, prefix + ".foot", frame, (math.radians(lift * 0.55), 0.0, 0.0))
for name, frame in (("Contact_A", 1), ("Passing_A", 7), ("Contact_B", 13), ("Passing_B", 19)):
    add_marker(move, name, frame)

# ATTACK: crouch, roar/open jaw, lunge/head strike, then recover.
attack = new_action(
    rig,
    "Attack",
    "One-shot dragon bite/head strike: anticipation, jaw opening, forward lunge, impact and recovery.",
    False,
    36,
)
attack_keys = [
    (1, (0.0, 0.0, 0.0), 0.0, 0.0, 0.0, 0.0),
    (8, (0.0, 0.035, -0.004), 7.0, -12.0, -30.0, -5.0),
    (14, (0.0, 0.018, 0.002), 2.0, -18.0, -38.0, -7.0),
    (19, (0.0, -0.105, -0.012), -9.0, 22.0, 5.0, 11.0),
    (24, (0.0, -0.082, -0.006), -4.0, 10.0, -4.0, 5.0),
    (36, (0.0, 0.0, 0.0), 0.0, 0.0, 0.0, 0.0),
]
for frame, location, body_pitch, head_pitch, jaw_open, tail_swing in attack_keys:
    key_object(rig, frame, location, (0.0, 0.0, 0.0))
    key_bone(rig, "body", frame, (math.radians(body_pitch), 0.0, 0.0))
    key_bone(rig, "chest", frame, (math.radians(body_pitch * 0.55), 0.0, 0.0))
    key_bone(rig, "neck", frame, (math.radians(head_pitch * 0.45), 0.0, 0.0))
    key_bone(rig, "head", frame, (math.radians(head_pitch), 0.0, 0.0))
    key_bone(rig, "jaw", frame, (math.radians(jaw_open), 0.0, 0.0))
    key_bone(rig, "tail.01", frame, (0.0, 0.0, math.radians(tail_swing)))
    brace = 12.0 if frame == 19 else 6.0 if frame in (8, 14, 24) else 0.0
    for prefix in ("fore.L", "fore.R"):
        key_bone(rig, prefix + ".upper", frame, (math.radians(-brace), 0.0, 0.0))
        key_bone(rig, prefix + ".lower", frame, (math.radians(brace * 0.75), 0.0, 0.0))
add_marker(attack, "Anticipation", 8)
add_marker(attack, "Jaw_Open", 14)
add_marker(attack, "Impact", 19)
add_marker(attack, "Recover", 24)

# DEFEATED: stagger, legs buckle, roll onto the creature's left side, settle.
defeated = new_action(
    rig,
    "Defeated",
    "One-shot defeat: stagger, collapse, side fall and final settled pose.",
    False,
    58,
)
defeat_keys = [
    (1, (0.0, 0.0, 0.0), (0.0, 0.0, 0.0), 0.0, 0.0),
    (10, (-0.008, 0.012, -0.004), (math.radians(-3), math.radians(-7), 0.0), 4.0, -5.0),
    (20, (-0.018, 0.018, -0.025), (math.radians(2), math.radians(-22), 0.0), 9.0, -9.0),
    (34, (-0.045, 0.022, -0.060), (math.radians(3), math.radians(-62), 0.0), 14.0, -14.0),
    (48, (-0.068, 0.025, -0.085), (math.radians(4), math.radians(-88), math.radians(2)), 18.0, -18.0),
    (58, (-0.071, 0.025, -0.088), (math.radians(4), math.radians(-91), math.radians(2)), 19.0, -20.0),
]
for frame, location, rotation, head_drop, jaw_open in defeat_keys:
    key_object(rig, frame, location, rotation)
    key_bone(rig, "neck", frame, (math.radians(head_drop * 0.45), 0.0, 0.0))
    key_bone(rig, "head", frame, (math.radians(head_drop), 0.0, 0.0))
    key_bone(rig, "jaw", frame, (math.radians(jaw_open), 0.0, 0.0))
    key_bone(rig, "tail.01", frame, (0.0, 0.0, math.radians(min(22.0, frame * 0.4))))
    key_bone(rig, "tail.02", frame, (0.0, 0.0, math.radians(-min(28.0, frame * 0.5))))
    fold = 0.0 if frame == 1 else min(34.0, frame * 0.68)
    for prefix in ("fore.L", "fore.R", "hind.L", "hind.R"):
        key_bone(rig, prefix + ".upper", frame, (math.radians(-fold * 0.45), 0.0, 0.0))
        key_bone(rig, prefix + ".lower", frame, (math.radians(fold), 0.0, 0.0))
        key_bone(rig, prefix + ".foot", frame, (math.radians(-fold * 0.35), 0.0, 0.0))
add_marker(defeated, "Stagger", 10)
add_marker(defeated, "Knees_Buckle", 20)
add_marker(defeated, "Ground_Impact", 48)
add_marker(defeated, "Settled", 58)

# Friendly in-file documentation.
readme = bpy.data.texts.new("README_Animations")
readme.write(
    "DRAGON TURTLE - RIGGED ANIMATION SOURCE\n\n"
    "Forward axis: -Y | Up axis: +Z | Units: meters | FPS: 24\n\n"
    "ACTIONS\n"
    "Move      frames 1-25  loop, root motion toward -Y\n"
    "Attack    frames 1-36  one-shot, impact at frame 19\n"
    "Defeated  frames 1-58  one-shot, settled from frame 58\n\n"
    "Select Dragon_Turtle_Rig and use Dope Sheet > Action Editor to switch clips.\n"
    "The source Meshy FBX was a single unrigged mesh. This file adds a custom turtle/dragon armature and deterministic weights.\n"
)

scene = bpy.context.scene
scene.render.fps = 24
scene.frame_start = 1
scene.frame_end = 25
rig.animation_data.action = move
reset_pose(rig)
scene.frame_set(1)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
mesh.select_set(False)

Path(BLEND_OUT).parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)

# Export one FBX containing the armature, skinned mesh and all three actions.
bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
export_kwargs = dict(
    filepath=FBX_OUT,
    use_selection=True,
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0.0,
    object_types={"ARMATURE", "MESH"},
    axis_forward="-Z",
    axis_up="Y",
)
try:
    bpy.ops.wm.fbx_export(**export_kwargs)
except (AttributeError, RuntimeError):
    bpy.ops.export_scene.fbx(**export_kwargs)

print("SAVED_BLEND", BLEND_OUT)
print("SAVED_FBX", FBX_OUT)
print("ACTIONS", [(action.name, tuple(round(v, 2) for v in action.frame_range)) for action in bpy.data.actions])
