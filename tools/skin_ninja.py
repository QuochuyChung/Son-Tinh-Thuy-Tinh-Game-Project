"""Ninja (the rooster thieves on Map_SonTinh): put the Meshy textured mesh on the Mixamo rig that came with the animation files.

    blender -b --python tools/skin_ninja.py -- <mixamo_clip.fbx> <textured.fbx> <out_skinned.fbx> <preview_dir>

* mixamo_clip.fbx : any of the Mixamo downloads (armature + the untextured mesh, skinned by Mixamo)
* textured.fbx    : the Meshy textured export of the same model (10571 vertices against Mixamo's 10572: one vertex was merged, so the
                    weights are copied from the nearest vertex of the Mixamo mesh rather than by index)
* out_skinned.fbx : armature (bind pose) + Ninja_Body, no animation, no materials: the model for Unity (NpcBuilder, Humanoid); the clips
                    stay in their own Mixamo files
* preview_dir     : rest.png (front + side + back), weights_far.txt (worst nearest-vertex distances)
"""
import bpy, sys, os, math
import mathutils
from mathutils import Vector
import numpy as np

clip_fbx, tex_fbx, out_fbx, out_dir = sys.argv[sys.argv.index("--") + 1:][:4]
os.makedirs(out_dir, exist_ok=True)
report = []
def log(s): print("NJ", s); report.append(s)

def import_new(path):
    pre = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.context.scene.objects if o not in pre]

bpy.ops.wm.read_factory_settings(use_empty=True)
rig = import_new(clip_fbx)
A = [o for o in rig if o.type == 'ARMATURE'][0]
M = [o for o in rig if o.type == 'MESH'][0]
for a in list(bpy.data.actions): bpy.data.actions.remove(a)   # bind pose only
A.animation_data_clear()
for pb in A.pose.bones: pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
bpy.context.view_layer.update()
T = [o for o in import_new(tex_fbx) if o.type == 'MESH'][0]

mw = [M.matrix_world @ v.co for v in M.data.vertices]
tw = [T.matrix_world @ v.co for v in T.data.vertices]
h = max(p.z for p in mw) - min(p.z for p in mw)
log(f"mixamo mesh {len(mw)} verts, textured {len(tw)} verts, height {h:.4f} / {max(p.z for p in tw) - min(p.z for p in tw):.4f}")
kd = mathutils.kdtree.KDTree(len(mw))
for i, p in enumerate(mw): kd.insert(p, i)
kd.balance()
twin = []; dists = []
for p in tw:
    co, idx, d = kd.find(p); twin.append(idx); dists.append(d)
dists = np.array(dists)
log(f"nearest Mixamo vertex: max {dists.max() / h:.6f} heights, {int((dists > 0.001 * h).sum())} vertices further than 0.001 heights")

# copy the groups vertex by vertex
groups = {g.index: g.name for g in M.vertex_groups}
T.vertex_groups.clear()
for g in M.vertex_groups: T.vertex_groups.new(name=g.name)
for i, j in enumerate(twin):
    for ge in M.data.vertices[j].groups:
        T.vertex_groups[groups[ge.group]].add([i], ge.weight, 'REPLACE')
mod = T.modifiers.new("Armature", 'ARMATURE'); mod.object = A
mw_t = T.matrix_world.copy(); T.parent = A; T.matrix_world = mw_t
T.name = "Ninja_Body"; T.data.name = "Ninja_Body"
bpy.data.objects.remove(M, do_unlink=True)

# preview: rest pose, textured
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.3, 0.3, 0.33)
cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = h * 1.15
cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
zc = min(p.z for p in tw) + h / 2
shots = []
for name, d in (('front', Vector((0, -1, 0))), ('side', Vector((1, 0, 0))), ('back', Vector((0, 1, 0)))):
    cam.location = Vector((0, 0, zc)) + d * 5; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.resolution_x = 500; sc.render.resolution_y = 700
    p = os.path.join(out_dir, f"_rest_{name}.png"); sc.render.filepath = p; bpy.ops.render.render(write_still=True); shots.append(p)
imgs = []
for p in shots:
    im = bpy.data.images.load(p); imgs.append(np.array(im.pixels[:]).reshape(im.size[1], im.size[0], 4)); bpy.data.images.remove(im); os.remove(p)
s_ = np.concatenate(imgs, axis=1)
o = bpy.data.images.new("sheet", s_.shape[1], s_.shape[0], alpha=True)
o.pixels.foreach_set(s_.astype(np.float32).ravel()); o.filepath_raw = os.path.join(out_dir, "rest.png"); o.file_format = 'PNG'; o.save()
cam_obj = cam; bpy.data.objects.remove(cam_obj, do_unlink=True)

T.data.materials.clear()
bpy.ops.object.select_all(action='DESELECT')
for ob in (A, T): ob.select_set(True)
bpy.context.view_layer.objects.active = A
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
                         embed_textures=False, use_armature_deform_only=False)
log(f"exported {out_fbx}")
with open(os.path.join(out_dir, "skin.txt"), "w", encoding="utf-8") as fh: fh.write("\n".join(report) + "\n")
