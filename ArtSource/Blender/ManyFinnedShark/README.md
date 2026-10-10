# Many-Finned Shark rig

## Deliverables

- `ManyFinnedShark_Rigged.blend`: editable Blender 5.2 source.
- `../../Meshy/CaMap9Vay/CaMap9Vay.fbx`: textured Meshy source whose UV map is preserved by the rig.
- `../../../Assets/Art/Characters/ManyFinnedShark/ManyFinnedShark_Rigged.fbx`: Unity-ready skinned mesh and baked clips.
- `../../Meshy/ManyFinnedShark/ManyFinnedShark_Rigged.fbx`: archived 3D model source.
- `Previews/`: visual pose checks for each animation.

## Skeleton

`Root -> Body -> Spine_01 -> Spine_02 -> Tail_01 -> Tail_02 -> Tail_03`

`Jaw` is parented to `Body` and deforms the lower snout during the attack.

## Animation clips (24 FPS)

- `Shark_Swim`: frames 1-49, designed to loop.
- `Shark_Attack`: frames 1-36, recoil, lunge, open jaw, bite, settle.
- `Shark_Defeated`: frames 1-50, impact, side roll, curl, sink.

## Unity import

Use the Generic rig. Enable **Loop Time** only for `Shark_Swim`; keep the other two clips non-looping. The FBX was exported with Unity's conventional `-Z Forward / Y Up` axes and without extra leaf bones.

The procedural source and validation scripts live in `tools/generate_many_finned_shark_rig.py` and `tools/validate_many_finned_shark_rig.py`. Both now reject an FBX with no UV map so the Meshy base colour cannot silently collapse to one flat colour.
