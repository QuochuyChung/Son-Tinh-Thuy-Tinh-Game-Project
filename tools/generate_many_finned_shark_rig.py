import bpy
import math
import os
import sys
from mathutils import Vector


def args_after_double_dash():
    argv = sys.argv
    return argv[argv.index("--") + 1 :] if "--" in argv else []


def ensure_dir(path):
    os.makedirs(path, exist_ok=True)


def look_at(obj, point):
    direction = Vector(point) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


args = args_after_double_dash()
if len(args) != 3:
    raise SystemExit("Expected: input.fbx output.blend output.fbx")

input_fbx, output_blend, output_fbx = map(os.path.abspath, args)
ensure_dir(os.path.dirname(output_blend))
ensure_dir(os.path.dirname(output_fbx))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=input_fbx)

meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if not meshes:
    raise RuntimeError("No mesh was imported from the FBX")

# Merge imported mesh pieces while preserving their transforms.
bpy.ops.object.select_all(action="DESELECT")
for obj in meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
mesh = bpy.context.view_layer.objects.active
mesh.name = "ManyFinnedShark_Mesh"
mesh.data.name = "ManyFinnedShark_Geo"
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

# The Meshy colour maps depend on the authored UV layout. Refuse to produce a
# rigged FBX if a wrong/untextured source file is supplied.
if not mesh.data.uv_layers or len(mesh.data.uv_layers.active.data) == 0:
    raise RuntimeError("Source shark has no UV map; use ArtSource/Meshy/CaMap9Vay/CaMap9Vay.fbx")

# Smooth shading keeps the low-poly silhouette but removes faceted lighting noise.
for poly in mesh.data.polygons:
    poly.use_smooth = True

base_color_candidates = [
    os.path.join(os.path.dirname(input_fbx), name)
    for name in os.listdir(os.path.dirname(input_fbx))
    if "basecolor" in name.lower() and name.lower().endswith((".png", ".jpg", ".jpeg"))
]
if not base_color_candidates:
    raise RuntimeError("Meshy base-colour texture was not found beside the source FBX")

material = bpy.data.materials.new("M_CaMap9Vay")
material.diffuse_color = (1.0, 1.0, 1.0, 1.0)
material.metallic = 0.0
material.roughness = 0.72
material.use_nodes = True
base_color_image = bpy.data.images.load(sorted(base_color_candidates)[0], check_existing=True)
base_color_node = material.node_tree.nodes.new("ShaderNodeTexImage")
base_color_node.name = "CaMap9Vay_BaseColor"
base_color_node.image = base_color_image
principled = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
material.node_tree.links.new(base_color_node.outputs["Color"], principled.inputs["Base Color"])
mesh.data.materials.clear()
mesh.data.materials.append(material)

# The source shark points toward -Y and its tail extends toward +Y.
# A non-deforming root keeps locomotion separate from the deforming body chain.
arm_data = bpy.data.armatures.new("ManyFinnedShark_Rig")
armature = bpy.data.objects.new("ManyFinnedShark_Rig", arm_data)
bpy.context.collection.objects.link(armature)
armature.show_in_front = True
armature.display_type = "WIRE"

bpy.context.view_layer.objects.active = armature
armature.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")


def add_bone(name, head, tail, parent=None, connected=False, deform=True):
    bone = arm_data.edit_bones.new(name)
    bone.head = head
    bone.tail = tail
    bone.roll = 0.0
    bone.use_deform = deform
    if parent:
        bone.parent = arm_data.edit_bones[parent]
        bone.use_connect = connected
    return bone


add_bone("Root", (0, -0.18, 0), (0, -0.08, 0), deform=False)
add_bone("Body", (0, -0.46, 0), (0, -0.16, 0), parent="Root")
add_bone("Spine_01", (0, -0.16, 0), (0, 0.02, 0), parent="Body", connected=True)
add_bone("Spine_02", (0, 0.02, 0), (0, 0.17, 0), parent="Spine_01", connected=True)
add_bone("Tail_01", (0, 0.17, 0), (0, 0.29, 0), parent="Spine_02", connected=True)
add_bone("Tail_02", (0, 0.29, 0), (0, 0.39, 0), parent="Tail_01", connected=True)
add_bone("Tail_03", (0, 0.39, 0), (0, 0.50, 0), parent="Tail_02", connected=True)
add_bone("Jaw", (0, -0.29, -0.035), (0, -0.48, -0.04), parent="Body")

bpy.ops.object.mode_set(mode="OBJECT")

# Smooth, deterministic weights along the body axis. The lower snout is assigned
# to the jaw so the attack clip can open the mouth even though the source is one mesh.
deform_groups = ["Body", "Spine_01", "Spine_02", "Tail_01", "Tail_02", "Tail_03", "Jaw"]
groups = {name: mesh.vertex_groups.new(name=name) for name in deform_groups}
centers = [
    ("Body", -0.31),
    ("Spine_01", -0.07),
    ("Spine_02", 0.095),
    ("Tail_01", 0.23),
    ("Tail_02", 0.34),
    ("Tail_03", 0.445),
]

for vertex in mesh.data.vertices:
    y, z = vertex.co.y, vertex.co.z
    jaw_weight = 0.0
    if y < -0.30 and z < -0.035:
        y_factor = min(1.0, max(0.0, (-0.30 - y) / 0.16))
        z_factor = min(1.0, max(0.0, (-0.035 - z) / 0.075))
        jaw_weight = min(0.92, 0.35 + 0.57 * max(y_factor, z_factor))

    if y <= centers[0][1]:
        body_weights = {centers[0][0]: 1.0}
    elif y >= centers[-1][1]:
        body_weights = {centers[-1][0]: 1.0}
    else:
        body_weights = {}
        for (left_name, left_y), (right_name, right_y) in zip(centers, centers[1:]):
            if left_y <= y <= right_y:
                t = (y - left_y) / (right_y - left_y)
                # Smoothstep avoids visible pinches at the segment boundaries.
                t = t * t * (3.0 - 2.0 * t)
                body_weights[left_name] = 1.0 - t
                body_weights[right_name] = t
                break

    body_scale = 1.0 - jaw_weight
    for group_name, weight in body_weights.items():
        if weight * body_scale > 0.0001:
            groups[group_name].add([vertex.index], weight * body_scale, "REPLACE")
    if jaw_weight > 0.0001:
        groups["Jaw"].add([vertex.index], jaw_weight, "REPLACE")

modifier = mesh.modifiers.new("ManyFinnedShark_Armature", "ARMATURE")
modifier.object = armature
modifier.use_deform_preserve_volume = True
mesh.parent = armature
mesh.matrix_parent_inverse = armature.matrix_world.inverted()

scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = 50
scene.render.fps = 24
scene.render.fps_base = 1.0

animated_bones = ["Root", "Body", "Spine_01", "Spine_02", "Tail_01", "Tail_02", "Tail_03", "Jaw"]
for pose_bone in armature.pose.bones:
    pose_bone.rotation_mode = "XYZ"


def reset_pose():
    for name in animated_bones:
        pb = armature.pose.bones[name]
        pb.location = (0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)


def key_pose(frame, rotations=None, locations=None):
    rotations = rotations or {}
    locations = locations or {}
    reset_pose()
    for name, value in rotations.items():
        armature.pose.bones[name].rotation_euler = value
    for name, value in locations.items():
        armature.pose.bones[name].location = value
    for name in animated_bones:
        pb = armature.pose.bones[name]
        pb.keyframe_insert("location", frame=frame, group=name)
        pb.keyframe_insert("rotation_euler", frame=frame, group=name)
        pb.keyframe_insert("scale", frame=frame, group=name)


def finish_action(action, loop=False):
    # Blender 5.x stores curves in layered Action channel bags. The action-level
    # cyclic flag is understood by the Dope Sheet and survives FBX baking.
    action.use_cyclic = loop
    action.use_fake_user = True


armature.animation_data_create()

# Swim: a traveling S-curve with larger motion toward the caudal fin.
swim = bpy.data.actions.new("Shark_Swim")
armature.animation_data.action = swim
swim_frames = [1, 7, 13, 19, 25, 31, 37, 43, 49]
for frame in swim_frames:
    phase = 2.0 * math.pi * (frame - 1) / 48.0
    key_pose(
        frame,
        rotations={
            "Body": (0, 0, math.radians(1.8) * math.sin(phase)),
            "Spine_01": (0, 0, math.radians(4.0) * math.sin(phase - 0.35)),
            "Spine_02": (0, 0, math.radians(8.0) * math.sin(phase - 0.75)),
            "Tail_01": (0, 0, math.radians(15.0) * math.sin(phase - 1.10)),
            "Tail_02": (0, 0, math.radians(24.0) * math.sin(phase - 1.45)),
            "Tail_03": (0, 0, math.radians(34.0) * math.sin(phase - 1.80)),
        },
        locations={"Root": (0.012 * math.sin(phase), 0, 0.008 * math.sin(phase * 2.0))},
    )
finish_action(swim, loop=True)

# Attack: recoil, open jaw, forward burst, bite, and settle.
attack = bpy.data.actions.new("Shark_Attack")
armature.animation_data.action = attack
key_pose(1)
key_pose(
    7,
    rotations={
        "Body": (0, 0, math.radians(-5)),
        "Spine_01": (0, 0, math.radians(8)),
        "Tail_01": (0, 0, math.radians(15)),
        "Tail_02": (0, 0, math.radians(22)),
        "Tail_03": (0, 0, math.radians(28)),
        "Jaw": (math.radians(-10), 0, 0),
    },
    locations={"Root": (0, 0.035, 0)},
)
key_pose(
    13,
    rotations={
        "Body": (0, 0, math.radians(4)),
        "Spine_01": (0, 0, math.radians(-7)),
        "Tail_01": (0, 0, math.radians(-18)),
        "Tail_02": (0, 0, math.radians(-28)),
        "Tail_03": (0, 0, math.radians(-34)),
        "Jaw": (math.radians(-28), 0, 0),
    },
    locations={"Root": (0, -0.18, 0.012)},
)
key_pose(
    18,
    rotations={"Body": (0, 0, math.radians(2)), "Tail_02": (0, 0, math.radians(12))},
    locations={"Root": (0, -0.22, 0)},
)
key_pose(28, locations={"Root": (0, -0.04, 0)})
key_pose(36)
finish_action(attack)

# Defeat: impact, loss of balance, roll onto the side, and sink.
defeat = bpy.data.actions.new("Shark_Defeated")
armature.animation_data.action = defeat
key_pose(1)
key_pose(
    9,
    rotations={
        "Body": (math.radians(10), math.radians(-4), math.radians(-8)),
        "Spine_01": (0, 0, math.radians(8)),
        "Tail_01": (0, 0, math.radians(18)),
        "Tail_02": (0, 0, math.radians(25)),
        "Tail_03": (0, 0, math.radians(30)),
    },
    locations={"Root": (0, 0.015, 0.03)},
)
key_pose(
    22,
    rotations={
        "Root": (0, math.radians(58), math.radians(-4)),
        "Body": (math.radians(8), 0, math.radians(-4)),
        "Spine_01": (0, 0, math.radians(10)),
        "Spine_02": (0, 0, math.radians(11)),
        "Tail_01": (0, 0, math.radians(14)),
        "Tail_02": (0, 0, math.radians(20)),
        "Tail_03": (0, 0, math.radians(22)),
        "Jaw": (math.radians(-9), 0, 0),
    },
    locations={"Root": (0.02, 0.02, -0.08)},
)
key_pose(
    38,
    rotations={
        "Root": (0, math.radians(92), math.radians(-8)),
        "Body": (math.radians(5), 0, 0),
        "Spine_01": (0, 0, math.radians(8)),
        "Spine_02": (0, 0, math.radians(10)),
        "Tail_01": (0, 0, math.radians(13)),
        "Tail_02": (0, 0, math.radians(15)),
        "Tail_03": (0, 0, math.radians(17)),
        "Jaw": (math.radians(-13), 0, 0),
    },
    locations={"Root": (0.035, 0.04, -0.24)},
)
key_pose(
    50,
    rotations={
        "Root": (0, math.radians(102), math.radians(-10)),
        "Spine_01": (0, 0, math.radians(7)),
        "Spine_02": (0, 0, math.radians(9)),
        "Tail_01": (0, 0, math.radians(11)),
        "Tail_02": (0, 0, math.radians(13)),
        "Tail_03": (0, 0, math.radians(14)),
        "Jaw": (math.radians(-14), 0, 0),
    },
    locations={"Root": (0.045, 0.05, -0.36)},
)
finish_action(defeat)

armature["clip_notes"] = "Shark_Swim 1-49 loop; Shark_Attack 1-36; Shark_Defeated 1-50"
armature["forward_axis"] = "-Y"
armature["rig_version"] = "1.0"
armature["source_uv_map"] = mesh.data.uv_layers.active.name

# Leave Swim active in the authoring file.
armature.animation_data.action = swim
scene.frame_start = 1
scene.frame_end = 49
scene.frame_set(1)

# Save the editable source before adding temporary preview lights/camera.
bpy.ops.wm.save_as_mainfile(filepath=output_blend)

# Export only the production mesh and armature. Each action becomes a Unity clip.
bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True)
armature.select_set(True)
bpy.context.view_layer.objects.active = armature
bpy.ops.export_scene.fbx(
    filepath=output_fbx,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    apply_scale_options="FBX_SCALE_ALL",
    add_leaf_bones=False,
    use_armature_deform_only=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,
    axis_forward="-Z",
    axis_up="Y",
    path_mode="AUTO",
    embed_textures=False,
)

# Render three visual checks beside the .blend file.
preview_dir = os.path.join(os.path.dirname(output_blend), "Previews")
ensure_dir(preview_dir)
camera_data = bpy.data.cameras.new("Preview_Camera")
camera = bpy.data.objects.new("Preview_Camera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (2.75, -0.55, 0.34)
camera_data.lens = 58
look_at(camera, (0, -0.03, -0.02))
scene.camera = camera

key_data = bpy.data.lights.new("Preview_Key", "AREA")
key_data.energy = 650
key_data.shape = "DISK"
key_data.size = 3.0
key = bpy.data.objects.new("Preview_Key", key_data)
bpy.context.collection.objects.link(key)
key.location = (1.4, -1.2, 1.8)
look_at(key, (0, 0, 0))

fill_data = bpy.data.lights.new("Preview_Fill", "AREA")
fill_data.energy = 350
fill_data.size = 2.5
fill = bpy.data.objects.new("Preview_Fill", fill_data)
bpy.context.collection.objects.link(fill)
fill.location = (-1.3, 0.8, 0.5)
look_at(fill, (0, 0, 0))

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 960
scene.render.resolution_y = 540
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("Preview_World")
scene.world.color = (0.018, 0.028, 0.045)

for action, frame, filename in [
    (swim, 13, "Shark_Swim.png"),
    (attack, 13, "Shark_Attack.png"),
    (defeat, 38, "Shark_Defeated.png"),
]:
    armature.animation_data.action = action
    scene.frame_set(frame)
    scene.render.filepath = os.path.join(preview_dir, filename)
    bpy.ops.render.render(write_still=True)

print("SHARK_RIG_OUTPUT_BLEND=" + output_blend)
print("SHARK_RIG_OUTPUT_FBX=" + output_fbx)
print("SHARK_RIG_ACTIONS=" + ",".join(sorted(a.name for a in bpy.data.actions if a.name.startswith("Shark_"))))
