import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { createReadStream, promises as fs } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { isUpgradeRequest, upgrade } from './ws-lite.mjs';
import { SERVER } from '../src/settings.js';

const WEB_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const DEFAULT_SAMPLES = path.resolve(WEB_ROOT, '../../tools/web-orion/samples');
const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.glb': 'model/gltf-binary',
  '.svg': 'image/svg+xml',
  '.woff2': 'font/woff2',
};

export function safeUploadName(name) {
  return typeof name === 'string' && SERVER.nameRe.test(name) && !name.startsWith('.') ? name : null;
}

export function uploadLimitFor(name) {
  const ext = path.extname(name).toLowerCase();
  if (ext === '.png') return SERVER.maxPngBytes;
  if (ext === '.json') return SERVER.maxJsonBytes;
  return null;
}

export function parseArgs(argv) {
  const out = { port: SERVER.port, dir: path.resolve('inbox'), samples: DEFAULT_SAMPLES, host: '0.0.0.0' };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--port') out.port = Number(argv[++i]);
    else if (a === '--dir') out.dir = path.resolve(argv[++i]);
    else if (a === '--samples') out.samples = path.resolve(argv[++i]);
    else if (a === '--host') out.host = argv[++i];
  }
  return out;
}

export function resolveUnder(root, urlPath) {
  const decoded = decodeURIComponent(urlPath).replace(/\\/g, '/');
  if (decoded.split('/').some((seg) => seg === '..')) return null;
  const abs = path.resolve(root, '.' + (decoded.startsWith('/') ? decoded : '/' + decoded));
  const rel = path.relative(root, abs);
  if (rel.startsWith('..') || path.isAbsolute(rel)) return null;
  return abs;
}

async function serveFile(res, abs) {
  let stat;
  try {
    stat = await fs.stat(abs);
  } catch {
    return false;
  }
  if (stat.isDirectory()) return serveFile(res, path.join(abs, 'index.html'));
  res.writeHead(200, {
    'Content-Type': MIME[path.extname(abs).toLowerCase()] || 'application/octet-stream',
    'Content-Length': stat.size,
    'Cache-Control': 'no-store',
  });
  createReadStream(abs).pipe(res);
  return true;
}

function json(res, status, body) {
  const text = JSON.stringify(body);
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Content-Length': Buffer.byteLength(text) });
  res.end(text);
}

export function createRelay() {
  const clients = new Set();
  const lastSlider = new Map();
  let lastExport = null;

  function peersOf(role) {
    const other = role === 'mac' ? 'quest' : 'mac';
    return [...clients].filter((c) => c.role === other);
  }

  function broadcast(from, msg) {
    const text = JSON.stringify(msg);
    for (const peer of peersOf(from.role)) peer.conn.send(text);
  }

  function attach(conn) {
    const client = { conn, role: null };
    clients.add(client);
    conn.on('message', (raw) => {
      let msg;
      try {
        msg = JSON.parse(String(raw));
      } catch {
        conn.send(JSON.stringify({ type: 'error', message: 'invalid json' }));
        return;
      }
      if (msg.type === 'hello') {
        if (!SERVER.roles.includes(msg.role)) {
          conn.send(JSON.stringify({ type: 'error', message: `role must be ${SERVER.roles.join('|')}` }));
          return;
        }
        client.role = msg.role;
        conn.send(JSON.stringify({ type: 'welcome', role: msg.role, peers: peersOf(msg.role).length }));
        for (const [key, value] of lastSlider) conn.send(JSON.stringify({ type: 'slider', key, value, replay: true }));
        if (msg.role === 'mac' && lastExport) conn.send(JSON.stringify({ ...lastExport, replay: true }));
        for (const peer of peersOf(msg.role)) peer.conn.send(JSON.stringify({ type: 'peer', role: msg.role, online: true }));
        return;
      }
      if (!client.role) {
        conn.send(JSON.stringify({ type: 'error', message: 'send hello first' }));
        return;
      }
      if (msg.type === 'slider') lastSlider.set(msg.key, msg.value);
      if (msg.type === 'export') lastExport = msg;
      broadcast(client, msg);
    });
    conn.on('close', () => {
      clients.delete(client);
      if (client.role) {
        for (const peer of peersOf(client.role)) peer.conn.send(JSON.stringify({ type: 'peer', role: client.role, online: false }));
      }
    });
    return client;
  }

  function announceExport(msg) {
    lastExport = msg;
    const text = JSON.stringify(msg);
    for (const c of clients) if (c.role === 'mac') c.conn.send(text);
  }

  return { attach, announceExport, clients, lastSlider };
}

async function readBody(req, limit) {
  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > limit) {
      const err = new Error('payload too large');
      err.status = 413;
      throw err;
    }
    chunks.push(chunk);
  }
  return Buffer.concat(chunks);
}

export function createOrionServer(options = {}) {
  const opts = { ...parseArgs([]), ...options };
  const relay = createRelay();
  const uploads = [];

  const server = http.createServer(async (req, res) => {
    const url = new URL(req.url, 'http://localhost');
    try {
      if (req.method === 'POST' && url.pathname === SERVER.uploadPath) {
        const name = safeUploadName(url.searchParams.get('name'));
        if (!name) return json(res, 400, { error: 'name must match [A-Za-z0-9._-]+ with no slashes' });
        const limit = uploadLimitFor(name);
        if (!limit) return json(res, 400, { error: 'only .png and .json uploads are accepted' });
        const declared = Number(req.headers['content-length'] || 0);
        if (declared > limit) return json(res, 413, { error: `payload too large (limit ${limit} bytes)` });
        const body = await readBody(req, limit);
        await fs.mkdir(opts.dir, { recursive: true });
        const abs = path.join(opts.dir, name);
        await fs.writeFile(abs, body);
        uploads.push(name);
        const event = { type: 'export', path: name, bytes: body.length, url: `/inbox/${name}` };
        if (name.toLowerCase().endsWith('.json')) relay.announceExport(event);
        return json(res, 200, { ok: true, ...event });
      }
      if (req.method !== 'GET' && req.method !== 'HEAD') return json(res, 405, { error: 'method not allowed' });
      if (url.pathname === '/health') return json(res, 200, { ok: true, clients: relay.clients.size });
      let abs = null;
      if (url.pathname.startsWith('/inbox/')) abs = resolveUnder(opts.dir, url.pathname.slice('/inbox'.length));
      else if (url.pathname.startsWith('/samples/')) abs = resolveUnder(opts.samples, url.pathname.slice('/samples'.length));
      else abs = resolveUnder(WEB_ROOT, url.pathname);
      if (abs && (await serveFile(res, abs))) return;
      json(res, 404, { error: 'not found' });
    } catch (err) {
      json(res, err.status || 500, { error: err.message });
    }
  });

  server.on('upgrade', (req, socket, head) => {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname !== SERVER.wsPath || !isUpgradeRequest(req)) {
      socket.write('HTTP/1.1 404 Not Found\r\n\r\n');
      socket.destroy();
      return;
    }
    relay.attach(upgrade(req, socket, head));
  });

  function listen() {
    return new Promise((resolve, reject) => {
      server.once('error', reject);
      server.listen(opts.port, opts.host, () => {
        server.off('error', reject);
        resolve(server.address());
      });
    });
  }

  function close() {
    return new Promise((resolve) => {
      for (const c of relay.clients) c.conn.close();
      server.closeAllConnections?.();
      server.close(() => resolve());
    });
  }

  return { server, relay, uploads, listen, close, options: opts, webRoot: WEB_ROOT };
}

function lanAddresses() {
  try {
    return Object.values(os.networkInterfaces())
      .flat()
      .filter((i) => i && i.family === 'IPv4' && !i.internal)
      .map((i) => i.address);
  } catch {
    return [];
  }
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const opts = parseArgs(process.argv.slice(2));
  const app = createOrionServer(opts);
  const addr = await app.listen();
  console.log(`ORION web server  http://localhost:${addr.port}/  (local development only, no auth)`);
  for (const ip of lanAddresses()) console.log(`  LAN:  http://${ip}:${addr.port}/   ws://${ip}:${addr.port}${SERVER.wsPath}`);
  console.log(`  inbox: ${opts.dir}`);
  console.log(`  samples: ${opts.samples}`);
}
