import test from 'node:test';
import assert from 'node:assert/strict';
import { layoutLetters, centerSpan, wordExtent, wordCenterX } from '../src/layout.js';
import { LETTER_NAMES, WORD } from '../src/settings.js';

const bounds = {
  O1: { minX: -0.3073, maxX: -0.1686, originX: -0.2379, originY: 0, originZ: 0 },
  R: { minX: -0.1579, maxX: -0.0344, originX: -0.0961, originY: 0.0027, originZ: 0 },
  I: { minX: -0.0275, maxX: 0.0139, originX: -0.0068, originY: 0.0027, originZ: 0 },
  O2: { minX: 0.0246, maxX: 0.1634, originX: 0.094, originY: 0, originZ: 0 },
  N: { minX: 0.1741, maxX: 0.3073, originX: 0.2407, originY: 0.0027, originZ: 0 },
};
const near = (a, b, eps = 1e-6) => Math.abs(a - b) < eps;

test('natural width of the GLB is the 0.615 m baseline', () => {
  assert.ok(near(wordExtent(bounds, 1), WORD.widthM, 2e-3));
  assert.ok(near(wordCenterX(bounds), 0, 1e-3));
});

test('tracking 1 reproduces the GLB positions', () => {
  const { letters, rotationYRad } = layoutLetters({ bounds, word: { tracking: 1, thickness: 1, rotationYDeg: 0 }, letters: [] });
  for (const name of LETTER_NAMES) {
    assert.ok(near(letters[name].position[0], bounds[name].originX), name);
    assert.ok(near(letters[name].position[1], bounds[name].originY), name);
    assert.deepEqual(letters[name].scale, [1, 1, 1]);
  }
  assert.equal(rotationYRad, 0);
});

test('center span scales linearly with tracking', () => {
  for (const t of [0, 0.5, 1, 1.5, 2]) {
    assert.ok(near(centerSpan(bounds, t), centerSpan(bounds, 1) * t), `tracking ${t}`);
    const { letters } = layoutLetters({ bounds, word: { tracking: t, thickness: 1, rotationYDeg: 0 }, letters: [] });
    const centers = LETTER_NAMES.map((n) => letters[n].position[0] - (bounds[n].originX - (bounds[n].minX + bounds[n].maxX) / 2));
    assert.ok(near(Math.max(...centers) - Math.min(...centers), centerSpan(bounds, t)), `tracking ${t} placed`);
  }
});

test('tracking widens the word extent by the center span delta', () => {
  const w1 = wordExtent(bounds, 1);
  const w2 = wordExtent(bounds, 2);
  assert.ok(near(w2 - w1, centerSpan(bounds, 1), 1e-6));
});

test('thickness maps to Z scale only, rotation to radians', () => {
  const { letters, rotationYRad } = layoutLetters({ bounds, word: { tracking: 1, thickness: 2.5, rotationYDeg: 90 }, letters: [] });
  for (const name of LETTER_NAMES) assert.deepEqual(letters[name].scale, [1, 1, 2.5]);
  assert.ok(near(rotationYRad, Math.PI / 2));
});

test('letter offsets are added after tracking', () => {
  const { letters } = layoutLetters({
    bounds,
    word: { tracking: 1.5, thickness: 1, rotationYDeg: 0 },
    letters: [{ name: 'I', offset: [0.01, 0.05, -0.02] }],
  });
  const base = layoutLetters({ bounds, word: { tracking: 1.5, thickness: 1, rotationYDeg: 0 }, letters: [] }).letters.I.position;
  assert.ok(near(letters.I.position[0] - base[0], 0.01));
  assert.ok(near(letters.I.position[1] - base[1], 0.05));
  assert.ok(near(letters.I.position[2] - base[2], -0.02));
  assert.ok(near(letters.R.position[0], base[0] + (bounds.R.originX - bounds.I.originX) * 1.5, 1e-3));
});

test('missing word values fall back to settings defaults', () => {
  const a = layoutLetters({ bounds, word: {}, letters: [] });
  const b = layoutLetters({ bounds, word: { tracking: WORD.tracking.default, thickness: WORD.thickness.default, rotationYDeg: WORD.rotationYDeg.default }, letters: [] });
  assert.deepEqual(a, b);
});

test('missing bounds throw', () => {
  assert.throws(() => layoutLetters({ bounds: { ...bounds, N: undefined }, word: {}, letters: [] }), /missing bounds for N/);
});
