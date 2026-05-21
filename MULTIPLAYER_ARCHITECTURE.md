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

## 15. 팀 협업 가이드 (새 미니게임 추가)

새 미니게임(러닝 외 자전거, 퀴즈, 미니배틀 등)을 추가하려는 팀원용 가이드.

### 15.1 Branch 전략

```
main                        ← 안정 stable
└── multi-base              ← 멀티 인프라 integration (모든 미니게임의 공통 base)
    ├── multi/race-run      ← 러닝 게임 (이미 작업 중)
    ├── multi/your-game     ← 새 미니게임은 여기서 분기
    └── multi/...
```

#### 시작 절차

```bash
git fetch origin
git checkout multi-base
git pull origin multi-base
git checkout -b multi/your-game     # 자기 미니게임 이름으로
```

#### 완료 후

- `multi/your-game` → `multi-base`로 PR
- 리뷰 후 merge
- 다른 팀원은 `git pull origin multi-base`로 동기화

### 15.2 git에서 받는 것 / 받지 못하는 것

**git pull로 자동으로 받는 것 (셋업 불필요):**
- ✅ 모든 씬 파일 (`.unity`) - Hierarchy + Inspector 값 전부
- ✅ Player Prefab + 모든 컴포넌트 설정
- ✅ NetworkManager 설정, ConnectionApproval 등
- ✅ Build Settings에 등록된 씬 목록
- ✅ DefaultNetworkPrefabs 등록 정보
- ✅ Tag/Layer 정의 (Ground, Player 등)
- ✅ Project Settings 전반
- ✅ **Unity 패키지** (NGO, Multiplayer Services, TMP, URP 등) - 자동 설치

> Asset Serialization Mode = Force Text 모드라 모든 Unity 데이터가 텍스트로 저장됨.

#### Unity 패키지 자동 설치 동작

`Project-Seoul/Packages/manifest.json` + `packages-lock.json`이 git에 포함되어 있어서:
1. git pull 후 Unity로 프로젝트 처음 열면 "Hold on... Importing assets" 진행바 뜸
2. Unity가 manifest.json 읽고 필요한 패키지를 자동 다운로드 (Library/PackageCache/에 캐시)
3. 첫 import는 1~5분 소요 (이후는 즉시)

**Package Manager 들어가서 NGO, Lobby, Relay 등을 일일이 설치할 필요 X.** 이미 manifest에 적혀있음.

만약 패키지 import가 막히면:
- `Project-Seoul/Library/` 폴더 삭제 후 Unity 재실행 → 캐시 재생성
- Window → Package Manager에서 강제 새로고침

**받지 못하는 것 (개인이 설정 필요):**
- ❌ UGS 프로젝트 연결 (Editor → Project Settings → Services → Link to Unity Project ID)
- ❌ Unity Editor 로그인 (우상단 클라우드 아이콘)
- ❌ Editor Layout, Theme 등 개인 환경 설정

> UGS 프로젝트 연결: 팀에서 만든 UGS 프로젝트의 owner가 본인 이메일을 멤버로 초대해주면 자동 연결됨. 또는 자기만의 UGS 프로젝트로 새로 연결.

### 15.3 새 미니게임 씬 셋업

#### 씬 생성

위치: `Project-Seoul/Assets/_Project/Scenes/`
이름 컨벤션: **`NN_GameName.unity`** (NN: 05~99 사용)
- 예: `05_Bike.unity`, `06_Quiz.unity`, `07_Battle.unity`

#### 필수 Hierarchy

```
NN_YourGame
├── Main Camera               (필요시 CameraFollow 추가)
├── Directional Light
├── Plane / Ground            (콜라이더 + Layer=Ground)
├── (게임별 GameObject들)
└── NetworkYourGameManager    (Empty GameObject)
     ├── NetworkObject        ← NGO 인식용
     └── NetworkYourGameManager (Script)
```

> NetworkManager는 00_Bootstrap에서 DontDestroyOnLoad라 자동으로 따라옵니다. 새 씬에 추가할 필요 X.

#### 게임 매니저 패턴

`NetworkRaceManager`를 참고하세요. 핵심 패턴:

```csharp
public class NetworkYourGameManager : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // 중요: 즉시 Player spawn 금지. 모든 클라 로드 완료까지 대기.
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
        // ...자기 게임 위치에 맞춰 spawn
        var go = Instantiate(prefab, spawnPos, Quaternion.identity);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
    }
}
```

> **OnNetworkSpawn에서 즉시 SpawnAsPlayerObject 호출하면 클라이언트가 잘못된 씬에 spawn받음.** 반드시 `OnLoadEventCompleted` 사용.

### 15.4 코드 컨벤션

| 영역 | 위치 / 네임스페이스 |
|---|---|
| 멀티 공통 (Bootstrap, Lobby) | `Assets/Scripts/Network/` / `Seoul.Network.*` |
| 게임 매니저 (NetworkXxxManager) | `Assets/Scripts/Network/Game/` / `Seoul.Network.Game` |
| 게임별 로직 (Player 외) | `Assets/Scripts/<YourGame>/` / 자유 |
| 입력 추상화 | 기존 `IInputProvider` 활용 (필요시 확장) |

### 15.5 Lobby에서 자기 게임으로 진입

현재 `LobbyRoomController.OnStartClicked()`가 `"03_Race"`로 하드코딩되어 있어요.
다중 미니게임 지원하려면 **둘 중 선택**:

**옵션 A: GameSelector UI 추가**
- 02_LobbyRoom에 게임 선택 드롭다운/버튼 추가
- 호스트가 선택한 게임으로 `NetworkManager.Singleton.SceneManager.LoadScene(selectedScene, ...)`

**옵션 B: 임시로 자기 씬을 직접 호출**
- 작업 중에는 `LobbyRoomController.raceSceneName`을 자기 씬으로 변경하고 commit하지 말 것
- 또는 디버그 빌드로 자기 씬에서 직접 Play

**최종 통합 시점**: A 옵션을 multi-base에 적용하고 모든 게임이 거기 등록.

### 15.6 Build Settings 등록

```
File → Build Profiles → Scene List
   ↓
드래그로 자기 씬 추가 (NN_YourGame.unity)
```

> Build Settings는 `ProjectSettings/EditorBuildSettings.asset`에 저장되어 git에 올라감.
> 여러 명이 동시에 씬 추가하면 merge conflict 발생 가능. PR 시점에 한 명씩 추가 권장.

### 15.7 DefaultNetworkPrefabs 등록

자기 게임에서 동적 spawn할 NetworkObject 프리팹이 있다면:

```
Assets/DefaultNetworkPrefabs.asset 더블클릭
   ↓
+ 버튼 → 프리팹 드래그
```

> 이 파일도 git에 올라감. Merge conflict 주의.

### 15.8 협업 체크리스트

작업 시작 전:
- [ ] `git pull origin multi-base` 최신 상태 확인
- [ ] `multi/your-game` branch 생성
- [ ] Unity Editor에서 UGS 프로젝트 연결 확인 (`Project Settings → Services`)

작업 중:
- [ ] 공통 코드(`Assets/Scripts/Network/Bootstrap`, `Lobby`) 수정 시 팀원에게 알림
- [ ] `LobbyRoomController.raceSceneName` 수정한 채 commit 금지
- [ ] 자기 씬에 게임 매니저 패턴 따랐는지 확인 (`OnLoadEventCompleted` 사용)

PR 전:
- [ ] `multi-base`를 최신으로 rebase 또는 merge
- [ ] 멀티 테스트 (호스트 + 클라이언트 2개 인스턴스)
- [ ] 자기 씬이 Build Settings에 등록됐는지
- [ ] DefaultNetworkPrefabs에 새 프리팹 등록됐는지

---

## 16. 자주 빠지는 함정

Phase 1+2 작업 중 마주친 실제 문제들. 참고하세요.

| 증상 | 원인 | 해결 |
|---|---|---|
| 같은 PC 두 인스턴스가 같은 PlayerId | UGS Auth가 PlayerPrefs 토큰 공유 | `InitializationOptions.SetProfile()`로 인스턴스마다 다른 프로필 |
| 클라이언트에 Player 안 보임 | `OnNetworkSpawn`에서 즉시 spawn → 클라 로드 중 | `SceneManager.OnLoadEventCompleted` 사용 |
| LaneManager not found | 02_LobbyRoom에서 Player가 spawn됨 | `CreatePlayerObject = false` + 게임 씬에서 매니저가 spawn |
| 좌우/W/S 입력 안 됨 + 캐릭터 떨어짐 | groundCheckDistance 부적절 + Ground Layer 불일치 | Layer 정확히 매칭 + Distance 0.15~0.3 |
| NGO Scene Sync 충돌 | `SceneManager.LoadScene`(비네트워크) 사용 | `NetworkManager.Singleton.SceneManager.LoadScene` 사용 (호스트만) |
| 클라가 02_LobbyRoom에서 "No active session" | NGO Scene Sync가 await 완료 전 호출 | LobbyRoomController.Start를 Coroutine으로 변경, 대기 |
| 빌드 후 멀티 안 됨 | NetworkPrefabsList 또는 Build Settings 누락 | DefaultNetworkPrefabs.asset + EditorBuildSettings 확인 |
