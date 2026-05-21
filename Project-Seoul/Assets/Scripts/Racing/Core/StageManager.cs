using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform goalPoint;

    public UnityEvent OnStageStart;
    public UnityEvent OnAllPlayersFinished;

    private readonly List<PlayerController> _players         = new();
    private readonly List<PlayerController> _finishedPlayers = new();
    private bool _stageActive;

    public bool      StageActive    => _stageActive;
    public Transform GoalPoint      => goalPoint;
    public Transform StartPoint     => startPoint;
    public int       FinishedCount  => _finishedPlayers.Count;
    public int       PlayerCount    => _players.Count;

    public System.Collections.Generic.IReadOnlyList<PlayerController> FinishedPlayers => _finishedPlayers;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RegisterPlayer(PlayerController player)
    {
        if (!_players.Contains(player)) _players.Add(player);
    }

    public void StartStage()
    {
        _stageActive = true;
        _finishedPlayers.Clear();
        OnStageStart?.Invoke();
        Debug.Log("스테이지 시작!");
    }

    public void PlayerReachedGoal(PlayerController player)
    {
        if (_finishedPlayers.Contains(player) || !_stageActive) return;
        _finishedPlayers.Add(player);
        Debug.Log($"{player.name} 결승 도착! ({_finishedPlayers.Count}/{_players.Count})");

        if (_finishedPlayers.Count >= _players.Count)
        {
            _stageActive = false;
            OnAllPlayersFinished?.Invoke();
            Debug.Log("전원 도착 - 스테이지 종료");
        }
    }

    public int GetFinishRank(PlayerController player) =>
        _finishedPlayers.IndexOf(player) + 1;
}
