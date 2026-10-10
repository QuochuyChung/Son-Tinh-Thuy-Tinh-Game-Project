import bpy

obj = next(o for o in bpy.context.scene.objects if o.type == "MESH")
mesh = obj.data
adj = [set() for _ in mesh.vertices]
for edge in mesh.edges:
    a, b = edge.vertices
    adj[a].add(b)
    adj[b].add(a)
seen = set()
parts = []
for start in range(len(mesh.vertices)):
    if start in seen:
        continue
    stack = [start]
    seen.add(start)
    comp = []
    while stack:
        v = stack.pop()
        comp.append(v)
        for n in adj[v]:
            if n not in seen:
                seen.add(n)
                stack.append(n)
    coords = [obj.matrix_world @ mesh.vertices[i].co for i in comp]
    lo = tuple(round(min(v[k] for v in coords), 4) for k in range(3))
    hi = tuple(round(max(v[k] for v in coords), 4) for k in range(3))
    parts.append((len(comp), lo, hi))
for i, info in enumerate(sorted(parts, reverse=True)):
    print(i, info)
