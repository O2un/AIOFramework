const WebSocket = require('ws');

// 8080 포트로 서버 실행
const wss = new WebSocket.Server({ port: 8080 });

console.log("🚀 유니티 테스트용 웹소켓 서버가 8080 포트에서 실행 중입니다.");

wss.on('connection', function connection(ws) {
    console.log("✅ 유니티 클라이언트 연결됨");

    ws.on('message', function incoming(message) {
        try {
            // Buffer를 문자열로 변환 후 파싱
            const packet = JSON.parse(message.toString());
            console.log(`📩 수신 이벤트: ${packet.Event}`, packet.Data);

            // 간단한 로그인 테스트 응답
            if (packet.Event === 'login') {
                const response = {
                    Event: 'loginResult', // Subsystem의 규약에 맞춤
                    Data: {
                        success: true,
                        message: "로그인 성공",
                        token: "test_token_12345"
                    }
                };
                ws.send(JSON.stringify(response));
                console.log(`📤 응답 전송: loginResult`);
            } 
            // 기타 이벤트는 그대로 에코(Echo)
            else {
                const echo = {
                    event: `${packet.Event}Result`,
                    data: { ...packet.Data, serverTime: new Date().toISOString() }
                };
                ws.send(JSON.stringify(echo));
            }
        } catch (e) {
            console.error("❌ 데이터 파싱 에러:", e.message);
        }
    });

    ws.on('close', () => console.log("❌ 클라이언트 연결 종료"));
});