// 창의 [시작]으로 띄운 서버에 여러 소켓으로 붙어 왕복을 검사한다. 서버를 직접 띄우지 않는다.

const WebSocket = require('ws');

const URL_BASE = 'ws://localhost:8080';
const BUILD_ID = '20260731120000-abcdef01';
const OTHER_BUILD_ID = '20260731120000-abcdef02';
const results = [];
let nextUniqueKey = 0n;

function check(name, ok, detail) {
    results.push(ok);
    console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? ' — ' + detail : ''}`);
}

function connect() {
    const ws = new WebSocket(URL_BASE);
    ws.inbox = [];
    ws.on('message', raw => ws.inbox.push(JSON.parse(raw.toString())));

    return new Promise((resolve, reject) => {
        ws.on('open', () => resolve(ws));
        ws.on('error', reject);
    });
}

function send(ws, event, data) {
    const uniqueKey = String(++nextUniqueKey);
    ws.send(JSON.stringify({ event, uniqueKey, data }));
    return uniqueKey;
}

function waitFor(ws, event, uniqueKey = null, ms = 2000) {
    return new Promise((resolve, reject) => {
        const started = Date.now();
        const tick = setInterval(() => {
            const idx = ws.inbox.findIndex(p => p.event === event && (null === uniqueKey || p.uniqueKey === uniqueKey));
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
    let leaver = null;

    try {
        host = await connect();
        const hostReady = await waitFor(host, 'connectionReady');
        check('서버 등록 뒤 논리 연결이 완료된다', true === hostReady.data.isReady);

        const createKey = send(host, 'createRoom', { playerId: 'player-host', port: 7777, maxPlayers: 4, buildCompatibilityId: BUILD_ID });
        const created = await waitFor(host, 'createRoomAck', createKey);
        const hostConn = created.data.connection;
        const roomCode = hostConn.roomCode;

        check('봉투가 소문자로 온다', 'createRoomAck' === created.event && !!created.data);
        check('Ack 가 요청 uniqueKey 를 그대로 돌려준다', createKey === created.uniqueKey, created.uniqueKey);
        check('응답이 isSuccess 를 준다', true === created.data.isSuccess, JSON.stringify(created.data.reason));
        check('방 코드 6자리 [A-Z2-9]', /^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$/.test(roomCode), roomCode);
        check('호스트 주소가 IPv4', /^\d+\.\d+\.\d+\.\d+$/.test(hostConn.address), `${hostConn.address}:${hostConn.port}`);
        check('sessionId 발급', !!hostConn.sessionId);
        check('방 호환성 값을 성공 응답에 반사한다', BUILD_ID === hostConn.buildCompatibilityId, hostConn.buildCompatibilityId);

        guest = await connect();
        const guestReady = await waitFor(guest, 'connectionReady');
        check('게스트도 서버 등록 뒤 논리 연결이 완료된다', true === guestReady.data.isReady);

        const joinKey = send(guest, 'joinRoom', { playerId: 'player-guest', roomCode: roomCode.toLowerCase(), buildCompatibilityId: BUILD_ID });
        const joined = await waitFor(guest, 'joinRoomAck', joinKey);
        const guestConn = joined.data.connection;

        check('소문자 방 코드도 참가된다', true === joined.data.isSuccess, joined.data.reason ?? '');
        check('게스트가 호스트 endpoint 를 받는다', guestConn.address === hostConn.address && 7777 === guestConn.port);
        check('역할은 서버가 지정한다', 'Host' === hostConn.role && 'Client' === guestConn.role, `${hostConn.role}/${guestConn.role}`);

        const pushed = await waitFor(host, 'roomUpdated');
        check('참가자 변동이 방 전체에 푸시된다', 2 === pushed.data.players.length, JSON.stringify(pushed.data.players));
        check('푸시가 playerId 와 호스트 여부를 준다',
            'player-host' === pushed.data.hostPlayerId && true === pushed.data.players.find(p => 'player-host' === p.playerId)?.isHost,
            JSON.stringify(pushed.data));

        const missingKey = send(guest, 'joinRoom', { playerId: 'player-guest', roomCode: 'ZZZZZZ' });
        const missing = await waitFor(guest, 'joinRoomAck', missingKey);
        check('없는 방은 ROOM_NOT_FOUND', 'ROOM_NOT_FOUND' === missing.data.reason);

        const invalidKey = send(guest, 'joinRoom', { playerId: 'player-guest', roomCode: 123 });
        const invalid = await waitFor(guest, 'joinRoomAck', invalidKey);
        check('잘못된 방 코드 타입은 요청만 거절한다', 'INVALID_ROOM_CODE' === invalid.data.reason);

        const invalidCreateKey = send(guest, 'createRoom', { playerId: '', port: 0, maxPlayers: 0 });
        const invalidCreate = await waitFor(guest, 'createRoomAck', invalidCreateKey);
        check('필수 생성 값 누락은 실패 Ack 로 거절한다',
            false === invalidCreate.data.isSuccess && 'INVALID_PLAYER_ID' === invalidCreate.data.reason);

        const invalidPortKey = send(guest, 'createRoom', { playerId: 'player-guest', port: 0, maxPlayers: 4 });
        const invalidPort = await waitFor(guest, 'createRoomAck', invalidPortKey);
        check('잘못된 포트는 실패 Ack 로 거절한다',
            false === invalidPort.data.isSuccess && 'INVALID_PORT' === invalidPort.data.reason);

        const invalidMaxPlayersKey = send(guest, 'createRoom', { playerId: 'player-guest', port: 7778, maxPlayers: 0 });
        const invalidMaxPlayers = await waitFor(guest, 'createRoomAck', invalidMaxPlayersKey);
        check('잘못된 정원은 실패 Ack 로 거절한다',
            false === invalidMaxPlayers.data.isSuccess && 'INVALID_MAX_PLAYERS' === invalidMaxPlayers.data.reason);

        const invalidPlayerKey = send(guest, 'joinRoom', { playerId: '', roomCode });
        const invalidPlayer = await waitFor(guest, 'joinRoomAck', invalidPlayerKey);
        check('빈 참가자 ID 는 실패 Ack 로 거절한다',
            false === invalidPlayer.data.isSuccess && 'INVALID_PLAYER_ID' === invalidPlayer.data.reason);

        const otherBuildKey = send(guest, 'joinRoom', { playerId: 'player-other-build', roomCode, buildCompatibilityId: OTHER_BUILD_ID });
        const otherBuild = await waitFor(guest, 'joinRoomAck', otherBuildKey);
        check('다른 빌드 ID 는 BUILD_INCOMPATIBLE 로 거절한다',
            false === otherBuild.data.isSuccess && 'BUILD_INCOMPATIBLE' === otherBuild.data.reason, otherBuild.data.reason);

        const missingBuildKey = send(guest, 'joinRoom', { playerId: 'player-no-build', roomCode });
        const missingBuild = await waitFor(guest, 'joinRoomAck', missingBuildKey);
        check('빌드 ID 누락도 BUILD_INCOMPATIBLE 로 거절한다',
            false === missingBuild.data.isSuccess && 'BUILD_INCOMPATIBLE' === missingBuild.data.reason, missingBuild.data.reason);

        const badFormatBuildKey = send(guest, 'createRoom', { playerId: 'player-guest', port: 7779, maxPlayers: 4, buildCompatibilityId: 'not-an-id' });
        const badFormatBuild = await waitFor(guest, 'createRoomAck', badFormatBuildKey);
        check('형식이 깨진 빌드 ID 로는 방을 만들지 못한다',
            false === badFormatBuild.data.isSuccess && 'BUILD_INCOMPATIBLE' === badFormatBuild.data.reason, badFormatBuild.data.reason);

        const rejectedListKey = send(guest, 'roomList', {});
        const rejectedList = await waitFor(guest, 'roomListAck', rejectedListKey);
        check('거절된 요청이 방 상태를 바꾸지 않는다',
            2 === rejectedList.data.rooms.find(r => r.roomCode === roomCode)?.currentPlayers,
            JSON.stringify(rejectedList.data.rooms));

        const echoedKey = send(guest, 'echo', { ping: 1 });
        const echoed = await waitFor(guest, 'echoAck', echoedKey);
        check('잘못된 요청 뒤에도 서버가 계속 응답한다', 1 === echoed.data.ping);

        const firstListKey = send(guest, 'roomList', {});
        const secondListKey = send(guest, 'roomList', {});
        const secondList = await waitFor(guest, 'roomListAck', secondListKey);
        const list = await waitFor(guest, 'roomListAck', firstListKey);
        check('동일 이벤트 동시 요청을 uniqueKey 로 구분한다',
            secondListKey === secondList.uniqueKey && firstListKey === list.uniqueKey);

        const summary = list.data.rooms.find(r => r.roomCode === roomCode);
        check('roomList 가 방을 준다', !!summary);
        check('RoomSummary 가 정원과 참가 가능 여부를 준다',
            2 === summary?.currentPlayers && 4 === summary?.maxPlayers && true === summary?.isJoinable,
            JSON.stringify(summary));

        const wrongLeaveKey = send(guest, 'leaveRoom', { sessionId: 'wrong-session' });
        const wrongLeave = await waitFor(guest, 'leaveRoomAck', wrongLeaveKey);
        check('다른 sessionId 의 이탈 요청을 거절한다',
            false === wrongLeave.data.isSuccess && 'SESSION_MISMATCH' === wrongLeave.data.reason);

        leaver = await connect();
        const leaverJoinKey = send(leaver, 'joinRoom', { playerId: 'player-leaver', roomCode, buildCompatibilityId: BUILD_ID });
        const leaverJoined = await waitFor(leaver, 'joinRoomAck', leaverJoinKey);
        const leaverLeaveKey = send(leaver, 'leaveRoom', { sessionId: leaverJoined.data.connection.sessionId });
        const leaverLeft = await waitFor(leaver, 'leaveRoomAck', leaverLeaveKey);
        check('일치하는 sessionId 의 이탈 요청은 성공한다',
            true === leaverLeft.data.isSuccess && null === leaverLeft.data.reason);
        leaver.terminate();
        leaver = null;

        host.terminate();
        host = null;

        const closed = await waitFor(guest, 'sessionClosed');
        check('호스트 소켓 close 가 세션 종료로 이어진다', 'HOST_LEFT' === closed.data.reason);
        check('세션 종료가 sessionId 를 준다', closed.data.sessionId === hostConn.sessionId, closed.data.sessionId);

        const afterKey = send(guest, 'roomList', {});
        const after = await waitFor(guest, 'roomListAck', afterKey);
        check('파기된 방은 목록에서 사라진다', false === after.data.rooms.some(r => r.roomCode === roomCode));
    } catch (e) {
        check('예외 없이 완주', false, e.message);
    }

    if (host) host.terminate();
    if (guest) guest.terminate();
    if (leaver) leaver.terminate();

    const passed = results.filter(Boolean).length;
    console.log(`\n자가 진단 ${passed}/${results.length} 통과`);
    process.exit(passed === results.length ? 0 : 1);
})();
