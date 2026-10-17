# Blender: procedural low-poly grass tuft (curved double-width blades, ~700 tris). Usage: blender -b --python make_grass_tuft.py -- out.fbx
import bpy, bmesh, math, random, sys
out = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
rng = random.Random(11)
bm = bmesh.new(); uv = bm.loops.layers.uv.new('UVMap')
SEG = 5
for i in range(46):
    yaw = rng.uniform(0, math.tau); rad = abs(rng.gauss(0, 0.22)) ** 1.0
    ox, oy = math.cos(yaw) * rad, math.sin(yaw) * rad
    h = rng.uniform(0.45, 1.0) * (1.15 - rad * 0.6); w = rng.uniform(0.035, 0.06)
    lean = rng.uniform(0.15, 0.6) + rad * 0.6; face = yaw + rng.uniform(-0.5, 0.5)
    dx, dy = math.cos(face), math.sin(face); px, py = -dy, dx
    rows = []
    for s in range(SEG + 1):
        t = s / SEG; bend = lean * t * t * h
        cx, cy, cz = ox + dx * bend, oy + dy * bend, h * t * (1 - 0.18 * lean * t)
        ww = w * (1 - t) ** 0.8 + 0.002
        rows.append((bm.verts.new((cx - px * ww, cy - py * ww, cz)), bm.verts.new((cx + px * ww, cy + py * ww, cz)), t))
    for s in range(SEG):
        a, b, t0 = rows[s]; c, d, t1 = rows[s + 1]
        f = bm.faces.new((a, b, d, c))
        for l, (u, v) in zip(f.loops, [(0.2, t0), (0.8, t0), (0.8, t1), (0.2, t1)]): l[uv].uv = (u, v)
bmesh.ops.triangulate(bm, faces=bm.faces[:])
me = bpy.data.meshes.new('grass_tuft'); bm.to_mesh(me); bm.free()
o = bpy.data.objects.new('grass_tuft', me); bpy.context.scene.collection.objects.link(o)
bpy.context.view_layer.objects.active = o; o.select_set(True)
print('TUFT faces', len(me.polygons), 'dims', tuple(round(x, 2) for x in o.dimensions))
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_unit_scale=True, bake_space_transform=False, axis_forward='-Z', axis_up='Y')
