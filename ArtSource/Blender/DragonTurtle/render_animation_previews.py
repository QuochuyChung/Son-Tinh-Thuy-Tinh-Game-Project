import bpy
import sys
from pathlib import Path
from mathutils import Vector


args = sys.argv[sys.argv.index("--") + 1:]
blend_path = str(Path(args[0]).resolve())
output_dir = Path(args[1]).resolve()
output_dir.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=blend_path)
scene = bpy.context.scene
rig = bpy.data.objects["Dragon_Turtle_Rig"]
mesh = bpy.data.objects["Dragon_Turtle_Mesh"]

scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 800
scene.render.resolution_y = 800
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
camera.name = "Preview_Camera_Temporary"
camera.data.lens = 57
scene.camera = camera


def evaluated_bounds():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh.evaluated_get(depsgraph)
    evaluated_mesh = evaluated.to_mesh()
    coords = [evaluated.matrix_world @ vertex.co for vertex in evaluated_mesh.vertices]
    evaluated.to_mesh_clear()
    lo = Vector(tuple(min(v[i] for v in coords) for i in range(3)))
    hi = Vector(tuple(max(v[i] for v in coords) for i in range(3)))
    return lo, hi


shots = [
    ("Move", 7, "DragonTurtle_Move.png"),
    ("Attack", 19, "DragonTurtle_Attack.png"),
    ("Defeated", 58, "DragonTurtle_Defeated.png"),
]

for action_name, frame, filename in shots:
    rig.animation_data.action = bpy.data.actions[action_name]
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    lo, hi = evaluated_bounds()
    center = (lo + hi) * 0.5
    size = max(hi - lo)
    # Three-quarter view from the creature's front-left; move the camera farther
    # back for the fallen pose so the whole silhouette remains visible.
    camera.location = center + Vector((-1.05, -1.35, 0.62)) * size
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(output_dir / filename)
    bpy.ops.render.render(write_still=True)
    print("RENDERED", action_name, frame, scene.render.filepath)
