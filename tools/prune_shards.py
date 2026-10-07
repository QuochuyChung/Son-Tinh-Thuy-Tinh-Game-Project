"""make_proxy.py <in_notex.fbx> <out.fbx> <min_verts> <drop_cape 0/1> <render.png>
Deletes small loose pieces (components with < min_verts vertices); optionally also the cape component.
Exports an FBX (same method as strip_material.py) and renders 4 views of what is left."""
import bpy, bmesh, sys, math
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst, min_verts, drop_cape, png = argv[0], argv[1], int(argv[2]), int(argv[3]), argv[4]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
o = [x for x in bpy.context.scene.objects if x.type == 'MESH'][0]
bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()

seen = {}; comps = []
for v in bm.verts:
    if v.index in seen: continue
    cid = len(comps); stack = [v]; seen[v.index] = cid; members = []
    while stack:
        cur = stack.pop(); members.append(cur)
        for e in cur.link_edges:
            w = e.other_vert(cur)
            if w.index not in seen: seen[w.index] = cid; stack.append(w)
    comps.append(members)

zs = [v.co.z for v in bm.verts]; zmin, zmax = min(zs), max(zs); H = zmax - zmin
kill = []
kept_small = 0
for c in comps:
    n = len(c)
    pts = [v.co for v in c]
    zlo = (min(p.z for p in pts) - zmin) / H; zhi = (max(p.z for p in pts) - zmin) / H
    width = max(p.x for p in pts) - min(p.x for p in pts)
    is_cape = n >= 300 and zlo < 0.10 and zhi > 0.80 and width > 0.45 * 1.0   # the one big sheet from the shoulders to the ankles
    if n < min_verts or (drop_cape and is_cape):
        kill.extend(c)
print(f"PROXY comps={len(comps)} deleting_verts={len(kill)} of {len(bm.verts)}")
bmesh.ops.delete(bm, geom=kill, context='VERTS')
# drop verts left without faces
loose = [v for v in bm.verts if not v.link_faces]
if loose: bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.to_mesh(o.data)
print(f"PROXY remaining verts={len(bm.verts)} faces={len(bm.faces)}")
bm.free()

o.data.materials.clear()
for img in list(bpy.data.images): bpy.data.images.remove(img)
for mat in list(bpy.data.materials): bpy.data.materials.remove(mat)

# export exactly like strip_material.py
bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
bpy.context.view_layer.objects.active = o
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, embed_textures=False, path_mode='COPY', add_leaf_bones=False)
print("PROXY exported", dst)

# render 4 views (flat grey, studio light)
xs = [v.co.x for v in o.data.vertices]; ys = [v.co.y for v in o.data.vertices]
cx = (min(xs)+max(xs))/2; cy = (min(ys)+max(ys))/2; spacing = H * 0.85
for k in range(4):
    c = o.copy(); c.data = o.data
    bpy.context.scene.collection.objects.link(c)
    pivot = Vector((cx, cy, 0))
    rot = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(90*k), 4, 'Z') @ Matrix.Translation(-pivot)
    c.matrix_world = Matrix.Translation(Vector((spacing*(k-1.5), 0, 0))) @ rot @ o.matrix_world
o.hide_render = True
cam_data = bpy.data.cameras.new("cam"); cam_data.type = 'ORTHO'; cam_data.ortho_scale = spacing*4.05
cam = bpy.data.objects.new("cam", cam_data); bpy.context.scene.collection.objects.link(cam)
cam.location = (cx, cy - H*4, zmin + H/2); cam.rotation_euler = (math.radians(90), 0, 0)
sc = bpy.context.scene; sc.camera = cam
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'
sc.display.shading.single_color = (0.75, 0.75, 0.78)
sc.view_settings.view_transform = 'Standard'
sc.render.resolution_x = 1800; sc.render.resolution_y = 700
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.22, 0.22, 0.25)
sc.render.filepath = png
bpy.ops.render.render(write_still=True)
print("PROXY rendered", png)
