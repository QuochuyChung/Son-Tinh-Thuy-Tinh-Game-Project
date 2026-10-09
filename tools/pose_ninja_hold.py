"""Ninja leader holding the rooster: the Mixamo Idle with both arms replaced by a hug in front of the chest (Mixamo has no carry clip).

    blender -b --python tools/pose_ninja_hold.py -- <mixamo_idle.fbx> <out_clip.fbx> <preview_dir>

The body keeps the Idle's breathing and weight shifts; the arms are posed every frame by direction (world space, the model faces -Y,
its left is +X): upper arms forward-down and a little out, forearms folded in towards the middle, hands under the bird. A small sway is
added so the hold is alive (the rooster struggles: Fly clip in Unity). Exported as a Mixamo-style FBX (armature + mesh + one action), so
Unity imports it like the other clips (NpcBuilder, own avatar). Preview: hold.png (front + side, two frames).
"""
import bpy, sys, os, math
from mathutils import Vector, Matrix, Quaternion
import numpy as np

idle_fbx, out_fbx, out_dir = sys.argv[sys.argv.index("--") + 1:][:3]
os.makedirs(out_dir, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=idle_fbx)
A = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
act = A.animation_data.action
sc = bpy.context.scene
f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
sc.frame_start, sc.frame_end = f0, f1
sc.render.fps = 30

ARMS = {s: [f"mixamorig:{s}Shoulder", f"mixamorig:{s}Arm", f"mixamorig:{s}ForeArm", f"mixamorig:{s}Hand"] for s in ("Left", "Right")}
# drop the Idle's own arm curves (the hold replaces them), keep everything else
def curves(a):
    try: return a.fcurves
    except AttributeError: return [fc for l in a.layers for s in l.strips for cb in s.channelbags for fc in cb.fcurves]
def remove_curve(a, fc):
    try: a.fcurves.remove(fc); return
    except AttributeError: pass
    for l in a.layers:
        for s in l.strips:
            for cb in s.channelbags:
                if fc in list(cb.fcurves): cb.fcurves.remove(fc); return
names = set(n for v in ARMS.values() for n in v[1:])
for fc in list(curves(act)):
    if any(f'"{n}"' in fc.data_path for n in names): remove_curve(act, fc)

pb = A.pose.bones
inv = A.matrix_world.inverted().to_3x3().normalized()
def aim(name, direction, twist_up=None):
    """Turn a pose bone so its head->tail points along `direction` (world)."""
    b = pb[name]
    m = b.matrix.copy()                                   # armature space, current pose
    cur = (m.to_3x3() @ Vector((0, 1, 0))).normalized()
    want = (inv @ direction.normalized()).normalized()
    q = cur.rotation_difference(want)
    rot = q.to_matrix() @ m.to_3x3()
    b.matrix = Matrix.Translation(m.translation) @ rot.to_4x4()
    bpy.context.view_layer.update()

def pose(frame):
    t = (frame - f0) / 30.0
    sway = 0.06 * math.sin(2 * math.pi * t * 1.3)          # the bird pushes the arms a little
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        sh, up, fo, ha = ARMS[side]
        aim(up, Vector((0.32 * sx, -0.62, -0.72 + sway)))
        aim(fo, Vector((-0.80 * sx, -0.55, 0.12 + sway * 0.5)))
        aim(ha, Vector((-0.85 * sx, -0.35, 0.30)))
frames = list(range(f0, f1 + 1))
for f in frames:
    sc.frame_set(f)
    for side in ARMS.values():
        for n in side[1:]: pb[n].rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()
    pose(f)
    for side in ARMS.values():
        for n in side[1:]:
            pb[n].keyframe_insert("rotation_quaternion", frame=f)
act.name = "HoldRooster"

# preview
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.3, 0.3, 0.33)
mesh = [o for o in sc.objects if o.type == 'MESH'][0]
sc.frame_set(f0)
_ev = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get()); _m = _ev.to_mesh()
_pts = [mesh.matrix_world @ v.co for v in _m.vertices]; _ev.to_mesh_clear()
zs = [p.z for p in _pts]; h = max(zs) - min(zs); zc = min(zs) + h / 2
xc = sum(p.x for p in _pts) / len(_pts); yc = sum(p.y for p in _pts) / len(_pts)
cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = h * 1.1
cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
sc.render.resolution_x = 420; sc.render.resolution_y = 560
shots = []
for f in (f0, (f0 + f1) // 2):
    sc.frame_set(f)
    for name, d in (('front', Vector((0, -1, 0))), ('side', Vector((1, 0, 0))), ('q', Vector((0.7, -0.7, 0.2)).normalized())):
        cam.location = Vector((xc, yc, zc)) + d * 5; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        p = os.path.join(out_dir, f"_h_{f}_{name}.png"); sc.render.filepath = p; bpy.ops.render.render(write_still=True); shots.append(p)
imgs = []
for p in shots:
    im = bpy.data.images.load(p); imgs.append(np.array(im.pixels[:]).reshape(im.size[1], im.size[0], 4)); bpy.data.images.remove(im); os.remove(p)
s_ = np.concatenate(imgs, axis=1)
o = bpy.data.images.new("sheet", s_.shape[1], s_.shape[0], alpha=True)
o.pixels.foreach_set(s_.astype(np.float32).ravel()); o.filepath_raw = os.path.join(out_dir, "hold.png"); o.file_format = 'PNG'; o.save()
bpy.data.objects.remove(cam, do_unlink=True)

sc.frame_set(f0)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=True,
                         bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                         bake_anim_simplify_factor=0.0, embed_textures=False)
print("NJ exported", out_fbx)
