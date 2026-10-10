import bpy
import json
import sys
from mathutils import Vector


source = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
if hasattr(bpy.ops.wm, "fbx_import"):
    bpy.ops.wm.fbx_import(filepath=source)
else:
    bpy.ops.import_scene.fbx(filepath=source)

report = {"objects": [], "actions": []}
for obj in bpy.context.scene.objects:
    item = {
        "name": obj.name,
        "type": obj.type,
        "parent": obj.parent.name if obj.parent else None,
        "dimensions": [round(v, 6) for v in obj.dimensions],
        "location": [round(v, 6) for v in obj.location],
    }
    if obj.type == "MESH":
        coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
        if coords:
            lo = Vector(tuple(min(v[i] for v in coords) for i in range(3)))
            hi = Vector(tuple(max(v[i] for v in coords) for i in range(3)))
            item.update({
                "vertices": len(obj.data.vertices),
                "edges": len(obj.data.edges),
                "polygons": len(obj.data.polygons),
                "world_min": [round(v, 6) for v in lo],
                "world_max": [round(v, 6) for v in hi],
                "vertex_groups": [group.name for group in obj.vertex_groups],
                "uv_layers": [layer.name for layer in obj.data.uv_layers],
                "materials": [material.name if material else None for material in obj.data.materials],
            })
    elif obj.type == "ARMATURE":
        item["bones"] = [bone.name for bone in obj.data.bones]
    report["objects"].append(item)

for action in bpy.data.actions:
    report["actions"].append({
        "name": action.name,
        "frame_range": [round(v, 3) for v in action.frame_range],
        "slots": len(getattr(action, "slots", [])),
    })

print("DRAGON_TURTLE_REPORT=" + json.dumps(report, ensure_ascii=False))
