import json
import math
import struct
import unittest

from verify_glb import parse_glb, topology


class GlbVerifierTests(unittest.TestCase):
    points = [(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1)]
    triangles = [0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3]

    def test_closed_surface_has_no_hole(self):
        self.assertEqual(topology(self.points, self.triangles, 1e-7), (2, 0))

    def test_normal_seam_duplicates_are_welded(self):
        points = [self.points[index] for index in self.triangles]
        self.assertEqual(topology(points, list(range(len(points))), 1e-7), (2, 0))

    def test_torus_has_one_hole(self):
        points, triangles = [], []
        rings, sides = 12, 8
        for i in range(rings):
            for j in range(sides):
                u, v = 2 * math.pi * i / rings, 2 * math.pi * j / sides
                points.append(((2 + math.cos(v)) * math.cos(u),
                               (2 + math.cos(v)) * math.sin(u), math.sin(v)))
        for i in range(rings):
            for j in range(sides):
                a = i * sides + j
                b = ((i + 1) % rings) * sides + j
                c = ((i + 1) % rings) * sides + (j + 1) % sides
                d = i * sides + (j + 1) % sides
                triangles.extend((a, b, c, a, c, d))
        self.assertEqual(topology(points, triangles, 1e-7), (0, 1))

    def test_open_surface_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'not closed/manifold'):
            topology(self.points, self.triangles[:-3], 1e-7)

    def test_duplicate_faces_are_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Duplicate triangle'):
            topology(self.points, self.triangles + self.triangles[:3], 1e-7)

    def test_disconnected_shells_are_rejected(self):
        points = self.points + [(x + 3, y, z) for x, y, z in self.points]
        triangles = self.triangles + [index + 4 for index in self.triangles]
        with self.assertRaisesRegex(ValueError, 'not one connected surface'):
            topology(points, triangles, 1e-7)

    def test_inconsistent_winding_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Inconsistent triangle winding'):
            topology(self.points, list(reversed(self.triangles[:3])) + self.triangles[3:], 1e-7)

    def test_embedded_glb_chunks_and_length(self):
        document = json.dumps({'asset': {'version': '2.0'}, 'buffers': [{'byteLength': 4}]}).encode()
        document += b' ' * (-len(document) % 4)
        chunks = struct.pack('<I4s', len(document), b'JSON') + document
        chunks += struct.pack('<I4s', 4, b'BIN\0') + bytes(4)
        data = struct.pack('<4sII', b'glTF', 2, 12 + len(chunks)) + chunks
        parsed, binary = parse_glb(data)
        self.assertEqual(parsed['asset']['version'], '2.0')
        self.assertEqual(binary, bytes(4))
        with self.assertRaisesRegex(ValueError, 'Invalid GLB header'):
            parse_glb(data[:-1])


if __name__ == '__main__':
    unittest.main()
