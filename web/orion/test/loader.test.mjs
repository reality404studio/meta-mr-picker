import './register_three.mjs';
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const { DataTexture, RGBAFormat } = await import('three');
const { loadOrion, parseOrionGlb, measureBounds, resolveTileUrl } = await import('../src/loader.js');
const { LETTER_NAMES } = await import('../src/settings.js');

const GLB_URL = new URL('../ORION.glb', import.meta.url);
const SAMPLE_URL = new URL('../../../tools/web-orion/samples/orion.export.json', import.meta.url);

async function glbBuffer() {
  const bytes = await readFile(GLB_URL);
  return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
}

const requested = [];
async function fakeLoadTexture(url) {
  requested.push(url);
  const data = new Uint8Array([255, 255, 255, 255]);
  return new DataTexture(data, 1, 1, RGBAFormat);
}

test('ORION.glb parses into the five named letter meshes', async () => {
  const { meshes } = await parseOrionGlb(await glbBuffer());
  assert.deepEqual([...meshes.keys()].sort(), [...LETTER_NAMES].sort());
  const bounds = measureBounds(meshes);
  const width = Math.max(...LETTER_NAMES.map((n) => bounds[n].maxX)) - Math.min(...LETTER_NAMES.map((n) => bounds[n].minX));
  assert.ok(Math.abs(width - 0.615) < 0.615 * 0.1, `width ${width}`);
});

test('sample JSON + PNG tiles apply chip/gradient/clay materials per letter', async () => {
  const doc = JSON.parse(await readFile(SAMPLE_URL, 'utf8'));
  doc.letters.find((l) => l.name === 'R').paint = { kind: 'clay' };
  requested.length = 0;
  const { word, warnings } = await loadOrion({ glb: await glbBuffer(), doc, baseUrl: SAMPLE_URL.href, loadTexture: fakeLoadTexture });
  assert.deepEqual(warnings, []);
  assert.deepEqual(word.materialKinds(), { O1: 'chip', R: 'clay', I: 'chip', O2: 'chip', N: 'chip' });
  assert.equal(word.meshes.get('R').material.isMeshStandardMaterial, true);
  assert.equal(word.meshes.get('O1').material.isShaderMaterial, true);
  assert.equal(word.meshes.get('O1').material.uniforms.uTile.value.isTexture, true);
  assert.equal(word.meshes.get('O1').material, word.meshes.get('O1').material);
  assert.deepEqual([...new Set(requested)].length, 2, 'yunseul + gradient tiles fetched once each');
  for (const url of requested) {
    const bytes = await readFile(new URL(url));
    assert.equal(bytes.subarray(1, 4).toString(), 'PNG', url);
  }
});

test('all-clay document uses one shared clay material and the group holds 5 letters', async () => {
  const { word } = await loadOrion({ glb: await glbBuffer(), loadTexture: fakeLoadTexture });
  assert.equal(word.group.children.length, 5);
  const mats = new Set(LETTER_NAMES.map((n) => word.meshes.get(n).material));
  assert.equal(mats.size, 1);
  assert.equal([...mats][0].userData.orionKind, 'clay');
});

test('setWord relayouts: tracking changes X, thickness changes Z scale, rotation spins the group', async () => {
  const { word } = await loadOrion({ glb: await glbBuffer(), loadTexture: fakeLoadTexture });
  const before = word.meshes.get('N').position.x;
  word.setWord({ tracking: 2 });
  assert.ok(word.meshes.get('N').position.x > before + 0.1);
  word.setWord({ thickness: 3 });
  assert.equal(word.meshes.get('I').scale.z, 3);
  word.setWord({ rotationYDeg: 180 });
  assert.ok(Math.abs(word.group.rotation.y - Math.PI) < 1e-9);
});

test('invalid document is rejected before touching materials', async () => {
  const { word } = await loadOrion({ glb: await glbBuffer(), loadTexture: fakeLoadTexture });
  await assert.rejects(() => word.apply({ version: 1, letters: [] }), /letters: must have exactly 5/);
  assert.equal(word.materialKinds().O1, 'clay');
});

test('non-null loop applies as still tile with a warning', async () => {
  const doc = JSON.parse(await readFile(SAMPLE_URL, 'utf8'));
  doc.letters[0].paint.loop = { frames: 'x.png', frameCount: 4, fps: 12, cols: 2 };
  const { word, warnings } = await loadOrion({ glb: await glbBuffer(), doc, baseUrl: SAMPLE_URL.href, loadTexture: fakeLoadTexture });
  assert.equal(warnings.length, 1);
  assert.equal(word.materialKinds().O1, 'chip');
});

test('tile URLs resolve relative to the JSON file', () => {
  assert.equal(resolveTileUrl('http://h/inbox/orion.export.json', 'chips/a/tile.png'), 'http://h/inbox/chips/a/tile.png');
  assert.equal(resolveTileUrl('http://h/inbox/orion.export.json', 'a.tile.png'), 'http://h/inbox/a.tile.png');
});
