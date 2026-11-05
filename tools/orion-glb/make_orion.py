import json
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
SETTINGS = json.loads(Path(__file__).with_name("orion_settings.json").read_text())


def text_object(body, font, size=1.0, solid=False):
    curve = bpy.data.curves.new(body, "FONT")
    curve.body = body
    curve.font = font
    curve.size = size
    curve.resolution_u = SETTINGS["curve_resolution"]
    curve.fill_mode = "BOTH"
    if solid:
        curve.extrude = SETTINGS["extrude_m"]
        curve.bevel_depth = SETTINGS["bevel_m"]
        curve.bevel_resolution = SETTINGS["bevel_resolution"]
    obj = bpy.data.objects.new(body, curve)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.update()
    return obj


def bounds(obj):
    points = [Vector(p) for p in obj.bound_box]
    return [min(p[a] for p in points) for a in range(3)], [
        max(p[a] for p in points) for a in range(3)
    ]


def measure(body, font):
    obj = text_object(body, font)
    result = bounds(obj)
    curve = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.curves.remove(curve)
    return result


def main():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    font = bpy.data.fonts.load(str(ROOT / SETTINGS["font"]))
    word = SETTINGS["text"]
    low, high = measure(word, font)
    size = (SETTINGS["height_m"] - 2 * SETTINGS["bevel_m"]) / (high[1] - low[1])
    material = bpy.data.materials.new("ORION_Clay")
    material.diffuse_color = SETTINGS["base_color"]
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = SETTINGS["base_color"]
    bsdf.inputs["Roughness"].default_value = SETTINGS["roughness"]
    bsdf.inputs["Metallic"].default_value = SETTINGS["metallic"]
    objects = []
    for index, (letter, name) in enumerate(zip(word, SETTINGS["node_names"], strict=True)):
        _, prefix_high = measure(word[:index + 1], font)
        _, glyph_high = measure(letter, font)
        offset = (prefix_high[0] - glyph_high[0]) * size
        obj = text_object(letter, font, size, solid=True)
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.convert(target="MESH")
        obj.name = name
        obj.data.name = name
        mesh = bmesh.new()
        mesh.from_mesh(obj.data)
        bmesh.ops.remove_doubles(mesh, verts=list(mesh.verts), dist=SETTINGS["weld_tolerance_m"])
        bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
        bmesh.ops.triangulate(mesh, faces=list(mesh.faces))
        mesh.to_mesh(obj.data)
        mesh.free()
        obj.data.update()
        vertices = [v.co.copy() for v in obj.data.vertices]
        lower = [min(v[a] for v in vertices) for a in range(3)]
        upper = [max(v[a] for v in vertices) for a in range(3)]
        pivot = Vector(((lower[0] + upper[0]) / 2, lower[1], (lower[2] + upper[2]) / 2))
        for polygon in obj.data.polygons:
            polygon.use_smooth = abs(polygon.normal.z) < 0.999
        for vertex in obj.data.vertices:
            x, y, z = vertex.co - pivot
            vertex.co = (x, -z, y)
        obj.location = (offset + pivot.x, 0.0, pivot.y)
        obj.data.materials.clear()
        obj.data.materials.append(material)
        obj.data.update()
        objects.append(obj)
    world_points = [obj.location + v.co for obj in objects for v in obj.data.vertices]
    center_x = (min(v.x for v in world_points) + max(v.x for v in world_points)) / 2
    bottom = min(v.z for v in world_points)
    for obj in objects:
        obj.location.x -= center_x
        obj.location.z -= bottom
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    output = ROOT / SETTINGS["outputs"][0]
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(output), export_format="GLB", use_selection=True,
        export_yup=True, export_materials="EXPORT", export_animations=False,
        export_cameras=False, export_lights=False,
    )
    data = output.read_bytes()
    for relative in SETTINGS["outputs"][1:]:
        destination = ROOT / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
    print(f"GENERATED nodes={','.join(SETTINGS['node_names'])} bytes={len(data)}")


if __name__ == "__main__":
    main()
