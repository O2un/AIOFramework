// 창의 [시작]으로 띄운 서버에 소켓 2개로 붙어 왕복을 검사한다. 서버를 직접 띄우지 않는다.

const WebSocket = require('ws');

const URL_BASE = 'ws://localhost:8080';
const results = [];

function check(name, ok, detail) {
    results.push(ok);
    console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? ' — ' + detail : ''}`);
}

function connect(clientId) {
    const ws = new WebSocket(`${URL_BASE}?clientId=${clientId}`);
    ws.inbox = [];
    ws.on('message', raw => ws.inbox.push(JSON.parse(raw.toString())));

    return new Promise((resolve, reject) => {
        ws.on('open', () => resolve(ws));
        ws.on('error', reject);
    });
}

const send = (ws, event, data) => ws.send(JSON.stringify({ event, data }));

function waitFor(ws, event, ms = 2000) {
    return new Promise((resolve, reject) => {
        const started = Date.now();
        const tick = setInterval(() => {
            const idx = ws.inbox.findIndex(p => p.event === event);
            if (idx >= 0) {
                clearInterval(tick);
                resolve(ws.inbox.splice(idx, 1)[0]);
                return;
            }
            if (Date.now() - started > ms) {
                clearInterval(tick);
                reject(new Error(`응답 없음: ${event}`));
            }
        }, 20);
    });
}

(async () => {
    let host = null;
    let guest = null;

    try {
        host = await connect('smoke-host');
        send(host, 'createRoom', { port: 7777, maxPlayers: 4 });
        const created = await waitFor(host, 'createRoomResult');
        const roomCode = created.data.roomCode;

        check('봉투가 소문자로 온다', 'createRoomResult' === created.event && !!created.data);
        check('방 코드 6자리 [A-Z2-9]', /^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$/.test(roomCode), roomCode);
        check('호스트 주소가 IPv4', /^\d+\.\d+\.\d+\.\d+$/.test(created.data.address), `${created.data.address}:${created.data.port}`);
        check('sessionId 발급', !!created.data.sessionId);

        guest = await connect('smoke-guest');
        send(guest, 'joinRoom', { roomCode: roomCode.toLowerCase() });
        const joined = await waitFor(guest, 'joinRoomResult');

        check('소문자 방 코드도 참가된다', true === joined.data.success, joined.data.error ?? '');
        check('게스트가 호스트 endpoint 를 받는다', joined.data.address === created.data.address && 7777 === joined.data.port);

        const pushed = await waitFor(host, 'roomState');
        check('참가자 변동이 방 전체에 푸시된다', 2 === pushed.data.players.length, JSON.stringify(pushed.data.players));

        send(guest, 'joinRoom', { roomCode: 'ZZZZZZ' });
        const missing = await waitFor(guest, 'joinRoomResult');
        check('없는 방은 ROOM_NOT_FOUND', 'ROOM_NOT_FOUND' === missing.data.error);

        send(guest, 'joinRoom', { roomCode: 123 });
        const invalid = await waitFor(guest, 'joinRoomResult');
        check('잘못된 방 코드 타입은 요청만 거절한다', 'INVALID_ROOM_CODE' === invalid.data.error);

        send(guest, 'echo', { ping: 1 });
        const echoed = await waitFor(guest, 'echoResult');
        check('잘못된 요청 뒤에도 서버가 계속 응답한다', 1 === echoed.data.ping);

        send(guest, 'roomList', {});
        const list = await waitFor(guest, 'roomListResult');
        check('roomList 가 참가 가능한 방을 준다', list.data.rooms.some(r => r.roomCode === roomCode));

        host.terminate();
        host = null;

        const closed = await waitFor(guest, 'sessionClosed');
        check('호스트 소켓 close 가 세션 종료로 이어진다', 'HOST_LEFT' === closed.data.reason);

        send(guest, 'roomList', {});
        const after = await waitFor(guest, 'roomListResult');
        check('파기된 방은 목록에서 사라진다', false === after.data.rooms.some(r => r.roomCode === roomCode));
    } catch (e) {
        check('예외 없이 완주', false, e.message);
    }

    if (host) host.terminate();
    if (guest) guest.terminate();

    const passed = results.filter(Boolean).length;
    console.log(`\n자가 진단 ${passed}/${results.length} 통과`);
    process.exit(passed === results.length ? 0 : 1);
})();
