"""Render four frames of a runtime FBX clip on the current Azure Sovereign rig."""

import os
import sys

import bpy
import numpy as np
from mathutils import Vector


args = sys.argv[sys.argv.index("--") + 1 :]
clip_path, output_path = map(os.path.abspath, args[:2])

armature = bpy.data.objects["Armature"]
camera = bpy.data.objects["PreviewCamera"]
body = bpy.data.objects["ThuyTinhAzure_Body"]

before = set(bpy.context.scene.objects)
bpy.ops.import_scene.fbx(filepath=clip_path)
imported = [obj for obj in bpy.context.scene.objects if obj not in before]
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
except Exception:
    pass

scene = bpy.context.scene
first = max(1, int(action.frame_range[0]))
last = max(first, int(action.frame_range[1]))
frames = sorted({first, first + (last - first) // 3, first + 2 * (last - first) // 3, last})

os.makedirs(os.path.dirname(output_path), exist_ok=True)
pictures = []
for frame in frames:
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    temporary = os.path.join(os.path.dirname(output_path), f"_runtime_{frame:04d}.png")
    scene.render.filepath = temporary
    bpy.ops.render.render(write_still=True)
    image = bpy.data.images.load(temporary)
    pictures.append(np.array(image.pixels[:]).reshape(image.size[1], image.size[0], 4))
    bpy.data.images.remove(image)
    os.remove(temporary)

sheet = np.concatenate(pictures, axis=1)
output = bpy.data.images.new("RuntimePoseSheet", sheet.shape[1], sheet.shape[0], alpha=True)
output.pixels.foreach_set(sheet.astype(np.float32).ravel())
output.filepath_raw = output_path
output.file_format = "PNG"
output.save()
print(f"runtime-preview:{os.path.basename(clip_path)} frames={frames} output={output_path}")
