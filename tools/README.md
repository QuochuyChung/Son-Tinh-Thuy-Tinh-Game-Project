# Blender tools for Meshy → Mixamo → Unity characters

All scripts run headless: `blender --background --python tools/<script>.py -- <args>`
(Blender 5.2 at `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`). Usage is at the top of each file.

## Simple character (like the old Sơn Tinh / Thủy Tinh)

1. `strip_material.py` — remove material and textures from the Meshy FBX, keeping the exact mesh and UVs.
2. Upload the result to Mixamo, rig it, download Idle (With Skin).

## Complex character (many loose pieces: feathers, cape, skirt, separate boots) — Sơn Tinh v2

Mixamo fails on these ("Unknown error while generating motion") or rigs the legs wrongly, because it weights
every loose piece separately. Workaround: rig a clean stand-in body, then copy its skin weights onto the real mesh.

1. `strip_material.py` — as above.
2. `prune_shards.py <in> <out> <min_verts> <drop_cape 0/1> <render.png>` — delete tiny loose pieces (feathers) and optionally the cape.
3. `list_mesh_parts.py <pruned.fbx> <render.png>` — prints every loose piece with its height/width and renders them in different colours,
   so you can pick which ones are the body (head/torso/arms/hands/pants/boots).
4. `make_mannequin.py <pruned.fbx> <out.fbx> <render.png>` — keeps only the body pieces, solidifies and voxel-remeshes them into one
   watertight mesh (1 piece, no holes). **The list of body pieces (`KEEP`) is hard-coded for Sơn Tinh v2**: edit it from step 3's output.
5. Mixamo: upload the mannequin, place the markers (knees at the crease above the boots, groin where the legs split, wrists at the
   narrowest point above the fist, elbows mid-arm), pick the **no-fingers** skeleton for fist hands, check Walk/Run in the preview.
   Download Idle (FBX for Unity, With Skin) plus Walk/Run (Without Skin, not In Place).
6. `transfer_weights.py` — copies the skin weights from the rigged mannequin onto the complete mesh, with special rules:
   cape follows the torso/hips (never the legs or arms), loose pieces are attached rigidly to one bone each, fists and bracers
   only use the hand/forearm bones, the skirt follows the thighs, the centre panel follows both thighs equally.
   It renders run/walk previews and writes the skinned FBX for Unity.
7. In Unity: import the skinned FBX as Humanoid, set the URP material to **Render Face: Both** (the cape is a single-sided sheet).

7b. Fists (done after step 6, on the skinned FBX): Meshy models the hands as fused lumps and the no-finger rig cannot move fingers anyway.
   `replace_fists.py <in_skinned.fbx> <out_skinned.fbx> <closeup_prefix> <basecolor.png> <normal.png>` swaps both fists for clenched fists
   (curled fingers + thumb, built by `fist_builder.py`), weighted 100 % to the hand bones, skin colour picked from the base-colour
   texture. Every other vertex, weight and bone stays identical (checked by position). Replace `Assets/.../son_tinh_v2.fbx` with the
   output (keep its `.meta`), then **re-run PART 2 of the cloth script** — the vertex count changes (4514 -> 6337 cloth vertices), so
   the cape's coefficients must be assigned again. `fist_builder.find_fists_and_bracers()` finds the fists by position/size and is tuned
   for Sơn Tinh v2; it raises an error on any other mesh.

8. Cape flutter: `tools/unity/SonTinhCapeCloth.cs.txt` (Unity editor scripts that add a Cloth + collider set to the prefab;
   see `docs/progress.md` section 9.3 for the parameters and the traps).

## Modular character (body + separately generated garment) — Thủy Tinh v2

Much easier than the complex pipeline above, and the result is better: the body is one clean mesh that Mixamo rigs directly (full 65-bone
skeleton, fingers included), and the clothes are a second Meshy mesh that is fitted and skinned onto that rig.

1. Body: Meshy (T-pose, simple clothes, ~8k tris) -> `strip_material.py` -> Mixamo -> download Idle/Walk/Run **With Skin**.
2. Garment: Meshy from the garment concept sheet (skirt + pants + boots in one piece, textured FBX).
3. `fit_garment.py <idle.fbx> <garment_textured.fbx> <out_skinned.fbx> <preview_dir> [<run.fbx>] [thigh=0.5 ...]` — scales/shears the garment onto
   the body (soles on the ground, waistband on the torso), deletes the naked legs hidden under it, copies skin weights from the nearest body
   surface (skirt panels follow the thighs only partly, `thigh=`, the rest follows the hips), and exports armature + 2 meshes
   (`ThuyTinh_Body`, `ThuyTinh_Garment`). Check `<preview_dir>/fit_*.png` and `run_*.png`.
4. `pack_metallic_smoothness.py` for each Meshy metallic+roughness pair (URP wants one texture: R = metallic, A = smoothness).
5. Unity: Humanoid import of the skinned FBX, one material per mesh (copy `M_SonTinh_v2`, swap the 3 textures; garment `_Cull` = 0),
   clips imported from the three Mixamo files (each with its own avatar), `AOC_*` from the shared base controller, player prefab = copy of an
   existing `Player_*_v2` with the Model child replaced. Measure walk/run speed by playing each clip in a PlayableGraph with root motion on and
   dividing the root travel of one loop by its duration (this reproduced the Sơn Tinh v2 numbers exactly).
   Optional back piece (feather mantle / cape): `mantle=<textured.fbx>` plus `mlen`, `mwidth`, `mmargin` (see the docstring) — fitted at the neck,
   pushed out of the body along the surface normal. By default (`capebones=1`) it gets 15 extra bones `Cape_<column>_<segment>` (5 chains of 3
   under Spine2) and soft skinning; in Unity `OutfitSpringBones` (Assets/Scripts/Characters) animates them. `capebones=0` gives the plain
   rigid version. The skirt panels hanging away from the legs get 16 bones `Skirt_<column>_<segment>` (8 chains of 2 under Hips, soft skinning) by default
   (`skirtbones=0` turns it off; `sktop=0.50` = height fraction where the swinging part starts, `boottop=0.27` = pieces whose highest point is below this
   height fraction count as boots and stay rigid); the same component swings them, one `Group` per bone prefix. Do NOT use Unity Cloth for a mantle made of many loose pieces: it was tried and the feathers collapse into strips.
   Use `name=SonTinh` for the mesh prefix.
   If skirt panels clip through the legs too much, lower `Max Angle` / raise `Stiffness` on the skirt group (not Unity Cloth, see above).

Do NOT re-run `transfer_weights.py` for Sơn Tinh v2 just to change something small: the cape arguments (`cape_push` / `cape_widen` /
`cape_arm_follow`) used for the exported FBX were not recorded, so a re-run would change the cape. Edit the finished skinned FBX instead
(as `replace_fists.py` does).

Gotchas found while building this: inspect with Blender before blaming Mixamo (loose-piece count: old characters had 19-25, the
failing mesh had 193); after test-posing in Blender reset the armature object's location and pose before exporting; with Blender
5.x actions need `animation_data.action_slot` set when you apply a Mixamo action to a different armature.

## NPC: clothed body + separate cape, rig from a clip whose mesh has no UVs — Hùng Vương

`fit_hung_vuong.py <idle.fbx> <body_textured.fbx> <cape_textured.fbx> <out_skinned.fbx> <preview_dir> [<clip.fbx> ...] [key=value ...]`
(docstring has every option). Used for Hùng Vương (06/10, second version: the clothes are part of the Meshy body, the old separate
pants/robe are gone):

    blender -b --python tools/fit_hung_vuong.py -- ArtSource/Mixamo/HungVuong/hung_vuong_idle.fbx ArtSource/Meshy/HungVuong/hung_vuong_mesh.fbx ArtSource/Meshy/HungVuong/hung_vuong_cape_mesh.fbx ArtSource/Mixamo/HungVuong/hung_vuong_skinned.fbx <preview_dir> ArtSource/Mixamo/HungVuong/hung_vuong_talking.fbx ArtSource/Mixamo/HungVuong/hung_vuong_pointing.fbx ArtSource/Mixamo/HungVuong/hung_vuong_nod.fbx cdrop=-0.04 blend=<abs path>/ArtSource/Mixamo/HungVuong/hung_vuong_fit.blend

(use absolute paths for `blend=` and for `pack_metallic_smoothness.py`: Blender changes its working directory.)

- **Body**: Mixamo rigged the untextured Meshy export (`hung_vuong_generate.fbx`, no UVs); the textured export is the same mesh (8069
  vertices, all with an exact twin), so it takes the rig's weights vertex by vertex. Two fixes on top: the fused Meshy fingers get their
  finger weights averaged over the hand (`frad` 0.012 body heights; the webbing tore up to 5.6x when the hand opened), and the tabard
  panel hanging between the legs shares both thighs equally near the middle (`pband` 0.06; it tore 2-4.7x when the legs parted).
- **Cape**: scaled uniformly to `clen` 0.60 of the body height, collar ring centred on the Neck joint and raised `cdrop=-0.04` so the shoulder
  drape sits on the shoulders. It is widened **row by row** (sideways and backwards separately, smoothed over the height) until it clears the
  torso/legs, in T-pose and in the idle's first pose (arms down) - the old per-vertex push onto the skin crumpled the drape. Arms are not
  taken into account (they move in front of it). Weights are rigid: collar/shoulders 100 % Spine2, the sheet on 5 x 3 `Cape_<c>_<s>` chains
  under Spine2 (= rigid in every clip; `OutfitSpringBones` sways them in Unity).
- **No collar** (06/10, second request: the hugging collar folded and crumpled round the neck in Unity, "drop the collar, just attach the
  cape to the robe"): the dense stand-up collar band (top `ccut` 0.08 of the Meshy cape, 1840 verts) is deleted, so the sheet's top edge sits at
  the base of the neck. That top edge (`ctop` 0.14 / `chug` 0.05 of the remaining cape) is laid onto the shoulders / upper back `cgap` 0.006
  body heights off the robe and takes the robe's weights (arms -> that side's clavicle, neck / head -> Spine2), fading into the rigid / chain
  weights below. Cape stretch: max 1.24 / 1.27 / 1.53 / 1.83 (Idle / Talking / Pointing / Nod), 1 and 5 edges over 1.3 (Pointing / Nod, at
  the top edge over the shoulders). `ccut=0` brings the collar back.
- Measured (edge length / bind length, every 3rd frame, `stretch.txt`): cape 1.000 before the collar change (see above); body 1.0 in T-pose, worst
  2.35 / 2.77 / 2.45 / 1.78 (Idle / Talking / Pointing / Nod), all on tiny edges between the fused fingers (Mixamo's own finger
  weights on a mitten hand); `CapeIn` (cape vertices inside the body) 2 in T-pose, at most 46-124 per frame in the clips (where the arms
  and the hair touch it).
Previews: `fit_tpose.png`, `pose_<clip>.png` (front + back three-quarter, 4 frames), `close_<clip>.png` (collar / shoulders, 3 views x 4 frames).
Unity side: `Tools ▸ Son Tinh Thuy Tinh ▸ Build Hung Vuong NPC` (`Assets/Editor/HungVuongBuilder.cs`), or in batch mode
`unity run . -- -executeMethod SonTinhThuyTinh.EditorTools.HungVuongBuilder.BuildBatch -hvShots <dir>` (also writes check images incl. close-ups;
skinned meshes are skinned on the CPU for those because BakeMesh returns nothing in batch mode). Materials: body Render Face Front, cape Both.
If the model or a clip is replaced by one with different bone lengths, delete its `.meta` first (only the builder's own prefab/controller
reference them), otherwise the old avatar configuration gives "Avatar Rig Configuration mis-match".
