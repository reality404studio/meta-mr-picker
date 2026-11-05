import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { Box3, Vector3 } from './vendor/three/three.module.js';
import { GLTFLoader } from './vendor/three/addons/loaders/GLTFLoader.js';

const root = new URL('../../', import.meta.url);
const settings = JSON.parse(await readFile(new URL('./orion_settings.json', import.meta.url), 'utf8'));
const loader = new GLTFLoader();

try {
  const files = await Promise.all(settings.outputs.map(path => readFile(new URL(path, root))));
  for (const bytes of files) {
    assert.ok(bytes.equals(files[0]), 'Unity/web copies differ');
    const array = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
    const gltf = await loader.parseAsync(array, '');
    const meshes = [];
    gltf.scene.traverse(object => { if (object.isMesh) meshes.push(object); });
    assert.equal(meshes.length, 5);
    assert.deepEqual(meshes.map(mesh => mesh.name).sort(), [...settings.node_names].sort());
    assert.deepEqual(gltf.scene.children.map(child => child.name).sort(), [...settings.node_names].sort());
    assert.equal(new Set(meshes.map(mesh => mesh.geometry)).size, 5);
    assert.equal(new Set(meshes.map(mesh => mesh.material)).size, 1);
    const dimensions = new Box3().setFromObject(gltf.scene).getSize(new Vector3());
    assert.ok(Math.abs(dimensions.x - settings.expected_width_m) <= settings.expected_width_m * settings.dimension_tolerance);
    assert.ok(Math.abs(dimensions.y - settings.height_m) <= settings.height_m * settings.dimension_tolerance);
    for (const mesh of meshes) mesh.geometry.dispose();
    meshes[0].material.dispose();
  }
  console.log(`PASS GLTFLoader: Mesh=5 names=${settings.node_names.join(',')} copies=2`);
} catch (error) {
  console.error(`FAIL GLTFLoader: ${error.message}`);
  process.exitCode = 1;
}
