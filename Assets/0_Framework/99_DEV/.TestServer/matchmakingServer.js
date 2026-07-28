const crypto = require('crypto');

// 손으로 입력하는 값이라 혼동 쌍(O/0, I/1)은 네 글자를 다 뺐다.
const CODE_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

const rooms = new Map();
const roomOf = new Map();

let server = null;

function newRoomCode() {
    for (;;) {
        let code = '';
        for (let i = 0; i < 6; i++) code += CODE_CHARS[crypto.randomInt(CODE_CHARS.length)];
        if (!rooms.has(code)) return code;
    }
}

function stateOf(room) {
    return {
        roomCode: room.roomCode,
        sessionId: room.sessionId,
        hostClientId: room.hostClientId,
        address: room.address,
        port: room.port,
        maxPlayers: room.maxPlayers,
        players: [...room.players]
    };
}

function broadcast(room, event, data) {
    for (const clientId of room.players) {
        const target = server.clients.get(clientId);
        if (target) server.send(target, event, data);
    }
}

// leaveRoom 과 소켓 close 가 같이 타는 이탈 경로. 둘을 갈라 두면 유령 방이 쌓인다.
function leave(ws) {
    const clientId = ws.ctx.clientId;
    const roomCode = roomOf.get(clientId);
    roomOf.delete(clientId);

    const room = roomCode ? rooms.get(roomCode) : null;
    if (!room) return;

    if (room.hostClientId === clientId) {
        rooms.delete(room.roomCode);

        for (const memberId of room.players) {
            if (memberId === clientId) continue;

            roomOf.delete(memberId);

            const target = server.clients.get(memberId);
            if (target) server.send(target, 'sessionClosed', { roomCode: room.roomCode, reason: 'HOST_LEFT' });
        }

        server.log(`방 파기: ${room.roomCode} (호스트 이탈)`);
        return;
    }

    room.players = room.players.filter(id => id !== clientId);
    broadcast(room, 'roomState', stateOf(room));
    server.log(`방 이탈: ${room.roomCode} ← ${clientId} (${room.players.length}/${room.maxPlayers})`);
}

function createRoom(ws, data) {
    const ctx = ws.ctx;
    if (roomOf.has(ctx.clientId)) leave(ws);

    const room = {
        roomCode: newRoomCode(),
        sessionId: crypto.randomUUID(),
        hostClientId: ctx.clientId,
        address: ctx.address,
        port: data.port,
        maxPlayers: data.maxPlayers ?? 4,
        players: [ctx.clientId]
    };

    rooms.set(room.roomCode, room);
    roomOf.set(ctx.clientId, room.roomCode);

    // connectionToken 은 발급만 하고 검증하지 않는다 (PRD-01 범위 밖).
    server.send(ws, 'createRoomResult', { success: true, connectionToken: crypto.randomUUID(), ...stateOf(room) });
    server.log(`방 생성: ${room.roomCode} @ ${room.address}:${room.port}`);
}

function joinRoom(ws, data) {
    const room = rooms.get((data.roomCode ?? '').toUpperCase());

    if (!room) {
        server.send(ws, 'joinRoomResult', { success: false, error: 'ROOM_NOT_FOUND' });
        return;
    }

    if (room.players.length >= room.maxPlayers) {
        server.send(ws, 'joinRoomResult', { success: false, error: 'ROOM_FULL' });
        return;
    }

    const ctx = ws.ctx;
    if (roomOf.has(ctx.clientId)) leave(ws);

    if (!room.players.includes(ctx.clientId)) room.players.push(ctx.clientId);
    roomOf.set(ctx.clientId, room.roomCode);

    server.send(ws, 'joinRoomResult', { success: true, connectionToken: crypto.randomUUID(), ...stateOf(room) });
    broadcast(room, 'roomState', stateOf(room));
    server.log(`방 참가: ${room.roomCode} ← ${ctx.clientId} (${room.players.length}/${room.maxPlayers})`);
}

function leaveRoom(ws) {
    leave(ws);
    server.send(ws, 'leaveRoomResult', { success: true });
}

function roomList(ws) {
    const list = [...rooms.values()]
        .filter(room => room.players.length < room.maxPlayers)
        .map(room => ({
            roomCode: room.roomCode,
            playerCount: room.players.length,
            maxPlayers: room.maxPlayers
        }));

    server.send(ws, 'roomListResult', { rooms: list });
}

function register(serverApi) {
    server = serverApi;

    server.on('createRoom', createRoom);
    server.on('joinRoom', joinRoom);
    server.on('leaveRoom', leaveRoom);
    server.on('roomList', roomList);
    server.onDisconnect(leave);
}

module.exports = { register };
