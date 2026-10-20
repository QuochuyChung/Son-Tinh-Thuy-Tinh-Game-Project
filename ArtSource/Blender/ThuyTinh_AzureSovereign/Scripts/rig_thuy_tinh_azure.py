"""Rig the Azure Tide Sovereign body and Azure Plume Mantle to Thuy Tinh's Mixamo skeleton.

The Meshy body is authored in an arms-down pose, while the reusable Unity avatar is a
Mixamo T-pose.  We therefore transfer weights against frame 1 of the old idle clip, then
inverse-skin every vertex back into the skeleton's bind pose.  The exported avatar keeps
the old bone hierarchy, so all existing Humanoid clips and the Animator Override
Controller can be reused unchanged.

Usage:
    blender --background --python rig_thuy_tinh_azure.py -- \
      donor_idle.fbx body_textured.fbx mantle_textured.fbx out.fbx preview_dir \
      [preview_clip.fbx ...] blend=out.blend bodytex=body.png mantletex=mantle.png
"""

import bmesh
import bpy
import math
import mathutils
import numpy as np
import os
import sys
from mathutils import Matrix, Vector


arguments = sys.argv[sys.argv.index("--") + 1 :]
positional = [arg for arg in arguments if "=" not in arg]
options = dict(arg.split("=", 1) for arg in arguments if "=" in arg)
donor_path, body_path, mantle_path, export_path, preview_dir = positional[:5]
preview_dir = os.path.abspath(preview_dir)
preview_clips = positional[5:]
blend_path = options.get("blend")
body_texture_path = options.get("bodytex")
mantle_texture_path = options.get("mantletex")
mantle_length = float(options.get("mlen", 0.84))
mantle_width = float(options.get("mwidth", 1.45))
mantle_gap = float(options.get("mgap", 0.010))
mantle_raise = float(options.get("mraise", 0.025))
os.makedirs(preview_dir, exist_ok=True)


def log(message):
    print("TTA", message)


def import_fbx(path):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.abspath(path))
    return [obj for obj in bpy.context.scene.objects if obj not in before]


def world_vertices(obj, evaluated=False):
    if not evaluated:
        return [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated_object = obj.evaluated_get(depsgraph)
    mesh = evaluated_object.to_mesh()
    points = [evaluated_object.matrix_world @ vertex.co for vertex in mesh.vertices]
    polygons = [tuple(poly.vertices) for poly in mesh.polygons]
    evaluated_object.to_mesh_clear()
    return points, polygons


def connected_components(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    component_of = {}
    components = []
    for start in bm.verts:
        if start.index in component_of:
            continue
        component_id = len(components)
        component_of[start.index] = component_id
        stack = [start]
        members = []
        while stack:
            vertex = stack.pop()
            members.append(vertex.index)
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other.index not in component_of:
                    component_of[other.index] = component_id
                    stack.append(other)
        components.append(members)
    bm.free()
    return component_of, components


def normalize_top_four(weights):
    chosen = sorted(weights.items(), key=lambda item: -item[1])[:4]
    total = sum(weight for _, weight in chosen) or 1.0
    return [(name, weight / total) for name, weight in chosen]


def average_weights(weight_sets):
    result = {}
    for weights in weight_sets:
        for name, weight in weights.items():
            result[name] = result.get(name, 0.0) + weight / len(weight_sets)
    return result


def material_with_base_color(name, texture_path):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    principled = nodes.get("Principled BSDF")
    image = bpy.data.images.load(os.path.abspath(texture_path), check_existing=True)
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    material.node_tree.links.new(texture.outputs["Color"], principled.inputs["Base Color"])
    material.node_tree.links.new(texture.outputs["Alpha"], principled.inputs["Alpha"])
    return material


def matrix_weighted_sum(weighted_matrices):
    result = Matrix(((0.0, 0.0, 0.0, 0.0),) * 4)
    for matrix, weight in weighted_matrices:
        for row in range(4):
            for column in range(4):
                result[row][column] += matrix[row][column] * weight
    return result


bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# Donor skeleton and its skinned mesh, evaluated in the first frame of the old idle.
donor_objects = import_fbx(donor_path)
armature = next(obj for obj in donor_objects if obj.type == "ARMATURE")
donor = next(obj for obj in donor_objects if obj.type == "MESH")
armature.name = "Armature"
scene.frame_set(1)
armature.data.pose_position = "POSE"
bpy.context.view_layer.update()
donor_points, donor_polygons = world_vertices(donor, evaluated=True)
donor_group_names = {group.index: group.name for group in donor.vertex_groups}
donor_weights = [
    {donor_group_names[item.group]: item.weight for item in vertex.groups if item.weight > 1e-5}
    for vertex in donor.data.vertices
]
valid_bones = {bone.name for bone in armature.data.bones}
donor_min = Vector(tuple(min(point[axis] for point in donor_points) for axis in range(3)))
donor_max = Vector(tuple(max(point[axis] for point in donor_points) for axis in range(3)))
donor_height = donor_max.z - donor_min.z
donor_center = (donor_min + donor_max) * 0.5
donor_bvh = mathutils.bvhtree.BVHTree.FromPolygons(donor_points, donor_polygons)
log(f"donor vertices={len(donor_points)} bones={len(valid_bones)} height={donor_height:.4f}")

# Meshy body: scale/centre it onto the donor's posed silhouette, then transfer weights.
body_objects = import_fbx(body_path)
body = next(obj for obj in body_objects if obj.type == "MESH")
for obj in body_objects:
    if obj is not body:
        bpy.data.objects.remove(obj, do_unlink=True)
body.name = "ThuyTinhAzure_Body"
body.data.name = body.name
source_body_points = world_vertices(body)
source_min = Vector(tuple(min(point[axis] for point in source_body_points) for axis in range(3)))
source_max = Vector(tuple(max(point[axis] for point in source_body_points) for axis in range(3)))
source_height = source_max.z - source_min.z
scale = donor_height / source_height
source_center = (source_min + source_max) * 0.5
posed_body_points = [
    Vector(
        (
            donor_center.x + (point.x - source_center.x) * scale,
            donor_center.y + (point.y - source_center.y) * scale,
            donor_min.z + (point.z - source_min.z) * scale,
        )
    )
    for point in source_body_points
]

raw_weights = []
distances = []
for point in posed_body_points:
    location, _normal, polygon_index, distance = donor_bvh.find_nearest(point)
    polygon = donor_polygons[polygon_index]
    contributions = []
    contribution_total = 0.0
    for vertex_index in polygon:
        value = 1.0 / ((donor_points[vertex_index] - location).length + 1e-6) ** 2
        contributions.append((vertex_index, value))
        contribution_total += value
    weights = {}
    for vertex_index, value in contributions:
        for bone_name, bone_weight in donor_weights[vertex_index].items():
            if bone_name in valid_bones:
                weights[bone_name] = weights.get(bone_name, 0.0) + bone_weight * value / contribution_total
    raw_weights.append(weights)
    distances.append(distance)

# Keep Meshy's many ornaments coherent.  Small disconnected pieces use one averaged
# attachment, while the long back-hair component follows Head/Neck instead of the arms.
component_of, components = connected_components(body)
largest_component = max(components, key=len)


def fold_arm_weights(weights, target):
    folded = {}
    for bone_name, weight in weights.items():
        short_name = bone_name.split(":")[-1]
        if any(token in short_name for token in ("Arm", "Hand", "Shoulder")):
            bone_name = target
        folded[bone_name] = folded.get(bone_name, 0.0) + weight
    return folded


def point_segment_distance(point, start, end):
    direction = end - start
    length_squared = direction.length_squared
    if length_squared < 1e-12:
        return (point - start).length
    along = min(max((point - start).dot(direction) / length_squared, 0.0), 1.0)
    return (point - (start + along * direction)).length


for members in components:
    points = [posed_body_points[index] for index in members]
    low = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    center = (low + high) * 0.5
    relative_center_z = (center.z - donor_min.z) / donor_height
    is_back_hair = (
        len(members) > 700
        and relative_center_z > 0.66
        and high.y > donor_center.y + 0.05 * donor_height
        and low.z < donor_min.z + 0.60 * donor_height
    )
    arm_accessory = (
        abs(center.x - donor_center.x) > 0.105 * donor_height
        and 0.43 < relative_center_z < 0.80
    )
    if is_back_hair:
        for index in members:
            z_fraction = min(max((posed_body_points[index].z - (donor_min.z + 0.60 * donor_height)) / (0.28 * donor_height), 0.0), 1.0)
            raw_weights[index] = {
                "mixamorig:Neck": 0.55 - 0.20 * z_fraction,
                "mixamorig:Head": 0.45 + 0.20 * z_fraction,
            }
    elif members is not largest_component and not arm_accessory:
        target = "mixamorig:Hips" if relative_center_z < 0.60 else "mixamorig:Spine2"
        for index in members:
            raw_weights[index] = fold_arm_weights(raw_weights[index], target)

    if len(members) < 140 and not is_back_hair:
        averaged = average_weights([raw_weights[index] for index in members])
        for index in members:
            raw_weights[index] = dict(averaged)

# Separate pale exposed arms/hands from the dark hanging waist cloth.  They overlap in the
# arms-down reference pose, so position alone cannot distinguish them, but the authored albedo
# can.  A one-ring expansion keeps turquoise tattoos on the skin attached to the arm bones.
albedo_garment_vertices_fixed = 0
if body_texture_path and body.data.uv_layers.active is not None:
    albedo = bpy.data.images.load(os.path.abspath(body_texture_path), check_existing=True)
    width, height = albedo.size
    pixels = np.asarray(albedo.pixels[:], dtype=np.float32).reshape(height, width, 4)
    color_sums = np.zeros((len(body.data.vertices), 3), dtype=np.float32)
    color_counts = np.zeros(len(body.data.vertices), dtype=np.int32)
    uv_data = body.data.uv_layers.active.data
    for polygon in body.data.polygons:
        for loop_index in polygon.loop_indices:
            vertex_index = body.data.loops[loop_index].vertex_index
            uv = uv_data[loop_index].uv
            x = min(int((uv.x % 1.0) * width), width - 1)
            y = min(int((uv.y % 1.0) * height), height - 1)
            color_sums[vertex_index] += pixels[y, x, :3]
            color_counts[vertex_index] += 1
    colors = color_sums / np.maximum(color_counts[:, None], 1)
    skin_seed = (colors[:, 0] > 0.42) & (colors[:, 2] > 0.32) & (colors.mean(axis=1) > 0.40)
    adjacency = [set() for _ in body.data.vertices]
    for edge in body.data.edges:
        left, right = edge.vertices
        adjacency[left].add(right)
        adjacency[right].add(left)
    skin_region = skin_seed.copy()
    for _ in range(2):
        expanded = skin_region.copy()
        for index, neighbors in enumerate(adjacency):
            if not skin_region[index] and neighbors:
                ratio = sum(bool(skin_region[neighbor]) for neighbor in neighbors) / len(neighbors)
                if ratio >= 0.45:
                    expanded[index] = True
        skin_region = expanded

    # The body skin (face, torso and both arms) is authored as one connected shell.  Use
    # albedo only to identify that shell, then include every vertex on its arm sections.
    # This avoids leaving dark tattoo/back-facing arm vertices attached to the torso.
    substantial_components = [members for members in components if len(members) >= 0.15 * len(largest_component)]
    skin_component = max(
        substantial_components,
        key=lambda members: (
            sum(bool(skin_seed[index]) for index in members) / max(len(members), 1),
            sum(bool(skin_seed[index]) for index in members),
        ),
    )
    skin_component_set = set(skin_component)
    skin_seed_ratio = sum(bool(skin_seed[index]) for index in skin_component) / len(skin_component)
    log(f"skin shell vertices={len(skin_component)} albedoSeedRatio={skin_seed_ratio:.3f}")

    posed_arm_segments = {}
    donor_arm_trees = {}
    for side in ("Left", "Right"):
        segments_for_side = []
        for short_name in ("Shoulder", "Arm", "ForeArm", "Hand"):
            pose_bone = armature.pose.bones["mixamorig:" + side + short_name]
            segments_for_side.append(
                (
                    "mixamorig:" + side + short_name,
                    armature.matrix_world @ pose_bone.head,
                    armature.matrix_world @ pose_bone.tail,
                )
            )
        posed_arm_segments[side] = segments_for_side

        # Copy the donor's proven shoulder/elbow/wrist/finger gradients instead of creating
        # synthetic two-bone weights.  The latter collapses volume whenever a clip bends the
        # elbow strongly (run, cast and attack poses).
        donor_arm_indices = []
        for donor_index, weights in enumerate(donor_weights):
            arm_weight = sum(
                weight for name, weight in weights.items()
                if side in name.split(":")[-1]
                and any(token in name.split(":")[-1] for token in ("Shoulder", "Arm", "Hand"))
            )
            if arm_weight > 0.20:
                donor_arm_indices.append(donor_index)
        tree = mathutils.kdtree.KDTree(len(donor_arm_indices))
        for donor_index in donor_arm_indices:
            tree.insert(donor_points[donor_index], donor_index)
        tree.balance()
        donor_arm_trees[side] = tree

    arm_skin_vertices_reweighted = 0
    for index, point in enumerate(posed_body_points):
        relative_z = (point.z - donor_min.z) / donor_height
        side = "Left" if point.x >= donor_center.x else "Right"
        distances_to_arm = sorted(
            (
                point_segment_distance(point, start, end),
                bone_name,
            )
            for bone_name, start, end in posed_arm_segments[side]
        )
        if (
            index in skin_component_set
            and 0.43 < relative_z < 0.82
            and abs(point.x - donor_center.x) > 0.065 * donor_height
            and distances_to_arm[0][0] < 0.060 * donor_height
        ):
            sampled = {}
            sampled_total = 0.0
            for _coordinate, donor_index, distance in donor_arm_trees[side].find_n(point, 6):
                influence = 1.0 / (distance + 0.004 * donor_height) ** 2
                sampled_total += influence
                for bone_name, bone_weight in donor_weights[donor_index].items():
                    if bone_name in valid_bones:
                        sampled[bone_name] = sampled.get(bone_name, 0.0) + bone_weight * influence
            raw_weights[index] = {
                bone_name: weight / sampled_total
                for bone_name, weight in sampled.items()
                if weight > 1e-8
            }
            arm_skin_vertices_reweighted += 1

        arm_weight = sum(
            weight for name, weight in raw_weights[index].items()
            if any(token in name.split(":")[-1] for token in ("Arm", "Hand", "Shoulder"))
        )
        if relative_z < 0.65 and arm_weight > 1e-5 and not skin_region[index]:
            raw_weights[index] = fold_arm_weights(raw_weights[index], "mixamorig:Hips")
            albedo_garment_vertices_fixed += 1

    # The exposed skin is a separate connected shell; the largest shell is the outfit.  Its
    # lower half contains the long belt panels that overlap the hands in the source pose, so no
    # arm or finger influence is valid there even where gold trim made the albedo look skin-like.
    lower_outfit_vertices_fixed = 0
    for index in largest_component:
        point = posed_body_points[index]
        relative_z = (point.z - donor_min.z) / donor_height
        if relative_z >= 0.64:
            continue
        before = sum(
            weight for name, weight in raw_weights[index].items()
            if any(token in name.split(":")[-1] for token in ("Arm", "Hand", "Shoulder"))
        )
        if before > 1e-5:
            raw_weights[index] = fold_arm_weights(raw_weights[index], "mixamorig:Hips")
            lower_outfit_vertices_fixed += 1
    log(
        f"arm skin vertices reweighted={arm_skin_vertices_reweighted} "
        f"albedo waist garment vertices fixed={albedo_garment_vertices_fixed} "
        f"lower outfit vertices fixed={lower_outfit_vertices_fixed}"
    )

final_weights = [normalize_top_four(weights) for weights in raw_weights]

# The donor is still in its arms-down reference pose here.  These matrices convert a
# reference-pose point back to the skeleton's bind pose.
deform_matrices = {
    bone.name: armature.pose.bones[bone.name].matrix @ bone.matrix_local.inverted()
    for bone in armature.data.bones
}
armature_inverse = armature.matrix_world.inverted()


# A hanging waist panel occupies nearly the same space as the hands in Meshy's arms-down
# source pose.  Nearest-surface transfer can therefore assign it to Hand/ForeArm and turn the
# panel into a rigid spear whenever an attack raises the arm.  Detect those false positives in
# the reconstructed bind pose: real arm geometry remains close to an arm bone, while the belt
# panels stretch far away from every arm segment.  Fold only those outliers back to Hips/Spine2.
arm_segments = []
for side in ("Left", "Right"):
    for short_name in ("Shoulder", "Arm", "ForeArm", "Hand"):
        bone = armature.data.bones["mixamorig:" + side + short_name]
        arm_segments.append((armature.matrix_world @ bone.head_local, armature.matrix_world @ bone.tail_local))

garment_vertices_fixed = 0
for index, (point, weights) in enumerate(zip(posed_body_points, final_weights)):
    arm_weight = sum(
        weight for name, weight in weights
        if any(token in name.split(":")[-1] for token in ("Arm", "Hand", "Shoulder"))
    )
    if arm_weight < 0.12:
        continue
    skin_matrix = matrix_weighted_sum([(deform_matrices[name], weight) for name, weight in weights])
    candidate_bind_world = armature.matrix_world @ (skin_matrix.inverted_safe() @ (armature_inverse @ point))
    arm_distance = min(point_segment_distance(candidate_bind_world, start, end) for start, end in arm_segments)
    relative_z = (point.z - donor_min.z) / donor_height
    if arm_distance > 0.105 * donor_height and relative_z < 0.73:
        target = "mixamorig:Hips" if relative_z < 0.60 else "mixamorig:Spine2"
        raw_weights[index] = fold_arm_weights(raw_weights[index], target)
        garment_vertices_fixed += 1

final_weights = [normalize_top_four(weights) for weights in raw_weights]
log(f"waist garment arm-weight outliers fixed={garment_vertices_fixed}")
log(
    f"body vertices={len(posed_body_points)} components={len(components)} scale={scale:.4f} "
    f"nearest median={sorted(distances)[len(distances)//2]/donor_height:.4f}BH max={max(distances)/donor_height:.4f}BH"
)

# Inverse skinning converts the Meshy arms-down shape into the donor T-pose bind shape.
bind_points = []
for point, weights in zip(posed_body_points, final_weights):
    skin_matrix = matrix_weighted_sum([(deform_matrices[name], weight) for name, weight in weights])
    bind_points.append(skin_matrix.inverted_safe() @ (armature_inverse @ point))

# Report components whose bind-pose span is suspiciously wide.  Bind points are in
# armature-local coordinates, so compare their world-space span with donor_height.
armature_scale_x = abs(armature.matrix_world.to_scale().x)
for component_index, members in enumerate(components):
    posed_low_x = min(posed_body_points[index].x for index in members)
    posed_high_x = max(posed_body_points[index].x for index in members)
    bind_low_x = min(bind_points[index].x for index in members)
    bind_high_x = max(bind_points[index].x for index in members)
    arm_weight = sum(
        sum(weight for name, weight in final_weights[index] if any(token in name.split(":")[-1] for token in ("Arm", "Hand", "Shoulder")))
        for index in members
    ) / len(members)
    bind_span_world = (bind_high_x - bind_low_x) * armature_scale_x
    if bind_span_world > 0.20 * donor_height or arm_weight > 0.20:
        log(
            f"component {component_index} verts={len(members)} posedX=({posed_low_x:.3f},{posed_high_x:.3f}) "
            f"bindX=({bind_low_x:.3f},{bind_high_x:.3f}) avgArm={arm_weight:.3f}"
        )

for vertex, point in zip(body.data.vertices, bind_points):
    vertex.co = point
body.data.update()
body.vertex_groups.clear()
for bone in armature.data.bones:
    body.vertex_groups.new(name=bone.name)
for index, weights in enumerate(final_weights):
    for bone_name, weight in weights:
        body.vertex_groups[bone_name].add([index], weight, "REPLACE")
body.parent = armature
body.matrix_parent_inverse = Matrix.Identity(4)
body.matrix_basis = Matrix.Identity(4)
modifier = body.modifiers.new("Armature", "ARMATURE")
modifier.object = armature

if body_texture_path:
    body.data.materials.clear()
    body.data.materials.append(material_with_base_color("M_ThuyTinhAzure_Body", body_texture_path))

bpy.data.objects.remove(donor, do_unlink=True)

# Return the skeleton to its original bind pose before adding the mantle.
armature.animation_data_clear()
armature.data.pose_position = "REST"
scene.frame_set(1)
bpy.context.view_layer.update()

def bone_world(short_name):
    return armature.matrix_world @ armature.data.bones["mixamorig:" + short_name].head_local


feet_front = bone_world("LeftToeBase") + bone_world("RightToeBase") - bone_world("LeftFoot") - bone_world("RightFoot")
feet_front.z = 0.0
feet_front.normalize()
neck = bone_world("Neck")
left_arm = bone_world("LeftArm")
right_arm = bone_world("RightArm")
shoulder_width = abs(left_arm.x - right_arm.x)

# Mantle: fit its collar to the neck/shoulders, put its feather mass behind the body,
# and add five three-segment Cape_ chains for the existing OutfitSpringBones component.
mantle_objects = import_fbx(mantle_path)
mantle = next(obj for obj in mantle_objects if obj.type == "MESH")
for obj in mantle_objects:
    if obj is not mantle:
        bpy.data.objects.remove(obj, do_unlink=True)
mantle.name = "ThuyTinhAzure_Mantle"
mantle.data.name = mantle.name
source_mantle_points = world_vertices(mantle)
mantle_min_z = min(point.z for point in source_mantle_points)
mantle_max_z = max(point.z for point in source_mantle_points)
mantle_height = mantle_max_z - mantle_min_z
top_ring = [point for point in source_mantle_points if (point.z - mantle_min_z) / mantle_height > 0.94]
ring_x = sum(point.x for point in top_ring) / len(top_ring)
ring_y = sum(point.y for point in top_ring) / len(top_ring)
shoulder_band = [point for point in source_mantle_points if 0.82 < (point.z - mantle_min_z) / mantle_height < 0.94]
band_width = max(point.x for point in shoulder_band) - min(point.x for point in shoulder_band)
mantle_scale = mantle_length * donor_height / mantle_height
width_scale = min(max(mantle_width * shoulder_width / max(band_width * mantle_scale, 1e-5), 0.95), 1.75)
middle = [point for point in source_mantle_points if 0.30 < (point.z - mantle_min_z) / mantle_height < 0.70]
middle_y = sum(point.y for point in middle) / len(middle)
flip = (middle_y - ring_y) * feet_front.y > 0.0
mantle_points = []
for point in source_mantle_points:
    dx = (point.x - ring_x) * mantle_scale * width_scale
    dy = (point.y - ring_y) * mantle_scale
    if flip:
        dx, dy = -dx, -dy
    # Bias the hanging mass behind the spine while keeping the collar close to the neck.
    vertical = (mantle_max_z - point.z) / mantle_height
    # Keep the collar on top of the shoulders, then progressively move the hanging mass
    # behind the spine.  The small forward bias prevents the enlarged collar from disappearing
    # inside the body's shoulder garment.
    behind = -feet_front * (mantle_gap * donor_height * (0.10 + 0.90 * vertical))
    shoulder_drape = feet_front * (0.008 * donor_height * (1.0 - vertical) ** 2)
    mantle_points.append(
        Vector((neck.x + dx, neck.y + dy, neck.z + mantle_raise * donor_height - vertical * mantle_length * donor_height))
        + behind
        + shoulder_drape
    )

# Bake the fitted world points into armature-local coordinates.
for vertex, point in zip(mantle.data.vertices, mantle_points):
    vertex.co = armature_inverse @ point
mantle.data.update()

if mantle_texture_path:
    mantle.data.materials.clear()
    mantle.data.materials.append(material_with_base_color("M_ThuyTinhAzure_Mantle", mantle_texture_path))

top_z = max(point.z for point in mantle_points)
bottom_z = min(point.z for point in mantle_points)
cape_height = top_z - bottom_z
columns = 5
segments = 3
levels = [0.10, 0.38, 0.68, 1.0]
joint_rows = []
for level in levels:
    target_z = top_z - level * cape_height
    close = [point for point in mantle_points if abs(point.z - target_z) < 0.055 * cape_height]
    if not close:
        close = [min(mantle_points, key=lambda point: abs(point.z - target_z))]
    minimum_x = min(point.x for point in close)
    maximum_x = max(point.x for point in close)
    span = max(maximum_x - minimum_x, 0.05 * donor_height)
    row = []
    for column in range(columns):
        x = minimum_x + span * (column + 0.5) / columns
        near = [point for point in close if abs(point.x - x) < span / columns]
        if not near:
            near = close
        row.append(Vector((x, sum(point.y for point in near) / len(near), target_z)))
    joint_rows.append(row)

bpy.context.view_layer.objects.active = armature
armature.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
inverse_armature = armature.matrix_world.inverted()
edit_bones = armature.data.edit_bones
cape_bone_names = {}
for column in range(columns):
    parent = edit_bones["mixamorig:Spine2"]
    for segment in range(segments):
        name = f"Cape_{column}_{segment}"
        bone = edit_bones.new(name)
        bone.head = inverse_armature @ joint_rows[segment][column]
        bone.tail = inverse_armature @ joint_rows[segment + 1][column]
        bone.parent = parent
        bone.use_connect = False
        cape_bone_names[(column, segment)] = name
        parent = bone
bpy.ops.object.mode_set(mode="OBJECT")

mantle.vertex_groups.clear()
for bone in armature.data.bones:
    mantle.vertex_groups.new(name=bone.name)
minimum_x = min(point.x for point in mantle_points)
maximum_x = max(point.x for point in mantle_points)
span_x = max(maximum_x - minimum_x, 1e-5)
for index, point in enumerate(mantle_points):
    vertical = min(max((top_z - point.z) / cape_height, 0.0), 1.0)
    if vertical <= levels[0]:
        mantle.vertex_groups["mixamorig:Spine2"].add([index], 1.0, "REPLACE")
        continue
    segment_position = min(max((vertical - levels[0]) / (1.0 - levels[0]) * segments, 0.0), segments - 1e-5)
    segment = min(int(segment_position), segments - 1)
    next_segment = min(segment + 1, segments - 1)
    segment_mix = segment_position - math.floor(segment_position)
    column_position = min(max((point.x - minimum_x) / span_x * (columns - 1), 0.0), columns - 1.0)
    column = min(int(column_position), columns - 2)
    next_column = column + 1
    column_mix = column_position - column
    weights = {
        cape_bone_names[(column, segment)]: (1.0 - column_mix) * (1.0 - segment_mix),
        cape_bone_names[(next_column, segment)]: column_mix * (1.0 - segment_mix),
        cape_bone_names[(column, next_segment)]: (1.0 - column_mix) * segment_mix,
        cape_bone_names[(next_column, next_segment)]: column_mix * segment_mix,
    }
    for bone_name, weight in weights.items():
        if weight > 1e-5:
            mantle.vertex_groups[bone_name].add([index], weight, "REPLACE")

mantle.parent = armature
mantle.matrix_parent_inverse = Matrix.Identity(4)
mantle.matrix_basis = Matrix.Identity(4)
mantle_modifier = mantle.modifiers.new("Armature", "ARMATURE")
mantle_modifier.object = armature
log(
    f"mantle vertices={len(mantle.data.vertices)} scale={mantle_scale:.4f} width={width_scale:.3f} "
    f"flip={flip} top={top_z:.3f} bottom={bottom_z:.3f} cape_bones={columns*segments}"
)

# Preview setup.
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE" if body_texture_path else "OBJECT"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.view_settings.view_transform = "Standard"
scene.world = bpy.data.worlds.new("PreviewWorld")
scene.world.color = (0.025, 0.04, 0.075)
scene.render.resolution_x = 640
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
camera_data = bpy.data.cameras.new("PreviewCamera")
camera_data.type = "ORTHO"
camera_data.ortho_scale = donor_height * 1.18
camera = bpy.data.objects.new("PreviewCamera", camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
center_z = donor_min.z + donor_height * 0.5


def render_view(filename, location, target):
    camera.location = Vector(location)
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(preview_dir, filename)
    bpy.ops.render.render(write_still=True)
    log(f"preview {scene.render.filepath}")


armature.data.pose_position = "REST"
bpy.context.view_layer.update()
render_view("bind_front.png", (donor_center.x, donor_center.y - 2.4 * donor_height, center_z), (donor_center.x, donor_center.y, center_z))
render_view("bind_back.png", (donor_center.x, donor_center.y + 2.4 * donor_height, center_z), (donor_center.x, donor_center.y, center_z))
render_view("bind_side.png", (donor_center.x + 2.4 * donor_height, donor_center.y, center_z), (donor_center.x, donor_center.y, center_z))

# Pose previews prove that the old clips drive the new body.  Each clip gets four frames in one horizontal sheet.
try:
    for clip_path in preview_clips:
        imported = import_fbx(clip_path)
        source_armature = next(obj for obj in imported if obj.type == "ARMATURE")
        action = source_armature.animation_data.action
        for obj in imported:
            obj.hide_render = True
            obj.hide_viewport = True
        armature.data.pose_position = "POSE"
        armature.animation_data_create()
        armature.animation_data.action = action
        try:
            armature.animation_data.action_slot = action.slots[0]
        except Exception as error:
            log(f"action slot note: {error}")
        first = max(1, int(action.frame_range[0]))
        last = max(first, int(action.frame_range[1]))
        frames = sorted({first, first + (last-first)//3, first + 2*(last-first)//3, last})
        pictures = []
        for frame in frames:
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            temporary = os.path.join(preview_dir, f"_pose_{frame:04d}.png")
            scene.render.filepath = temporary
            camera.location = Vector((donor_center.x, donor_center.y - 2.4 * donor_height, center_z))
            camera.rotation_euler = (Vector((donor_center.x, donor_center.y, center_z)) - camera.location).to_track_quat("-Z", "Y").to_euler()
            bpy.ops.render.render(write_still=True)
            image = bpy.data.images.load(temporary)
            pictures.append(np.array(image.pixels[:]).reshape(image.size[1], image.size[0], 4))
            bpy.data.images.remove(image)
            os.remove(temporary)
        sheet = np.concatenate(pictures, axis=1)
        output = bpy.data.images.new("PoseSheet", sheet.shape[1], sheet.shape[0], alpha=True)
        output.pixels.foreach_set(sheet.astype(np.float32).ravel())
        clip_name = os.path.splitext(os.path.basename(clip_path))[0]
        output.filepath_raw = os.path.join(preview_dir, f"pose_{clip_name}.png")
        output.file_format = "PNG"
        output.save()
        bpy.data.images.remove(output)
        armature.animation_data_clear()
        for obj in imported:
            bpy.data.objects.remove(obj, do_unlink=True)
        log(f"pose sheet {clip_name} frames={frames}")
except Exception as error:
    log(f"pose previews failed: {error}")
    raise

# Save the editable Blender source with materials, then export a clean material-less FBX for Unity.
armature.animation_data_clear()
armature.data.pose_position = "REST"
scene.frame_set(1)
bpy.context.view_layer.update()
if blend_path:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(blend_path))
    log(f"saved blend {os.path.abspath(blend_path)}")

body.data.materials.clear()
mantle.data.materials.clear()
bpy.ops.object.select_all(action="DESELECT")
for obj in (armature, body, mantle):
    obj.hide_viewport = False
    obj.hide_render = False
    obj.select_set(True)
bpy.context.view_layer.objects.active = armature
bpy.ops.export_scene.fbx(
    filepath=os.path.abspath(export_path),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=False,
    embed_textures=False,
    path_mode="COPY",
    use_armature_deform_only=False,
)
log(f"exported {os.path.abspath(export_path)}")
