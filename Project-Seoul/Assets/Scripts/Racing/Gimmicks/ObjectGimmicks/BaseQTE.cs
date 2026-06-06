/*
using UnityEngine;
using Seoul.Network.Game;

public abstract class BaseQTE : MonoBehaviour
{
    [Header("Base QTE Settings")]
    [SerializeField] protected float timeLimit = 3f;
    [SerializeField] protected int successScore = 100;
    [SerializeField] protected float speedSlowMultiplier = 0.3f; // QTE 중 감속 비율

    [Header("Base Lane Settings")]
    [SerializeField] private int minLane = 0;
    [SerializeField] private int maxLane = 2;

    protected bool _isQteActive = false;
    protected float _timer = 0f;
    protected NetworkPlayer _localNetworkPlayer;

    protected virtual void Start()
    {
        var lm = LaneManager.Instance;
        if (lm != null) lm.FitToLaneRange(transform, minLane, maxLane);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<NetworkPlayer>(out var netPlayer)) return;
        if (!netPlayer.IsLocalPlayer || netPlayer.IsFullyFinished.Value) return;

        if (_isQteActive) return;

        _localNetworkPlayer = netPlayer;
        _isQteActive = true;
        _timer = timeLimit;

        // 🌟 1. 속도 감소 적용
        ApplyPlayerSpeed(speedSlowMultiplier);

        // 🌟 2. [추가] 플레이어 인풋 잠금 (대시나 점프 씹힘 및 선점 방지)
        var controller = _localNetworkPlayer.GetComponent<PlayerController>();
        if (controller != null)
        {
            // 아무 입력도 받지 않는 빈 인풋 프로바이더로 교체하여 조작 권한을 QTE 스크립트가 온전히 가져옴
            controller.Initialize(new NullInputProvider());
        }

        OnQteStart();
    }



    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent<NetworkPlayer>(out var netPlayer)) return;
        if (netPlayer.IsLocalPlayer && _localNetworkPlayer == netPlayer)
        {
            ResetQteSession();
        }
    }

    protected abstract void OnQteStart();
    protected abstract void OnQteUpdate();

    protected virtual void Update()
    {
        if (!_isQteActive) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            HandleFailure();
            return;
        }

        OnQteUpdate();
    }

    public enum QteActionType
    {
        WallJump,
        SubwayGetOn,
        SubwayHandle,
        BikeTrick,
        CardTag,
        Elevator
    }

    protected abstract QteActionType GetActionType();

    protected void HandleSuccess()
    {
        if (_localNetworkPlayer != null)
        {
            // 🌟 구버전 메서드명(ServerRpc Suffix)으로 서버에 요청
            _localNetworkPlayer.RequestQTEResultServerRpc(true, successScore, GetActionType());
        }
        OnLocalSuccessVisual();
        ResetQteSession();
    }

    protected void HandleFailure()
    {
        if (_localNetworkPlayer != null)
        {
            // 🌟 구버전 메서드명(ServerRpc Suffix)으로 서버에 요청
            _localNetworkPlayer.RequestQTEResultServerRpc(false, 0, GetActionType());
        }
        ResetQteSession();
    }

    protected virtual void ResetQteSession()
    {
        // 🌟 3. [추가] 플레이어 인풋 원상 복구 (다시 조작 가능하게 변경)
        if (_localNetworkPlayer != null)
        {
            var controller = _localNetworkPlayer.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.Initialize(new PlayerInputProvider());
            }
        }

        ApplyPlayerSpeed(1.0f); // 원래 속도 복구
        _isQteActive = false;
        _localNetworkPlayer = null;
    }
    protected abstract void OnLocalSuccessVisual();

    // BaseQTE.cs 내부의 기존 비어있던 메서드를 수정합니다.

    private const string QteSlowKey = "QTE_Slow"; // 딕셔너리 고유 키값 정의

    /// <summary>
    /// 플레이어 컨트롤러의 멀티 슬롯 속도 시스템을 호출하여 속도를 조절합니다.
    /// </summary>
    private void ApplyPlayerSpeed(float multiplier)
    {
        if (_localNetworkPlayer != null)
        {
            // NetworkPlayer 컴포넌트가 붙은 오브젝트에서 PlayerController를 가져옵니다.
            var controller = _localNetworkPlayer.GetComponent<PlayerController>();
            if (controller != null)
            {
                if (multiplier < 1.0f)
                {
                    // 🌟 1보다 작으면 속도 감속 슬롯 추가!
                    controller.SetSpeedMultiplier(QteSlowKey, multiplier);
                    Debug.Log($"[QTE] 플레이어 속도 감소 적용: x{multiplier}");
                }
                else
                {
                    // 🌟 1.0f(원래대로) 요청이 오면 해당 슬롯을 깔끔하게 제거!
                    controller.ClearSpeedMultiplier(QteSlowKey);
                    Debug.Log("[QTE] 플레이어 속도 제한 해제 (원래 속도 복구)");
                }
            }
        }
    }
}
*/