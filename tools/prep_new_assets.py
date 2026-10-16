r"""Prepare the Meshy bridge, rocks and gems of D:\...ssets (Cau-go, Da-assets, Da-quy-do, Da-quy-xanh) for Unity: decimated FBX without material,
base colour + normal copied, metallic + roughness packed. Output: Assets/Art/Props/<name>.fbx + <name>_basecolor / _normal / _metallic_smoothness.png.

    python tools/prep_new_assets.py [source_root]
"""
import os, subprocess, shutil, sys, glob
ROOT = sys.argv[1] if len(sys.argv) > 1 else r"D:\ForStudies\FPTU_Season\Season07\PRU213\assets"
BLENDER = r"D:\Tools\Blender\blender.exe"
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "Assets", "Art", "Props")
# (name, folder, stem prefix, faces)
JOBS = [("bridge", "Cau-go", "Meshy_AI_Wooden_plank_bridge", 0), ("rock_slate", "Da-assets", "Meshy_AI_Ancient_Slate_Slab", 8000),
        ("rock_kaya", "Da-assets", "Meshy_AI_Kaya3", 0), ("rock_monolith", "Da-assets", "Meshy_AI_Monolithic_Fragment", 12000),
        ("rock_formation", "Da-assets", "Meshy_AI_Rock_Formation", 12000), ("rock_fragment", "Da-assets", "Meshy_AI_Stone_Fragment", 6000),
        ("rock_big", "Da-assets", "Meshy_AI_rock_", 12000), ("rock_tundra", "Da-assets", "Meshy_AI_\u82d4\u539f", 12000),
        ("rock_cave", "Da-assets", "Meshy_AI_\u98ce\u683c\u5316\u5927\u578b\u5c71\u6d1e", 14000), ("gem_red", "Da-quy-do", "Meshy_AI__", 0),
        ("crystal_blue", "Da-quy-xanh", "Meshy_AI_Azure_Crystal_Spire", 0),
        ("watchtower", "Ngon-hai-dang", "Meshy_AI_Sunspire_Watchtower", 0)]
os.makedirs(OUT, exist_ok=True)
for name, folder, prefix, faces in JOBS:
    dirs = [d for d in glob.glob(os.path.join(ROOT, folder, prefix + "*_texture_fbx"))]
    assert dirs, (name, prefix)
    d = dirs[0]; fbx = glob.glob(os.path.join(d, "*_texture.fbx"))[0]; stem = fbx[:-4]
    cmd = [BLENDER, "-b", "--python", os.path.join(REPO, "tools", "prep_prop.py"), "--", fbx, os.path.join(OUT, name + ".fbx")] + (["faces=%d" % faces] if faces else [])
    out = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
    print(name, [l for l in out.splitlines() if l.startswith("PREP")])
    shutil.copy(stem + ".png", os.path.join(OUT, name + "_basecolor.png"))
    for suffix, target in (("_normal.png", "_normal.png"), ("_emission.png", "_emission.png")):
        if os.path.exists(stem + suffix): shutil.copy(stem + suffix, os.path.join(OUT, name + target))
    if os.path.exists(stem + "_metallic.png") and os.path.exists(stem + "_roughness.png"):
        pack = [BLENDER, "-b", "--python", os.path.join(REPO, "tools", "pack_metallic_smoothness.py"), "--", stem + "_metallic.png", stem + "_roughness.png", os.path.join(OUT, name + "_metallic_smoothness.png")]
        subprocess.run(pack, capture_output=True)
print("done")
