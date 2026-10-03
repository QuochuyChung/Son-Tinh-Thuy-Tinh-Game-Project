import bpy, bmesh, sys, math
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
src, png = argv[0], argv[1]
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
zs = [v.co.z for v in bm.verts]; zmin, zmax = min(zs), max(zs); H = zmax - zmin
order = sorted(range(len(comps)), key=lambda i: -len(comps[i]))
names = ["red","green","blue","yellow","purple","cyan","orange","pink","lime","brown","white","teal"]
pal = [(0.9,0.15,0.15),(0.15,0.7,0.15),(0.2,0.35,0.95),(0.95,0.85,0.1),(0.7,0.25,0.9),(0.1,0.85,0.85),(1,0.5,0.05),(1,0.5,0.7),(0.6,1,0.2),(0.5,0.3,0.1),(1,1,1),(0.0,0.5,0.5)]
col = {}
print("PART idx color verts z%[lo,hi] x[lo,hi] y[lo,hi]")
for rank, i in enumerate(order):
    c = comps[i]; p = [v.co for v in c]
    col[i] = pal[rank % 12]
    print(f"PART {i:3d} {names[rank%12]:7s} n={len(c):4d} z%=[{(min(q.z for q in p)-zmin)/H*100:3.0f},{(max(q.z for q in p)-zmin)/H*100:3.0f}] x=[{min(q.x for q in p):6.3f},{max(q.x for q in p):6.3f}] y=[{min(q.y for q in p):6.3f},{max(q.y for q in p):6.3f}]")
lay = bm.verts.layers.color.new("grp")
for v in bm.verts:
    c = col[seen[v.index]]; v[lay] = (c[0], c[1], c[2], 1)
bm.to_mesh(o.data); bm.free()
o.data.color_attributes.active_color = o.data.color_attributes["grp"]
xs = [v.co.x for v in o.data.vertices]; ys = [v.co.y for v in o.data.vertices]
cx = (min(xs)+max(xs))/2; cy = (min(ys)+max(ys))/2; sp = H*0.85
for k in range(4):
    c = o.copy(); c.data = o.data; bpy.context.scene.collection.objects.link(c)
    pv = Vector((cx, cy, 0))
    rot = Matrix.Translation(pv) @ Matrix.Rotation(math.radians(90*k), 4, 'Z') @ Matrix.Translation(-pv)
    c.matrix_world = Matrix.Translation(Vector((sp*(k-1.5), 0, 0))) @ rot @ o.matrix_world
o.hide_render = True
cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = sp*4.05
cam = bpy.data.objects.new("c", cd); bpy.context.scene.collection.objects.link(cam)
cam.location = (cx, cy - H*4, zmin + H/2); cam.rotation_euler = (math.radians(90), 0, 0)
sc = bpy.context.scene; sc.camera = cam
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'VERTEX'
sc.view_settings.view_transform = 'Standard'
sc.render.resolution_x = 1800; sc.render.resolution_y = 700
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.3, 0.3, 0.33)
sc.render.filepath = png
bpy.ops.render.render(write_still=True)
print("PART rendered")
