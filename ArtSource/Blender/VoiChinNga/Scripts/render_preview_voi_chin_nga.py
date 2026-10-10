import bpy
import math
import sys
from mathutils import Vector

src = sys.argv[sys.argv.index("--") + 1]
dst = sys.argv[sys.argv.index("--") + 2]
extra = sys.argv[sys.argv.index("--") + 3:]
action_name = extra[0] if extra else None
frame = int(extra[1]) if len(extra) > 1 else 1

bpy.ops.wm.read_factory_settings(use_empty=True)
if src.lower().endswith(".blend"):
    bpy.ops.wm.open_mainfile(filepath=src)
elif hasattr(bpy.ops.wm, "fbx_import"):
    bpy.ops.wm.fbx_import(filepath=src)
else:
    bpy.ops.import_scene.fbx(filepath=src)

mesh = next(o for o in bpy.context.scene.objects if o.type == "MESH")
rig = next((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), None)
if rig and action_name:
    rig.animation_data_create()
    rig.animation_data.action = bpy.data.actions[action_name]
bpy.context.scene.frame_set(frame)
verts = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
lo = Vector(tuple(min(v[i] for v in verts) for i in range(3)))
hi = Vector(tuple(max(v[i] for v in verts) for i in range(3)))
center = (lo + hi) * 0.5
size = max(hi - lo)

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 900
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = dst
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new("Preview World")
scene.world.color = (0.035, 0.04, 0.05)
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "WORLD"

# Soft clay material to reveal the silhouette and armor details.
mat = bpy.data.materials.new("Preview Clay")
mat.diffuse_color = (0.45, 0.52, 0.58, 1.0)
mesh.data.materials.clear()
mesh.data.materials.append(mat)

bpy.ops.object.camera_add(location=center + Vector((1.25, -1.65, 0.72)) * size)
cam = bpy.context.object
scene.camera = cam
direction = center - cam.location
cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
cam.data.lens = 58

bpy.ops.object.light_add(type="AREA", location=center + Vector((-0.9, -1.0, 1.5)) * size)
bpy.context.object.data.energy = 900
bpy.context.object.data.shape = "DISK"
bpy.context.object.data.size = 1.5 * size
bpy.ops.object.light_add(type="AREA", location=center + Vector((1.1, 0.8, 0.7)) * size)
bpy.context.object.data.energy = 500
bpy.context.object.data.size = 1.2 * size
bpy.ops.object.light_add(type="AREA", location=center + Vector((0.0, 0.0, 2.0)) * size)
bpy.context.object.data.energy = 350
bpy.context.object.data.size = 1.0 * size

bpy.ops.render.render(write_still=True)
