const crypto = require('crypto');

// 손으로 입력하는 값이라 혼동 쌍(O/0, I/1)은 네 글자를 다 뺐다.
const CODE_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

const rooms = new Map();
const roomOf = new Map();

// 방 소속은 소켓을 찾을 수 있는 clientId 로 들고, DTO 로 나갈 때만 playerId 로 바꾼다.
const playerIdOf = new Map();

let server = null;

const isPlayerId = (value) => 'string' === typeof value && 0 < value.trim().length;
const isPort = (value) => Number.isInteger(value) && 0 < value && 65535 >= value;
const isMaxPlayers = (value) => Number.isInteger(value) && 0 < value && 2147483647 >= value;

function newRoomCode() {
    for (;;) {
        let code = '';
        for (let i = 0; i < 6; i++) code += CODE_CHARS[crypto.randomInt(CODE_CHARS.length)];
        if (!rooms.has(code)) return code;
    }
}

const playerIdFor = (clientId) => playerIdOf.get(clientId) ?? clientId;

function connectionOf(room, role) {
    return {
        sessionId: room.sessionId,
        roomCode: room.roomCode,
        role,
        address: room.address,
        port: room.port,
        // connectionToken 은 발급만 하고 검증하지 않는다 (PRD-02 범위 밖).
        connectionToken: crypto.randomUUID()
    };
}

function stateOf(room) {
    return {
        roomCode: room.roomCode,
        hostPlayerId: playerIdFor(room.hostClientId),
        players: room.players.map(clientId => ({
            playerId: playerIdFor(clientId),
            isHost: clientId === room.hostClientId
        }))
    };
}

function summaryOf(room) {
    return {
        roomCode: room.roomCode,
        hostPlayerId: playerIdFor(room.hostClientId),
        currentPlayers: room.players.length,
        maxPlayers: room.maxPlayers,
        isJoinable: room.players.length < room.maxPlayers
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
    playerIdOf.delete(clientId);

    const room = roomCode ? rooms.get(roomCode) : null;
    if (!room) return;

    if (room.hostClientId === clientId) {
        rooms.delete(room.roomCode);

        for (const memberId of room.players) {
            if (memberId === clientId) continue;

            roomOf.delete(memberId);

            const target = server.clients.get(memberId);
            if (target) server.send(target, 'sessionClosed', { sessionId: room.sessionId, reason: 'HOST_LEFT' });
        }

        server.log(`방 파기: ${room.roomCode} (호스트 이탈)`);
        return;
    }

    room.players = room.players.filter(id => id !== clientId);
    broadcast(room, 'roomUpdated', stateOf(room));
    server.log(`방 이탈: ${room.roomCode} ← ${clientId} (${room.players.length}/${room.maxPlayers})`);
}

function createRoom(ws, data, uniqueKey) {
    if (false === isPlayerId(data.playerId)) {
        server.send(ws, 'createRoomAck', { isSuccess: false, reason: 'INVALID_PLAYER_ID', connection: null }, uniqueKey);
        return;
    }

    if (false === isPort(data.port)) {
        server.send(ws, 'createRoomAck', { isSuccess: false, reason: 'INVALID_PORT', connection: null }, uniqueKey);
        return;
    }

    if (false === isMaxPlayers(data.maxPlayers)) {
        server.send(ws, 'createRoomAck', { isSuccess: false, reason: 'INVALID_MAX_PLAYERS', connection: null }, uniqueKey);
        return;
    }

    const ctx = ws.ctx;
    if (roomOf.has(ctx.clientId)) leave(ws);

    const room = {
        roomCode: newRoomCode(),
        sessionId: crypto.randomUUID(),
        hostClientId: ctx.clientId,
        address: ctx.address,
        port: data.port,
        maxPlayers: data.maxPlayers,
        players: [ctx.clientId]
    };

    rooms.set(room.roomCode, room);
    roomOf.set(ctx.clientId, room.roomCode);
    playerIdOf.set(ctx.clientId, data.playerId);

    server.send(ws, 'createRoomAck', { isSuccess: true, reason: null, connection: connectionOf(room, 'Host') }, uniqueKey);
    server.log(`방 생성: ${room.roomCode} @ ${room.address}:${room.port}`);
}

function joinRoom(ws, data, uniqueKey) {
    if (false === isPlayerId(data.playerId)) {
        server.send(ws, 'joinRoomAck', { isSuccess: false, reason: 'INVALID_PLAYER_ID', connection: null }, uniqueKey);
        return;
    }

    if ('string' !== typeof data.roomCode) {
        server.send(ws, 'joinRoomAck', { isSuccess: false, reason: 'INVALID_ROOM_CODE', connection: null }, uniqueKey);
        return;
    }

    const room = rooms.get(data.roomCode.toUpperCase());

    if (!room) {
        server.send(ws, 'joinRoomAck', { isSuccess: false, reason: 'ROOM_NOT_FOUND', connection: null }, uniqueKey);
        return;
    }

    if (room.players.length >= room.maxPlayers) {
        server.send(ws, 'joinRoomAck', { isSuccess: false, reason: 'ROOM_FULL', connection: null }, uniqueKey);
        return;
    }

    const ctx = ws.ctx;
    if (roomOf.has(ctx.clientId)) leave(ws);

    if (!room.players.includes(ctx.clientId)) room.players.push(ctx.clientId);
    roomOf.set(ctx.clientId, room.roomCode);
    playerIdOf.set(ctx.clientId, data.playerId);

    server.send(ws, 'joinRoomAck', { isSuccess: true, reason: null, connection: connectionOf(room, 'Client') }, uniqueKey);
    broadcast(room, 'roomUpdated', stateOf(room));
    server.log(`방 참가: ${room.roomCode} ← ${ctx.clientId} (${room.players.length}/${room.maxPlayers})`);
}

function leaveRoom(ws, data, uniqueKey) {
    const roomCode = roomOf.get(ws.ctx.clientId);
    const room = roomCode ? rooms.get(roomCode) : null;

    if (!room) {
        server.send(ws, 'leaveRoomAck', { isSuccess: false, reason: 'NOT_IN_ROOM' }, uniqueKey);
        return;
    }

    if ('string' !== typeof data.sessionId || room.sessionId !== data.sessionId) {
        server.send(ws, 'leaveRoomAck', { isSuccess: false, reason: 'SESSION_MISMATCH' }, uniqueKey);
        return;
    }

    leave(ws);
    server.send(ws, 'leaveRoomAck', { isSuccess: true, reason: null }, uniqueKey);
}

// 정원이 찬 방도 내려보낸다. 걸러 버리면 RoomSummary.isJoinable 이 항상 true 인 죽은 필드가 된다.
function roomList(ws, data, uniqueKey) {
    const list = [...rooms.values()].map(summaryOf);

    server.send(ws, 'roomListAck', { isSuccess: true, reason: null, rooms: list }, uniqueKey);
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
