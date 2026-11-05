import { Box3, Group, Mesh } from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { LETTER_NAMES, LOOK } from './settings.js';
import { assertExport, makeClayExport, validateExport } from './schema.js';
import { createChipMaterial, createClayMaterial, createOutlineMaterial, materialKindFor } from './material.js';
import { layoutLetters } from './layout.js';

export async function parseOrionGlb(arrayBuffer) {
  const gltf = await new GLTFLoader().parseAsync(arrayBuffer, '');
  const meshes = new Map();
  gltf.scene.traverse((o) => {
    if (o.isMesh && LETTER_NAMES.includes(o.name)) meshes.set(o.name, o);
  });
  const missing = LETTER_NAMES.filter((n) => !meshes.has(n));
  if (missing.length) throw new Error(`ORION.glb: missing letter nodes ${missing.join(' ')}`);
  return { scene: gltf.scene, meshes };
}

export function measureBounds(meshes) {
  const bounds = {};
  const box = new Box3();
  for (const name of LETTER_NAMES) {
    const mesh = meshes.get(name);
    mesh.updateWorldMatrix(true, false);
    mesh.geometry.computeBoundingBox();
    box.copy(mesh.geometry.boundingBox).applyMatrix4(mesh.matrixWorld);
    bounds[name] = {
      minX: box.min.x,
      maxX: box.max.x,
      originX: mesh.position.x,
      originY: mesh.position.y,
      originZ: mesh.position.z,
    };
  }
  return bounds;
}

export function resolveTileUrl(baseUrl, tilePath) {
  return new URL(tilePath, baseUrl).href;
}

export class OrionWord {
  constructor({ meshes, bounds, loadTexture, look = LOOK }) {
    this.meshes = meshes;
    this.bounds = bounds;
    this.loadTexture = loadTexture;
    this.look = look;
    this.group = new Group();
    this.group.name = 'ORION';
    this.outlines = new Map();
    this.clay = createClayMaterial(look);
    this.doc = makeClayExport();
    this.textureCache = new Map();
    for (const name of LETTER_NAMES) {
      const mesh = meshes.get(name);
      mesh.material = this.clay;
      this.group.add(mesh);
      const outline = new Mesh(mesh.geometry, createOutlineMaterial(look));
      outline.name = `${name}_outline`;
      outline.visible = look.outlineDefault;
      mesh.add(outline);
      this.outlines.set(name, outline);
    }
  }

  async apply(doc, { baseUrl = '' } = {}) {
    assertExport(doc);
    const { warnings } = validateExport(doc);
    this.doc = structuredClone(doc);
    const byName = new Map(doc.letters.map((l) => [l.name, l]));
    for (const name of LETTER_NAMES) {
      const mesh = this.meshes.get(name);
      const paint = byName.get(name).paint;
      if (materialKindFor(paint) === 'clay') {
        mesh.material = this.clay;
        continue;
      }
      const key = paint.tile ? resolveTileUrl(baseUrl || 'file:///', paint.tile) : null;
      let texture = this.textureCache.get(key);
      if (!texture && this.loadTexture) {
        texture = await this.loadTexture(key, paint);
        this.textureCache.set(key, texture);
      }
      mesh.material = createChipMaterial(paint, texture, this.look);
    }
    this.relayout();
    return { warnings };
  }

  setWord(partial) {
    Object.assign(this.doc.word, partial);
    this.relayout();
  }

  setLetterPaint(name, paint) {
    const letter = this.doc.letters.find((l) => l.name === name);
    letter.paint = paint;
  }

  setOutline(on) {
    for (const o of this.outlines.values()) o.visible = !!on;
  }

  relayout() {
    const placed = layoutLetters({ bounds: this.bounds, word: this.doc.word, letters: this.doc.letters });
    for (const name of LETTER_NAMES) {
      const mesh = this.meshes.get(name);
      mesh.position.fromArray(placed.letters[name].position);
      mesh.scale.fromArray(placed.letters[name].scale);
    }
    this.group.rotation.y = placed.rotationYRad;
  }

  materialKinds() {
    const out = {};
    for (const name of LETTER_NAMES) out[name] = this.meshes.get(name).material.userData.orionKind;
    return out;
  }
}

export async function loadOrion({ glb, doc, baseUrl, loadTexture, look }) {
  const { meshes } = await parseOrionGlb(glb);
  const bounds = measureBounds(meshes);
  const word = new OrionWord({ meshes, bounds, loadTexture, look });
  const result = doc ? await word.apply(doc, { baseUrl }) : { warnings: [] };
  word.relayout();
  return { word, warnings: result.warnings };
}
