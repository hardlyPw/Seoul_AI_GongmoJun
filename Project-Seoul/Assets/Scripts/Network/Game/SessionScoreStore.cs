using System.Collections.Generic;
using UnityEngine;

namespace Seoul.Network.Game
{
    public class SessionScoreStore : MonoBehaviour
    {
        public static SessionScoreStore Instance { get; private set; }

        private readonly Dictionary<ulong, int> _scores = new();
        private readonly Dictionary<string, HashSet<string>> _consumedItems = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int GetScore(ulong clientId)
            => _scores.TryGetValue(clientId, out var s) ? s : 0;

        public void SetScore(ulong clientId, int value)
            => _scores[clientId] = value;

        public void AddScore(ulong clientId, int amount)
        {
            _scores.TryGetValue(clientId, out var current);
            _scores[clientId] = current + amount;
        }

        public void ResetAll()
        {
            _scores.Clear();
            _consumedItems.Clear();
        }

        public IReadOnlyDictionary<ulong, int> All => _scores;

        // ── 로컬 로드 스테이지(2/3) 아이템 소비 추적 (서버 권위) ──

        public bool MarkItemConsumed(string sceneName, string itemId)
        {
            if (!_consumedItems.TryGetValue(sceneName, out var set))
            {
                set = new HashSet<string>();
                _consumedItems[sceneName] = set;
            }
            return set.Add(itemId);
        }

        public IEnumerable<string> GetConsumedItems(string sceneName)
        {
            return _consumedItems.TryGetValue(sceneName, out var set)
                ? (IEnumerable<string>)set
                : System.Array.Empty<string>();
        }
    }
}
