using System.Runtime.CompilerServices;

// Netcode wire 구조와 정적 Bridge 는 어셈블리 밖으로 새지 않게 internal 로 묶여 있다.
// 그 경계를 유지한 채로 변환·수명 계약을 검증하려면 테스트 어셈블리에만 내부를 열어야 한다.
[assembly: InternalsVisibleTo("O2un.Network.Tests")]
