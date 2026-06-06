using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerAirborneState : IPlayerState
{
    private float _airTimer;
    private int _qteSuccessCount;
    private readonly Key[] _qteKeys = { Key.W, Key.A, Key.S, Key.D };
    private Key _currentRequiredKey;

    public void EnterState(PlayerController player)
    {
        _airTimer = 3f; // 기획 규격인 총 체공 시간 3초 고정
        _qteSuccessCount = 0;
        GenerateNextQTEKey();

        // 초기 도약 시점에 강력한 Y축 상승 속도를 인젝션하여 고고도 점프를 강제 발동시킵니다.
        player.SetVelocityY(15f);
    }

    public void UpdateState(PlayerController player)
    {
        if (SceneManager.GetActiveScene().name != "05_Stage_Bicycle")
        {
            player.ChangeState(player.IdleState);
            return;
        }

        _airTimer -= Time.deltaTime;
        if (_airTimer <= 0f)
        {
            Debug.Log($"[QTE] 제한 시간 초과로 묘기 실패!");
            player.ChangeState(player.IdleState);
            return;
        }

        if (_qteSuccessCount < 5)
        {
            HandleQTEInputCheck(player);
        }
    }

    public void FixedUpdateState(PlayerController player)
    {
        // 상태 진입 후 경과된 시간 산출 (0초 ~ 3초)
        float elapsedTime = 3f - _airTimer;

        // [물리 핵심 보정]: 3초 수명 주기 내부에서 프레임 단위로 Y축 속도를 제어하여 실제 도약 및 체공 구현
        if (elapsedTime < 0.8f)
        {
            // 1단계(초기 0.8초): 강한 상승 속도를 중력 가속도와 유사하게 서서히 줄여나가며 포물선 상승 구현
            float ascentVelocity = Mathf.Lerp(15f, 0f, elapsedTime / 0.8f);
            player.SetVelocityY(ascentVelocity);
        }
        else if (elapsedTime >= 0.8f && elapsedTime < 2.4f)
        {
            // 2단계(중반 1.6초): 최고 고도 정점 부근. 하강 속도를 -0.3f 수준으로 극도로 억제하여 
            // 허공에 부드럽게 떠서 글라이딩(체공)하며 QTE 입력에 집중할 수 있는 환경을 만듭니다.
            player.SetVelocityY(-0.3f);
        }
        else
        {
            // 3단계(마지막 0.6초): 착지 시퀀스. 바닥으로 속도를 가속 증가시켜 지면 착딩을 유도합니다.
            float descentVelocity = Mathf.Lerp(-0.3f, -14f, (elapsedTime - 2.4f) / 0.6f);
            player.SetVelocityY(descentVelocity);
        }

        // 전진 속도는 WalkSpeed 기준선으로 자동 유지 보장
        player.CalculateForwardVelocity(player.WalkSpeed);
    }

    public void OnCollisionCheck(PlayerController player, Collider other) { }

    public void ExitState(PlayerController player)
    {
        // 착지 완료 시 충돌 반동 레이어를 소거하기 위해 Y축 속도를 제로 아웃합니다.
        player.SetVelocityY(0f);
        Debug.Log($"[QTE] 체공 상태 종료 - 착지 완료");
    }

    private void GenerateNextQTEKey()
    {
        int rand = Random.Range(0, _qteKeys.Length);
        _currentRequiredKey = _qteKeys[rand];
        Debug.Log($"[QTE UI] 다음 입력 키 -> {_currentRequiredKey.ToString()}");
    }

    private void HandleQTEInputCheck(PlayerController player)
    {
        foreach (Key key in _qteKeys)
        {
            if (player.Input.GetQTEKeyDown(key))
            {
                if (key == _currentRequiredKey)
                {
                    _qteSuccessCount++;
                    Debug.Log($"[QTE] 정확한 입력! 현재 성공 횟수: {_qteSuccessCount}/5");

                    if (_qteSuccessCount >= 5)
                    {
                        TriggerQTESuccessReward(player);
                    }
                    else
                    {
                        GenerateNextQTEKey();
                    }
                }
                else
                {
                    Debug.Log($"[QTE] 잘못된 입력! 성공 횟수 초기화");
                    _qteSuccessCount = 0;
                }
                break;
            }
        }
    }

    private void TriggerQTESuccessReward(PlayerController player)
    {
        Debug.Log($"[QTE] 5회 연속 커맨드 입력 성공! 묘기 추가 스코어 +30pt 획득 요청 전송");
        if (player.TryGetComponent<NetworkItemInventory>(out var inventory))
        {
            inventory.RequestAddScoreServerRpc(30);
        }
    }
}