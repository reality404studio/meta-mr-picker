import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { createOrionServer } from '../server/server.mjs';
import { SERVER } from '../src/settings.js';

const CHROME = process.env.CHROME || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
const hasChrome = existsSync(CHROME);

async function waitFor(fn, { tries = 60, ms = 250 } = {}) {
  let last;
  for (let i = 0; i < tries; i++) {
    try {
      const v = await fn();
      if (v) return v;
    } catch (e) {
      last = e;
    }
    await new Promise((r) => setTimeout(r, ms));
  }
  throw last || new Error('waitFor timeout');
}

function cdp(wsUrl) {
  const ws = new WebSocket(wsUrl);
  let id = 0;
  const pending = new Map();
  const pageLog = [];
  ws.addEventListener('message', (ev) => {
    const msg = JSON.parse(ev.data);
    if (msg.method === 'Runtime.exceptionThrown') pageLog.push(msg.params.exceptionDetails?.exception?.description || msg.params.exceptionDetails?.text);
    if (msg.method === 'Runtime.consoleAPICalled') pageLog.push(msg.params.args.map((a) => a.value ?? a.description).join(' '));
    if (msg.id && pending.has(msg.id)) {
      const { resolve, reject } = pending.get(msg.id);
      pending.delete(msg.id);
      msg.error ? reject(new Error(msg.error.message)) : resolve(msg.result);
    }
  });
  const ready = new Promise((r) => ws.addEventListener('open', r));
  return {
    ready,
    send: (method, params = {}) =>
      new Promise((resolve, reject) => {
        const i = ++id;
        pending.set(i, { resolve, reject });
        ws.send(JSON.stringify({ id: i, method, params }));
      }),
    close: () => ws.close(),
    pageLog,
  };
}

async function evalJs(client, expression) {
  const { result, exceptionDetails } = await client.send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
  if (exceptionDetails) throw new Error(exceptionDetails.exception?.description || exceptionDetails.text);
  return result.value;
}

test('browser: editor presets, sliders -> quest relay, hero GLB export', { skip: !hasChrome && 'Chrome not installed' }, async () => {
  const inbox = await mkdtemp(path.join(os.tmpdir(), 'orion-browser-'));
  const profile = await mkdtemp(path.join(os.tmpdir(), 'orion-chrome-'));
  const app = createOrionServer({ port: 0, host: '127.0.0.1', dir: inbox });
  const addr = await app.listen();
  const base = `http://127.0.0.1:${addr.port}`;
  const chrome = spawn(
    CHROME,
    [
      '--headless=new',
      '--use-angle=swiftshader',
      '--enable-unsafe-swiftshader',
      '--window-size=1280,720',
      '--remote-debugging-port=0',
      `--user-data-dir=${profile}`,
      '--no-first-run',
      'about:blank',
    ],
    { stdio: 'ignore' },
  );
  let client;
  try {
    const debugPort = await waitFor(async () => Number((await readFile(path.join(profile, 'DevToolsActivePort'), 'utf8')).split('\n')[0]) || null);
    const targets = await waitFor(async () => {
      const res = await fetch(`http://127.0.0.1:${debugPort}/json/list`);
      const list = await res.json();
      return list.find((t) => t.type === 'page') || null;
    });
    client = cdp(targets.webSocketDebuggerUrl);
    await client.ready;
    await client.send('Page.enable');
    await client.send('Runtime.enable');

    const questMsgs = [];
    const quest = new WebSocket(`ws://127.0.0.1:${addr.port}${SERVER.wsPath}`);
    await new Promise((r) => quest.addEventListener('open', r));
    quest.addEventListener('message', (e) => questMsgs.push(JSON.parse(e.data)));
    quest.send(JSON.stringify({ type: 'hello', role: 'quest' }));

    await client.send('Page.navigate', { url: `${base}/?mode=editor` });
    await waitFor(() => evalJs(client, `document.body.dataset.ready === '1'`)).catch((e) => {
      throw new Error(`editor never became ready: ${e.message}\n${client.pageLog.join('\n')}`);
    });
    assert.deepEqual(await evalJs(client, `window.orion.word.materialKinds()`), { O1: 'clay', R: 'clay', I: 'clay', O2: 'clay', N: 'clay' });
    assert.equal(await evalJs(client, `document.querySelectorAll('#presets button').length`), 4);

    await evalJs(client, `document.querySelector('#presets button[data-preset="yunseul"]').click()`);
    await waitFor(() => evalJs(client, `window.orion.word.materialKinds().O1 === 'chip'`));
    assert.equal(await evalJs(client, `window.orion.word.meshes.get('N').material.uniforms.uTile.value.image.width`), 512);

    await evalJs(client, `(() => { const s = document.getElementById('s-tracking'); s.value = 1.6; s.dispatchEvent(new Event('input', { bubbles: true })); })()`);
    await waitFor(() => questMsgs.some((m) => m.type === 'slider' && m.key === 'tracking'));
    const slider = questMsgs.find((m) => m.type === 'slider' && m.key === 'tracking');
    assert.equal(slider.value, 1.6);
    assert.equal(await evalJs(client, `window.orion.word.doc.word.tracking`), 1.6);
    assert.equal(await evalJs(client, `document.getElementById('dot-quest').classList.contains('on')`), true);

    quest.send(JSON.stringify({ type: 'pullout', t: 1 }));
    await waitFor(() => evalJs(client, `window.orion.word.group.visible === false`));

    await client.send('Page.navigate', { url: `${base}/?mode=hero&spin=0` });
    await waitFor(() => evalJs(client, `document.body.dataset.ready === '1' && window.orion.mode === 'hero'`));
    assert.deepEqual(await evalJs(client, `window.orion.word.materialKinds()`), { O1: 'chip', R: 'chip', I: 'chip', O2: 'chip', N: 'chip' });

    const glbHeader = await evalJs(
      client,
      `(async () => {
        const { GLTFExporter } = await import('/vendor/three/addons/exporters/GLTFExporter.js');
        const buf = await new GLTFExporter().parseAsync(window.orion.word.group, { binary: true, onlyVisible: true });
        const u8 = new Uint8Array(buf);
        return { magic: String.fromCharCode(...u8.subarray(0, 4)), bytes: u8.length };
      })()`,
    );
    assert.equal(glbHeader.magic, 'glTF');
    assert.ok(glbHeader.bytes > 10000, `glb bytes ${glbHeader.bytes}`);

    const doc = JSON.stringify({
      version: 1,
      exportedAt: '2026-11-06T03:00:00+09:00',
      word: { tracking: 1.2, thickness: 2, rotationYDeg: 0 },
      letters: ['O1', 'R', 'I', 'O2', 'N'].map((name) => ({ name, offset: [0, 0, 0], paint: { kind: 'clay' } })),
    });
    const up = await fetch(`${base}${SERVER.uploadPath}?name=orion.export.json`, { method: 'POST', body: doc });
    assert.equal(up.status, 200);
    await waitFor(() => evalJs(client, `window.orion.word.materialKinds().O1 === 'clay' && window.orion.word.doc.word.thickness === 2`));
    assert.equal(await evalJs(client, `window.orion.word.meshes.get('I').scale.z`), 2);

    quest.close();
  } finally {
    client?.close();
    const exited = new Promise((r) => chrome.once('exit', r));
    chrome.kill('SIGKILL');
    await exited;
    await app.close();
    await rm(inbox, { recursive: true, force: true });
    await rm(profile, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 });
  }
});
