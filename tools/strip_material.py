"""Strip material + textures from a Meshy FBX, keeping the exact same mesh and UVs.

Why: Mixamo rejects FBX files that carry an embedded PBR material ("unable to map your existing
skeleton"). Do NOT re-generate on Meshy with texture off instead: that produces a different mesh
with no UVs, and the textured version can no longer be applied to the rigged one in Unity.

Usage (run from the repo root):
    "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" --background --python tools/strip_material.py -- <textured_in.fbx> <no_material_out.fbx>

Upload the output to Mixamo. Keep the textured original for the Unity material.
"""
import bpy
import sys

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)

mesh_objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
print(f"Imported {len(mesh_objs)} mesh objects")
for obj in mesh_objs:
    data = obj.data
    print(f"  {obj.name}: verts={len(data.vertices)} uv_layers={len(data.uv_layers)}")
    data.materials.clear()

for img in list(bpy.data.images):
    bpy.data.images.remove(img)
for mat in list(bpy.data.materials):
    bpy.data.materials.remove(mat)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(
    filepath=dst,
    use_selection=True,
    embed_textures=False,
    path_mode='COPY',
    add_leaf_bones=False,
)
print(f"Exported to {dst}")
