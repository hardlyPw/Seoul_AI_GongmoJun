using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 골목길 담장 QTE. IsTrigger BoxCollider 필요. minLane~maxLane 범위를 자동으로 커버.
// 구역 진입 시 QTE 시작. 제한 시간 내 키 시퀀스 입력 성공 → 담장 통과 + 스코어
// 실패(시간 초과) → 넘어짐
public class WallQTE : MonoBehaviour
{
    [SerializeField] private Key[]      qteSequence  = { Key.Q, Key.W, Key.E };
    [SerializeField] private float      timeLimit    = 3f;
    [SerializeField] private int        successScore = 100;
    [SerializeField] private GameObject wall;
    [SerializeField] private int        minLane      = 0;
    [SerializeField] private int        maxLane      = 2;

    private void Start()
    {
        var lm  = LaneManager.Instance;
        var pos = transform.position;
        pos.z              = lm.GetLaneCenterZ(minLane, maxLane);
        transform.position = pos;

        if (TryGetComponent<BoxCollider>(out var col))
        {
            var size = col.size;
            size.z   = lm.GetLaneSpanZ(minLane, maxLane);
            col.size = size;
        }

        if (wall != null)
        {
            var wallPos = wall.transform.position;
            wallPos.z              = lm.GetLaneCenterZ(minLane, maxLane);
            wall.transform.position = wallPos;

            if (wall.TryGetComponent<BoxCollider>(out var wallCol))
            {
                var size = wallCol.size;
                size.z      = lm.GetLaneSpanZ(minLane, maxLane);
                wallCol.size = size;
            }
        }
    }

    private class QTESession { public int step; public float timer; }
    private readonly Dictionary<PlayerController, QTESession> _sessions = new();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<PlayerController>(out var player)) return;
        if (_sessions.ContainsKey(player)) return;
        _sessions[player] = new QTESession { step = 0, timer = timeLimit };
        Debug.Log($"[QTE] 시작! 입력 시퀀스: {string.Join(" → ", qteSequence)} ({timeLimit}초)");
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
            _sessions.Remove(player);
    }

    private void Update()
    {
        var kb       = Keyboard.current;
        if (kb == null) return;

        var toRemove = new List<PlayerController>();

        foreach (var (player, session) in _sessions)
        {
            session.timer -= Time.deltaTime;
            if (session.timer <= 0f)
            {
                player.TriggerFall();
                Debug.Log("[QTE] 시간 초과 - 넘어짐");
                toRemove.Add(player);
                continue;
            }

            if (kb[qteSequence[session.step]].wasPressedThisFrame)
            {
                session.step++;
                if (session.step >= qteSequence.Length)
                {
                    OnSuccess(player);
                    toRemove.Add(player);
                }
            }
        }

        foreach (var p in toRemove) _sessions.Remove(p);
    }

    private void OnSuccess(PlayerController player)
    {
        ScoreManager.Instance?.AddScore(player, successScore);
        Debug.Log($"[QTE] 성공! (+{successScore}점)");
        if (wall != null) wall.SetActive(false);
    }
}
