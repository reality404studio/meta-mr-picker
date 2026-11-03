#!/usr/bin/env node
// 작업대(my-mr-studio)의 mood-picker 보드를 GitHub에서 읽을 수 있는 마크다운으로 내보낸다.
// 다른 앱 보드는 읽지 않는다. 공통 보드는 mood-picker 가 가져다 쓰는 과업(shared ref)만 포함한다.
// 사용: node scripts/export-board.mjs   (보통 scripts/sync-docs.sh 가 부른다)
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const APP = 'mood-picker';
const here = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const studioRoot = path.resolve(here, '../..');
const { createStudio } = await import(pathToFileURL(path.join(studioRoot, 'server/store.mjs')).href);
const studio = createStudio(studioRoot);
const app = studio.project(APP).pipeline();
const shared = studio.project('shared').pipeline();

const outDir = path.join(here, 'board');
const nodesDir = path.join(outDir, 'nodes');
fs.rmSync(outDir, { recursive: true, force: true });
fs.mkdirSync(nodesDir, { recursive: true });

const ICON = { done: '✅', doing: '🔶', review: '👀', proof: '🧪', todo: '⬜', blocked: '⛔' };
const KO = { done: '완료', doing: '진행 중', review: '확인 필요', proof: '증명', todo: '대기', blocked: '막힘' };
const KIND = { decide: '결정', collect: '수집', analyze: '분석', write: '작성', make: '생성', build: '구현', verify: '검증' };
const WHO = { agent: '에이전트', human: '사람' };
const esc = (s = '') => String(s).replace(/\|/g, '\\|').replace(/\n/g, ' ');
const kids = (pl, id) => pl.nodes.filter((n) => n.parent === id).sort((a, b) => a.order - b.order);
const tasksIn = (pl, stage) => pl.nodes.filter((n) => !n.parent && n.stage === stage).sort((a, b) => a.order - b.order);
const byId = Object.fromEntries(app.nodes.map((n) => [n.id, n]));
const title = (id) => byId[id]?.title || id;

// 노드 메모 경로: 작업대 안 상대 링크를 이 레포 기준으로 고친다
function readNotes(dir, id) {
  const f = path.join(studioRoot, 'studio', dir, 'nodes', id, 'README.md');
  if (!fs.existsSync(f)) return '';
  return fs.readFileSync(f, 'utf8')
    .replaceAll('](../../HANDOFF.md)', '](../../docs/HANDOFF.md)')
    .replaceAll('](../../DECIDED.md)', '](../../docs/DECIDED.md)')
    .replaceAll('](../../REJECTED.md)', '](../../docs/REJECTED.md)');
}
function assetList(dir, id) {
  const f = path.join(studioRoot, 'studio', dir, 'nodes', id, 'assets', 'index.json');
  if (!fs.existsSync(f)) return [];
  try { return JSON.parse(fs.readFileSync(f, 'utf8')); } catch { return []; }
}

function subLines(pl, task) {
  return kids(pl, task.id).map((s) => {
    const box = s.status === 'done' ? '[x]' : '[ ]';
    const cur = s.isCurrent ? ' ▶' : '';
    const st = s.status !== 'done' && s.status !== 'todo' ? ` ${ICON[s.status]} ${KO[s.status]}` : '';
    return `- ${box}${cur} **${esc(s.title)}** · ${WHO[s.by] || s.by} · ${KIND[s.kind] || s.kind}${st}${s.how ? `\n  <br><sub>${esc(s.how)}</sub>` : ''}`;
  });
}

function writeNodePage(pl, dir, task, extraTitle = '') {
  const lines = [`# ${ICON[task.status] || ''} ${task.title}${extraTitle}`, ''];
  lines.push(`[← 보드로](../BOARD.md)`, '');
  if (task.goal) lines.push(`- **목표:** ${task.goal}`);
  if (task.output) lines.push(`- **산출물:** ${task.output}`);
  if (task.waitingOn?.length) lines.push(`- **기다림:** ${task.waitingOn.map(title).join(', ')}`);
  lines.push('');
  const subs = subLines(pl, task);
  if (subs.length) lines.push('## 하위 과업', '', ...subs, '');
  const assets = assetList(dir, task.id);
  if (assets.length) lines.push('## 파일', '', ...assets.map((a) => `- ${a.file}${a.memo ? ` — ${esc(a.memo)}` : ''}`), '');
  const notes = readNotes(dir, task.id).replace(/^# .*\n/, '');
  if (notes.trim()) lines.push('## 메모', '', notes.trim(), '');
  fs.writeFileSync(path.join(nodesDir, `${task.id}.md`), lines.join('\n'));
}

// ---------- BOARD.md ----------
const now = new Date().toLocaleString('sv-SE', { timeZone: 'Asia/Seoul' }).slice(0, 16);
const tasks = app.nodes.filter((n) => !n.parent);
const doneTasks = tasks.filter((t) => t.status === 'done').length;
const md = [];
md.push(`# Gaze · Catch · Dab 보드`, "", `<sub>작업대 앱 id: ${APP}</sub>`, "");
md.push(`> 작업대 \`my-mr-studio\` 의 보드를 내보낸 **읽기 전용** 사본이에요. 갱신: ${now} KST · 과업 ${doneTasks}/${tasks.length} 완료`);
md.push('> 고칠 때는 작업대에서 (`npm run studio -- … --app mood-picker`), 그다음 `scripts/sync-docs.sh` → 커밋·푸시.', '');
md.push('범례: ✅ 완료 · 🔶 진행 중 · 👀 확인 필요 · ⬜ 대기 · ⛔ 막힘 · ▶ 과업 안에서 지금 차례', '');

md.push('## 지금 할 일', '');
const nextHuman = app.next.filter((n) => n.by === 'human');
const nextAgent = app.next.filter((n) => n.by === 'agent');
md.push('| 누가 | 하위 과업 | 과업 | 상태 |', '| --- | --- | --- | --- |');
for (const n of [...nextHuman, ...nextAgent]) {
  md.push(`| ${WHO[n.by]} | ${esc(n.title)} | [${esc(n.taskTitle)}](nodes/${n.task}.md) | ${ICON[n.status]} ${KO[n.status]} |`);
}
md.push('');

// 흐름도 (GitHub 이 mermaid 를 그려 준다)
md.push('## 흐름', '', '```mermaid', 'flowchart LR');
const mid = (id) => id.replace(/[^A-Za-z0-9_]/g, '_');
for (const st of app.stages) {
  md.push(`  subgraph ${mid('s_' + st.id)}["${st.title}"]`, '    direction TB');
  for (const t of tasksIn(app, st.id)) {
    const prog = t.progress?.total ? ` ${t.progress.done}/${t.progress.total}` : '';
    md.push(`    ${mid(t.id)}["${(t.title || '').replace(/"/g, "'")}${prog}"]:::${t.status}`);
  }
  md.push('  end');
}
for (const e of app.edges) md.push(`  ${mid(e.source)} ${e.kind === 'feedback' ? '-.->' : '-->'}${e.label ? `|${e.label}|` : ''} ${mid(e.target)}`);
md.push(
  '  classDef done fill:#dff3e4,stroke:#2e7d4f,color:#123',
  '  classDef doing fill:#fff1cc,stroke:#c28a00,color:#321',
  '  classDef review fill:#ece4ff,stroke:#6a4fc2,color:#213',
  '  classDef proof fill:#dceaff,stroke:#3f7fd6,color:#123',
  '  classDef todo fill:#f4f4f4,stroke:#999,color:#333',
  '  classDef blocked fill:#ffdede,stroke:#c22,color:#311',
  '```', '');

// 단계별 상세
for (const [i, st] of app.stages.entries()) {
  md.push(`## ${i + 1}. ${st.title}`, '', st.goal ? `_${st.goal}_` : '', '');
  for (const t of tasksIn(app, st.id)) {
    if (t.type === 'shared') {
      const ref = shared.nodes.find((n) => n.id === t.ref);
      if (ref) writeNodePage(shared, 'shared', ref, ' (공통)');
      md.push(`### ${ICON[t.status]} [${t.title}](nodes/${t.ref}.md) · 공통`, '');
      continue;
    }
    writeNodePage(app, `apps/${APP}`, t);
    const prog = t.progress?.total ? ` · ${t.progress.done}/${t.progress.total}` : '';
    md.push(`### ${ICON[t.status]} [${t.title}](nodes/${t.id}.md)${prog}`, '');
    if (t.goal) md.push(`${t.goal}`, '');
    if (t.waitingOn?.length) md.push(`기다림: ${t.waitingOn.map(title).join(', ')}`, '');
    md.push(...subLines(app, t), '');
  }
}

md.push('---', '', '- 결정: [정한 것](../docs/DECIDED.md) · [안 하기로 한 것](../docs/REJECTED.md)', '- 정본 컨셉: [HANDOFF](../docs/HANDOFF.md) · Unity 함정: [가드레일](../docs/UNITY_GUARDRAILS.md)', '');
fs.writeFileSync(path.join(outDir, 'BOARD.md'), md.join('\n'));
console.log(`board/ 내보내기 완료 (과업 ${tasks.length}개)`);
