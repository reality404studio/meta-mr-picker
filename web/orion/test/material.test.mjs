import './register_three.mjs';
import test from 'node:test';
import assert from 'node:assert/strict';

const {
  parseHexColor,
  normalizeDir,
  chipUniforms,
  materialKindFor,
  createChipMaterial,
  createClayMaterial,
  createOutlineMaterial,
  srgbToLinear,
  unityToThreeDir,
} = await import('../src/material.js');
const { LOOK } = await import('../src/settings.js');

const chip = {
  kind: 'chip',
  id: 'p1',
  lightTone: '#F5EDE0',
  midTone: '#a69989',
  shadowTone: '#5C5449',
  lightDir: [0.6, 0.65, -0.45],
  tile: 'chips/p1/tile.png',
  loop: null,
};

test('parseHexColor accepts upper and lower case hex', () => {
  assert.deepEqual(parseHexColor('#FF0000'), [1, 0, 0]);
  assert.deepEqual(parseHexColor('#00ff00'), [0, 1, 0]);
  const [r, g, b] = parseHexColor('#A69989');
  assert.ok(Math.abs(r - 0xa6 / 255) < 1e-9 && Math.abs(g - 0x99 / 255) < 1e-9 && Math.abs(b - 0x89 / 255) < 1e-9);
});

test('parseHexColor rejects bad hex', () => {
  for (const bad of ['#FFF', 'FF0000', '#GG0000', '#1234567', null, 12]) {
    assert.throws(() => parseHexColor(bad), /invalid color/);
  }
});

test('srgbToLinear endpoints', () => {
  assert.equal(srgbToLinear(0), 0);
  assert.ok(Math.abs(srgbToLinear(1) - 1) < 1e-12);
  assert.ok(srgbToLinear(0.5) < 0.5);
});

test('normalizeDir normalizes and falls back on zero vector', () => {
  const n = normalizeDir([3, 0, 4]);
  assert.deepEqual(n, [0.6, 0, 0.8]);
  assert.deepEqual(normalizeDir([0, 0, 0]), [0, 1, 0]);
  assert.deepEqual(normalizeDir([0, 0, 0], [0, 0, 1]), [0, 0, 1]);
  assert.throws(() => normalizeDir([1, 2]), /invalid lightDir/);
  assert.throws(() => normalizeDir([1, NaN, 0]), /invalid lightDir/);
});

test('chipUniforms converts colors to linear and normalizes lightDir', () => {
  const u = chipUniforms(chip);
  const len = Math.hypot(...u.lightDir);
  assert.ok(Math.abs(len - 1) < 1e-9);
  assert.ok(u.midTone.every((c) => c > 0 && c < 1));
  assert.ok(u.lightTone[0] > u.midTone[0] && u.midTone[0] > u.shadowTone[0]);
  assert.equal(u.tilesPerM, LOOK.triplanarTilesPerM);
});

test('lightDir comes from Unity (left-handed): z is negated for three.js', () => {
  assert.deepEqual(unityToThreeDir([0.6, 0.65, -0.45]), [0.6, 0.65, 0.45]);
  const u = chipUniforms(chip);
  assert.ok(u.lightDir[2] > 0, 'light from the viewer side in three.js space');
  assert.ok(u.lightDir[0] > 0 && u.lightDir[1] > 0);
});

test('chipUniforms rejects clay', () => {
  assert.throws(() => chipUniforms({ kind: 'clay' }), /chip\|gradient/);
});

test('materialKindFor branches clay vs chip vs gradient', () => {
  assert.equal(materialKindFor({ kind: 'clay' }), 'clay');
  assert.equal(materialKindFor(chip), 'chip');
  assert.equal(materialKindFor({ ...chip, kind: 'gradient' }), 'chip');
  assert.equal(materialKindFor(undefined), 'clay');
});

test('createClayMaterial is matte #E7E7E7 standard material', () => {
  const m = createClayMaterial();
  assert.equal(m.isMeshStandardMaterial, true);
  assert.equal(m.roughness, 1);
  assert.equal(m.metalness, 0);
  assert.equal('#' + m.color.getHexString().toUpperCase(), LOOK.clayColor);
  assert.equal(m.userData.orionKind, 'clay');
});

test('createChipMaterial is a GLSL3 triplanar shader with 3 tones', () => {
  const m = createChipMaterial(chip, null);
  assert.equal(m.isShaderMaterial, true);
  assert.equal(m.glslVersion, '300 es');
  assert.match(m.fragmentShader, /triplanar/);
  for (const name of ['uTile', 'uLightTone', 'uMidTone', 'uShadowTone', 'uLightDir']) assert.ok(name in m.uniforms, name);
  assert.ok(Math.abs(m.uniforms.uLightDir.value.length() - 1) < 1e-9);
  assert.equal(m.userData.orionKind, 'chip');
});

test('createOutlineMaterial renders back faces in outline color', () => {
  const m = createOutlineMaterial();
  assert.equal(m.side, 1);
  assert.equal('#' + m.uniforms.uColor.value.getHexString().toUpperCase(), LOOK.outlineColor);
});
