"""Pack Meshy's separate metallic and roughness maps into one Unity/URP "metallic + smoothness" texture
(R = metallic, A = 1 - roughness; G and B unused). Import the result with sRGB OFF.

    blender --background --python tools/pack_metallic_smoothness.py -- <metallic.png> <roughness.png> <out.png>
"""
import bpy, sys
import numpy as np

metallic, roughness, out = sys.argv[sys.argv.index("--") + 1:][:3]
def load(p):
    im = bpy.data.images.load(p)
    w, h = im.size
    a = np.empty(w * h * 4, dtype=np.float32); im.pixels.foreach_get(a)
    return w, h, a.reshape(h, w, 4)
w, h, m = load(metallic)
w2, h2, r = load(roughness)
assert (w, h) == (w2, h2), f"size mismatch {w}x{h} vs {w2}x{h2}"
res = np.zeros((h, w, 4), dtype=np.float32)
res[..., 0] = m[..., 0]                    # metallic (the maps are grayscale)
res[..., 1] = m[..., 0]; res[..., 2] = m[..., 0]
res[..., 3] = 1.0 - r[..., 0]              # smoothness
img = bpy.data.images.new("packed", w, h, alpha=True, float_buffer=False)
img.colorspace_settings.name = 'Non-Color'
img.pixels.foreach_set(res.ravel()); img.filepath_raw = out; img.file_format = 'PNG'; img.save()
print(f"PACK {out}: {w}x{h} metallic mean={m[...,0].mean():.3f} smoothness mean={res[...,3].mean():.3f}")
