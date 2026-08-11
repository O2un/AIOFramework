# AIOFramework

**All in One Framework For Unity** — 게임이 아니라 재사용 가능한 Unity 프레임워크입니다.

Unity의 암묵적인 수명주기(`Awake`/`Start`/`Update`)와 전역 접근을 걷어내고, **DI 스코프 · 비동기 초기화 · 단방향 데이터 흐름**을 기본값으로 삼는 것이 목표입니다. "동작하는가"보다 **"올바른 계층에 올바른 의존 방향으로 놓였는가"** 를 품질 기준으로 둡니다.

| | |
| --- | --- |
| Unity | `6000.4.3f1` |
| 렌더 파이프라인 | URP `17.4.0` |
| 네임스페이스 | `O2un.*` |
| 코드 위치 | `Assets/0_Framework/00_Script/` |

---

## 프레임워크 개요

### [Miro 보드에서 보기](https://miro.com/app/live-embed/uXjVHdE1C40=/?embedMode=view_only_without_ui&moveToViewport=509%2C-418%2C1873%2C1137&embedId=568990435726)

![AIOFramework 프레임워크 개요 보드](https://github.com/user-attachments/assets/d6390a55-0882-4883-bb43-ea3c9bb7c106)

---

## 기술 스택

| 영역 | 사용 |
| --- | --- |
| DI | [VContainer](https://github.com/hadashiA/VContainer) |
| 비동기 | [UniTask](https://github.com/Cysharp/UniTask) |
| 리액티브 | [R3](https://github.com/Cysharp/R3) |
| 리소스 | Addressables |
| 네트워크 | Netcode for Entities, WebSocket, HTTP |
| UI | uGUI + UI Toolkit, TextMeshPro, Input System |
| 지역화 | Unity Localization |
| 데이터 | ExcelDataReader (NuGet) + Roslyn Source Generator |
| 정적 분석 | 자체 Roslyn Analyzer |

---

## 아키텍처

### 1. DI 스코프 기준 3분할

모든 매니저는 정적 싱글톤이 아니라 `SubSystemBase`를 상속하고 **VContainer 스코프에 등록되는 단일 인스턴스**입니다. 수명 경계에 따라 세 갈래로 나뉩니다.

| 계층 | 기반 클래스 | 수명 | 예시 |
| --- | --- | --- | --- |
| Engine | `EngineSubsystemBase` | 앱 전체 | `LogManager`, `SceneManager`, `NetworkManager`, `MultiplayerManager` |
| Game | `GameSubsystemBase` | 게임 세션 | `PoolingManager` |
| Editor | `EditSubsystemBase` | `UNITY_EDITOR` 전용 | — |

`SubSystemBase`는 `IInitializable`로 진입해 **비동기 초기화**를 돌리고, 완료 시점을 `UniTaskCompletionSource`로 공개합니다. 초기화가 끝나기 전에 접근하는 쪽은 `WaitUntilReadyAsync()`로 기다리므로 "초기화 순서에 의존하는 코드"가 생기지 않습니다.

```csharp
public abstract partial class SubSystemBase : SafeDisposableClass, IAsyncReady, IInitializable
{
    protected abstract UniTask InitAsync();
    public UniTask WaitUntilReadyAsync() => _readySource.Task;
}
```

### 2. MonoBehaviour 수명주기 통일 — `SafeMono` / `SafeUI`

Unity가 부르는 이벤트를 그대로 쓰면 초기화 시점이 프레임과 실행 순서에 묶입니다. 그래서 `MonoBehaviour`를 직접 상속하지 않고, `Awake`/`Start`는 `[Obsolete(error: true)]`로 **컴파일 단계에서 봉인**했습니다.

| Unity 이벤트 | 대신 쓰는 것 |
| --- | --- |
| `Awake` / `Start` | `protected override UniTask Init(CancellationToken ct)` |
| `OnEnable` / `OnDisable` | `SafeEnable` / `SafeDisable` |
| `OnDestroy` | `SafeDestroy` |

초기화는 비동기 함수 하나로 모이고, 그 안에서 다시 내부 함수로 갈라져 좀비 프로세스가 남지 않도록 `CancellationToken`이 끝까지 전달됩니다. R3 구독은 `DisposableR3`에 묶여 파괴 시점에 함께 정리됩니다.

- `SafeMono` — 일반 오브젝트
- `SafeUI` — uGUI 뷰
- `SafeUIToolkit` — UI Toolkit 뷰

### 3. MVVM

View는 **철저하게 표시 역할만** 합니다. ViewModel은 `MonoBehaviour`가 아닌 **순수 C# 클래스**이고, `ContextBase`가 이를 생성해 View에 바인딩하고 파괴까지 책임집니다.

```csharp
public abstract partial class ContextBase<V, M> : SafeMono, IContextBase, ISafeInitializable
    where V : class, IViewBase
    where M : class, IViewModelBase
{
    [RequireComponentField] private V _view;   // Roslyn 생성기가 참조 결선을 채운다
    public M Model { get; private set; }

    protected abstract M CreateModel();
}
```

| 타입 | 역할 |
| --- | --- |
| `ContextBase<V, M>` | ViewModel 생성 · 바인딩 · 소유. 수명의 주인 |
| `ViewBase<T>` / `ViewBaseToolkit<T>` | 표시와 입력 전달만. 상태를 갖지 않음 |
| `ViewModelBase` | 순수 C#. R3 `ReactiveProperty`로 상태 노출 |
| `SubViewBase` | 부모 View에 종속된 부분 뷰 |

데이터는 ViewModel → View 한 방향으로 흐르고, 반대 방향은 R3 스트림으로만 돌아옵니다.

### 4. 데이터 드리븐 — Excel에서 암호화 바이너리까지

기획 데이터는 Excel이 원본이고, 런타임은 암호화된 바이너리만 읽습니다. 사이는 전부 자동화되어 있습니다.

```
 .xlsx
   │  ExcelDataReader
   ▼
 ExcelDataPostprocessor        에디터 임포트 시 1차 산출
   │
   ▼
 StaticDataGenerator           Roslyn Source Generator — 1차 산출을 2차 가공
   │  *.g.cs
   ▼
 StaticData 파생 타입
   │  BinaryHelper / AES
   ▼
 암호화 바이너리
   │
   ▼
 StaticDataManager             런타임 로드
```

Excel을 임포트하면 에디터 후처리기가 1차 데이터를 뽑고, **Roslyn 코드 생성기가 그 결과를 2차 가공해** `partial` 클래스를 만들어냅니다. 손으로 쓰는 것은 `Set()` / `Link()` 같은 참조 결선 로직뿐입니다.

### 5. 네트워크

| 채널 | 구현 |
| --- | --- |
| HTTP | `Network/Core/webapi/HttpService` |
| WebSocket | `WebSocketClient`, `WebSocketMessenger`, `WebSocketMatchmakingService` |
| P2P | `IP2PTransport`, `P2PMessenger`, `P2PSessionCoordinator` |
| Dedicated Server | Netcode for Entities — 클라이언트 / 호스트 / 전용 서버 연결 모듈 분리 |

`NetworkRouter`가 패킷을 핸들러로 분배하고, `NetworkRequestTracker`가 요청–응답 대응을 관리합니다. 서버 핸들러 등록은 `ServerHandlerGenerator`(Roslyn)가 어트리뷰트를 훑어 자동 생성합니다.

### 6. Roslyn — 생성과 검사

프레임워크 규약 중 사람이 지키기 어려운 것들은 컴파일러가 대신 잡습니다.

**Source Generator**

| 생성기 | 하는 일 |
| --- | --- |
| `RequireComponent` | `[RequireComponentField]` 필드의 컴포넌트 참조를 자동 결선 |
| `StaticDataGenerator` | Excel 산출물에서 `StaticData` 파생 타입 생성 |
| `ServerHandlerGenerator` | 서버 패킷 핸들러 등록 코드 생성 |

**Analyzer**

| 규칙 | 잡는 것 |
| --- | --- |
| `MustCallBase` (OE0001) | `[MustCallBase]` 메서드를 override하면서 `base.X()`를 빠뜨린 경우 |
| `ReadOnlyTransform` (OE0002) | `[ReadOnlyTransform]`이 붙은 `Transform`을 외부에서 조작하는 경우 |

> `MustCallBase`는 부모의 `virtual` 초기화·정리 로직을 빠뜨리고 override하는, 상속 기반 프레임워크에서 가장 흔하고 가장 조용한 버그를 컴파일 에러로 바꿉니다.

---

## 어셈블리 구조

코드는 `.asmdef`로 나뉘고, 의존 방향은 아래 한 방향입니다.

```
O2un.Core  →  O2un.Data
           →  O2un.Network  →  O2un.SubSystem  →  O2un.UI  →  O2un.DI
```

| 어셈블리 | 폴더 |
| --- | --- |
| `O2un.Core` | `Core/` |
| `O2un.Data` | `Data/` |
| `O2un.Network` | `Network/` |
| `O2un.SubSystem` | `SubSystem/` |
| `O2un.UI` | `CommonUI/` |
| `O2un.DI` | `DependencyInjection/` |

**`O2un.Core`는 VContainer를 참조하지 않습니다.** Core가 DI 컨테이너를 모르는 것이 이 프레임워크의 경계이고, 의도된 제약입니다.

---

## 프로젝트 구조

```
Assets/0_Framework/00_Script/
├── Core/                  # 기반 계층 — DI 무의존
│   ├── MonoBehaviour/     # SafeMono, SafeUI, SafeUIToolkit
│   ├── MVVM/              # ContextBase, ViewBase, ViewModelBase
│   ├── CodeGenerator/     # Roslyn Source Generator
│   ├── CodeAnalyzer/      # Roslyn Analyzer
│   ├── Reactive/ Events/ ObjectPool/ Localization/ Loading/ Utils/
├── Data/                  # StaticData 파이프라인
├── Network/               # HTTP / WebSocket / P2P / Netcode
├── SubSystem/             # Engine · Game · Editor 서브시스템
├── CommonUI/              # 공용 UI (uGUI + UI Toolkit)
├── DependencyInjection/   # LifetimeScope, 부트스트랩
└── __DEV/                 # 개발 도구 · 데모
```

### 씬

| 씬 | 비고 |
| --- | --- |
| `Bootstrap` | 진입점. 전역 스코프 구성. 시작 씬은 `BootConfig`가 정한다 |
| `LoadingScene` | |
| `LobbyScene` | |
| `GameScene` | 현재 빈 씬 |
| `UIDemoScene` | 아래 [UI 데모](#ui-데모)를 담는 씬 |

### UI 데모

| 데모 | 내용 |
| --- | --- |
| Localization | 같은 화면을 uGUI와 UI Toolkit으로 각각 구현해 지역화 처리를 나란히 비교 |

---

## 라이선스

[LICENSE](LICENSE)
