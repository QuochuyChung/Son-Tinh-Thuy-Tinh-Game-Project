"""Prepare a Meshy prop for Unity: strip the embedded material (the URP material is made in Unity from the textures), optionally cut
the chin strap off the conical hat, and export an FBX of the same mesh and UVs.

    blender -b --python tools/prep_prop.py -- <textured_in.fbx> <out.fbx> [nostrap=1] [faces=N]

faces=N: the mesh is decimated (collapse, UVs kept) to about N faces (the Meshy rocks come with 200 000 - 800 000).
nostrap=1: the strap of the hat (thin islands of the mesh that hang under the rim: the loop and its two knots) is deleted, so a hat on a head
never has a cord through the face.
"""
import bpy, bmesh, sys

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
opt = dict(a.split('=', 1) for a in argv[2:] if '=' in a)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
if opt.get('nostrap') == '1':
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
    zs = [v.co.z for v in bm.verts]; z0, z1 = min(zs), max(zs)
    seen = set(); kill = []
    for v in bm.verts:
        if v.index in seen: continue
        st = [v]; comp = []; seen.add(v.index)
        while st:
            a = st.pop(); comp.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen: seen.add(b.index); st.append(b)
        ys = [c.co.y for c in comp]; zc = [c.co.z for c in comp]
        thin = (max(ys) - min(ys)) < 0.12
        low = min(zc) < z0 + 0.42 * (z1 - z0) and max(zc) < z0 + 0.70 * (z1 - z0)
        xs_ = [c.co.x for c in comp]
        strap = thin and low and (len(comp) >= 200 or (len(comp) >= 100 and max(abs(x_) for x_ in xs_) < 0.1))
        if strap:
            kill.extend(comp)
            print("PREP removed island", len(comp), "x", round(min(c.co.x for c in comp), 2), round(max(c.co.x for c in comp), 2), "y", round(min(ys), 2), round(max(ys), 2), "z", round(min(zc), 2), round(max(zc), 2))
    print("PREP strap islands removed: verts", len(kill), "of", len(bm.verts))
    bmesh.ops.delete(bm, geom=kill, context='VERTS')
    bm.to_mesh(obj.data); bm.free()
if opt.get('faces'):
    target = int(opt['faces']); n0 = len(obj.data.polygons)
    if n0 > target:
        mod = obj.modifiers.new("dec", 'DECIMATE'); mod.decimate_type = 'COLLAPSE'; mod.ratio = target / n0
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    print("PREP faces", n0, "->", len(obj.data.polygons))
obj.data.materials.clear()
for img in list(bpy.data.images): bpy.data.images.remove(img)
for mat in list(bpy.data.materials): bpy.data.materials.remove(mat)
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'MESH'}, embed_textures=False, path_mode='COPY')
print("PREP exported", dst)
