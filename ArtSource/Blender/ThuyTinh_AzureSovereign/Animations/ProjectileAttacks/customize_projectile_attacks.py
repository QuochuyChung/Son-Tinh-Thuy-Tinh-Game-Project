"""Create forward-directed water projectile attacks for Thuy Tinh.

The licensed source clips provide torso anticipation and recovery.  Two-bone IK is
layered over their arm motion so the casting hand(s) converge on the projectile path
instead of sweeping sideways.  Blender's FBX exporter bakes the evaluated constraints,
leaving ordinary humanoid animation clips for Unity.
"""

import bpy
import math
import os
import sys
from mathutils import Vector


ROOT = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
OUT = os.path.join(
    ROOT,
    "ArtSource",
    "Blender",
    "ThuyTinh_AzureSovereign",
    "Animations",
    "ProjectileAttacks",
)
SOURCE = os.path.join(
    ROOT,
    "Assets",
    "Kevin Iglesias",
    "Human Animations",
    "Animations",
    "Male",
    "Combat",
    "Spellcasting",
    "MagicAttacks",
    "Directional",
)
os.makedirs(OUT, exist_ok=True)


ATTACKS = (
    {
        "name": "thuy_tinh_projectile_left",
        "source": "HumanM@MagicAttackDirect1H01_L.fbx",
        "release": 18,
        "mode": "LEFT",
        "reach": 69.0,
    },
    {
        "name": "thuy_tinh_projectile_right",
        "source": "HumanM@MagicAttackDirect1H01_R.fbx",
        "release": 18,
        "mode": "RIGHT",
        "reach": 69.0,
    },
    {
        "name": "thuy_tinh_projectile_two_hand",
        "source": "HumanM@MagicAttackDirect2H01.fbx",
        "release": 27,
        "mode": "BOTH",
        "reach": 72.0,
    },
    {
        "name": "thuy_tinh_projectile_heavy",
        "source": "HumanM@MagicAttackDirect2H01.fbx",
        "release": 29,
        "mode": "HEAVY",
        "reach": 84.0,
    },
)


def smoothstep(edge0, edge1, value):
    if edge1 <= edge0:
        return 1.0 if value >= edge1 else 0.0
    t = min(max((value - edge0) / (edge1 - edge0), 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def influence_at(frame, first, release, last, heavy=False):
    lead = 13 if heavy else 9
    fade_in = smoothstep(release - lead, release - 3, frame)
    fade_out = 1.0 - smoothstep(release + 5, last - 2, frame)
    return min(fade_in, fade_out)


def get_fcurves(action):
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    curves = []
    for layer in action.layers:
        for strip in layer.strips:
            for channelbag in getattr(strip, "channelbags", []):
                curves.extend(channelbag.fcurves)
    return curves


def make_target(name, armature):
    target = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(target)
    target.empty_display_type = "SPHERE"
    target.empty_display_size = 3.0
    return target


def configure_arm_ik(armature, side):
    suffix = ".L" if side == "L" else ".R"
    forearm = armature.pose.bones[f"B-forearm{suffix}"]
    hand_target = make_target(f"WaterCast_Hand{suffix}", armature)
    pole_target = make_target(f"WaterCast_Elbow{suffix}", armature)
    aim_target = make_target(f"WaterCast_Aim{suffix}", armature)
    constraint = forearm.constraints.new("IK")
    constraint.name = f"WaterCastIK{suffix}"
    constraint.target = hand_target
    constraint.pole_target = pole_target
    constraint.chain_count = 2
    constraint.use_tail = True
    constraint.use_stretch = False
    constraint.pole_angle = 0.0
    hand = armature.pose.bones[f"B-hand{suffix}"]
    aim_constraint = hand.constraints.new("DAMPED_TRACK")
    aim_constraint.name = f"WaterCastAim{suffix}"
    aim_constraint.target = aim_target
    # A Blender bone's local +Y axis runs from its head to its tail. Pointing that
    # axis down the projectile lane keeps the fingers/wrist from twisting sideways.
    aim_constraint.track_axis = "TRACK_Y"
    return forearm, constraint, hand_target, pole_target, aim_constraint, aim_target


def build_attack(spec):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source_path = os.path.join(SOURCE, spec["source"])
    bpy.ops.import_scene.fbx(filepath=source_path)
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    action = armature.animation_data.action
    action.name = spec["name"]
    first = max(1, int(round(action.frame_range[0])))
    last = max(first, int(round(action.frame_range[1])))
    release = min(max(spec["release"], first + 2), last - 2)
    active = {"L", "R"} if spec["mode"] in {"BOTH", "HEAVY"} else {"L" if spec["mode"] == "LEFT" else "R"}
    controls = {side: configure_arm_ik(armature, side) for side in ("L", "R")}

    scene = bpy.context.scene
    scene.frame_start = first
    scene.frame_end = last
    scene.render.fps = 30

    for frame in range(first, last + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        chest = armature.matrix_world @ armature.pose.bones["B-chest"].head
        heavy = spec["mode"] == "HEAVY"
        weight = influence_at(frame, first, release, last, heavy)
        thrust = smoothstep(release - (12 if heavy else 8), release, frame)
        if frame > release:
            thrust = 1.0 - 0.22 * smoothstep(release + 3, last, frame)

        for side, sign in (("L", 1.0), ("R", -1.0)):
            forearm, constraint, hand_target, pole_target, aim_constraint, aim_target = controls[side]
            is_active = side in active
            if is_active:
                side_offset = (7.0 if len(active) == 2 else 5.0) * sign
                forward = -(25.0 + (spec["reach"] - 25.0) * thrust)
                lift = 10.0 - 8.0 * thrust
                target = chest + Vector((side_offset, forward, lift))
                arm_weight = weight
            else:
                # The non-casting hand guards the chest instead of flying out sideways.
                target = chest + Vector((10.0 * sign, -23.0, 3.0))
                arm_weight = weight * 0.82

            pole = chest + Vector((31.0 * sign, -30.0, -7.0 + 4.0 * thrust))
            # The imported action is authored in centimetres and carries an object-level
            # conversion matrix.  Pose coordinates are therefore not at rest-pose scale;
            # build the controls directly in evaluated world space.
            hand_target.location = target
            pole_target.location = pole
            aim_target.location = target + Vector((0.0, -45.0, 0.0))
            hand_target.keyframe_insert("location", frame=frame)
            pole_target.keyframe_insert("location", frame=frame)
            aim_target.keyframe_insert("location", frame=frame)
            constraint.influence = arm_weight
            constraint.keyframe_insert("influence", frame=frame)
            aim_constraint.influence = arm_weight
            aim_constraint.keyframe_insert("influence", frame=frame)

    for control in controls.values():
        for obj in (control[2], control[3], control[5]):
            if obj.animation_data and obj.animation_data.action:
                for curve in get_fcurves(obj.animation_data.action):
                    for key in curve.keyframe_points:
                        key.interpolation = "BEZIER"

    scene.frame_set(release)
    bpy.context.view_layer.update()
    positions = {}
    directions = {}
    for side in ("L", "R"):
        hand_bone = armature.pose.bones[f"B-hand.{side}"]
        hand = armature.matrix_world @ hand_bone.head
        tail = armature.matrix_world @ hand_bone.tail
        direction = (tail - hand).normalized()
        positions[side] = tuple(round(value, 2) for value in hand)
        directions[side] = tuple(round(value, 3) for value in direction)
    print(
        f"ATTACK {spec['name']} frames={first}-{last} release={release} "
        f"wrists={positions} hand_forward={directions}"
    )

    # Save an editable Blender source before creating the engine FBX.
    blend_path = os.path.join(OUT, f"{spec['name']}.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)

    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    fbx_path = os.path.join(OUT, f"{spec['name']}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        object_types={"ARMATURE"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        axis_forward="-Z",
        axis_up="Y",
    )
    print(f"EXPORTED {fbx_path}")


for attack in ATTACKS:
    build_attack(attack)

