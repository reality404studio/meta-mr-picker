import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { validateExport, assertExport, makeClayExport } from '../src/schema.js';

const SAMPLE_URL = new URL('../../../tools/web-orion/samples/orion.export.json', import.meta.url);

async function sample() {
  return JSON.parse(await readFile(SAMPLE_URL, 'utf8'));
}

test('sample export passes', async () => {
  const r = validateExport(await sample());
  assert.deepEqual(r.errors, []);
  assert.equal(r.ok, true);
});

test('clay default export passes', () => {
  assert.equal(validateExport(makeClayExport()).ok, true);
});

test('wrong letter count fails with path', async () => {
  const doc = await sample();
  doc.letters.pop();
  const r = validateExport(doc);
  assert.equal(r.ok, false);
  assert.ok(r.errors.some((e) => e.startsWith('letters:') && e.includes('5')), r.errors.join('\n'));
});

test('unknown letter name fails with index path', async () => {
  const doc = await sample();
  doc.letters[2].name = 'Z';
  const r = validateExport(doc);
  assert.ok(r.errors.some((e) => e.startsWith('letters[2].name')), r.errors.join('\n'));
});

test('duplicate letter name fails', async () => {
  const doc = await sample();
  doc.letters[3].name = 'O1';
  const r = validateExport(doc);
  assert.ok(r.errors.some((e) => e.includes('letters[3].name') && e.includes('duplicate')), r.errors.join('\n'));
});

test('paint.kind mismatch fails with path', async () => {
  const doc = await sample();
  doc.letters[0].paint.kind = 'cel';
  const r = validateExport(doc);
  assert.ok(r.errors.some((e) => e.startsWith('letters[0].paint.kind')), r.errors.join('\n'));
});

test('missing tile path fails with path', async () => {
  const doc = await sample();
  delete doc.letters[1].paint.tile;
  const r = validateExport(doc);
  assert.ok(r.errors.some((e) => e.startsWith('letters[1].paint.tile')), r.errors.join('\n'));
});

test('tile path escaping the JSON folder fails', async () => {
  const doc = await sample();
  doc.letters[1].paint.tile = '../secret/tile.png';
  assert.ok(validateExport(doc).errors.some((e) => e.startsWith('letters[1].paint.tile')));
  doc.letters[1].paint.tile = '/etc/tile.png';
  assert.ok(validateExport(doc).errors.some((e) => e.startsWith('letters[1].paint.tile')));
});

test('clay with color fields fails with path', async () => {
  const doc = await sample();
  doc.letters[4].paint = { kind: 'clay', lightTone: '#FFFFFF', tile: 'x.png' };
  const r = validateExport(doc);
  assert.ok(r.errors.some((e) => e.startsWith('letters[4].paint.lightTone')), r.errors.join('\n'));
  assert.ok(r.errors.some((e) => e.startsWith('letters[4].paint.tile')), r.errors.join('\n'));
});

test('bad hex, zero lightDir, missing loop fail with paths', async () => {
  const doc = await sample();
  doc.letters[0].paint.midTone = '#GG0000';
  doc.letters[0].paint.lightDir = [0, 0, 0];
  delete doc.letters[0].paint.loop;
  const r = validateExport(doc);
  for (const field of ['midTone', 'lightDir', 'loop']) {
    assert.ok(r.errors.some((e) => e.startsWith(`letters[0].paint.${field}`)), `${field}: ${r.errors.join('\n')}`);
  }
});

test('non-null loop is a warning, not an error', async () => {
  const doc = await sample();
  doc.letters[0].paint.loop = { frames: 'x.png', frameCount: 4, fps: 12, cols: 2 };
  const r = validateExport(doc);
  assert.equal(r.ok, true);
  assert.ok(r.warnings.some((w) => w.startsWith('letters[0].paint.loop')));
});

test('version and word validation', async () => {
  const doc = await sample();
  doc.version = 2;
  doc.word.thickness = 0;
  doc.word.tracking = 'wide';
  const r = validateExport(doc);
  assert.ok(r.errors.some((e) => e.startsWith('version')));
  assert.ok(r.errors.some((e) => e.startsWith('word.thickness')));
  assert.ok(r.errors.some((e) => e.startsWith('word.tracking')));
});

test('assertExport throws with all messages', () => {
  assert.throws(() => assertExport({ version: 1 }), /letters: must be an array/);
});
