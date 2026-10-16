"""Cut one banner (pole, crossbar, cloth and tassels) out of the Meshy royal throne FBX and export it as its own FBX (same UVs, so it keeps
the throne's texture).

    blender -b --python tools/extract_banner.py -- <throne.fbx> <banner_out.fbx>

The throne set has two banners on poles standing left and right of the chair (x = -0.34 and +0.34 in the model); the mesh is one object, so the
connected islands whose centre is on the left side and that rise above the dais are kept.
"""
import bpy, bmesh, sys
argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
obj = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bm = bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
seen = set(); keep = set()
for v in bm.verts:
    if v.index in seen: continue
    st = [v]; comp = []; seen.add(v.index)
    while st:
        a = st.pop(); comp.append(a)
        for e in a.link_edges:
            b = e.other_vert(a)
            if b.index not in seen: seen.add(b.index); st.append(b)
    xs = [c.co.x for c in comp]; zs = [c.co.z for c in comp]
    cx = sum(xs) / len(xs)
    if cx < -0.25 and max(zs) > -0.1:                      # the left banner: x well left of the chair, rising above the dais
        keep.update(c.index for c in comp)
kill = [v for v in bm.verts if v.index not in keep]
print("BANNER kept verts", len(keep), "of", len(bm.verts))
bmesh.ops.delete(bm, geom=kill, context='VERTS')
bm.to_mesh(obj.data); bm.free()
xs = [v.co.x for v in obj.data.vertices]; zs = [v.co.z for v in obj.data.vertices]; ys = [v.co.y for v in obj.data.vertices]
print("BANNER bbox x", round(min(xs), 3), round(max(xs), 3), "y", round(min(ys), 3), round(max(ys), 3), "z", round(min(zs), 3), round(max(zs), 3))
obj.data.materials.clear()
for img in list(bpy.data.images): bpy.data.images.remove(img)
for mat in list(bpy.data.materials): bpy.data.materials.remove(mat)
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'MESH'}, embed_textures=False, path_mode='COPY')
print("BANNER exported", dst)
