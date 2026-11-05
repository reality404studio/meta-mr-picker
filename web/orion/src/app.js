import {
  Clock,
  Color,
  DirectionalLight,
  HemisphereLight,
  MeshStandardMaterial,
  PerspectiveCamera,
  Scene,
  SRGBColorSpace,
  TextureLoader,
  WebGLRenderer,
  Vector3,
} from 'three';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';
import { CAMERA, LETTER_NAMES, LIGHT, SAMPLE_EXPORT_URL, SERVER, UI, WORD } from './settings.js';
import { loadOrion, resolveTileUrl } from './loader.js';
import { makeClayExport } from './schema.js';

const params = new URLSearchParams(location.search);
const mode = ['editor', 'hero', 'look'].includes(params.get('mode')) ? params.get('mode') : 'editor';
document.body.dataset.mode = mode;
document.documentElement.style.setProperty('--body', `${UI.bodyPx}px`);
document.documentElement.style.setProperty('--slider-label', `${UI.sliderLabelPx}px`);

const $ = (id) => document.getElementById(id);
const toast = (text) => {
  $('toast').textContent = text;
  if (text) setTimeout(() => ($('toast').textContent = ''), 4000);
};

const canvas = $('view');
const renderer = new WebGLRenderer({ canvas, antialias: true, preserveDrawingBuffer: true });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.outputColorSpace = SRGBColorSpace;
const scene = new Scene();
scene.background = new Color(UI.background);
const camera = new PerspectiveCamera(CAMERA.fov, 1, 0.01, 20);
const cam = CAMERA[mode];
camera.position.fromArray(cam.position);
camera.lookAt(new Vector3(...cam.target));

const key = new DirectionalLight(0xffffff, LIGHT.keyIntensity);
key.position.fromArray(LIGHT.keyDir);
scene.add(key);
scene.add(new HemisphereLight(0xffffff, 0x444444, LIGHT.fillIntensity));

function resize() {
  const { clientWidth: w, clientHeight: h } = canvas.parentElement;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
}
addEventListener('resize', resize);
resize();

const textureLoader = new TextureLoader();
const loadTexture = (url) => textureLoader.loadAsync(url);

async function fetchDoc(url) {
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw new Error(`${url}: HTTP ${res.status}`);
  return { doc: await res.json(), baseUrl: new URL(url, location.href).href };
}

const glb = await (await fetch('./ORION.glb')).arrayBuffer();
const { word } = await loadOrion({ glb, loadTexture });
scene.add(word.group);
window.orion = { word, scene, renderer, mode };

let samplePaints = [];
let sampleBase = new URL(SAMPLE_EXPORT_URL, location.href).href;
try {
  const { doc, baseUrl } = await fetchDoc(SAMPLE_EXPORT_URL);
  sampleBase = baseUrl;
  const seen = new Set();
  for (const l of doc.letters) {
    if (l.paint.kind !== 'clay' && !seen.has(l.paint.id)) {
      seen.add(l.paint.id);
      samplePaints.push(l.paint);
    }
  }
  if (mode !== 'editor') await applyDoc(doc, baseUrl);
} catch (err) {
  toast(`samples unavailable: ${err.message}`);
}

async function applyDoc(doc, baseUrl) {
  const { warnings } = await word.apply(doc, { baseUrl });
  if (warnings.length) toast(warnings.join('\n'));
  syncSliders();
  updateLoaderLine(baseUrl);
}

function syncSliders() {
  for (const k of ['tracking', 'thickness', 'rotationYDeg']) {
    const input = $(`s-${k}`);
    if (!input) continue;
    input.value = word.doc.word[k];
    $(`o-${k}`).textContent = formatValue(k, word.doc.word[k]);
  }
}

function formatValue(k, v) {
  return k === 'rotationYDeg' ? `${Math.round(v)}°` : Number(v).toFixed(2);
}

function updateLoaderLine(baseUrl) {
  const el = $('loader-line');
  if (!el) return;
  const rel = baseUrl ? baseUrl.replace(location.origin, '') : SAMPLE_EXPORT_URL;
  el.textContent = `loadOrion({ glb: 'ORION.glb', doc: '${rel}' })`;
}

let socket = null;
let sendQueue = [];
function send(msg) {
  const text = JSON.stringify(msg);
  if (socket && socket.readyState === WebSocket.OPEN) socket.send(text);
  else sendQueue.push(text);
}

function connectSocket() {
  const proto = location.protocol === 'https:' ? 'wss' : 'ws';
  socket = new WebSocket(`${proto}://${location.host}${SERVER.wsPath}`);
  socket.addEventListener('open', () => {
    $('dot-server').classList.add('on');
    socket.send(JSON.stringify({ type: 'hello', role: 'mac' }));
    for (const t of sendQueue) socket.send(t);
    sendQueue = [];
  });
  socket.addEventListener('close', () => {
    $('dot-server').classList.remove('on');
    $('dot-quest').classList.remove('on');
    setTimeout(connectSocket, 1500);
  });
  socket.addEventListener('message', async (ev) => {
    let msg;
    try {
      msg = JSON.parse(ev.data);
    } catch {
      return;
    }
    if (msg.type === 'welcome') $('dot-quest').classList.toggle('on', msg.peers > 0);
    if (msg.type === 'peer' && msg.role === 'quest') $('dot-quest').classList.toggle('on', msg.online);
    if (msg.type === 'pullout' && mode === 'editor') {
      word.group.visible = false;
      toast('PULLED OUT');
    }
    if (msg.type === 'export' && mode === 'hero' && !msg.replay) {
      try {
        const { doc, baseUrl } = await fetchDoc(msg.url || `/inbox/${msg.path}`);
        await applyDoc(doc, baseUrl);
        word.group.visible = true;
      } catch (err) {
        toast(`export failed: ${err.message}`);
      }
    }
  });
}
if (location.protocol.startsWith('http')) connectSocket();

if (mode === 'editor') {
  for (const k of ['tracking', 'thickness', 'rotationYDeg']) {
    const input = $(`s-${k}`);
    const spec = WORD[k];
    input.min = spec.min;
    input.max = spec.max;
    input.step = spec.step;
    input.addEventListener('input', () => {
      const value = Number(input.value);
      word.setWord({ [k]: value });
      $(`o-${k}`).textContent = formatValue(k, value);
      send({ type: 'slider', key: k, value });
    });
  }
  syncSliders();
  const presets = $('presets');
  for (const paint of samplePaints) {
    const btn = document.createElement('button');
    btn.dataset.preset = paint.id;
    btn.textContent = paint.id.replace(/^paint_\d+_\d+_/, 'CHIP ').replace(/_sample$/, '').toUpperCase();
    presets.appendChild(btn);
  }
  presets.addEventListener('click', async (ev) => {
    const btn = ev.target.closest('button[data-preset]');
    if (!btn) return;
    const id = btn.dataset.preset;
    const paint = id === 'clay' ? { kind: 'clay' } : samplePaints.find((p) => p.id === id);
    const doc = structuredClone(word.doc);
    for (const l of doc.letters) l.paint = structuredClone(paint);
    await word.apply(doc, { baseUrl: sampleBase });
    for (const b of presets.querySelectorAll('button')) b.classList.toggle('on', b === btn);
  });
}

if (mode === 'hero') {
  $('btn-outline').addEventListener('click', (ev) => {
    const on = !word.outlines.get('O1').visible;
    word.setOutline(on);
    ev.currentTarget.textContent = on ? 'OUTLINE ON' : 'OUTLINE OFF';
  });
  $('btn-download').addEventListener('click', async () => {
    const original = new Map();
    for (const name of LETTER_NAMES) {
      const mesh = word.meshes.get(name);
      original.set(name, mesh.material);
      if (mesh.material.userData.orionKind === 'chip') {
        const tile = mesh.material.uniforms.uTile.value;
        mesh.material = new MeshStandardMaterial({ map: tile, roughness: 1, metalness: 0 });
      }
    }
    try {
      const result = await new GLTFExporter().parseAsync(word.group, { binary: true, onlyVisible: true });
      const blob = new Blob([result], { type: 'model/gltf-binary' });
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = 'ORION.export.glb';
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 2000);
    } finally {
      for (const [name, m] of original) word.meshes.get(name).material = m;
    }
  });
  updateLoaderLine(sampleBase);
}

if (params.get('doc')) {
  try {
    const { doc, baseUrl } = await fetchDoc(params.get('doc'));
    await applyDoc(doc, baseUrl);
  } catch (err) {
    toast(`doc failed: ${err.message}`);
  }
} else if (mode === 'editor') {
  await word.apply(makeClayExport());
  syncSliders();
}

const clock = new Clock();
let frames = 0;
function frame() {
  const dt = clock.getDelta();
  if (mode === 'hero' && params.get('spin') !== '0') word.group.rotation.y += ((UI.heroSpinDegPerSec * Math.PI) / 180) * dt;
  renderer.render(scene, camera);
  frames += 1;
  if (frames === 3) document.body.dataset.ready = '1';
  requestAnimationFrame(frame);
}
frame();
export { word, scene, renderer, camera, mode, resolveTileUrl };
