# 사용: Blender -b --python make_glyph.py -- <font> <char> <out.glb> <height_m> <extrude> <bevel> <bevel_res> <curve_res>
import sys
import bpy
from mathutils import Vector

font_path, ch, out, height, extrude, bevel, bres, cres = sys.argv[sys.argv.index("--") + 1:][:8]
height, extrude, bevel, bres, cres = float(height), float(extrude), float(bevel), int(bres), int(cres)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
font = bpy.data.fonts.load(font_path)


def make(size, solid):
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


m = make(1.0, False)
lo, hi = bb(m)
size = (height - 2 * bevel) / (hi[1] - lo[1])
bpy.data.objects.remove(m, do_unlink=True)

o = make(size, True)
bpy.ops.object.select_all(action="DESELECT")
o.select_set(True)
bpy.context.view_layer.objects.active = o
bpy.ops.object.convert(target="MESH")
o = bpy.context.active_object
# 글자 중심을 원점에
lo, hi = bb(o)
c = [(lo[i] + hi[i]) / 2 for i in range(3)]
for v in o.data.vertices:
    x, y, z = v.co - Vector(c)
    v.co = (x, -z, y)  # make_orion.py 와 같은 방향: 글자면이 glTF +Z, 높이가 glTF +Y
mat = bpy.data.materials.new("Clay")
mat.use_nodes = True
o.data.materials.append(mat)
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True, export_apply=True, export_yup=True, export_animations=False, export_cameras=False, export_lights=False)
lo, hi = bb(o)
print("RESULT size", [round(hi[i] - lo[i], 4) for i in range(3)], "verts", len(o.data.vertices))
