"""Blender: turn the 1.7M-face Meshy lotus into a light model with a baked texture.
    blender -b --python tools/bake_lotus.py -- <src.fbx> <out_dir> [voxel=0.012] [faces=7000] [tex=1024]
Steps: voxel remesh (a clean closed surface), decimate, smart UV, bake the base colour and the normal from the original, export fbx + pngs."""
import bpy, sys, os, bmesh
argv = sys.argv[sys.argv.index('--') + 1:]
src, out = argv[0], argv[1]
opt = dict(a.split('=', 1) for a in argv[2:] if '=' in a)
voxel = float(opt.get('voxel', 0.012)); faces = int(opt.get('faces', 7000)); size = int(opt.get('tex', 1024))
os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
hi = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
hi.name = 'hi'
print('LOTUS hi faces', len(hi.data.polygons), 'dims', tuple(round(x, 3) for x in hi.dimensions))
# the low model: a copy remeshed with voxels, then decimated
lo = hi.copy(); lo.data = hi.data.copy(); lo.name = 'lo'; bpy.context.collection.objects.link(lo)
bpy.context.view_layer.objects.active = lo
for o in bpy.context.scene.objects: o.select_set(o == lo)
lo.data.materials.clear()
rem = lo.modifiers.new('rem', 'REMESH'); rem.mode = 'VOXEL'; rem.voxel_size = voxel; rem.adaptivity = 0.0
bpy.ops.object.modifier_apply(modifier='rem')
print('LOTUS remeshed faces', len(lo.data.polygons))
dec = lo.modifiers.new('dec', 'DECIMATE'); dec.decimate_type = 'COLLAPSE'; dec.ratio = max(0.01, faces / max(1, len(lo.data.polygons)))
bpy.ops.object.modifier_apply(modifier='dec')
print('LOTUS decimated faces', len(lo.data.polygons))
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.1, island_margin=0.004); bpy.ops.object.mode_set(mode='OBJECT')
# bake targets
def image(name, srgb):
    im = bpy.data.images.new(name, size, size, alpha=False); im.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'; return im
mat = bpy.data.materials.new('bake'); mat.use_nodes = True; lo.data.materials.append(mat)
nodes = mat.node_tree.nodes
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 8
sc.render.bake.use_selected_to_active = True; sc.render.bake.cage_extrusion = 0.03; sc.render.bake.max_ray_distance = 0.08; sc.render.bake.margin = 6
for o in bpy.context.scene.objects: o.select_set(False)
hi.select_set(True); lo.select_set(True); bpy.context.view_layer.objects.active = lo
for kind, srgb in (('base', True), ('normal', False)):
    im = image('lotus_' + kind, srgb)
    n = nodes.new('ShaderNodeTexImage'); n.image = im; nodes.active = n
    if kind == 'base':
        sc.render.bake.use_pass_direct = False; sc.render.bake.use_pass_indirect = False; sc.render.bake.use_pass_color = True
        bpy.ops.object.bake(type='DIFFUSE')
    else:
        bpy.ops.object.bake(type='NORMAL')
    im.filepath_raw = os.path.join(out, 'lotus_' + ('basecolor' if kind == 'base' else 'normal') + '.png'); im.file_format = 'PNG'; im.save()
    print('LOTUS baked', kind)
# export the low model only, no material
for o in bpy.context.scene.objects: o.select_set(o == lo)
lo.data.materials.clear()
bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'lotus.fbx'), use_selection=True, apply_unit_scale=True, bake_space_transform=False, axis_forward='-Z', axis_up='Y')
print('LOTUS exported')
