import { LETTER_NAMES, PAINT_KINDS } from './settings.js';

export const HEX_RE = /^#[0-9a-fA-F]{6}$/;
const COLOR_FIELDS = ['lightTone', 'midTone', 'shadowTone'];
const TILE_FIELDS = ['id', 'tile', 'lightDir', 'loop', ...COLOR_FIELDS];

function isNumber(v) {
  return typeof v === 'number' && Number.isFinite(v);
}

function isVec3(v) {
  return Array.isArray(v) && v.length === 3 && v.every(isNumber);
}

function isPlainObject(v) {
  return v !== null && typeof v === 'object' && !Array.isArray(v);
}

function safeRelativePath(p) {
  if (typeof p !== 'string' || p.length === 0) return false;
  if (p.startsWith('/') || p.startsWith('\\') || /^[A-Za-z]:/.test(p)) return false;
  if (/^[a-z]+:/i.test(p)) return false;
  return !p.split(/[\\/]/).some((seg) => seg === '..');
}

function validateWord(word, errors) {
  if (!isPlainObject(word)) {
    errors.push('word: must be an object');
    return;
  }
  for (const key of ['tracking', 'thickness', 'rotationYDeg']) {
    if (!isNumber(word[key])) errors.push(`word.${key}: must be a finite number`);
  }
  if (isNumber(word.tracking) && word.tracking < 0) errors.push('word.tracking: must be >= 0');
  if (isNumber(word.thickness) && word.thickness <= 0) errors.push('word.thickness: must be > 0');
}

function validatePaint(paint, path, errors) {
  if (!isPlainObject(paint)) {
    errors.push(`${path}: must be an object`);
    return;
  }
  if (!PAINT_KINDS.includes(paint.kind)) {
    errors.push(`${path}.kind: must be one of ${PAINT_KINDS.join('|')}`);
    return;
  }
  if (paint.kind === 'clay') {
    for (const f of TILE_FIELDS) {
      if (f in paint) errors.push(`${path}.${f}: not allowed when kind is "clay"`);
    }
    return;
  }
  if (typeof paint.id !== 'string' || paint.id.length === 0) errors.push(`${path}.id: must be a non-empty string`);
  for (const f of COLOR_FIELDS) {
    if (!HEX_RE.test(paint[f] ?? '')) errors.push(`${path}.${f}: must be a #RRGGBB color`);
  }
  if (!isVec3(paint.lightDir)) errors.push(`${path}.lightDir: must be [x, y, z] numbers`);
  else if (paint.lightDir.every((c) => c === 0)) errors.push(`${path}.lightDir: must not be a zero vector`);
  if (!safeRelativePath(paint.tile)) errors.push(`${path}.tile: must be a relative path without ".."`);
  if (!('loop' in paint)) errors.push(`${path}.loop: required (null until loop chips ship)`);
}

export function validateExport(doc) {
  const errors = [];
  const warnings = [];
  if (!isPlainObject(doc)) return { ok: false, errors: ['root: must be an object'], warnings };
  if (doc.version !== 1) errors.push('version: must be 1');
  if (typeof doc.exportedAt !== 'string' || Number.isNaN(Date.parse(doc.exportedAt))) {
    errors.push('exportedAt: must be an ISO-8601 date string');
  }
  validateWord(doc.word, errors);
  if (!Array.isArray(doc.letters)) {
    errors.push('letters: must be an array');
  } else {
    if (doc.letters.length !== LETTER_NAMES.length) {
      errors.push(`letters: must have exactly ${LETTER_NAMES.length} entries (got ${doc.letters.length})`);
    }
    const seen = new Set();
    doc.letters.forEach((letter, i) => {
      const path = `letters[${i}]`;
      if (!isPlainObject(letter)) {
        errors.push(`${path}: must be an object`);
        return;
      }
      if (!LETTER_NAMES.includes(letter.name)) {
        errors.push(`${path}.name: must be one of ${LETTER_NAMES.join(' ')}`);
      } else if (seen.has(letter.name)) {
        errors.push(`${path}.name: duplicate "${letter.name}"`);
      }
      seen.add(letter.name);
      if (!isVec3(letter.offset)) errors.push(`${path}.offset: must be [x, y, z] meters`);
      validatePaint(letter.paint, `${path}.paint`, errors);
      if (isPlainObject(letter.paint) && letter.paint.kind !== 'clay' && letter.paint.loop != null) {
        warnings.push(`${path}.paint.loop: loop chips are not supported yet; drawing the still tile`);
      }
    });
    if (errors.length === 0) {
      const missing = LETTER_NAMES.filter((n) => !seen.has(n));
      if (missing.length) errors.push(`letters: missing ${missing.join(' ')}`);
    }
  }
  return { ok: errors.length === 0, errors, warnings };
}

export function assertExport(doc) {
  const result = validateExport(doc);
  if (!result.ok) {
    const err = new Error(`orion.export.json invalid:\n  ${result.errors.join('\n  ')}`);
    err.errors = result.errors;
    throw err;
  }
  return result;
}

export function makeClayExport(now = new Date()) {
  return {
    version: 1,
    exportedAt: now.toISOString(),
    word: { tracking: 1, thickness: 1, rotationYDeg: 0 },
    letters: LETTER_NAMES.map((name) => ({ name, offset: [0, 0, 0], paint: { kind: 'clay' } })),
  };
}
