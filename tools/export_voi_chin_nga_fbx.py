import bpy
import sys


def argument(index):
    args = sys.argv[sys.argv.index("--") + 1 :]
    return args[index]


source_blend = argument(0)
output_fbx = argument(1)

bpy.ops.wm.open_mainfile(filepath=source_blend)

rig = bpy.data.objects.get("War_Elephant_Rig")
mesh = bpy.data.objects.get("War_Elephant_Mesh")
if rig is None or mesh is None:
    raise RuntimeError("Không tìm thấy War_Elephant_Rig hoặc War_Elephant_Mesh")

for obj in bpy.context.selected_objects:
    obj.select_set(False)
rig.select_set(True)
mesh.select_set(True)
bpy.context.view_layer.objects.active = rig

# Giữ toàn bộ Action trong file FBX để Unity tạo các clip Idle/Walk/Attack/Death.
bpy.ops.export_scene.fbx(
    filepath=output_fbx,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    add_leaf_bones=False,
    use_armature_deform_only=False,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,
    path_mode="AUTO",
    embed_textures=False,
)

print("EXPORTED", output_fbx)
print("ACTIONS", sorted(action.name for action in bpy.data.actions))
