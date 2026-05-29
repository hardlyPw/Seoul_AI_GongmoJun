using System.Collections.Generic;

namespace Seoul.Network.Game
{
    // 클라이언트 사이드 아이템 소비 캐시 + 이벤트.
    // 다음 경로로 RaiseItemConsumed가 호출됨:
    //  (1) 내가 직접 먹은 직후 (로컬 즉시 반영)
    //  (2) 다른 플레이어가 먹은 걸 서버가 broadcast 했을 때 (BroadcastItemConsumedClientRpc)
    //  (3) 스테이지 입장 시 서버에서 기존 소비 목록을 받았을 때 (ReceiveConsumedItemListClientRpc)
    public static class StageItemSync
    {
        public static event System.Action<string, string> OnItemConsumed; // (sceneName, itemId)

        private static readonly Dictionary<string, HashSet<string>> _cache = new();

        public static void RaiseItemConsumed(string sceneName, string itemId)
        {
            if (!_cache.TryGetValue(sceneName, out var set))
            {
                set = new HashSet<string>();
                _cache[sceneName] = set;
            }
            set.Add(itemId);
            OnItemConsumed?.Invoke(sceneName, itemId);
        }

        public static bool IsConsumed(string sceneName, string itemId)
        {
            return _cache.TryGetValue(sceneName, out var set) && set.Contains(itemId);
        }

        public static void ClearScene(string sceneName)
        {
            _cache.Remove(sceneName);
        }

        public static void ClearAll()
        {
            _cache.Clear();
        }
    }
}
