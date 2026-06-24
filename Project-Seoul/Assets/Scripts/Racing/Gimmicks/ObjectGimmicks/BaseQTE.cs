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
        if (netPlayer.IsFullyFinished.Value) return;
        if (_isQteActive) return;

        // 🌟 [핵심 수정] 오직 이 캐릭터의 소유자(Owner) 화면에서만 이 QTE 컴포넌트가 활성화되도록 합니다.
        // 다른 사람 화면에서 내가 트리거에 부딪힌 것은 무시합니다.
        if (!netPlayer.IsOwner) return; 

        _localNetworkPlayer = netPlayer;
        _isQteActive = true;
        _timer = timeLimit;

        ApplyPlayerSpeed(speedSlowMultiplier);

        var controller = _localNetworkPlayer.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.Initialize(new QTEInputProvider());
        }

        OnQteStart();
    }

    protected virtual void Update()
    {
        // 이제 OnTriggerEnter에서 IsOwner인 경우만 활성화했으므로 안전합니다.
        if (!_isQteActive || _localNetworkPlayer == null) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            HandleFailure();
            return;
        }

        OnQteUpdate();
    }



    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent<NetworkPlayer>(out var netPlayer)) return;
        if (netPlayer.IsOwner && _localNetworkPlayer == netPlayer)
        {
            ResetQteSession();
        }
    }

    protected abstract void OnQteStart();
    protected abstract void OnQteUpdate();

    public enum QteActionType
    {
        WallJump,
        SubwayGetOn,
        SubwayHandle,
        BikeTrick,
        CardTag,

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
                // QTE 종료 후 원래 입력 방식으로 복구
                controller.Initialize(new PlayerInputProvider());
            }
        }

        ApplyPlayerSpeed(1.0f); // 원래 속도 복구
        _isQteActive = false;
        _localNetworkPlayer = null;
    }

    /// <summary>
    /// QTE 활성화 상태 확인 (QTE 중 다른 입력 차단용)
    /// </summary>
    public bool IsQTEActive => _isQteActive;
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