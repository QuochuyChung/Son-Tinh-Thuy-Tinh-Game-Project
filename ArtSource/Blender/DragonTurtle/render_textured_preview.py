import bpy
import sys
from pathlib import Path
from mathutils import Vector


args = sys.argv[sys.argv.index("--") + 1:]
blend_path = str(Path(args[0]).resolve())
output_path = str(Path(args[1]).resolve())

bpy.ops.wm.open_mainfile(filepath=blend_path)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 900
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("Preview World")
scene.world.color = (0.035, 0.045, 0.055)

rig = bpy.data.objects["Dragon_Turtle_Rig"]
mesh = bpy.data.objects["Dragon_Turtle_Mesh"]
rig.animation_data.action = bpy.data.actions["Move"]
scene.frame_set(1)
bpy.context.view_layer.update()

corners = [mesh.matrix_world @ Vector(corner) for corner in mesh.bound_box]
low = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
high = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
center = (low + high) * 0.5
size = max(high - low)

bpy.ops.object.camera_add(location=center + Vector((-1.0, -1.45, 0.72)) * size)
camera = bpy.context.object
camera.data.lens = 58
camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
scene.camera = camera

def add_area(name, location, energy, size_value):
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = energy
    light.data.shape = "DISK"
    light.data.size = size_value
    light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()


add_area("Key", center + Vector((-0.8, -0.8, 1.5)) * size, 85, size * 1.8)
add_area("Fill", center + Vector((1.2, -0.2, 0.6)) * size, 45, size * 1.5)
add_area("Rim", center + Vector((0.0, 1.0, 1.2)) * size, 70, size * 1.2)

scene.render.filepath = output_path
bpy.ops.render.render(write_still=True)
print("RENDERED_TEXTURED_PREVIEW", output_path)
