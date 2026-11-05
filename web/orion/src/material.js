import {
  Color,
  Vector3,
  ShaderMaterial,
  MeshStandardMaterial,
  BackSide,
  GLSL3,
  RepeatWrapping,
  SRGBColorSpace,
} from 'three';
import { LOOK } from './settings.js';
import { HEX_RE } from './schema.js';

export function parseHexColor(hex) {
  if (typeof hex !== 'string' || !HEX_RE.test(hex)) {
    throw new Error(`invalid color "${hex}" (expected #RRGGBB)`);
  }
  const n = parseInt(hex.slice(1), 16);
  return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
}

export function srgbToLinear(c) {
  return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
}

export function normalizeDir(dir, fallback = [0, 1, 0]) {
  if (!Array.isArray(dir) || dir.length !== 3 || !dir.every((c) => Number.isFinite(c))) {
    throw new Error(`invalid lightDir ${JSON.stringify(dir)}`);
  }
  const len = Math.hypot(dir[0], dir[1], dir[2]);
  if (len < 1e-9) return [...fallback];
  return [dir[0] / len, dir[1] / len, dir[2] / len];
}

export function unityToThreeDir(dir) {
  return [dir[0], dir[1], -dir[2]];
}

export function chipUniforms(paint, look = LOOK) {
  if (!paint || (paint.kind !== 'chip' && paint.kind !== 'gradient')) {
    throw new Error(`chipUniforms: paint.kind must be chip|gradient (got ${paint && paint.kind})`);
  }
  return {
    lightTone: parseHexColor(paint.lightTone).map(srgbToLinear),
    midTone: parseHexColor(paint.midTone).map(srgbToLinear),
    shadowTone: parseHexColor(paint.shadowTone).map(srgbToLinear),
    lightDir: unityToThreeDir(normalizeDir(paint.lightDir)),
    tilesPerM: look.triplanarTilesPerM,
    blendSharpness: look.triplanarBlendSharpness,
    rampShadowEnd: look.rampShadowEnd,
    rampLightStart: look.rampLightStart,
    rampSoftness: look.rampSoftness,
  };
}

export function materialKindFor(paint) {
  if (!paint) return 'clay';
  return paint.kind === 'clay' ? 'clay' : 'chip';
}

const VERT = /* glsl */ `
out vec3 vObjPos;
out vec3 vObjNormal;
out vec3 vWorldNormal;
void main() {
  vObjPos = position;
  vObjNormal = normalize(normal);
  vWorldNormal = normalize(mat3(modelMatrix) * normal);
  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}
`;

const FRAG = /* glsl */ `
precision highp float;
uniform sampler2D uTile;
uniform vec3 uLightTone;
uniform vec3 uMidTone;
uniform vec3 uShadowTone;
uniform vec3 uLightDir;
uniform float uTilesPerM;
uniform float uBlendSharpness;
uniform float uRampShadowEnd;
uniform float uRampLightStart;
uniform float uRampSoftness;
in vec3 vObjPos;
in vec3 vObjNormal;
in vec3 vWorldNormal;
out vec4 outColor;

vec3 triplanar(sampler2D tex, vec3 p, vec3 n) {
  vec3 w = pow(abs(n), vec3(uBlendSharpness));
  w /= (w.x + w.y + w.z);
  vec3 cx = texture(tex, p.zy * uTilesPerM).rgb;
  vec3 cy = texture(tex, p.xz * uTilesPerM).rgb;
  vec3 cz = texture(tex, p.xy * uTilesPerM).rgb;
  return cx * w.x + cy * w.y + cz * w.z;
}

void main() {
  vec3 albedo = triplanar(uTile, vObjPos, normalize(vObjNormal));
  float ndl = dot(normalize(vWorldNormal), normalize(uLightDir)) * 0.5 + 0.5;
  float toMid = smoothstep(uRampShadowEnd - uRampSoftness, uRampShadowEnd + uRampSoftness, ndl);
  float toLight = smoothstep(uRampLightStart - uRampSoftness, uRampLightStart + uRampSoftness, ndl);
  vec3 tone = mix(uShadowTone, uMidTone, toMid);
  tone = mix(tone, uLightTone, toLight);
  vec3 tint = tone / max(uMidTone, vec3(0.02));
  outColor = vec4(albedo * tint, 1.0);
}
`;

export function prepareTileTexture(texture) {
  texture.wrapS = RepeatWrapping;
  texture.wrapT = RepeatWrapping;
  texture.colorSpace = SRGBColorSpace;
  texture.needsUpdate = true;
  return texture;
}

export function createChipMaterial(paint, tileTexture, look = LOOK) {
  const u = chipUniforms(paint, look);
  const material = new ShaderMaterial({
    glslVersion: GLSL3,
    vertexShader: VERT,
    fragmentShader: FRAG,
    uniforms: {
      uTile: { value: tileTexture ? prepareTileTexture(tileTexture) : null },
      uLightTone: { value: new Vector3(...u.lightTone) },
      uMidTone: { value: new Vector3(...u.midTone) },
      uShadowTone: { value: new Vector3(...u.shadowTone) },
      uLightDir: { value: new Vector3(...u.lightDir) },
      uTilesPerM: { value: u.tilesPerM },
      uBlendSharpness: { value: u.blendSharpness },
      uRampShadowEnd: { value: u.rampShadowEnd },
      uRampLightStart: { value: u.rampLightStart },
      uRampSoftness: { value: u.rampSoftness },
    },
  });
  material.name = `orion-chip:${paint.id}`;
  material.userData.orionKind = 'chip';
  material.userData.paint = paint;
  return material;
}

export function createClayMaterial(look = LOOK) {
  const material = new MeshStandardMaterial({
    color: new Color(look.clayColor),
    roughness: look.clayRoughness,
    metalness: 0,
  });
  material.name = 'orion-clay';
  material.userData.orionKind = 'clay';
  return material;
}

export function createOutlineMaterial(look = LOOK) {
  const material = new ShaderMaterial({
    glslVersion: GLSL3,
    side: BackSide,
    uniforms: {
      uColor: { value: new Color(look.outlineColor) },
      uWidth: { value: look.outlineWidthM },
    },
    vertexShader: /* glsl */ `
      uniform float uWidth;
      void main() {
        vec3 p = position + normalize(normal) * uWidth;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(p, 1.0);
      }
    `,
    fragmentShader: /* glsl */ `
      precision highp float;
      uniform vec3 uColor;
      out vec4 outColor;
      void main() { outColor = vec4(uColor, 1.0); }
    `,
  });
  material.name = 'orion-outline';
  material.userData.orionKind = 'outline';
  return material;
}
