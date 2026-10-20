"""Print deterministic geometry/rig metadata for one or more FBX files.

Usage:
    blender --background --python tools/inspect_fbx.py -- file1.fbx [file2.fbx ...]
"""

import json
import os
import sys

import bmesh
import bpy


def connected_components(mesh_object):
    mesh = bmesh.new()
    mesh.from_mesh(mesh_object.data)
    mesh.verts.ensure_lookup_table()
    seen = set()
    components = []
    for vertex in mesh.verts:
        if vertex.index in seen:
            continue
        stack = [vertex]
        seen.add(vertex.index)
        indices = []
        while stack:
            current = stack.pop()
            indices.append(current.index)
            for edge in current.link_edges:
                other = edge.other_vert(current)
                if other.index not in seen:
                    seen.add(other.index)
                    stack.append(other)
        points = [mesh_object.matrix_world @ mesh_object.data.vertices[index].co for index in indices]
        components.append(
            {
                "vertices": len(indices),
                "min": [round(min(point[i] for point in points), 6) for i in range(3)],
                "max": [round(max(point[i] for point in points), 6) for i in range(3)],
            }
        )
    mesh.free()
    return sorted(components, key=lambda item: item["vertices"], reverse=True)


def bounds(mesh_object):
    points = [mesh_object.matrix_world @ vertex.co for vertex in mesh_object.data.vertices]
    return {
        "min": [round(min(point[i] for point in points), 6) for i in range(3)],
        "max": [round(max(point[i] for point in points), 6) for i in range(3)],
    }


def inspect(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    meshes = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        components = connected_components(obj)
        meshes.append(
            {
                "name": obj.name,
                "vertices": len(obj.data.vertices),
                "polygons": len(obj.data.polygons),
                "materials": [material.name for material in obj.data.materials if material],
                "vertex_groups": len(obj.vertex_groups),
                "components": len(components),
                "largest_components": components[:20],
                "bounds": bounds(obj),
            }
        )
    armatures = []
    for obj in bpy.context.scene.objects:
        if obj.type == "ARMATURE":
            armatures.append(
                {
                    "name": obj.name,
                    "bones": len(obj.data.bones),
                    "first_bones": [bone.name for bone in list(obj.data.bones)[:20]],
                    "actions": [action.name for action in bpy.data.actions],
                }
            )
    return {
        "file": os.path.abspath(path),
        "meshes": meshes,
        "armatures": armatures,
        "objects": [
            {
                "name": obj.name,
                "type": obj.type,
                "location": [round(value, 6) for value in obj.location],
                "scale": [round(value, 6) for value in obj.scale],
                "parent": obj.parent.name if obj.parent else None,
                "matrix_world_scale": [round(value, 6) for value in obj.matrix_world.to_scale()],
            }
            for obj in bpy.context.scene.objects
        ],
    }


arguments = sys.argv[sys.argv.index("--") + 1 :]
for source_path in arguments:
    print("INSPECT_JSON " + json.dumps(inspect(source_path), ensure_ascii=False))
