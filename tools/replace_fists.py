"""Swap Meshy's fused fists for clenched fists on an already skinned FBX (armature + mesh), keeping everything else
(skin weights, cape, UVs, bone names) exactly as it is.

    blender --background --python tools/replace_fists.py -- <in_skinned.fbx> <out_skinned.fbx> <closeup_prefix> [<basecolor.png> [<normal.png>]]

* the fist geometry comes from fist_builder.py (read its docstring for how it is made)
* new fist vertices are weighted 100 % to mixamorig:LeftHand / RightHand (they are solid, there are no finger bones)
* the new fists get UVs inside a flat patch of skin colour of the base-colour texture (picked automatically), so
  they take the same material as the rest of the character
* the export uses the same settings as transfer_weights.py, so the Unity import (Humanoid, avatars, clips) is unaffected
"""
import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fist_builder

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst, prefix = argv[0], argv[1], argv[2]
basecolor = os.path.abspath(argv[3]) if len(argv) > 3 else None
normal = os.path.abspath(argv[4]) if len(argv) > 4 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
A = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
F = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = F
print(f"RF mesh {F.name}: verts={len(F.data.vertices)} groups={len(F.vertex_groups)} scale={tuple(F.scale)}")
fist_builder.replace_fists(F, basecolor, normal)
fist_builder.render_closeups(F, prefix, 'Left')

bpy.ops.object.select_all(action='DESELECT')
A.select_set(True); F.select_set(True); bpy.context.view_layer.objects.active = A
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         add_leaf_bones=False, bake_anim=False, embed_textures=False, path_mode='COPY',
                         use_armature_deform_only=False)
print("RF exported", dst)
