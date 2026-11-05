export const LETTER_NAMES = ['O1', 'R', 'I', 'O2', 'N'];

export const PAINT_KINDS = ['chip', 'gradient', 'clay'];

export const WORD = {
  widthM: 0.615,
  heightM: 0.12,
  tracking: { min: 0, max: 2, step: 0.01, default: 1 },
  thickness: { min: 0.05, max: 3, step: 0.01, default: 1 },
  rotationYDeg: { min: -180, max: 180, step: 1, default: 0 },
};

export const LOOK = {
  clayColor: '#E7E7E7',
  clayRoughness: 1.0,
  outlineColor: '#15317C',
  outlineWidthM: 0.0025,
  outlineDefault: false,
  triplanarTilesPerM: 6,
  triplanarBlendSharpness: 4,
  rampShadowEnd: 0.35,
  rampLightStart: 0.65,
  rampSoftness: 0.12,
};

export const UI = {
  background: '#16171b',
  ink: '#F0EEE6',
  accent: '#D23A2C',
  fontFamily: "'IBM Plex Mono', ui-monospace, Menlo, monospace",
  bodyPx: 24,
  sliderLabelPx: 32,
  heroSpinDegPerSec: 12,
};

export const CAMERA = {
  fov: 30,
  editor: { position: [0, 0.08, 1.1], target: [0, 0.06, 0] },
  hero: { position: [0, 0.1, 1.0], target: [0, 0.06, 0] },
  look: { position: [0, 0.06, 0.95], target: [0, 0.06, 0] },
};

export const LIGHT = {
  keyDir: [0.6, 0.65, 0.45],
  keyIntensity: 3.0,
  fillIntensity: 1.4,
};

export const SERVER = {
  port: 5180,
  wsPath: '/ws',
  uploadPath: '/upload',
  maxPngBytes: 5 * 1024 * 1024,
  maxJsonBytes: 200 * 1024,
  nameRe: /^[A-Za-z0-9._-]+$/,
  roles: ['mac', 'quest'],
};

export const SAMPLE_EXPORT_URL = 'samples/orion.export.json';
