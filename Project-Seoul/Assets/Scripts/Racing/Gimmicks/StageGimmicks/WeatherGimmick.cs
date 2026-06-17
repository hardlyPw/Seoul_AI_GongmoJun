using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    // 달리기 스테이지(Stage 1) 씬 루트에 NetworkObject + 이 컴포넌트를 가진 GameObject로 배치.
    // 서버가 스테이지 진입 시 랜덤 날씨를 결정하고 NetworkVariable로 동기화.
    // - Clear:   무효과
    // - Rain:    WeatherModifiers (넘어짐/회복) — PlayerController 적용은 후속 PR
    // - Typhoon: 씬의 ItemBox lane을 서버에서 셔플 → NetworkList로 전파
    // - Dust:    WeatherModifiers (스태미너 2배) — 후속 PR
    public class WeatherGimmick : NetworkBehaviour
    {
        public static WeatherGimmick Instance { get; private set; }

        public NetworkVariable<WeatherType> Current = new(
            WeatherType.Clear,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 태풍 시 셔플된 ItemBox lane 인덱스 목록. 서버에서 채우고 클라가 적용.
        // 인덱스 매칭은 FindObjectsOfType<ItemBox>를 (x, z) 좌표로 정렬한 순서를 기준.
        private NetworkList<int> _itemBoxLanes;

        private void Awake()
        {
            Instance = this;
            _itemBoxLanes = new NetworkList<int>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            Current.OnValueChanged += OnWeatherChanged;
            _itemBoxLanes.OnListChanged += OnItemBoxLanesChanged;

            // 늦게 접속한 클라이언트도 현재 값으로 효과 적용
            WeatherModifiers.Apply(Current.Value);
            if (Current.Value == WeatherType.Typhoon) ApplyItemBoxLanes();

            if (!IsServer) return;

            var picked = (WeatherType)Random.Range(0, 4);
            Debug.Log($"[WeatherGimmick] Server picked weather: {picked}");
            Current.Value = picked;
        }

        public override void OnNetworkDespawn()
        {
            Current.OnValueChanged -= OnWeatherChanged;
            if (_itemBoxLanes != null) _itemBoxLanes.OnListChanged -= OnItemBoxLanesChanged;
        }

        private void OnWeatherChanged(WeatherType prev, WeatherType next)
        {
            WeatherModifiers.Apply(next);
            Debug.Log($"[WeatherGimmick] Weather: {prev} -> {next} ({WeatherModifiers.KoreanName(next)})");

            if (IsServer && next == WeatherType.Typhoon)
                ShuffleItemBoxesServer();
        }

        private void ShuffleItemBoxesServer()
        {
            int laneCount = LaneManager.Instance != null ? LaneManager.Instance.LaneCount : 6;
            var boxes = GetOrderedBoxes();

            _itemBoxLanes.Clear();
            for (int i = 0; i < boxes.Length; i++)
                _itemBoxLanes.Add(Random.Range(0, laneCount));

            Debug.Log($"[WeatherGimmick] Typhoon shuffled {boxes.Length} item boxes");
        }

        private void OnItemBoxLanesChanged(NetworkListEvent<int> _) => ApplyItemBoxLanes();

        private void ApplyItemBoxLanes()
        {
            if (_itemBoxLanes.Count == 0) return;
            if (LaneManager.Instance == null) return;

            var boxes = GetOrderedBoxes();
            int n = Mathf.Min(boxes.Length, _itemBoxLanes.Count);
            /*for (int i = 0; i < n; i++)
                boxes[i].SetLaneIndex(_itemBoxLanes[i]);*/
        }

        // 서버/클라가 동일한 순서로 ItemBox를 매칭하기 위한 결정적 정렬 (x, z 좌표 기준).
        private static ItemBox[] GetOrderedBoxes()
        {
            return FindObjectsOfType<ItemBox>(includeInactive: true)
                .OrderBy(b => b.transform.position.x)
                .ThenBy(b => b.transform.position.z)
                .ToArray();
        }
    }
}
