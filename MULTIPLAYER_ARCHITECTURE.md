# 멀티플레이어 달리기 (NGO 기반) — 아키텍처 설계 v3

> **개발 단계 / 공모전 제출용 / 1방 최대 4인**
> v2 (20인 기준)에서 4인 매치로 스케일 다운한 버전.

---

## 1. 개요 / 요구사항

| 항목 | 값 |
|---|---|
| 장르 | 6레인 측면 스크롤 멀티 러닝 |
| 동시 인원 | **1방 최대 4인** |
| 플랫폼 | PC (Windows 우선) |
| 목적 | 공모전 제출 (시연 안정성 최우선) |
| 안티치트 | 느슨 (서버 측 최소 검증만) |
| 운영 비용 | UGS 무료 티어 한도 내 |

---

## 2. 핵심 결정 사항

| 항목 | 결정 | 이유 |
|---|---|---|
| 서버 모델 | **Listen Server (Host-Client)** + Relay | 비용 0, 4인이면 호스트 PC 부하 거의 없음 |
| 권위 모델 | **Hybrid** (Owner Auth + Server Validation) | 반응성 + 매치 상태 신뢰성 |
| 트랜스포트 | UnityTransport + **Relay** | NAT 우회 |
| 방 관리 | **Unity Lobby** | 코드 조인 + 목록 동시 지원 |
| 인증 | **Anonymous Auth** | 심사위원이 가입 없이 바로 테스트 |
| 캐릭터 물리 | **Kinematic Rigidbody + 수동 이동** | NGO 동기화 안정성 |
| 빈 슬롯 처리 | **AI 봇 대체** | 4명 못 모으면 시연 불가 → 봇으로 채움 |

### Dedicated Server를 선택하지 않은 이유
- 공모전 시연 기간 한정 → 운영 비용 정당화 어려움
- 4인이면 호스트 부담 거의 0 (전송 데이터 KB/s 단위)
- 출시 결정 시 ServerAuth 구조 유지하고 Dedicated으로 이전 가능

---

## 3. 기술 스택

| 영역 | 패키지 / 서비스 | 버전(권장) |
|---|---|---|
| 코어 | `com.unity.netcode.gameobjects` | 2.x |
| 트랜스포트 | `com.unity.transport` | 2.x |
| Relay | `com.unity.services.relay` | latest |
| Lobby | `com.unity.services.lobby` | latest |
| Auth | `com.unity.services.authentication` | latest |
| Core SDK | `com.unity.services.core` | latest |

---

## 4. 네트워크 토폴로지

```
        ┌──────────────────────────────────┐
        │  HOST PC (서버 역할 + 본인 클라) │
        │  ┌────────────────────────────┐  │
        │  │ NetworkManager.StartHost() │  │
        │  │ ─ 게임 상태 권위           │  │
        │  │ ─ 골 도착 / 순위 판정      │  │
        │  │ ─ 로컬 PlayerController    │  │
        │  └────────────────────────────┘  │
        └──────────────┬───────────────────┘
                       │ (UDP)
                       ▼
            ┌──────────────────────┐
            │   Unity Relay        │  ← NAT 우회 중계
            └──────────┬───────────┘
                       │
        ┌──────────────┼──────────────┐
        ▼              ▼              ▼
   [Client A]      [Client B]    [Client C]   (호스트 포함 최대 4)
```

호스트 = 서버 = 본인 클라이언트.
Relay는 패킷만 중계 (게임 로직 X).

---

## 5. 권위 모델 (Hybrid) 상세

### 5.1 Owner 권위 (각 클라가 자기 캐릭터)
- 위치 / 점프 / 라인 변경 → Owner가 직접 적용 → NetworkTransform 브로드캐스트
- 자기 입력 0ms 반응

### 5.2 서버 권위 (호스트)
- 매치 상태: `Lobby / Countdown / Running / Finished`
- 타이머: 카운트다운, 결승 제한 시간
- 결승 도착 판정: 클라 보고 → 서버 검증 → 순위 부여
- 플레이어 입퇴장 / 봇 슬롯 채움
- 매치 재시작

### 5.3 느슨한 안티치트
| 검증 | 방법 |
|---|---|
| 비정상 빠른 도착 | Running 진입 후 최소 N초 경과 체크 |
| 골 도착 위치 | 결승선 트리거 근처여야 인정 |
| 위치 점프 | 직전 위치 대비 거리/속도 threshold 초과 시 위치 보정 (로그만) |

---

## 6. 게임 흐름

```
[Bootstrap] ─→ [Title] ─→ [Lobby Room] ─→ [Race] ─→ [Result] ─┐
   │                          │                                │
   │                    빈 슬롯 봇 채우기                       │
   │←──────────────────── 다시 / 로비 복귀 ────────────────────┘
```

### 6.1 Lobby
1. 호스트: Relay Allocation → JoinCode → Lobby 생성 → `StartHost()`
2. 클라: JoinCode로 Lobby 조인 → Relay 조인 → `StartClient()`
3. LobbyRoom에서 플레이어 슬롯 표시 (최대 4)
4. 호스트가 "시작" 누르면 빈 슬롯은 봇으로 채워서 매치 시작

### 6.2 Race
1. 호스트가 `NetworkSceneManager.LoadScene("Race")`
2. ConnectionApprovalHandler에서 레인별 스폰 분배 (4레인 사용)
3. 봇은 호스트가 NetworkObject로 추가 스폰
4. 3-2-1 카운트다운 → `RaceState.Running`
5. 각 클라 입력 → NetworkTransform 동기화
6. 결승 진입 → `ReportGoalServerRpc()` → 서버 검증 → `FinishOrder`
7. 전원 도착 또는 제한 시간 초과 → `EndRaceClientRpc()`

### 6.3 Result
1. `FinishOrder` 기반 순위 표시
2. 호스트가 "다시" / "로비" 선택
3. **다시**: NetworkPlayer Despawn → Race 씬 재로드 (빠른 재매치)
4. **로비**: LobbyRoom 씬으로 복귀, Lobby 유지

---

## 7. 파일 구조

```
Project-Seoul/Assets/Scripts/
│
├── Racing/                              # 게임플레이 (기존 + 일부 수정)
│   ├── PlayerController.cs              # Rigidbody → Kinematic 리팩토링
│   ├── IInputProvider.cs                # 기존
│   ├── PlayerInputProvider.cs           # 로컬 키보드
│   ├── NullInputProvider.cs             # ★ 신규 (원격용)
│   ├── BotInputProvider.cs              # ★ 신규 (봇 AI)
│   └── Core/
│       ├── LaneManager.cs
│       └── GoalTrigger.cs               # 멀티 대응으로 단순화
│
├── Network/                             # ★ 신규
│   ├── Bootstrap/
│   │   └── ServicesBootstrap.cs         # UGS 초기화 + 익명 로그인
│   │
│   ├── Lobby/
│   │   ├── LobbyManager.cs              # 생성/조인/Heartbeat
│   │   ├── RelayConnector.cs            # Relay Allocation + Transport
│   │   └── LobbyPlayerData.cs           # 이름, Ready
│   │
│   ├── Match/
│   │   ├── NetworkRaceManager.cs        # 서버 권위 상태머신
│   │   ├── RaceConfig.cs                # SO: 카운트다운, 제한 시간
│   │   ├── MatchLifecycle.cs            # 재시작 / 로비 복귀
│   │   ├── BotSpawner.cs                # ★ 빈 슬롯 봇 채움
│   │   └── PlayerSpawner.cs             # 레인별 스폰
│   │
│   ├── Player/
│   │   ├── NetworkPlayer.cs             # NetworkBehaviour, IsOwner 분기
│   │   ├── NetworkPlayerState.cs        # NetworkVariable 묶음
│   │   ├── PlayerNameTag.cs             # 이름표
│   │   └── ServerValidator.cs           # 골 도착 검증
│   │
│   ├── Connection/
│   │   ├── ConnectionApprovalHandler.cs # 4인 제한, 매치 중 거부
│   │   ├── DisconnectHandler.cs         # 이탈 처리
│   │   └── ConnectionPayload.cs
│   │
│   └── Utility/
│       ├── NetworkSingleton.cs
│       └── NetworkLog.cs
│
└── UI/
    ├── Title/
    │   └── TitleScreen.cs               # 호스트/조인 + 닉네임
    ├── Lobby/
    │   ├── LobbyRoomUI.cs               # 4슬롯 + Ready/Start
    │   └── LobbyPlayerSlot.cs
    ├── Race/
    │   ├── CountdownUI.cs
    │   ├── RaceHUD.cs                   # 자기 + 3명 위치
    │   └── PlayerNameTagUI.cs
    └── Result/
        └── ResultScreen.cs              # 4명 순위표
```

### 씬 구성
```
Assets/_Project/Scenes/
├── 00_Bootstrap.unity
├── 01_Title.unity
├── 02_LobbyRoom.unity
├── 03_Race.unity
└── 04_Result.unity
```

---

## 8. 핵심 클래스 설계

### 8.1 ServicesBootstrap
```csharp
public class ServicesBootstrap : MonoBehaviour {
    async void Start() {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        SceneManager.LoadScene("01_Title");
    }
}
```

### 8.2 LobbyManager (핵심 메서드)
| 메서드 | 책임 |
|---|---|
| `CreateLobbyAsync(name, max=4)` | Relay 발급 → Lobby 생성 → JoinCode 저장 → StartHost |
| `JoinByCodeAsync(code)` | Lobby 조인 → Relay 조인 → StartClient |
| `UpdatePlayerReadyAsync(bool)` | Player Data 갱신 |
| `StartMatch()` | (호스트) 빈 슬롯 봇 채움 → Race 씬 로드 |
| `LeaveLobbyAsync()` | 정리 |
| Heartbeat 코루틴 | 25초마다 `SendHeartbeatPingAsync` |
| Polling 코루틴 | 1.5초마다 플레이어 목록 갱신 |

### 8.3 NetworkPlayer
```csharp
[RequireComponent(typeof(PlayerController))]
public class NetworkPlayer : NetworkBehaviour {
    private PlayerController _controller;

    public NetworkVariable<FixedString32Bytes> PlayerName = new(
        writePerm: NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> IsBot = new();

    public override void OnNetworkSpawn() {
        _controller = GetComponent<PlayerController>();

        if (IsBot.Value && IsServer) {
            _controller.Initialize(new BotInputProvider());     // 봇 AI
        } else if (IsOwner) {
            _controller.Initialize(new PlayerInputProvider());  // 로컬 입력
            PlayerName.Value = PlayerPrefs.GetString("Nickname");
        } else {
            _controller.Initialize(new NullInputProvider());    // 원격 (NT가 위치 적용)
            var rb = GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
        }
    }
}
```

### 8.4 BotSpawner (4인 보장)
```csharp
public class BotSpawner : NetworkBehaviour {
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private int targetPlayerCount = 4;

    public void FillEmptySlots() {
        if (!IsServer) return;
        int human = NetworkManager.Singleton.ConnectedClientsList.Count;
        int needed = targetPlayerCount - human;

        for (int i = 0; i < needed; i++) {
            var go = Instantiate(playerPrefab, GetBotSpawnPos(i), Quaternion.identity);
            var no = go.GetComponent<NetworkObject>();
            var np = go.GetComponent<NetworkPlayer>();
            np.IsBot.Value = true;
            no.Spawn();   // OwnerClientId = ServerClientId (호스트 소유)
        }
    }
}
```

### 8.5 NetworkRaceManager (서버 권위 상태머신)
```csharp
public enum RaceState : byte { Waiting, Countdown, Running, Finished }

public class NetworkRaceManager : NetworkBehaviour {
    public NetworkVariable<RaceState> State = new(RaceState.Waiting);
    public NetworkVariable<float>     CountdownRemaining = new();
    public NetworkVariable<float>     RaceElapsed = new();
    public NetworkList<FinishEntry>   FinishOrder;

    [SerializeField] private RaceConfig config;
    private float _runningStartTime;

    public override void OnNetworkSpawn() {
        FinishOrder = new NetworkList<FinishEntry>();
        if (IsServer) StartCoroutine(ServerRun());
    }

    private IEnumerator ServerRun() {
        State.Value = RaceState.Countdown;
        for (float t = config.countdownSeconds; t > 0; t -= Time.deltaTime) {
            CountdownRemaining.Value = t;
            yield return null;
        }
        State.Value = RaceState.Running;
        _runningStartTime = (float)NetworkManager.ServerTime.Time;

        while (!IsAllFinishedOrTimeout()) {
            RaceElapsed.Value = (float)NetworkManager.ServerTime.Time - _runningStartTime;
            yield return null;
        }
        State.Value = RaceState.Finished;
        EndRaceClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReportGoalServerRpc(ServerRpcParams p = default) {
        if (State.Value != RaceState.Running) return;
        ulong cid = p.Receive.SenderClientId;
        if (AlreadyFinished(cid)) return;
        if (!ServerValidator.IsValidGoal(cid, _runningStartTime, config)) return;

        FinishOrder.Add(new FinishEntry {
            clientId = cid,
            finishTime = RaceElapsed.Value
        });
    }
}
```

### 8.6 ConnectionApprovalHandler (4인 제한)
```csharp
public class ConnectionApprovalHandler : MonoBehaviour {
    private const int MaxPlayers = 4;

    private void Start() {
        var nm = NetworkManager.Singleton;
        nm.ConnectionApprovalCallback = (req, res) => {
            if (nm.ConnectedClients.Count >= MaxPlayers) {
                res.Approved = false;
                res.Reason   = "Room full (4/4)";
                return;
            }
            if (NetworkRaceManager.Instance?.State.Value == RaceState.Running) {
                res.Approved = false;
                res.Reason   = "Match in progress";
                return;
            }
            res.Approved = true;
            res.CreatePlayerObject = true;
            res.Position = AssignLanePosition(nm.ConnectedClients.Count);
        };
    }
}
```

---

## 9. 동기화 전략

### 9.1 데이터 분류

| 데이터 | 빈도 | 방식 | 권위 |
|---|---|---|---|
| 위치 | ~20Hz | NetworkTransform | Owner |
| 회전 | 동기화 X | — | — |
| 현재 레인 | 변경 시 | NetworkVariable<byte> | Owner |
| 점프/낙하 상태 | 변경 시 | NetworkVariable<byte> | Owner |
| 스태미나 | **동기화 X** | 로컬만 | — |
| 닉네임 / IsBot | 1회 | NetworkVariable | Owner/Server |
| 매치 상태 | 변경 시 | NetworkVariable<RaceState> | Server |
| 경과 시간 | 0.5초마다 | NetworkVariable<float> | Server |
| 순위 | 변경 시 | NetworkList<FinishEntry> | Server |

### 9.2 트래픽 예상치 (4인 기준)
```
4명 매치, 20Hz 위치 동기화:
- 1명당 수신: 3명 × 20Hz × ~24byte ≈ 1.4 KB/s
- 호스트: 3명 송신 = ~4 KB/s (양방향)
```
일반 인터넷에서 사실상 무부담. 압축 옵션도 굳이 켤 필요 없음.

### 9.3 NetworkTransform 설정
- **Interpolate**: ON (원격 측 부드러움)
- **Position Threshold**: 0.01
- **Use Half Float Precision**: OFF (4인이라 트래픽 무관)
- **Sync Z**: ON, **Sync Rotation**: OFF
- **Authority**: Owner

---

## 10. 라이프사이클 / 엣지 케이스

### 10.1 호스트 이탈
- 모든 클라 연결 끊김 → `OnClientDisconnectCallback`
- "호스트가 나갔습니다" 모달 → 타이틀로 강제 복귀

### 10.2 클라이언트 이탈
- **매치 중**: 캐릭터 Despawn, 매치는 남은 인원으로 계속
- 이미 도착했다면 FinishOrder에서 순위 유지
- **로비 중**: 슬롯 비움 → 새 사람이 들어올 수 있음
- 4인이 3인 이하로 줄어도 매치 종료하지 않음 (시연 안정성)

### 10.3 빈 슬롯 봇 대체
- 호스트가 "시작" 누른 시점 → 사람 수 확인 → 부족한 만큼 봇 추가
- 봇은 `BotInputProvider`로 적당한 AI 주행 (난이도 조절)
- 봇은 NetworkObject 소유자가 호스트 → 호스트에서 시뮬레이션

### 10.4 매치 재시작 ("다시 하기")
1. 호스트가 `RestartMatchServerRpc()` 호출
2. 서버: 모든 NetworkPlayer Despawn → Race 씬 재로드
3. PlayerSpawner + BotSpawner 다시 실행
4. **목표: 매치 종료 → 다시 시작까지 5초 이내**

### 10.5 로비 복귀
1. 호스트가 LobbyRoom 씬 LoadScene
2. NetworkPlayer는 LobbyRoom에서 Despawn (UI 슬롯만 사용)
3. Lobby는 유지 → 새 사람 조인 가능

### 10.6 재연결
- **MVP 범위 외** — 끊긴 클라는 코드 재입력으로 새로 들어와야 함
- 진행 중 매치 복귀는 미지원 (Approval에서 차단)

---

## 11. 비용 / 인프라

### UGS 무료 티어 한도
| 서비스 | 한도 | 본 프로젝트 사용량 |
|---|---|---|
| Relay | 50 동시 사용자 | 4인 매치 기준 **최대 12 동시 매치** |
| Lobby | 무료 (호출 제한만) | 충분 |
| Auth | 무료 | 충분 |

**공모전 시연 비용: 0원**. 동시 12매치까지 무료라 사실상 한도 걱정 없음.

---

## 12. 구현 순서 (체크리스트)

### Phase 1 — 기반 다지기
- [ ] UGS 콘솔에서 프로젝트 연결, Auth/Lobby/Relay 활성화
- [ ] 패키지 설치
- [ ] 씬 5개 생성 (Bootstrap → Result)
- [ ] `ServicesBootstrap` 작성 후 익명 로그인 확인

### Phase 2 — PlayerController 리팩토링
- [ ] `Rigidbody` → `Kinematic Rigidbody`로 변경
- [ ] 중력/점프 수동 구현 (Vector3 Y 적분)
- [ ] `NullInputProvider`, `BotInputProvider` 작성
- [ ] 싱글플레이로 동작 확인

### Phase 3 — 로비
- [ ] `RelayConnector` (Allocation/Join + Transport 설정)
- [ ] `LobbyManager` (Heartbeat, Polling)
- [ ] Title / LobbyRoom UI (4슬롯)
- [ ] 두 인스턴스로 LAN 테스트

### Phase 4 — 매치 진입 & 스폰
- [ ] NetworkManager 프리팹, PlayerPrefab 등록
- [ ] `ConnectionApprovalHandler` (4인 제한 + 레인 분배)
- [ ] `NetworkPlayer` 컴포넌트
- [ ] NetworkTransform(Owner Auth) 설정 + 떨림 확인
- [ ] 4명 위치 동기화 확인

### Phase 5 — 매치 로직
- [ ] `NetworkRaceManager` 상태머신
- [ ] CountdownUI
- [ ] `GoalTrigger` → ReportGoalServerRpc
- [ ] `ServerValidator` (최소 검증)
- [ ] `FinishOrder` 기반 결과 표시

### Phase 6 — 봇 & 라이프사이클
- [ ] `BotSpawner` (빈 슬롯 채움)
- [ ] `BotInputProvider` AI 주행
- [ ] DisconnectHandler
- [ ] 빠른 재매치 흐름 (5초 이내 다시 시작)

### Phase 7 — 폴리시
- [ ] 핑 시뮬레이션 (Clumsy 등으로 100~200ms 테스트)
- [ ] 시연용 닉네임, 이름표
- [ ] 백업 시연 빌드 / 영상

---

## 13. 시연 체크리스트 (D-day 전)

| 항목 | 확인 |
|---|---|
| 호스트/클라 PC 분리 테스트 | □ |
| 서로 다른 ISP / NAT 환경에서 조인 | □ |
| 4인 풀방 동작 확인 | □ |
| 1~2명만 있는 상태에서 봇 채움 동작 | □ |
| 호스트 강제 종료 시 클라 정상 처리 | □ |
| 클라이언트 중간 이탈 시 매치 계속 진행 | □ |
| 결과 화면 → 다시 시작 5초 이내 | □ |
| Relay 사용량 모니터링 | □ |
| 백업 빌드 (싱글플레이 데모) | □ |
| 시연 영상 (네트워크 문제 대비) | □ |

---

## 14. 향후 확장 (출시 결정 시)

- **인원 확장**: 4 → 8 또는 그 이상 (트래픽 영향 미미하지만 레인 수 조정 필요)
- **Dedicated Server 전환**: NetworkRaceManager 그대로, Host 경로를 Unity Game Server Hosting으로 이전
- **ServerAuthoritative 강화**: Owner → Server 권위 이전, Client Prediction + Reconciliation
- **매치메이킹**: Matchmaker 서비스로 자동 매칭 (4인 자동 모집)
- **재연결 지원**: 진행 중 매치 복귀
- **아이템/기믹 복원**: 객체별 권위 모델 별도 설계
- **안티치트 강화**: 위치 검증, 강퇴/밴
- **리플레이 / 관전**: 서버 측 입력 기록

---

## 15. 팀원에게 — 새 미니게임 만들기

> 여기서부터 §16까지가 본인 작업 시작할 때 봐야 할 부분입니다. 위쪽(§1~§14)은 설계 배경이라 참고만 하세요.

새 미니게임(자전거, 퀴즈, 미니배틀 등)을 추가하실 거면 아래 순서대로 따라오세요.

### 15.1 Branch 전략 — 어디서 분기할지

전체 구조는 이렇습니다:

```
main                        ← 안정 stable
└── multi-base              ← 멀티 인프라가 다 들어있는 base. 여기서 시작하세요.
    ├── multi/race-run      ← 러닝 게임 (이미 다른 분이 작업 중)
    ├── multi/your-game     ← 여러분 미니게임은 이렇게 분기
    └── multi/...
```

**작업 시작할 때 이렇게 하세요:**

```bash
git fetch origin
git checkout multi-base
git pull origin multi-base
git checkout -b multi/<자기-게임-이름>     # 예: multi/bike, multi/quiz
```

**작업 끝나면:**

- 본인 branch → `multi-base`로 PR 올리세요
- 리뷰 후 merge
- 그 후 다른 분들도 `git pull origin multi-base`로 본인 변경사항 받아갑니다

### 15.2 받는 것 / 본인이 설정해야 할 것

git pull 한 번이면 거의 다 끝납니다. 본인이 직접 해야 할 건 딱 3개뿐이에요.

**자동으로 받는 것 (아무것도 안 해도 됩니다):**

- ✅ 모든 씬 파일 (`.unity`) — Hierarchy + Inspector 값까지 전부
- ✅ Player Prefab + 모든 컴포넌트 설정
- ✅ NetworkManager, ConnectionApproval 등 모든 설정
- ✅ Build Settings에 등록된 씬 목록
- ✅ DefaultNetworkPrefabs 등록 정보
- ✅ Tag/Layer 정의 (Ground, Player 등)
- ✅ Project Settings 전반
- ✅ **Unity 패키지** (NGO, Multiplayer Services, TMP, URP 등) — Unity가 알아서 설치해줍니다

> Asset Serialization Mode = Force Text라서 Unity의 모든 데이터가 텍스트 YAML로 저장돼요. git이 통째로 sync해줍니다.

#### Unity 패키지 자동 설치 — 중요합니다

**Package Manager 열어서 NGO나 Lobby/Relay 같은 거 일일이 설치하실 필요 없어요.**

`Packages/manifest.json`이 git에 들어있어서 절차가 이렇게 됩니다:

1. git pull 후 Unity로 프로젝트 처음 열기
2. "Hold on... Importing assets" 진행바가 뜹니다 (1~5분 소요, 인내심 필요)
3. Unity가 manifest.json 보고 필요한 패키지를 자동 다운로드 → `Library/PackageCache/`에 캐시
4. 끝나면 Package Manager → **In Project** 탭에 모든 패키지가 들어있을 거예요

확인 방법: Window → Package Manager → 좌측 **In Project** 탭 → Authentication, Multiplayer Services, Netcode for GameObjects 등이 보이면 OK.

import가 중간에 막히면:
- `Project-Seoul/Library/` 폴더 통째로 삭제 → Unity 재실행 (캐시 재생성)
- 또는 Window → Package Manager에서 좌상단 새로고침

**본인이 한 번씩 직접 해야 하는 것 (3가지):**

| 항목 | 어디서 | 비고 |
|---|---|---|
| ❌ UGS 프로젝트 연결 | Edit → Project Settings → Services → Link to Unity Project ID | 팀 owner에게 본인 이메일을 멤버로 초대해 달라고 요청하시면 자동 연결됩니다 |
| ❌ Unity Editor 로그인 | 우상단 클라우드 아이콘 | 본인 Unity 계정 |
| ❌ Editor 환경 (Layout, Theme) | 자유 | 취향대로 |

이 3가지만 처음에 한 번 해두시면 됩니다.

### 15.2a 멀티 테스트 — Multiplayer Play Mode (MPPM)로 빌드 없이 4인 동시 테스트

**빌드해서 .exe로 클라 인스턴스 띄우는 거 안 하셔도 됩니다.** Unity 공식 패키지 MPPM이 manifest에 들어있어서, Editor 안에서 가상 플레이어를 최대 3개 더 켤 수 있어요. Main + 가상 3 = 4인 동시 테스트.

#### 어떻게 작동하나요

- 가상 플레이어 = 별도 프로세스. 같은 프로젝트 폴더, 별도 Library/PlayerPrefs.
- Main Editor에서 Play 누르면 활성화된 가상 플레이어도 같이 Play 시작.
- 코드 수정은 Main에서만 컴파일하면 자동 반영.
- 우리 §16의 "같은 PlayerId로 들어옴" 버그도 안 생김 (PlayerPrefs가 인스턴스마다 따로라 자동 분리).

#### 처음 한 번 활성화

1. Unity 메뉴: **Window → Multiplayer → Multiplayer Play Mode**
2. 패널에서 `Player 2`, `Player 3`, `Player 4` 체크박스 켜기
3. ⚠️ **첫 활성화 시 Library 복제로 5~10분 걸립니다** (인스턴스당 디스크 5~10GB씩 추가)
4. 끝나면 Main 옆에 가상 플레이어 창이 나란히 뜸

#### 매번 테스트할 때

- Main Editor에서 그냥 Play
- 가상 플레이어 창에서 Create Room / Join Room 각자 누르면 4인 매치 성립
- 디버깅 끝나면 Stop 누르면 다 같이 종료

#### 자주 묻는 거

- **Q. .exe 빌드 테스트랑 결과가 다르지 않나요?**
  - 거의 동일. 단 빌드 전용 코드(`#if !UNITY_EDITOR`)는 가상 플레이어에서도 Editor로 인식됨. 공모전 시연 직전엔 한 번 실제 빌드 테스트 권장.
- **Q. 가상 플레이어가 안 떠요.**
  - Library 복제가 안 끝났거나, Asset 변경 후 Main Editor 컴파일이 진행 중일 수 있음. 콘솔 확인.
- **Q. 가상 플레이어에서 Console 로그 보고 싶어요.**
  - MPPM 패널의 해당 Player 항목에서 `Window` 버튼 누르면 그 인스턴스 전용 창 띄울 수 있음.

> **요약**: 활성화는 처음 한 번만, 그 후엔 Play 한 번으로 4인 테스트 가능. 빌드 시간 매번 절약됨.

### 15.3 새 미니게임 씬 만들기

#### 씬은 여기에 만드세요

- 위치: `Project-Seoul/Assets/_Project/Scenes/`
- 이름 규칙: **`NN_GameName.unity`** (NN은 05~99 중 아무거나)
  - 예: `05_Bike.unity`, `06_Quiz.unity`, `07_Battle.unity`

#### 씬 안에는 이렇게 구성하세요

```
NN_YourGame
├── Main Camera             (필요하면 CameraFollow 추가)
├── Directional Light
├── Plane / Ground          (Collider 추가 + Layer를 Ground로)
├── (게임별 GameObject들)
└── NetworkYourGameManager  (Empty GameObject)
     ├── NetworkObject      ← NGO가 인식하려면 반드시 추가
     └── NetworkYourGameManager (본인이 작성한 Script)
```

> NetworkManager는 00_Bootstrap에서 만들어진 게 DontDestroyOnLoad로 자동 따라옵니다. **본인 씬에 또 만들지 마세요.**

#### 게임 매니저 코드는 이 패턴 그대로 따르세요

`NetworkRaceManager.cs`가 좋은 참고 예시입니다. 핵심은 이 패턴이에요:

```csharp
public class NetworkYourGameManager : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // ⚠️ 여기서 즉시 Player를 spawn하면 안 됩니다.
        // 클라이언트가 아직 씬 로드 중일 수 있어요.
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
    }

    private void OnSceneLoaded(string sceneName, LoadSceneMode mode,
                                List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName != gameObject.scene.name) return;

        foreach (var clientId in clientsCompleted)
        {
            SpawnPlayerForClient(clientId);
        }
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        var prefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;
        // 본인 게임 위치에 맞춰 spawn 좌표 정하시면 됩니다
        var go = Instantiate(prefab, spawnPos, Quaternion.identity);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
    }
}
```

> **`OnNetworkSpawn`에서 바로 `SpawnAsPlayerObject` 호출하면 클라이언트가 잘못된 씬에 spawn되는 버그가 납니다. 반드시 `OnLoadEventCompleted` 안에서 spawn하세요.** (관련 디버깅 사례는 §16 참고)

### 15.4 코드는 어디에 두면 되나요

| 영역 | 위치 / 네임스페이스 |
|---|---|
| 멀티 공통 (Bootstrap, Lobby) | `Assets/Scripts/Network/` / `Seoul.Network.*` — **건드리지 마세요** |
| 본인 게임 매니저 (NetworkXxxManager) | `Assets/Scripts/Network/Game/` / `Seoul.Network.Game` |
| 본인 게임 전용 로직 | `Assets/Scripts/<YourGame>/` / 자유 |
| 입력 추상화 | 기존 `IInputProvider` 활용 (필요하면 본인 게임용 Provider 추가) |

### 15.5 Lobby에서 본인 게임으로 진입하기

지금 `LobbyRoomController.OnStartClicked()`가 `"03_Race"`로 하드코딩되어 있어요. 본인 게임으로 들어가게 하려면 둘 중 하나 선택하세요.

**옵션 A: GameSelector UI를 추가 (최종 통합 시 방식)**
- 02_LobbyRoom에 게임 선택 UI 추가
- 호스트가 선택한 게임으로 `NetworkManager.Singleton.SceneManager.LoadScene(selectedScene, ...)` 호출

**옵션 B: 작업 중에는 임시로 본인 씬 직접 호출**
- `LobbyRoomController.raceSceneName`을 본인 씬으로 잠깐 바꿔서 테스트
- ⚠️ **이 변경을 commit하지 마세요.** 다른 팀원 작업이 막힙니다.
- 또는 본인 씬에서 직접 Play (개발 중에만)

**최종 통합 시점에는 옵션 A로 통일.** 모든 게임이 등록된 다음 multi-base에 PR 올리는 방식입니다.

### 15.6 Build Settings에 본인 씬 등록하기

```
File → Build Profiles → Scene List
   ↓
본인 씬 (NN_YourGame.unity) 드래그해서 추가
```

> ⚠️ Build Settings는 `ProjectSettings/EditorBuildSettings.asset`에 저장되어 git에 올라갑니다. 여러 명이 동시에 씬 추가하면 **merge conflict**가 생겨요. PR 시점에 한 명씩 추가하시는 게 안전합니다.

### 15.7 본인 NetworkObject Prefab 등록하기

본인 게임에서 동적으로 spawn할 NetworkObject 프리팹(예: 아이템, 발사체)이 있다면 등록해주세요.

```
Assets/DefaultNetworkPrefabs.asset 더블클릭
   ↓
+ 버튼 → 본인 프리팹 드래그
```

> 이 파일도 git에 올라가서 merge conflict가 날 수 있어요. PR 시점에 조율하시면 됩니다.

### 15.8 작업 체크리스트 (출력해두고 쓰시면 좋아요)

**작업 시작 전:**
- [ ] `git pull origin multi-base`로 최신 상태 받기
- [ ] `multi/<자기-게임>` branch 생성
- [ ] Unity 열고 import 끝날 때까지 기다리기 (1~5분, 처음만)
- [ ] UGS 프로젝트 연결 확인 (Project Settings → Services에서 "Linked" 표시 보이면 OK)

**작업 중:**
- [ ] 공통 코드(`Assets/Scripts/Network/Bootstrap`, `Lobby`) 건드릴 일 생기면 팀에 미리 공유
- [ ] `LobbyRoomController.raceSceneName`을 본인 씬으로 바꾼 채로 commit 금지
- [ ] 본인 게임 매니저는 반드시 `OnLoadEventCompleted` 패턴 사용 (§15.3 참고)

**PR 올리기 전:**
- [ ] `multi-base` 최신으로 rebase 또는 merge
- [ ] 멀티 테스트 (MPPM 가상 플레이어로 4인 시뮬레이션 — §15.2a 참고)
- [ ] 본인 씬이 Build Settings에 등록됐는지 확인
- [ ] 새 NetworkObject 프리팹이 DefaultNetworkPrefabs에 등록됐는지 확인

---

## 16. 막히면 여기부터 보세요

작업 중 마주칠 가능성 높은 7가지 함정 + 해결법입니다.

| 증상 | 원인 | 해결 |
|---|---|---|
| 같은 PC에서 띄운 두 인스턴스가 같은 PlayerId로 들어옴 | UGS Auth가 PlayerPrefs 토큰 공유 | `InitializationOptions.SetProfile()`로 인스턴스마다 다른 프로필 분리 (`ServicesBootstrap.cs` 참고) |
| 클라이언트 화면에 Player 안 보임 | `OnNetworkSpawn`에서 즉시 spawn 호출 → 클라가 아직 씬 로드 중 | `NetworkManager.SceneManager.OnLoadEventCompleted` 안에서 spawn |
| Console에 "LaneManager not found" 에러 | Player가 02_LobbyRoom에서 잘못 spawn됨 | `ConnectionApprovalHandler`에서 `CreatePlayerObject = false`로 두고, 게임 씬의 매니저가 spawn 책임 |
| 좌우/W/S 입력 안 먹힘 + 캐릭터 바닥 뚫고 떨어짐 | groundCheckDistance 부적절 + Ground Layer 불일치 | Plane의 Layer와 PlayerController의 GroundLayer 정확히 매칭 + Distance 0.15~0.3 사이 |
| NGO Scene Sync 충돌로 씬 이상하게 전환됨 | Unity 기본 `SceneManager.LoadScene` 사용 | 호스트만 `NetworkManager.Singleton.SceneManager.LoadScene` 사용. 클라는 자동 따라옴 |
| 클라가 LobbyRoom에서 "No active session" 뜨며 Title로 복귀 | NGO Scene Sync가 `JoinRoomByCodeAsync` await 완료 전 호출됨 | `LobbyRoomController.Start`를 Coroutine으로 만들어 CurrentSession 채워질 때까지 대기 |
| 빌드해서 돌리니 멀티 안 됨 | NetworkPrefabsList 또는 Build Settings 누락 | DefaultNetworkPrefabs.asset + EditorBuildSettings 둘 다 확인 |

> 더 상세한 디버깅 이력이 필요하시면 작업한 분에게 `multiplayer-dev-journal.txt`(개발 일지) 요청해보세요.
