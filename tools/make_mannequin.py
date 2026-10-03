"""Build a watertight 'mannequin' (head/torso/arms/hands/pants/boots only) from the pruned no-cape proxy,
so Mixamo's auto-rigger gets one clean continuous body. Weights are transferred to the full mesh afterwards."""
import bpy, bmesh, sys, math
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst, png = argv[0], argv[1], argv[2]
KEEP = {0, 15, 13, 9, 18, 3, 10, 7, 19, 22, 11}   # indices from proxy_parts.py on son_tinh_v2_rig_nocape_notex.fbx
VOXEL = float(argv[3]) if len(argv) > 3 else 0.010
THICK = float(argv[4]) if len(argv) > 4 else 0.012

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
o = [x for x in bpy.context.scene.objects if x.type == 'MESH'][0]
bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
seen = {}; comps = []
for v in bm.verts:
    if v.index in seen: continue
    cid = len(comps); st = [v]; seen[v.index] = cid; mem = []
    while st:
        cur = st.pop(); mem.append(cur)
        for e in cur.link_edges:
            w = e.other_vert(cur)
            if w.index not in seen: seen[w.index] = cid; st.append(w)
    comps.append(mem)
kill = [v for i, c in enumerate(comps) if i not in KEEP for v in c]
bmesh.ops.delete(bm, geom=kill, context='VERTS')
bm.to_mesh(o.data); bm.free()
o.data.materials.clear()
print(f"MAN kept verts={len(o.data.vertices)} faces={len(o.data.polygons)}")

bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
bpy.context.view_layer.objects.active = o
mod = o.modifiers.new("sol", 'SOLIDIFY'); mod.thickness = THICK; mod.offset = 0.0
bpy.ops.object.modifier_apply(modifier="sol")
o.data.remesh_voxel_size = VOXEL
bpy.ops.object.voxel_remesh()
print(f"MAN remeshed verts={len(o.data.vertices)} faces={len(o.data.polygons)}")
bpy.ops.object.shade_smooth()

# connectivity + height report
bm = bmesh.new(); bm.from_mesh(o.data)
seen = set(); n_comp = 0
for v in bm.verts:
    if v.index in seen: continue
    n_comp += 1; st = [v]; seen.add(v.index)
    while st:
        cur = st.pop()
        for e in cur.link_edges:
            w = e.other_vert(cur)
            if w.index not in seen: seen.add(w.index); st.append(w)
zs = [v.co.z for v in bm.verts]
print(f"MAN components={n_comp} boundary_edges={sum(1 for e in bm.edges if e.is_boundary)} height={max(zs)-min(zs):.3f} zmin={min(zs):.3f}")
bm.free()

bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, embed_textures=False, path_mode='COPY', add_leaf_bones=False)
print("MAN exported", dst)

xs = [v.co.x for v in o.data.vertices]; ys = [v.co.y for v in o.data.vertices]; zs = [v.co.z for v in o.data.vertices]
H = max(zs) - min(zs); cx = (min(xs)+max(xs))/2; cy = (min(ys)+max(ys))/2; sp = H*0.85
for k in range(4):
    c = o.copy(); c.data = o.data; bpy.context.scene.collection.objects.link(c)
    pv = Vector((cx, cy, 0))
    rot = Matrix.Translation(pv) @ Matrix.Rotation(math.radians(90*k), 4, 'Z') @ Matrix.Translation(-pv)
    c.matrix_world = Matrix.Translation(Vector((sp*(k-1.5), 0, 0))) @ rot @ o.matrix_world
o.hide_render = True
cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = sp*4.05
cam = bpy.data.objects.new("c", cd); bpy.context.scene.collection.objects.link(cam)
cam.location = (cx, cy - H*4, min(zs) + H/2); cam.rotation_euler = (math.radians(90), 0, 0)
sc = bpy.context.scene; sc.camera = cam
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'
sc.display.shading.single_color = (0.75, 0.75, 0.78); sc.view_settings.view_transform = 'Standard'
sc.render.resolution_x = 1800; sc.render.resolution_y = 700
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.22, 0.22, 0.25)
sc.render.filepath = png
bpy.ops.render.render(write_still=True)
print("MAN rendered")
