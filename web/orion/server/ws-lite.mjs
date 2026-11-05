import { createHash } from 'node:crypto';
import { EventEmitter } from 'node:events';

const GUID = '258EAFA5-E914-47DA-95CA-C5AB0DC85B11';
const OP = { CONT: 0x0, TEXT: 0x1, BINARY: 0x2, CLOSE: 0x8, PING: 0x9, PONG: 0xa };
const MAX_MESSAGE_BYTES = 1024 * 1024;

export function isUpgradeRequest(req) {
  return /\bwebsocket\b/i.test(req.headers.upgrade || '') && !!req.headers['sec-websocket-key'];
}

export function acceptKey(key) {
  return createHash('sha1').update(key + GUID).digest('base64');
}

export function encodeFrame(opcode, payload = Buffer.alloc(0)) {
  const len = payload.length;
  let header;
  if (len < 126) {
    header = Buffer.from([0x80 | opcode, len]);
  } else if (len < 65536) {
    header = Buffer.alloc(4);
    header[0] = 0x80 | opcode;
    header[1] = 126;
    header.writeUInt16BE(len, 2);
  } else {
    header = Buffer.alloc(10);
    header[0] = 0x80 | opcode;
    header[1] = 127;
    header.writeBigUInt64BE(BigInt(len), 2);
  }
  return Buffer.concat([header, payload]);
}

export function decodeFrames(buffer) {
  const frames = [];
  let offset = 0;
  while (buffer.length - offset >= 2) {
    const b0 = buffer[offset];
    const b1 = buffer[offset + 1];
    const fin = (b0 & 0x80) !== 0;
    const opcode = b0 & 0x0f;
    const masked = (b1 & 0x80) !== 0;
    let len = b1 & 0x7f;
    let pos = offset + 2;
    if (len === 126) {
      if (buffer.length < pos + 2) break;
      len = buffer.readUInt16BE(pos);
      pos += 2;
    } else if (len === 127) {
      if (buffer.length < pos + 8) break;
      const big = buffer.readBigUInt64BE(pos);
      if (big > BigInt(MAX_MESSAGE_BYTES)) throw new Error('frame too large');
      len = Number(big);
      pos += 8;
    }
    if (len > MAX_MESSAGE_BYTES) throw new Error('frame too large');
    const maskLen = masked ? 4 : 0;
    if (buffer.length < pos + maskLen + len) break;
    let payload = buffer.subarray(pos + maskLen, pos + maskLen + len);
    if (masked) {
      const mask = buffer.subarray(pos, pos + 4);
      const out = Buffer.alloc(len);
      for (let i = 0; i < len; i++) out[i] = payload[i] ^ mask[i & 3];
      payload = out;
    } else {
      payload = Buffer.from(payload);
    }
    frames.push({ fin, opcode, payload });
    offset = pos + maskLen + len;
  }
  return { frames, rest: buffer.subarray(offset) };
}

export class WsConnection extends EventEmitter {
  constructor(socket) {
    super();
    this.socket = socket;
    this.buffer = Buffer.alloc(0);
    this.fragments = [];
    this.fragmentOpcode = null;
    this.closed = false;
    socket.on('data', (chunk) => this.onData(chunk));
    socket.on('close', () => this.finish());
    socket.on('error', () => this.finish());
    socket.on('end', () => this.finish());
  }

  onData(chunk) {
    this.buffer = Buffer.concat([this.buffer, chunk]);
    let decoded;
    try {
      decoded = decodeFrames(this.buffer);
    } catch {
      this.close(1009);
      return;
    }
    this.buffer = decoded.rest;
    for (const frame of decoded.frames) this.onFrame(frame);
  }

  onFrame({ fin, opcode, payload }) {
    if (opcode === OP.CLOSE) {
      this.close(1000);
      return;
    }
    if (opcode === OP.PING) {
      this.socket.write(encodeFrame(OP.PONG, payload));
      return;
    }
    if (opcode === OP.PONG) return;
    if (opcode === OP.TEXT || opcode === OP.BINARY) {
      this.fragmentOpcode = opcode;
      this.fragments = [payload];
    } else if (opcode === OP.CONT) {
      this.fragments.push(payload);
    } else {
      return;
    }
    if (!fin) return;
    const data = Buffer.concat(this.fragments);
    const type = this.fragmentOpcode;
    this.fragments = [];
    this.fragmentOpcode = null;
    this.emit('message', type === OP.TEXT ? data.toString('utf8') : data);
  }

  send(text) {
    if (this.closed || this.socket.destroyed) return false;
    const payload = Buffer.isBuffer(text) ? text : Buffer.from(String(text), 'utf8');
    this.socket.write(encodeFrame(Buffer.isBuffer(text) ? OP.BINARY : OP.TEXT, payload));
    return true;
  }

  close(code = 1000) {
    if (this.closed) return;
    const payload = Buffer.alloc(2);
    payload.writeUInt16BE(code, 0);
    try {
      this.socket.write(encodeFrame(OP.CLOSE, payload));
    } catch {}
    this.socket.end();
    this.finish();
  }

  finish() {
    if (this.closed) return;
    this.closed = true;
    this.emit('close');
  }
}

export function upgrade(req, socket, head) {
  const key = req.headers['sec-websocket-key'];
  socket.write(
    [
      'HTTP/1.1 101 Switching Protocols',
      'Upgrade: websocket',
      'Connection: Upgrade',
      `Sec-WebSocket-Accept: ${acceptKey(key)}`,
      '',
      '',
    ].join('\r\n'),
  );
  const conn = new WsConnection(socket);
  if (head && head.length) conn.onData(head);
  return conn;
}
