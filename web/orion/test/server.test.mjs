import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { createOrionServer, safeUploadName, uploadLimitFor, resolveUnder } from '../server/server.mjs';
import { encodeFrame, decodeFrames } from '../server/ws-lite.mjs';
import { SERVER } from '../src/settings.js';

function connect(url, role) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(url);
    const inbox = [];
    const waiters = [];
    ws.addEventListener('message', (ev) => {
      const msg = JSON.parse(ev.data);
      const w = waiters.shift();
      if (w) w(msg);
      else inbox.push(msg);
    });
    ws.addEventListener('error', (e) => reject(e));
    ws.addEventListener('open', () => {
      ws.send(JSON.stringify({ type: 'hello', role }));
      resolve({
        ws,
        send: (m) => ws.send(JSON.stringify(m)),
        next: (ms = 2000) =>
          new Promise((res, rej) => {
            if (inbox.length) return res(inbox.shift());
            const t = setTimeout(() => rej(new Error('timeout waiting for message')), ms);
            waiters.push((m) => {
              clearTimeout(t);
              res(m);
            });
          }),
        quiet: (ms = 150) => new Promise((res) => setTimeout(() => res(inbox.length === 0), ms)),
        close: () => ws.close(),
      });
    });
  });
}

async function withServer(fn) {
  const dir = await mkdtemp(path.join(os.tmpdir(), 'orion-inbox-'));
  const app = createOrionServer({ port: 0, host: '127.0.0.1', dir });
  const addr = await app.listen();
  const base = `http://127.0.0.1:${addr.port}`;
  try {
    await fn({ app, base, ws: `ws://127.0.0.1:${addr.port}${SERVER.wsPath}`, dir });
  } finally {
    await app.close();
    await rm(dir, { recursive: true, force: true });
  }
}

test('ws frame codec round-trips short, medium and long payloads', () => {
  for (const size of [0, 5, 125, 126, 65535, 65536]) {
    const payload = Buffer.alloc(size, 0x61);
    const { frames, rest } = decodeFrames(encodeFrame(0x1, payload));
    assert.equal(frames.length, 1);
    assert.equal(frames[0].payload.length, size);
    assert.equal(rest.length, 0);
  }
});

test('upload name whitelist and size limits', () => {
  assert.equal(safeUploadName('orion.export.json'), 'orion.export.json');
  assert.equal(safeUploadName('paint_001.tile.png'), 'paint_001.tile.png');
  for (const bad of ['../x', 'a/b.png', '..', '.hidden', 'a b.png', 'x\\y.png', '', null]) assert.equal(safeUploadName(bad), null, String(bad));
  assert.equal(uploadLimitFor('a.png'), SERVER.maxPngBytes);
  assert.equal(uploadLimitFor('a.json'), SERVER.maxJsonBytes);
  assert.equal(uploadLimitFor('a.exe'), null);
});

test('slider relays mac -> quest and is replayed to late joiners', async () => {
  await withServer(async ({ ws }) => {
    const mac = await connect(ws, 'mac');
    assert.equal((await mac.next()).type, 'welcome');
    const quest = await connect(ws, 'quest');
    assert.equal((await quest.next()).type, 'welcome');
    assert.deepEqual(await mac.next(), { type: 'peer', role: 'quest', online: true });

    mac.send({ type: 'slider', key: 'tracking', value: 1.4 });
    assert.deepEqual(await quest.next(), { type: 'slider', key: 'tracking', value: 1.4 });
    assert.equal(await mac.quiet(), true, 'sender does not get its own echo');

    quest.send({ type: 'pullout', t: 12.5 });
    assert.deepEqual(await mac.next(), { type: 'pullout', t: 12.5 });

    const late = await connect(ws, 'quest');
    assert.equal((await late.next()).type, 'welcome');
    assert.deepEqual(await late.next(), { type: 'slider', key: 'tracking', value: 1.4, replay: true });

    mac.close();
    quest.close();
    late.close();
  });
});

test('messages before hello and unknown roles are rejected', async () => {
  await withServer(async ({ ws }) => {
    const raw = new WebSocket(ws);
    const got = [];
    await new Promise((r) => raw.addEventListener('open', r));
    raw.addEventListener('message', (e) => got.push(JSON.parse(e.data)));
    raw.send(JSON.stringify({ type: 'slider', key: 'x', value: 1 }));
    raw.send(JSON.stringify({ type: 'hello', role: 'tv' }));
    raw.send('not json');
    await new Promise((r) => setTimeout(r, 150));
    assert.deepEqual(
      got.map((m) => m.type),
      ['error', 'error', 'error'],
    );
    raw.close();
  });
});

test('upload: bad name rejected, oversize rejected, good upload stored and announced as export', async () => {
  await withServer(async ({ base, ws, dir }) => {
    const mac = await connect(ws, 'mac');
    assert.equal((await mac.next()).type, 'welcome');

    let res = await fetch(`${base}${SERVER.uploadPath}?name=${encodeURIComponent('../x')}`, { method: 'POST', body: '{}' });
    assert.equal(res.status, 400);
    res = await fetch(`${base}${SERVER.uploadPath}?name=a%2Fb.json`, { method: 'POST', body: '{}' });
    assert.equal(res.status, 400);
    res = await fetch(`${base}${SERVER.uploadPath}?name=run.sh`, { method: 'POST', body: 'x' });
    assert.equal(res.status, 400);

    const big = new Uint8Array(SERVER.maxJsonBytes + 1);
    res = await fetch(`${base}${SERVER.uploadPath}?name=orion.export.json`, { method: 'POST', body: big });
    assert.equal(res.status, 413);

    const png = Buffer.concat([Buffer.from([0x89, 0x50, 0x4e, 0x47]), Buffer.alloc(64)]);
    res = await fetch(`${base}${SERVER.uploadPath}?name=yunseul.tile.png`, { method: 'POST', body: png });
    assert.equal(res.status, 200);
    assert.equal(await mac.quiet(), true, 'png upload does not announce');

    const doc = JSON.stringify({ version: 1 });
    res = await fetch(`${base}${SERVER.uploadPath}?name=orion.export.json`, { method: 'POST', body: doc });
    assert.equal(res.status, 200);
    const announced = await mac.next();
    assert.equal(announced.type, 'export');
    assert.equal(announced.path, 'orion.export.json');
    assert.equal(await readFile(path.join(dir, 'orion.export.json'), 'utf8'), doc);

    const served = await fetch(`${base}/inbox/orion.export.json`);
    assert.equal(served.status, 200);
    assert.equal(await served.text(), doc);
    const escaped = await fetch(`${base}/inbox/..%2F..%2Fetc%2Fpasswd`);
    assert.equal(escaped.status, 404);

    const lateMac = await connect(ws, 'mac');
    assert.equal((await lateMac.next()).type, 'welcome');
    const replay = await lateMac.next();
    assert.equal(replay.type, 'export');
    assert.equal(replay.replay, true);

    mac.close();
    lateMac.close();
  });
});

test('static files: index.html, vendor three, GLB and samples are served', async () => {
  await withServer(async ({ base }) => {
    for (const [p, type] of [
      ['/', 'text/html'],
      ['/index.html', 'text/html'],
      ['/vendor/three/three.module.js', 'text/javascript'],
      ['/ORION.glb', 'model/gltf-binary'],
      ['/samples/orion.export.json', 'application/json'],
      ['/samples/chips/yunseul/tile.png', 'image/png'],
    ]) {
      const res = await fetch(base + p);
      assert.equal(res.status, 200, p);
      assert.ok(res.headers.get('content-type').startsWith(type), p);
    }
    assert.equal((await fetch(`${base}/inbox/%2e%2e%2f%2e%2e%2fetc%2fpasswd`)).status, 404);
  });
});

test('resolveUnder never escapes its root', () => {
  const root = path.resolve('/srv/web');
  assert.equal(resolveUnder(root, '/../etc/passwd'), null);
  assert.equal(resolveUnder(root, '/%2e%2e/%2e%2e/etc/passwd'), null);
  assert.equal(resolveUnder(root, '/a/..%2F..%2Fb'), null);
  assert.equal(resolveUnder(root, '/a\\..\\..\\b'), null);
  assert.equal(resolveUnder(root, '/index.html'), path.join(root, 'index.html'));
  assert.equal(resolveUnder(root, '/'), root);
});
