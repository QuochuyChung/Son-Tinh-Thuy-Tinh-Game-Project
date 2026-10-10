import bpy
import json
import os
import sys


argv = sys.argv
args = argv[argv.index("--") + 1 :] if "--" in argv else []
if len(args) != 1:
    raise SystemExit("Expected exported FBX path after --")

fbx_path = os.path.abspath(args[0])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_path)

armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
actions = []
for action in bpy.data.actions:
    actions.append(
        {
            "name": action.name,
            "frame_start": round(action.frame_range[0], 3),
            "frame_end": round(action.frame_range[1], 3),
            "slots": len(action.slots),
        }
    )

report = {
    "fbx_exists": os.path.exists(fbx_path),
    "fbx_size": os.path.getsize(fbx_path),
    "armatures": [
        {
            "name": obj.name,
            "bones": [bone.name for bone in obj.data.bones],
        }
        for obj in armatures
    ],
    "meshes": [
        {
            "name": obj.name,
            "vertices": len(obj.data.vertices),
            "polygons": len(obj.data.polygons),
            "uv_layers": [
                {"name": layer.name, "count": len(layer.data)}
                for layer in obj.data.uv_layers
            ],
            "vertex_groups": [group.name for group in obj.vertex_groups],
            "armature_modifiers": [mod.object.name for mod in obj.modifiers if mod.type == "ARMATURE" and mod.object],
        }
        for obj in meshes
    ],
    "actions": sorted(actions, key=lambda item: item["name"]),
}

motion_checks = {}
if len(armatures) == 1 and len(meshes) == 1:
    armature = armatures[0]
    mesh = meshes[0]
    armature.animation_data_create()

    def posed_positions(frame):
        bpy.context.scene.frame_set(frame)
        depsgraph = bpy.context.evaluated_depsgraph_get()
        evaluated = mesh.evaluated_get(depsgraph)
        evaluated_mesh = evaluated.to_mesh()
        positions = [evaluated.matrix_world @ vertex.co for vertex in evaluated_mesh.vertices]
        evaluated.to_mesh_clear()
        return positions

    samples = {"Shark_Swim": 13, "Shark_Attack": 13, "Shark_Defeated": 38}
    for action in bpy.data.actions:
        short_name = action.name.split("|")[-1]
        if short_name not in samples:
            continue
        armature.animation_data.action = action
        start_positions = posed_positions(1)
        sample_positions = posed_positions(samples[short_name])
        max_displacement = max(
            (sample - start).length for start, sample in zip(start_positions, sample_positions)
        )
        motion_checks[short_name] = round(max_displacement, 6)
report["max_vertex_displacement"] = motion_checks

errors = []
if len(armatures) != 1:
    errors.append(f"Expected one armature, got {len(armatures)}")
if len(meshes) != 1:
    errors.append(f"Expected one mesh, got {len(meshes)}")
elif not meshes[0].data.uv_layers or len(meshes[0].data.uv_layers.active.data) == 0:
    errors.append("Rigged shark mesh has no UV map")
required_bones = {"Root", "Body", "Spine_01", "Spine_02", "Tail_01", "Tail_02", "Tail_03", "Jaw"}
if armatures and not required_bones.issubset({bone.name for bone in armatures[0].data.bones}):
    errors.append("Missing one or more required bones")
required_actions = {"Shark_Swim", "Shark_Attack", "Shark_Defeated"}
action_names = {action.name.split("|")[-1] for action in bpy.data.actions}
if not required_actions.issubset(action_names):
    errors.append(f"Missing clips: {sorted(required_actions - action_names)}")
for action_name in required_actions:
    if motion_checks.get(action_name, 0.0) < 0.001:
        errors.append(f"Clip has no measurable deformation: {action_name}")

report["valid"] = not errors
report["errors"] = errors
print("SHARK_VALIDATION=" + json.dumps(report, ensure_ascii=False))
if errors:
    raise SystemExit(1)
