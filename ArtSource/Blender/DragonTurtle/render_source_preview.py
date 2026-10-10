import bpy
import sys
from pathlib import Path
from mathutils import Vector


source = sys.argv[sys.argv.index("--") + 1]
output_dir = Path(sys.argv[sys.argv.index("--") + 2])
output_dir.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
if hasattr(bpy.ops.wm, "fbx_import"):
    bpy.ops.wm.fbx_import(filepath=source)
else:
    bpy.ops.import_scene.fbx(filepath=source)

mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
coords = [mesh.matrix_world @ vertex.co for vertex in mesh.data.vertices]
lo = Vector(tuple(min(v[i] for v in coords) for i in range(3)))
hi = Vector(tuple(max(v[i] for v in coords) for i in range(3)))
center = (lo + hi) * 0.5
size = max(hi - lo)

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 700
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "WORLD"
scene.display.shading.background_type = "THEME"

bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.lens = 55
scene.camera = camera

views = {
    "front": Vector((0.0, -1.45, 0.36)),
    "back": Vector((0.0, 1.45, 0.36)),
    "left": Vector((-1.25, 0.0, 0.30)),
    "right": Vector((1.25, 0.0, 0.30)),
}
for name, offset in views.items():
    camera.location = center + offset * size
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(output_dir / f"source_{name}.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)
