import hashlib
import json
import math
import struct
from collections import Counter, defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SETTINGS = json.loads(Path(__file__).with_name("orion_settings.json").read_text())


def require(condition, message):
    if not condition:
        raise ValueError(message)


def parse_glb(data):
    require(len(data) >= 20, "GLB header is truncated")
    magic, version, length = struct.unpack_from("<4sII", data)
    require((magic, version, length) == (b"glTF", 2, len(data)), "Invalid GLB header")
    chunks = []
    offset = 12
    while offset < len(data):
        require(offset + 8 <= len(data), "Truncated chunk header")
        size, kind = struct.unpack_from("<I4s", data, offset)
        offset += 8
        require(size % 4 == 0 and offset + size <= len(data), "Invalid chunk size")
        chunks.append((kind, data[offset:offset + size]))
        offset += size
    require([c[0] for c in chunks] == [b"JSON", b"BIN\0"], "Expected JSON and BIN chunks")
    document = json.loads(chunks[0][1])
    binary = chunks[1][1]
    require(document["asset"]["version"] == "2.0", "Expected glTF 2.0")
    require(len(document["buffers"]) == 1, "Expected one embedded buffer")
    buffer = document["buffers"][0]
    require("uri" not in buffer and 0 <= len(binary) - buffer["byteLength"] <= 3,
            "Invalid embedded buffer")
    require(not document.get("images"), "Clay must not depend on textures")
    return document, binary


def accessor(document, binary, index):
    item = document["accessors"][index]
    require(not item.get("sparse") and not item.get("normalized"), "Unsupported accessor")
    view = document["bufferViews"][item["bufferView"]]
    require(view["buffer"] == 0, "Accessor uses an external buffer")
    formats = {5121: "B", 5123: "H", 5125: "I", 5126: "f"}
    counts = {"SCALAR": 1, "VEC3": 3}
    fmt = "<" + formats[item["componentType"]] * counts[item["type"]]
    size = struct.calcsize(fmt)
    stride = view.get("byteStride", size)
    offset = view.get("byteOffset", 0) + item.get("byteOffset", 0)
    end = offset + (item["count"] - 1) * stride + size
    require(item["count"] > 0 and stride >= size, "Empty or invalid accessor")
    require(end <= view.get("byteOffset", 0) + view["byteLength"] <= len(binary),
            "Accessor exceeds buffer view")
    result = [struct.unpack_from(fmt, binary, offset + i * stride) for i in range(item["count"])]
    require(all(math.isfinite(x) for row in result for x in row), "Nonfinite accessor")
    return result


def topology(points, indices, tolerance):
    require(len(indices) % 3 == 0, "Incomplete triangle")
    welded = {}
    mapping = []
    for point in points:
        key = tuple(round(x / tolerance) for x in point)
        mapping.append(welded.setdefault(key, len(welded)))
    edges = Counter()
    directions = Counter()
    neighbours = defaultdict(set)
    faces = set()
    for i in range(0, len(indices), 3):
        raw = indices[i:i + 3]
        require(all(0 <= x < len(mapping) for x in raw), "Triangle index out of range")
        tri = [mapping[x] for x in raw]
        require(len(set(tri)) == 3, "Degenerate triangle")
        face = tuple(sorted(tri))
        require(face not in faces, "Duplicate triangle")
        faces.add(face)
        for a, b in zip(tri, tri[1:] + tri[:1]):
            edge = tuple(sorted((a, b)))
            edges[edge] += 1
            directions[edge] += 1 if a < b else -1
            neighbours[a].add(b)
            neighbours[b].add(a)
    require(len(neighbours) == len(welded), "Unused vertices")
    require(all(count == 2 for count in edges.values()), "Mesh is not closed/manifold")
    require(all(count == 0 for count in directions.values()), "Inconsistent triangle winding")
    reached = set()
    pending = [next(iter(neighbours))]
    while pending:
        vertex = pending.pop()
        if vertex not in reached:
            reached.add(vertex)
            pending.extend(neighbours[vertex] - reached)
    require(len(reached) == len(welded), "Letter is not one connected surface")
    chi = len(welded) - len(edges) + len(faces)
    require(chi <= 2 and chi % 2 == 0, f"Invalid Euler characteristic: {chi}")
    return chi, (2 - chi) // 2


def verify(data, settings=SETTINGS):
    document, binary = parse_glb(data)
    nodes = document["nodes"]
    names = settings["node_names"]
    require(len(nodes) == len(names) and sorted(n["name"] for n in nodes) == sorted(names),
            "Expected exactly O1, R, I, O2, N nodes")
    require(len(document["meshes"]) == len(names), "Expected five separate meshes")
    require(sorted(n["mesh"] for n in nodes) == list(range(len(names))), "Meshes must be distinct")
    require(len(document["scenes"]) == 1 and document.get("scene", 0) == 0, "Expected one scene")
    require(sorted(document["scenes"][0]["nodes"]) == list(range(len(names))),
            "Letters must be direct scene children")
    require(len(document["materials"]) == 1, "Expected one clay material")
    pbr = document["materials"][0]["pbrMetallicRoughness"]
    for key, wanted in [("baseColorFactor", settings["base_color"]),
                        ("roughnessFactor", [settings["roughness"]]),
                        ("metallicFactor", [settings["metallic"]])]:
        actual = pbr.get(key, 1.0)
        actual = actual if isinstance(actual, list) else [actual]
        require(len(actual) == len(wanted) and all(abs(a - b) < 1e-6 for a, b in zip(actual, wanted)),
                f"Incorrect clay {key}: {actual}")
    world = []
    centers = {}
    for node in nodes:
        require(not node.get("children") and "matrix" not in node, "Unexpected node hierarchy/transform")
        require(node.get("rotation", [0, 0, 0, 1]) == [0, 0, 0, 1] and
                node.get("scale", [1, 1, 1]) == [1, 1, 1], "Transforms must be baked into meshes")
        primitives = document["meshes"][node["mesh"]]["primitives"]
        require(len(primitives) == 1, "Expected one primitive per letter")
        primitive = primitives[0]
        require(primitive.get("mode", 4) == 4 and primitive["material"] == 0,
                "Expected clay triangles")
        points = accessor(document, binary, primitive["attributes"]["POSITION"])
        indices = [row[0] for row in accessor(document, binary, primitive["indices"])]
        chi, holes = topology(points, indices, settings["weld_tolerance_m"])
        name = node["name"]
        require(holes == settings["expected_holes"][name],
                f"{name}: holes={holes}, expected={settings['expected_holes'][name]}")
        low = [min(p[a] for p in points) for a in range(3)]
        high = [max(p[a] for p in points) for a in range(3)]
        epsilon = settings["weld_tolerance_m"] * 10
        require(abs(low[1]) < epsilon and abs(low[0] + high[0]) < epsilon and
                abs(low[2] + high[2]) < epsilon, f"{name}: pivot must be bottom center")
        translation = node.get("translation", [0, 0, 0])
        centers[name] = translation[0]
        world.extend(tuple(p[a] + translation[a] for a in range(3)) for p in points)
        print(f"PASS {name}: chi={chi} holes={holes} triangles={len(indices) // 3} pivot=bottom-center")
    require(all(centers[a] < centers[b] for a, b in zip(names, names[1:])), "Incorrect letter order")
    dimensions = [max(p[a] for p in world) - min(p[a] for p in world) for a in range(3)]
    expected = [settings["expected_width_m"], settings["height_m"],
                2 * (settings["extrude_m"] + settings["bevel_m"])]
    for axis, actual, target in zip("XYZ", dimensions, expected):
        require(abs(actual - target) <= target * settings["dimension_tolerance"],
                f"{axis}: {actual:.6f}m outside target {target:.6f}m ±10%")
    print(f"PASS dimensions: width={dimensions[0]:.6f}m height={dimensions[1]:.6f}m depth={dimensions[2]:.6f}m")


def main():
    data = (ROOT / SETTINGS["outputs"][0]).read_bytes()
    for output in SETTINGS["outputs"][1:]:
        require((ROOT / output).read_bytes() == data, "Unity/web GLBs differ")
    verify(data)
    print(f"PASS identical copies sha256={hashlib.sha256(data).hexdigest()}")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, KeyError, IndexError, struct.error) as error:
        raise SystemExit(f"FAIL GLB: {error}") from error
