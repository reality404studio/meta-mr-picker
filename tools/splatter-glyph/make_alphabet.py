# 사용: Blender -b --python make_alphabet.py -- <font> <out_dir> <cap_height_m> <extrude> <bevel> <bevel_res> <curve_res> [글자들=A..Z]
import string
import sys
import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
font_path, out_dir = args[0], args[1]
cap, extrude, bevel, bres, cres = float(args[2]), float(args[3]), float(args[4]), int(args[5]), int(args[6])
letters = args[7] if len(args) > 7 else string.ascii_uppercase

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
font = bpy.data.fonts.load(font_path)


def make(ch, size, solid):
    c = bpy.data.curves.new(ch, "FONT")
    c.body, c.font, c.size = ch, font, size
    c.resolution_u = cres
    c.fill_mode = "BOTH"
    if solid:
        c.extrude, c.bevel_depth, c.bevel_resolution = extrude, bevel, bres
    o = bpy.data.objects.new(ch, c)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.update()
    return o


def bb(o):
    pts = [Vector(p) for p in o.bound_box]
    return [min(p[a] for p in pts) for a in range(3)], [max(p[a] for p in pts) for a in range(3)]


ref = make("H", 1.0, False)
lo, hi = bb(ref)
size = (cap - 2 * bevel) / (hi[1] - lo[1])
bpy.data.objects.remove(ref, do_unlink=True)
mat = bpy.data.materials.new("Clay")

for ch in letters:
    o = make(ch, size, True)
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.active_object
    lo, hi = bb(o)
    c = [(lo[i] + hi[i]) / 2 for i in range(3)]
    for v in o.data.vertices:
        x, y, z = v.co - Vector(c)
        v.co = (x, -z, y)
    o.data.materials.append(mat)
    bpy.ops.export_scene.gltf(filepath=f"{out_dir}/GLYPH_{ch}.glb", export_format="GLB", use_selection=True, export_apply=True,
                              export_yup=True, export_animations=False, export_cameras=False, export_lights=False)
    lo, hi = bb(o)
    print("RESULT", ch, "size", [round(hi[i] - lo[i], 4) for i in range(3)], "verts", len(o.data.vertices))
    bpy.data.objects.remove(o, do_unlink=True)
