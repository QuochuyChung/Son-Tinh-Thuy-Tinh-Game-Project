"""Fit a separately generated garment (skirt + pants + boots) onto a Mixamo-rigged body and skin it with the body's rig.

    blender --background --python tools/fit_garment.py -- <rigged_body.fbx> <garment_textured.fbx> <out_skinned.fbx> <preview_dir> [<run.fbx|->] [key=value ...]

* rigged_body.fbx : a Mixamo download WITH SKIN (e.g. the Idle) of the body mesh; any clip works, only the rig + weights are used
* garment_textured.fbx : the Meshy textured FBX of the garment (keeps its UVs)
* out_skinned.fbx : armature + 2 or 3 skinned meshes (NAME_Body, NAME_Garment, optionally NAME_Mantle), no materials - assign them in Unity
* preview_dir : fit_front.png / fit_side.png (T-pose) and run_*.png (if a run clip is given) are written here
* key=value overrides: sx sy sz (garment scale; sz = garment height as a fraction of the body height, default 0.60),
  widen (extra width of the waistband, default 0.22), cut (the body is deleted below this fraction of its height, default 0.575),
  far/soft (distance, in body heights, where garment vertices stop following the leg bones and follow the thigh, default 0.028/0.03)

How it works: the garment is scaled so its soles sit on the body's soles and its top on the waist, sheared so the waistband
and the boots line up with the body, and the naked legs/shorts of the body (hidden under it) are removed. Weights are copied
from the nearest body surface; garment parts that hang away from the legs (skirt panels) are folded onto the thigh bones so they
do not tear when the knees bend; the middle panel follows both thighs equally.
"""
import bpy, bmesh, sys, math, os
import mathutils
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
body_fbx, garment_fbx, out_fbx, out_dir = argv[:4]
run_fbx = argv[4] if len(argv) > 4 and '=' not in argv[4] and argv[4] != '-' else None
opt = dict(a.split('=') for a in argv[4:] if '=' in a)
SZ = float(opt.get('sz', 0.60)); SX = float(opt.get('sx', SZ)); SY = float(opt.get('sy', SZ))
WIDEN = float(opt.get('widen', 0.22)); CUT = float(opt.get('cut', 0.575))
FAR = float(opt.get('far', 0.028)); SOFT = float(opt.get('soft', 0.03))
THIGH = float(opt.get('thigh', 0.5))      # how much the far-away skirt panels follow the thighs (the rest follows the hips)
NAME = opt.get("name", "ThuyTinh")             # prefix of the exported mesh objects (NAME_Body, NAME_Garment, NAME_Mantle)
os.makedirs(out_dir, exist_ok=True)


def smooth(t): t = min(max(t, 0.0), 1.0); return t * t * (3 - 2 * t)


bpy.ops.wm.read_factory_settings(use_empty=True)

# ---- 1. rigged body ----
bpy.ops.import_scene.fbx(filepath=body_fbx)
A = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
B = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
B.name = f"{NAME}_Body"; B.data.name = f"{NAME}_Body"
A.animation_data_clear()                       # the Mixamo clip is not needed here
A.data.pose_position = 'REST'
bpy.context.view_layer.update()
bnames = {g.index: g.name for g in B.vertex_groups}
bw = [B.matrix_world @ v.co for v in B.data.vertices]
bz0 = min(p.z for p in bw); bz1 = max(p.z for p in bw); BH = bz1 - bz0
def rel(z): return (z - bz0) / BH
print(f"FG body verts={len(bw)} H={BH:.3f} groups={len(bnames)}")
sole_b = [p for p in bw if rel(p.z) < 0.02]
bcx = sum(p.x for p in sole_b) / len(sole_b); bcy = sum(p.y for p in sole_b) / len(sole_b)
waist_b = [p for p in bw if 0.56 <= rel(p.z) <= 0.60 and abs(p.x) < 0.12]
wcx = sum(p.x for p in waist_b) / len(waist_b); wcy = sum(p.y for p in waist_b) / len(waist_b)
print(f"FG body soles centre=({bcx:+.3f},{bcy:+.3f}) waist centre=({wcx:+.3f},{wcy:+.3f})")

# ---- 2. garment ----
before = set(bpy.context.scene.objects)
bpy.ops.import_scene.fbx(filepath=garment_fbx)
G = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o not in before][0]
G.name = f"{NAME}_Garment"; G.data.name = f"{NAME}_Garment"
gw0 = [G.matrix_world @ v.co for v in G.data.vertices]
gz0 = min(p.z for p in gw0); gz1 = max(p.z for p in gw0); GH = gz1 - gz0
def grel(z): return (z - gz0) / GH
sole_g = [p for p in gw0 if grel(p.z) < 0.04]
gcx = sum(p.x for p in sole_g) / len(sole_g); gcy = sum(p.y for p in sole_g) / len(sole_g)
print(f"FG garment verts={len(gw0)} sole centre=({gcx:+.3f},{gcy:+.3f})")

def base(p):
    zr = grel(p.z)
    return Vector((bcx + (p.x - gcx) * SX, bcy + (p.y - gcy) * SY, bz0 + zr * SZ * BH)), zr
top = [base(p)[0] for p in gw0 if grel(p.z) > 0.94]
tcx = sum(p.x for p in top) / len(top); tcy = sum(p.y for p in top) / len(top)
shear = Vector((wcx - tcx, wcy - tcy))
print(f"FG shear of the waistband onto the torso: ({shear.x:+.3f},{shear.y:+.3f})")
fitted = []
for p in gw0:
    q, zr = base(p)
    k = smooth((zr - 0.0) / 1.0) * 0 + zr                     # linear shear from soles (0) to waist (1)
    q.x += shear.x * k; q.y += shear.y * k
    w = 1.0 + WIDEN * smooth((zr - 0.72) / 0.28)               # waistband a little wider so the torso does not poke through
    q.x = wcx + (q.x - wcx) * w; q.y = wcy + (q.y - wcy) * w
    fitted.append(q)
for v, q in zip(G.data.vertices, fitted): v.co = G.matrix_world.inverted() @ q
G.data.update()
print(f"FG garment fitted: height {max(q.z for q in fitted) - min(q.z for q in fitted):.3f}, top at {rel(max(q.z for q in fitted)):.3f} of body height")

# ---- 3. weights: nearest body surface (before the body is cut) ----
bmesh_polys = [tuple(p.vertices) for p in B.data.polygons]
bvh = mathutils.bvhtree.BVHTree.FromPolygons(bw, bmesh_polys)
bweights = [{bnames[g.group]: g.weight for g in v.groups if g.weight > 1e-4} for v in B.data.vertices]
LEGS = {'L': ('LeftLeg', 'LeftFoot', 'LeftToeBase'), 'R': ('RightLeg', 'RightFoot', 'RightToeBase')}
def short(n): return n.split(':')[-1]

def fold_legs(w):
    out = {}
    for b, x in w.items():
        s = short(b)
        for side, grp in LEGS.items():
            if s in grp: b = 'mixamorig:' + ('LeftUpLeg' if side == 'L' else 'RightUpLeg')
        if s.startswith('Left') and s not in ('LeftUpLeg', 'LeftLeg', 'LeftFoot', 'LeftToeBase') and ('Hand' in s or 'Arm' in s or 'Shoulder' in s): b = 'mixamorig:Hips'
        if s.startswith('Right') and s not in ('RightUpLeg', 'RightLeg', 'RightFoot', 'RightToeBase') and ('Hand' in s or 'Arm' in s or 'Shoulder' in s): b = 'mixamorig:Hips'
        out[b] = out.get(b, 0) + x
    return out

gweights = []; dists = []
for q in fitted:
    loc, nrm, idx, dist = bvh.find_nearest(q)
    poly = bmesh_polys[idx]
    ws = []; tot = 0.0
    for vi in poly:
        d = (bw[vi] - loc).length + 1e-5; ws.append((vi, 1.0 / (d * d))); tot += 1.0 / (d * d)
    w = {}
    for vi, k in ws:
        for b, x in bweights[vi].items(): w[b] = w.get(b, 0) + x * k / tot
    f = smooth((dist / BH - FAR) / SOFT)                       # far from the body: follow the thighs instead of the shins/feet
    if f > 0:
        folded = fold_legs(w)
        folded = {b: x * THIGH for b, x in folded.items()}
        folded['mixamorig:Hips'] = folded.get('mixamorig:Hips', 0) + (1 - THIGH)
        # (THIGH = 1: panels fully follow the thighs; 0: fully the hips)
        mixed = {}
        for b in set(w) | set(folded): mixed[b] = (1 - f) * w.get(b, 0) + f * folded.get(b, 0)
        w = mixed
        if abs(q.x - wcx) < 0.03 * BH:                          # middle panel: both thighs equally
            l = w.get('mixamorig:LeftUpLeg', 0); r = w.get('mixamorig:RightUpLeg', 0); a = (l + r) / 2
            if l or r: w['mixamorig:LeftUpLeg'] = a; w['mixamorig:RightUpLeg'] = a
    gweights.append(w); dists.append(dist)
print(f"FG garment vertex distance to body (body heights): min {min(dists)/BH:.3f} median {sorted(dists)[len(dists)//2]/BH:.3f} max {max(dists)/BH:.3f}")

# light smoothing of the weights along the garment's edges (the sheets are thin, neighbours should move alike)
bm = bmesh.new(); bm.from_mesh(G.data); bm.verts.ensure_lookup_table()
for it in range(2):
    new = []
    for i, v in enumerate(bm.verts):
        nb = [e.other_vert(v).index for e in v.link_edges]
        if not nb: new.append(gweights[i]); continue
        acc = {}
        for j in nb + [i, i]:
            for b, x in gweights[j].items(): acc[b] = acc.get(b, 0) + x / (len(nb) + 2)
        new.append(acc)
    gweights = new
bm.free()

# ---- skirt bones (skirtbones=1, default): the panels that hang away from the legs get 8 chains x 2 bones around the hips ----
#   sktop = body-relative height where the panels start to swing (default 0.50; the belt above stays rigid)
#   Bones Skirt_<column>_<segment> (column 0 = front, then round to the character's left, 45 degrees apart) hang from Hips;
#   in Unity OutfitSpringBones sways them gently while the character moves. Only vertices far from the body (f, the same mask that
#   folds panels onto the thighs) and below sktop are handed to the chains, so belt, pants and boots keep their weights.
if opt.get('skirtbones', '1') == '1':
    NSK, SSEG = 8, 2
    SK_TOP = float(opt.get('sktop', 0.50))
    zr = [rel(q.z) for q in fitted]
    # boots: whole pieces that stay below boottop (default 0.27 of the body height) never sway, even though they stand away from the legs
    BOOT_TOP = float(opt.get('boottop', 0.27))
    bmc = bmesh.new(); bmc.from_mesh(G.data); bmc.verts.ensure_lookup_table()
    comp_of = {}; comp_max = []
    for v0 in bmc.verts:
        if v0.index in comp_of: continue
        cid = len(comp_max); st = [v0]; comp_of[v0.index] = cid; mx = -9.0
        while st:
            cur = st.pop(); mx = max(mx, zr[cur.index])
            for e in cur.link_edges:
                o_ = e.other_vert(cur)
                if o_.index not in comp_of: comp_of[o_.index] = cid; st.append(o_)
        comp_max.append(mx)
    bmc.free()
    mask = [smooth((dists[i] / BH - FAR) / SOFT) * smooth((SK_TOP - zr[i]) / 0.05) * (0.0 if comp_max[comp_of[i]] < BOOT_TOP else 1.0) for i in range(len(fitted))]
    sel = [i for i in range(len(fitted)) if mask[i] > 0.5]
    if len(sel) >= 50:
        zs_ = sorted(zr[i] for i in sel); z_bot = zs_[int(len(zs_) * 0.03)]
        step = (SK_TOP - z_bot) / SSEG
        rows = [SK_TOP - k * step for k in range(SSEG + 1)]
        TWO_PI = 2 * math.pi
        def angle_of(q): return math.atan2(q.x - wcx, -(q.y - wcy)) % TWO_PI      # 0 = front (-Y), +90 degrees = the character's left (+X)
        ang = {i: angle_of(fitted[i]) for i in sel}
        rad = {i: math.hypot(fitted[i].x - wcx, fitted[i].y - wcy) for i in sel}
        skj = []                                                                    # skj[row][column] = world position
        prev_r = [0.12 * BH] * NSK
        for k, zk in enumerate(rows):
            row = []
            for c in range(NSK):
                th = c * TWO_PI / NSK
                near = [rad[i] for i in sel if abs((ang[i] - th + math.pi) % TWO_PI - math.pi) < math.pi / NSK * 1.2 and abs(zr[i] - zk) < step * 0.6]
                r = (sum(near) / len(near)) if near else prev_r[c]
                prev_r[c] = r
                row.append(Vector((wcx + r * math.sin(th), wcy - r * math.cos(th), bz0 + zk * BH)))
            skj.append(row)
        bpy.context.view_layer.objects.active = A; A.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        inv = A.matrix_world.inverted(); eb = A.data.edit_bones
        sk_names = {}
        for c in range(NSK):
            prev = eb['mixamorig:Hips']
            for j in range(SSEG):
                nm = f"Skirt_{c}_{j}"; b = eb.new(nm)
                b.head = inv @ skj[j][c]; b.tail = inv @ skj[j + 1][c]
                b.parent = prev; b.use_connect = False
                prev = b; sk_names[(c, j)] = nm
        bpy.ops.object.mode_set(mode='OBJECT')
        handed = 0
        for i in range(len(fitted)):
            m = mask[i]
            if m <= 0.001: continue
            s = min(max((SK_TOP - zr[i]) / step, 0.0), float(SSEG))
            wj = [max(0.0, 1.0 - abs(s - (j + 0.5))) for j in range(SSEG)]
            if s > SSEG - 0.5: wj = [0.0] * (SSEG - 1) + [1.0]
            tot = sum(wj)
            if tot <= 0: continue
            u = angle_of(fitted[i]) / (TWO_PI / NSK); a = int(u) % NSK; alpha = u - int(u); b2 = (a + 1) % NSK
            part = {}
            for j in range(SSEG):
                if wj[j] <= 0: continue
                part[sk_names[(a, j)]] = part.get(sk_names[(a, j)], 0) + (1 - alpha) * wj[j] * m
                part[sk_names[(b2, j)]] = part.get(sk_names[(b2, j)], 0) + alpha * wj[j] * m
            give = m * tot
            old = gweights[i]
            new_w = {b: x * (1 - give) for b, x in old.items()}
            for b, x in part.items(): new_w[b] = new_w.get(b, 0) + x
            gweights[i] = new_w; handed += 1
        print(f"FG skirt bones: {NSK} columns x {SSEG} segments under Hips, rows (body-relative z) {[round(r, 3) for r in rows]}, {len(sel)} panel vertices (mask > 0.5), {handed} vertices got chain weights")
    else:
        print("FG skirt bones: no skirt panels found (fewer than 50 vertices far from the body), skipped")

G.vertex_groups.clear()
for b in A.data.bones: G.vertex_groups.new(name=b.name)
for i, w in enumerate(gweights):
    top4 = sorted(w.items(), key=lambda t: -t[1])[:4]; s = sum(x for _, x in top4) or 1.0
    for b, x in top4: G.vertex_groups[b].add([i], x / s, 'REPLACE')
mod = G.modifiers.new("Armature", 'ARMATURE'); mod.object = A
G.parent = A; G.matrix_parent_inverse = A.matrix_world.inverted()

# ---- 3b. optional back piece (feather mantle / cape): mantle=<textured.fbx> ----
#   mlen    length of the mantle as a fraction of the body height (default 0.58)
#   mwidth  shoulder width of the mantle relative to the distance between the shoulder joints (default 1.4)
#   mmargin clearance behind the back/hair in body heights (default 0.012)
#   Weights: nearest body surface, but arms/hands/shoulders -> Spine2, legs -> Hips, head -> Neck (the cape must follow the torso,
#   never swing with the limbs); pieces under 120 vertices (single feathers) get one rigid weight set so they do not tear.
Mn = None
if opt.get('mantle'):
    MLEN = float(opt.get('mlen', 0.58)); MWIDTH = float(opt.get('mwidth', 1.4)); MMARGIN = float(opt.get('mmargin', 0.012))
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=opt['mantle'])
    Mn = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o not in before][0]
    Mn.name = f"{NAME}_Mantle"; Mn.data.name = f"{NAME}_Mantle"
    mw0 = [Mn.matrix_world @ v.co for v in Mn.data.vertices]
    mz0 = min(p.z for p in mw0); mz1 = max(p.z for p in mw0); MH = mz1 - mz0
    def mrel(z): return (z - mz0) / MH
    def bone_w(n): return A.matrix_world @ A.data.bones['mixamorig:' + n].head_local
    neck = bone_w('Neck'); la = bone_w('LeftArm'); ra = bone_w('RightArm')
    shoulder_w = abs(la.x - ra.x)
    ring = [p for p in mw0 if mrel(p.z) > 0.96]
    rcx = sum(p.x for p in ring) / len(ring); rcy = sum(p.y for p in ring) / len(ring)
    band = [p for p in mw0 if 0.80 <= mrel(p.z) <= 0.92]
    mband_w = max(p.x for p in band) - min(p.x for p in band)
    ms = MLEN * BH / MH
    msx = min(max(MWIDTH * shoulder_w / (mband_w * ms), 0.8), 1.8)
    print(f"FG mantle verts={len(mw0)} scale={ms:.3f} width factor={msx:.2f} (shoulder joints {shoulder_w:.3f}, mantle shoulder band {mband_w*ms*msx:.3f}) neck=({neck.x:+.3f},{neck.y:+.3f},{rel(neck.z):.3f})")
    # keep the mantle outside the body: a vertex that is inside the skin, or closer than the margin, is pushed out along the surface normal
    # (the body normals may point either way after the import: test with a point that is certainly inside, the Spine1 joint)
    ref = bone_w('Spine1'); l0, n0, i0, d0 = bvh.find_nearest(ref)
    outward = 1.0 if (ref - l0).dot(n0) < 0 else -1.0     # inside point behind the surface normal => normals point outward
    mfit = []; pushed = 0
    for p in mw0:
        q = Vector((neck.x + (p.x - rcx) * ms * msx, neck.y + (p.y - rcy) * ms, neck.z + 0.02 * BH - (mz1 - p.z) * ms))
        loc, nrm, idx, dist = bvh.find_nearest(q)
        if loc is not None:
            inside = ((q - loc).dot(nrm) * outward) < 0
            if inside or dist < MMARGIN * BH:
                q = loc + nrm * outward * (MMARGIN * BH); pushed += 1
        mfit.append(q)
    for v, q in zip(Mn.data.vertices, mfit): v.co = Mn.matrix_world.inverted() @ q
    Mn.data.update()
    print(f"FG mantle fitted: top at {rel(max(q.z for q in mfit)):.3f}, hem at {rel(min(q.z for q in mfit)):.3f} of body height; {pushed} vertices pushed out of the body")

    def fold_mantle(w):
        out = {}
        for b, x in w.items():
            s_ = short(b)
            if 'Leg' in s_ or 'Foot' in s_ or 'Toe' in s_: b = 'mixamorig:Hips'
            elif 'Arm' in s_ or 'Hand' in s_ or 'Shoulder' in s_: b = 'mixamorig:Spine2'
            elif s_.startswith('Head'): b = 'mixamorig:Neck'
            out[b] = out.get(b, 0) + x
        return out
    mweights = []
    for q in mfit:
        loc, nrm, idx, dist = bvh.find_nearest(q)
        poly = bmesh_polys[idx]; ws = []; tot = 0.0
        for vi in poly:
            d = (bw[vi] - loc).length + 1e-5; ws.append((vi, 1.0 / (d * d))); tot += 1.0 / (d * d)
        w = {}
        for vi, k_ in ws:
            for b, x in bweights[vi].items(): w[b] = w.get(b, 0) + x * k_ / tot
        mweights.append(fold_mantle(w))
    bm = bmesh.new(); bm.from_mesh(Mn.data); bm.verts.ensure_lookup_table()
    seen = {}; comps = []
    for v in bm.verts:
        if v.index in seen: continue
        cid = len(comps); st = [v]; seen[v.index] = cid; mem = []
        while st:
            cur = st.pop(); mem.append(cur.index)
            for e in cur.link_edges:
                o_ = e.other_vert(cur)
                if o_.index not in seen: seen[o_.index] = cid; st.append(o_)
        comps.append(mem)
    bm.free()
    rigid = 0
    for mem in comps:
        if len(mem) < 120:
            acc = {}
            for i in mem:
                for b, x in mweights[i].items(): acc[b] = acc.get(b, 0) + x / len(mem)
            for i in mem: mweights[i] = acc
            rigid += 1
    print(f"FG mantle: {len(comps)} pieces, {rigid} of them rigid (single feathers)")

    # ---- cape bones (capebones=1, default): 5 chains x 3 bones hanging from Spine2, soft skinning, driven by a spring script in Unity ----
    # Unity Cloth did not work on this mesh (185 loose feathers collapse into strips), so the flutter comes from bones: Cape_<column>_<segment>.
    if opt.get('capebones', '1') == '1':
        NCOL, NSEG = 5, 3
        T_LEV = [0.14, 0.38, 0.64, 1.0]                    # joint heights as a fraction of the mantle length; the top 14 % is rigid (collar / shoulders)
        z_top = max(q.z for q in mfit); z_hem = min(q.z for q in mfit); Hm = z_top - z_hem
        tv = [(z_top - q.z) / Hm for q in mfit]
        joints = []                                         # joints[level][column] = world position
        for k, tk in enumerate(T_LEV):
            tol = 0.05 if k < NSEG else 0.04
            sel = [i for i in range(len(mfit)) if abs(tv[i] - tk) < tol] or [min(range(len(mfit)), key=lambda i: abs(tv[i] - tk))]
            xs_ = [mfit[i].x for i in sel]; xa, xb = min(xs_), max(xs_); span = max(xb - xa, 0.02)
            row = []
            for c in range(NCOL):
                xc = xa + span * (c + 0.5) / NCOL
                near = [mfit[i].y for i in sel if abs(mfit[i].x - xc) < span / NCOL] or [mfit[i].y for i in sel]
                row.append(Vector((xc, sum(near) / len(near), z_top - tk * Hm)))
            joints.append(row)
        bpy.context.view_layer.objects.active = A; A.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        inv = A.matrix_world.inverted(); eb = A.data.edit_bones
        cape_names = {}
        for c in range(NCOL):
            prev = eb['mixamorig:Spine2']
            for j in range(NSEG):
                nm = f"Cape_{c}_{j}"; b = eb.new(nm)
                b.head = inv @ joints[j][c]; b.tail = inv @ joints[j + 1][c]
                b.parent = prev; b.use_connect = False
                prev = b; cape_names[(c, j)] = nm
        bpy.ops.object.mode_set(mode='OBJECT')
        def x_at(c, t):
            if t <= T_LEV[0]: return joints[0][c].x
            k = 0
            while k < NSEG - 1 and t > T_LEV[k + 1]: k += 1
            f = min((t - T_LEV[k]) / (T_LEV[k + 1] - T_LEV[k]), 1.0)
            return joints[k][c].x * (1 - f) + joints[k + 1][c].x * f
        mweights = []
        for i, q in enumerate(mfit):
            t = tv[i]
            if t <= T_LEV[0]: s = -(T_LEV[0] - t) / T_LEV[0]
            else:
                k = 0
                while k < NSEG - 1 and t > T_LEV[k + 1]: k += 1
                s = k + min((t - T_LEV[k]) / (T_LEV[k + 1] - T_LEV[k]), 1.0)
            wj = [max(0.0, 1.0 - abs(s - (j + 0.5))) for j in range(NSEG)]
            if s > NSEG - 0.5: wj = [0.0] * (NSEG - 1) + [1.0]
            anchor = max(0.0, 1.0 - sum(wj))
            xs_c = [x_at(c, t) for c in range(NCOL)]
            if q.x <= xs_c[0]: a, alpha = 0, 0.0
            elif q.x >= xs_c[-1]: a, alpha = NCOL - 2, 1.0
            else:
                a = max(c for c in range(NCOL - 1) if xs_c[c] <= q.x); alpha = (q.x - xs_c[a]) / max(xs_c[a + 1] - xs_c[a], 1e-6)
            w = {}
            if anchor > 0: w['mixamorig:Spine2'] = anchor
            for j in range(NSEG):
                if wj[j] <= 0: continue
                w[cape_names[(a, j)]] = w.get(cape_names[(a, j)], 0) + (1 - alpha) * wj[j]
                w[cape_names[(a + 1, j)]] = w.get(cape_names[(a + 1, j)], 0) + alpha * wj[j]
            mweights.append(w)
        print(f"FG cape bones: {NCOL} columns x {NSEG} segments under Spine2; joint rows (z rel. to body): {[round(rel(joints[k][0].z), 3) for k in range(len(joints))]}, mantle x range at the hem {joints[-1][0].x:+.3f}..{joints[-1][-1].x:+.3f}")
    Mn.vertex_groups.clear()
    for b in A.data.bones: Mn.vertex_groups.new(name=b.name)
    for i, w in enumerate(mweights):
        top4 = sorted(w.items(), key=lambda t: -t[1])[:4]; s_ = sum(x for _, x in top4) or 1.0
        for b, x in top4: Mn.vertex_groups[b].add([i], x / s_, 'REPLACE')
    mod = Mn.modifiers.new("Armature", 'ARMATURE'); mod.object = A
    Mn.parent = A; Mn.matrix_parent_inverse = A.matrix_world.inverted()

# ---- 4. remove the naked legs / shorts / feet of the body: they are hidden inside the garment ----
bm = bmesh.new(); bm.from_mesh(B.data); bm.verts.ensure_lookup_table()
kill = [v for v in bm.verts if rel((B.matrix_world @ v.co).z) < CUT]
bmesh.ops.delete(bm, geom=kill, context='VERTS')
loose = [v for v in bm.verts if not v.link_faces]
if loose: bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.to_mesh(B.data); print(f"FG body: removed {len(kill)} vertices below {CUT} of its height, {len(B.data.vertices)} left"); bm.free()

# ---- 5. previews ----
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'OBJECT'
sc.view_settings.view_transform = 'Standard'
B.color = (0.86, 0.68, 0.58, 1); G.color = (0.25, 0.42, 0.85, 1)
if Mn is not None: Mn.color = (0.2, 0.38, 0.26, 1)
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.22, 0.22, 0.25)
sc.render.resolution_x = 700; sc.render.resolution_y = 1000
cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = BH * 1.15
cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
cz = bz0 + BH / 2
def shot(path, loc, rot, scale=None):
    if scale: cd.ortho_scale = scale
    cam.location = loc; cam.rotation_euler = tuple(math.radians(a) for a in rot)
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)
shot(os.path.join(out_dir, "fit_front.png"), (0, -4, cz), (90, 0, 0))
shot(os.path.join(out_dir, "fit_side.png"), (4, 0, cz), (90, 0, 90))
print("FG rest previews written")

if run_fbx:
    pre = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=run_fbx)
    new = [o for o in bpy.context.scene.objects if o not in pre]
    src_arm = [o for o in new if o.type == 'ARMATURE'][0]
    act = src_arm.animation_data.action
    for o in new: o.hide_render = True; o.hide_viewport = True
    A.data.pose_position = 'POSE'
    A.animation_data_create(); A.animation_data.action = act
    try: A.animation_data.action_slot = act.slots[0]
    except Exception as e: print("FG slot note:", e)
    last = int(act.frame_range[1])
    import numpy as np
    A.location = (0, 0, 0); A.data.pose_position = 'REST'; bpy.context.view_layer.update()
    rest_hips = (A.matrix_world @ A.data.bones['mixamorig:Hips'].head_local).copy()
    A.data.pose_position = 'POSE'
    hips1 = None
    frames = sorted(set([1, max(1, last // 4), max(1, last // 2), max(1, (3 * last) // 4)]))
    sheets = {'side': [], 'front': []}
    for f in frames:
        sc.frame_set(f); A.location = (0, 0, 0); bpy.context.view_layer.update()
        hips = (A.matrix_world @ A.pose.bones['mixamorig:Hips'].matrix.translation).copy()
        if hips1 is None: hips1 = hips.copy()
        # keep the character in place: remove the run's travel, keep the vertical bob
        A.location = Vector((rest_hips.x - hips.x, rest_hips.y - hips.y, rest_hips.z - hips1.z)); bpy.context.view_layer.update()
        for view, loc, rot in (('side', (4, 0, cz), (90, 0, 90)), ('front', (0, -4, cz), (90, 0, 0))):
            p = os.path.join(out_dir, f"_{view}_{f:02d}.png"); shot(p, loc, rot)
            im = bpy.data.images.load(p); sheets[view].append(np.array(im.pixels[:]).reshape(im.size[1], im.size[0], 4)); bpy.data.images.remove(im); os.remove(p)
    for view, imgs in sheets.items():
        sheet = np.concatenate(imgs, axis=1)
        out = bpy.data.images.new(f"sheet_{view}", sheet.shape[1], sheet.shape[0], alpha=True)
        out.pixels.foreach_set(sheet.astype(np.float32).ravel()); out.filepath_raw = os.path.join(out_dir, f"run_{view}.png"); out.file_format = 'PNG'; out.save()
    print("FG run sheets written, frames", frames, "of", last)
    A.animation_data_clear(); A.location = (0, 0, 0)
    for o in new: bpy.data.objects.remove(o, do_unlink=True)

# ---- 6. export (armature + both meshes, no materials, bind pose) ----
A.animation_data_clear(); A.location = (0, 0, 0)
for pb in A.pose.bones:
    pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
A.data.pose_position = 'REST'
bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
for o in [x for x in (B, G, Mn) if x is not None]: o.data.materials.clear()
for img in list(bpy.data.images): bpy.data.images.remove(img) if img.users == 0 else None
bpy.ops.object.select_all(action='DESELECT')
for o in [x for x in (A, B, G, Mn) if x is not None]: o.select_set(True)
bpy.context.view_layer.objects.active = A
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         add_leaf_bones=False, bake_anim=False, embed_textures=False, path_mode='COPY',
                         use_armature_deform_only=False)
print("FG exported", out_fbx)
