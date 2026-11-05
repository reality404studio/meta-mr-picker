import { LETTER_NAMES, WORD } from './settings.js';

function requireBounds(bounds) {
  for (const name of LETTER_NAMES) {
    const b = bounds?.[name];
    if (!b || ![b.minX, b.maxX, b.originX, b.originY, b.originZ].every(Number.isFinite)) {
      throw new Error(`layout: missing bounds for ${name}`);
    }
  }
}

export function wordCenterX(bounds) {
  requireBounds(bounds);
  const min = Math.min(...LETTER_NAMES.map((n) => bounds[n].minX));
  const max = Math.max(...LETTER_NAMES.map((n) => bounds[n].maxX));
  return (min + max) / 2;
}

export function layoutLetters({ bounds, word, letters }) {
  const tracking = Number.isFinite(word?.tracking) ? word.tracking : WORD.tracking.default;
  const thickness = Number.isFinite(word?.thickness) ? word.thickness : WORD.thickness.default;
  const rotationYDeg = Number.isFinite(word?.rotationYDeg) ? word.rotationYDeg : WORD.rotationYDeg.default;
  const center = wordCenterX(bounds);
  const offsets = new Map((letters || []).map((l) => [l.name, l.offset || [0, 0, 0]]));
  const placed = {};
  for (const name of LETTER_NAMES) {
    const b = bounds[name];
    const glyphCenter = (b.minX + b.maxX) / 2;
    const pivotToOrigin = b.originX - glyphCenter;
    const off = offsets.get(name) || [0, 0, 0];
    placed[name] = {
      position: [
        center + (glyphCenter - center) * tracking + pivotToOrigin + off[0],
        b.originY + off[1],
        b.originZ + off[2],
      ],
      scale: [1, 1, thickness],
    };
  }
  return { letters: placed, rotationYRad: (rotationYDeg * Math.PI) / 180, center };
}

export function centerSpan(bounds, tracking) {
  requireBounds(bounds);
  const centers = LETTER_NAMES.map((n) => (bounds[n].minX + bounds[n].maxX) / 2);
  return (Math.max(...centers) - Math.min(...centers)) * tracking;
}

export function wordExtent(bounds, tracking) {
  const center = wordCenterX(bounds);
  let min = Infinity;
  let max = -Infinity;
  for (const name of LETTER_NAMES) {
    const b = bounds[name];
    const half = (b.maxX - b.minX) / 2;
    const c = center + ((b.minX + b.maxX) / 2 - center) * tracking;
    min = Math.min(min, c - half);
    max = Math.max(max, c + half);
  }
  return max - min;
}
