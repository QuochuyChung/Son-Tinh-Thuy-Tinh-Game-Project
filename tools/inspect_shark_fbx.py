import bpy
import json
import sys


def arg_after_double_dash():
    argv = sys.argv
    return argv[argv.index("--") + 1 :] if "--" in argv else []


args = arg_after_double_dash()
if not args:
    raise SystemExit("Expected FBX path after --")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=args[0])

report = []
for obj in bpy.context.scene.objects:
    local_bounds = [tuple(round(v, 6) for v in corner) for corner in obj.bound_box] if hasattr(obj, "bound_box") else []
    report.append(
        {
            "name": obj.name,
            "type": obj.type,
            "location": [round(v, 6) for v in obj.location],
            "rotation_euler": [round(v, 6) for v in obj.rotation_euler],
            "scale": [round(v, 6) for v in obj.scale],
            "dimensions": [round(v, 6) for v in obj.dimensions],
            "bounds": local_bounds,
            "vertices": len(obj.data.vertices) if obj.type == "MESH" else None,
            "polygons": len(obj.data.polygons) if obj.type == "MESH" else None,
            "uv_layers": [
                {
                    "name": layer.name,
                    "count": len(layer.data),
                    "min": [
                        round(min(item.uv.x for item in layer.data), 6),
                        round(min(item.uv.y for item in layer.data), 6),
                    ] if layer.data else None,
                    "max": [
                        round(max(item.uv.x for item in layer.data), 6),
                        round(max(item.uv.y for item in layer.data), 6),
                    ] if layer.data else None,
                }
                for layer in obj.data.uv_layers
            ] if obj.type == "MESH" else None,
            "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
            "parent": obj.parent.name if obj.parent else None,
        }
    )

    if obj.type == "MESH":
        ys = [v.co.y for v in obj.data.vertices]
        y_min, y_max = min(ys), max(ys)
        bins = []
        for i in range(10):
            lo = y_min + (y_max - y_min) * i / 10
            hi = y_min + (y_max - y_min) * (i + 1) / 10
            verts = [v.co for v in obj.data.vertices if lo <= v.co.y <= hi]
            if verts:
                bins.append(
                    {
                        "y": round((lo + hi) / 2, 5),
                        "x_span": round(max(v.x for v in verts) - min(v.x for v in verts), 5),
                        "z_span": round(max(v.z for v in verts) - min(v.z for v in verts), 5),
                        "count": len(verts),
                    }
                )
        report[-1]["y_profile"] = bins

print("SHARK_INSPECTION=" + json.dumps(report, ensure_ascii=False))
