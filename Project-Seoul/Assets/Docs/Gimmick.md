# [개발 명세] 멀티플레이어 환경 기믹 구현 가이드라인 (v1.0)

본 문서는 스테이지 전체에 시간이나 시스템 규칙으로 영향을 주는 'A 타입: 시스템/환경 기믹'을 신규 개발할 때 따르는 아키텍처 패턴과 네트워킹 규칙을 정의합니다.

## 1. 핵심 아키텍처 패턴

새로운 환경 기믹을 만들 때는 전략 패턴(Strategy Pattern)과 서버 권위 모델(Server-Authoritative)을 결합하여 구현합니다.

개방-폐쇄 원칙 (OCP): 새로운 기믹이 추가되거나 삭제되어도 NetworkRaceManager.cs 코드를 최대한 덜 수정하도록 합니다.

서버 권위 동기화: 오직 호스트(서버)만 시간을 계산하고 데이터를 결정하여 모든 클라이언트에게 ClientRpc로 패킷을 브로드캐스팅합니다.

## 2. 신규 기믹 개발 프로세스 (체크리스트)

새로운 기믹을 설계할 때 담당 개발자는 아래 4단계 규칙을 순서대로 구현해야 합니다.

**1단계: 기믹 식별자(Enum) 등록**
\Racing\Gimmicks 파일 내에 있는 GimmickType enum에 type을 추가합니다.

```C#
public enum GimmickType : byte
{
    Subwayquake,
    MeteorStrike,  // 신규 기믹 추가 예시
}
```

**2단계: IStageGimmick 인터페이스 상속 및 구현**
MonoBehaviour를 상속받지 않는 순수 C# 클래스로 파일을 생성하고, IStageGimmick 인터페이스의 라이프사이클을 구현합니다.

생성 위치: Assets/Scripts/Gimmicks/StageGimmicks/

네임스페이스: Seoul.Network.Game

```C#
using UnityEngine;

namespace Seoul.Network.Game
{
    public class MeteorStrikeGimmick : IStageGimmick
    {
        private float _timer;
        private float _interval;
        private NetworkRaceManager _raceManager;

        public MeteorStrikeGimmick(NetworkRaceManager raceManager, float interval = 4f)
        {
            _raceManager = raceManager;
            _interval = interval;
        }

        public void OnStageStart() { _timer = _interval; }

        public void OnStageUpdate()
        {
            // ⚠️ 호스트(서버)의 Update 단에서만 실행되는 루프입니다.
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                ExecuteGimmick();
                _timer = _interval;
            }
        }

        private void ExecuteGimmick()
        {
            // 1. 서버 권위 연산: 서버가 무작위 수치나 대상을 결정합니다.
            int targetLane = Random.Range(0, 6);

            // 2. 범용 RPC 송신: 공용 채널을 통해 모든 클라이언트에 결정된 값을 전송합니다.
            _raceManager.SendGimmickEventClientRpc(GimmickType.MeteorStrike, targetLane, 0f);
        }

        public void OnStageEnd() { /* 데이터 정리 필요 시 작성 */ }
    }
}
```

**3단계: NetworkRaceManager 범용 수신부(Switch) 등록**

클라이언트가 이벤트를 안전하게 수신할 수 있도록 NetworkRaceManager.SendGimmickEventClientRpc 내부의 switch문에 연출용 이벤트를 연결합니다.

```C#
[ClientRpc]
public void SendGimmickEventClientRpc(GimmickType type, int intParam, float floatParam)
{
    switch (type)
    {
        case GimmickType.Subwayquake:
            StageEventManager.TriggerCameraShake(0.6f, floatParam);
            StageEventManager.TriggerForceLaneChange(intParam);
            break;

        case GimmickType.MeteorStrike: // 👈 3단계: 여기에 내 기믹 수신 연출 연결!
            // 예시: 특정 레인 경고 UI를 켜거나 이펙트 매니저를 호출
            // StageEventManager.TriggerMeteorWarning(intParam);
            break;
    }
}
```

**4단계: 씬 로드 시 기믹 동적 주입 (Injection)**
NetworkRaceManager.OnLoadEventCompleted 내부에서 특정 씬 이름이 감지되었을 때 해당 기믹 클래스가 인스턴스화되도록 조립합니다.

```C#
private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
{
    // ... 플레이어 스폰 로직 생략 ...

    if (sceneName == "04_MeteorStage") // 👈 4단계: 내 씬 이름 매칭
    {
        SetActiveGimmick(new MeteorStrikeGimmick(this, 4f)); // 내 기믹 주입
    }
}
```

## 3. 네트워크 패킷 규칙 및 제약 사항

**범용 인자 규칙 (intParam, floatParam):**

기믹마다 필요한 데이터의 성격이 다릅니다. 가령 Subwayquake에서 intParam은 이동 방향(-1, 1)을 뜻하지만, MeteorStrike에서는 타격할 레인 인덱스(0~5)로 해석될 수 있습니다. 본인 기믹에 맞게 주석을 명시하고 변수를 자유롭게 가공해 쓰세요.

**난수 생성 제한 (Random):**

모든 Random.Range() 연산은 반드시 2단계의 OnStageUpdate()(서버 세션) 내에서만 실행되어야 합니다. 클라이언트 수신부(ClientRpc) 내부에서 난수를 발생시키면 각 플레이어의 화면 데이터가 일치하지 않는 Desync(동기화 오류)가 발생합니다.

## 4. 연출 및 조작 결합도 분리 규칙 (StageEventManager)

기믹 클래스가 플레이어 컴포넌트나 카메라 컴포넌트를 직접 참조(GetComponent 등)하여 값을 수정하는 행위를 절대 금지합니다. 모든 시각 연출 및 물리 조작은 StageEventManager.cs 우체통을 거쳐 발행-구독(Pub-Sub) 형태로 간접 통신해야 합니다.
