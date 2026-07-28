const WebSocket = require('ws');
const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const PORT = 8080;

// 에디터의 도메인 리로드가 stdout 파이프를 끊는다. 창은 이 파일을 따라 읽는다.
const LOG_PATH = path.join(__dirname, 'server.log');
fs.writeFileSync(LOG_PATH, '');

function log(line) {
    const stamped = `[${new Date().toTimeString().slice(0, 8)}] ${line}`;
    console.log(stamped);
    fs.appendFileSync(LOG_PATH, stamped + '\n');
}

// ── 봉투 ──────────────────────────────────────────────────────────
// 파서는 { event, data } 까지만 알고 이름으로 핸들러에 넘긴다.
// 새 패킷을 넣는 일이 파서 수정이 되어서는 안 된다.

const handlers = new Map();
const disconnectHandlers = [];
const clients = new Map();

const on = (event, handler) => handlers.set(event, handler);
const onDisconnect = (handler) => disconnectHandlers.push(handler);

function send(ws, event, data) {
    if (WebSocket.OPEN !== ws.readyState) return;
    ws.send(JSON.stringify({ event, data }));
}

function hostAddress(ws) {
    const addr = ws._socket.remoteAddress ?? '';

    // 노드가 주는 IPv6 표기(::ffff:127.0.0.1, ::1)를 그대로 넘기면 Netcode 가 못 쓴다.
    if (addr.startsWith('::ffff:')) return addr.slice(7);
    if ('::1' === addr) return '127.0.0.1';

    return addr;
}

function dispatch(ws, raw) {
    let packet;
    try {
        packet = JSON.parse(raw.toString());
    } catch (e) {
        log(`파싱 실패: ${e.message}`);
        return;
    }

    const handler = handlers.get(packet.event);
    if (!handler) {
        log(`미등록 이벤트: ${packet.event}`);
        return;
    }

    try {
        handler(ws, packet.data ?? {});
    } catch (e) {
        log(`이벤트 처리 실패: ${packet.event} — ${e.message}`);
        send(ws, `${packet.event}Result`, { success: false, error: 'INVALID_REQUEST' });
    }
}

// ── 모듈 ──────────────────────────────────────────────────────────

require('./matchmakingServer').register({ on, onDisconnect, send, log, clients });

on('login', (ws) => {
    send(ws, 'loginResult', { success: true, clientId: ws.ctx.clientId, token: 'test_token_12345' });
});

// 매치메이킹과 무관한 이벤트. 왕복하면 봉투가 용도에서 떨어진 것이다 (PRD-01 완료 기준).
on('echo', (ws, data) => {
    send(ws, 'echoResult', { ...data, serverTime: new Date().toISOString() });
});

// ── 서버 ──────────────────────────────────────────────────────────

const wss = new WebSocket.Server({ port: PORT });

// 비동기로 오는 에러다. 잡지 않으면 stderr 로만 죽는데 그 파이프는 아무도 안 본다.
wss.on('error', (err) => {
    log(`서버 기동 실패 — ${err.code ?? err.message}`);
    if ('EADDRINUSE' === err.code) {
        log(`포트 ${PORT} 를 다른 프로세스가 이미 쓰고 있습니다. 좀비 node 를 정리하세요.`);
    }
    process.exit(1);
});

wss.on('connection', (ws, req) => {
    const url = new URL(req.url, `http://${req.headers.host}`);
    const clientId = url.searchParams.get('clientId') ?? crypto.randomUUID();

    ws.ctx = { clientId, address: hostAddress(ws) };
    clients.set(clientId, ws);
    log(`연결: ${clientId}`);

    ws.on('message', raw => dispatch(ws, raw));

    ws.on('close', () => {
        for (const handler of disconnectHandlers) handler(ws);
        if (clients.get(clientId) === ws) clients.delete(clientId);
        log(`종료: ${clientId}`);
    });
});

log(`매치메이킹 서버 실행 중 — 포트 ${PORT}`);
