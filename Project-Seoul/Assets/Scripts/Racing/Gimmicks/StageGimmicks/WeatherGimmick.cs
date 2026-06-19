using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    // 씬 루트에 NetworkObject + 이 컴포넌트를 가진 GameObject로 배치하면 동작.
    // 서버가 OnNetworkSpawn 시점에 랜덤 날씨를 결정하고 NetworkVariable로 클라에 전파.
    // - Clear:   무효과
    // - Rain:    넘어짐 지속/회복 시간 2배 (PlayerStunState, PlayerController)
    // - Typhoon: 씬의 ItemBox lane을 서버에서 셔플 → NetworkList로 전파
    // - Dust:    스프린트 스태미너 소모 2배 (PlayerRunState)
    //
    // 스테이지 비종속: 씬에 컴포넌트만 배치하면 동작.
    // allowedWeathers로 스테이지별 후보 풀 조정 가능. useForcedWeather는 테스트용.
    public class WeatherGimmick : NetworkBehaviour
    {
        public static WeatherGimmick Instance { get; private set; }

        [Tooltip("후보 날씨 목록. 비어있으면 전체(Clear/Rain/Typhoon/Dust) 중 랜덤.")]
        [SerializeField] private WeatherType[] allowedWeathers;

        [Tooltip("디버그/테스트용. 켜면 forcedWeather로 고정.")]
        [SerializeField] private bool useForcedWeather = false;
        [SerializeField] private WeatherType forcedWeather = WeatherType.Clear;

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

            var picked = PickWeather();
            Debug.Log($"[WeatherGimmick] Server picked weather: {picked}");
            Current.Value = picked;
        }

        public override void OnNetworkDespawn()
        {
            Current.OnValueChanged -= OnWeatherChanged;
            if (_itemBoxLanes != null) _itemBoxLanes.OnListChanged -= OnItemBoxLanesChanged;
        }

        private WeatherType PickWeather()
        {
            if (useForcedWeather) return forcedWeather;

            if (allowedWeathers != null && allowedWeathers.Length > 0)
                return allowedWeathers[Random.Range(0, allowedWeathers.Length)];

            return (WeatherType)Random.Range(0, 4);
        }

        private void OnWeatherChanged(WeatherType prev, WeatherType next)
        {
            WeatherModifiers.Apply(next);
            Debug.Log($"[WeatherGimmick] Weather: {prev} -> {next} ({WeatherModifiers.KoreanName(next)}) | {DescribeEffects(next)}");

            if (IsServer && next == WeatherType.Typhoon)
                ShuffleItemBoxesServer();
        }

        private static string DescribeEffects(WeatherType w)
        {
            switch (w)
            {
                case WeatherType.Rain:
                    return $"넘어짐 지속 x{WeatherModifiers.FallDurationMult}, 회복 시간 x{WeatherModifiers.RecoveryTimeMult}";
                case WeatherType.Dust:
                    return $"스프린트 스태미너 소모 x{WeatherModifiers.SprintDrainMult}";
                case WeatherType.Typhoon:
                    return "아이템 박스 lane 셔플";
                case WeatherType.Clear:
                default:
                    return "효과 없음";
            }
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
            for (int i = 0; i < n; i++)
                boxes[i].SetLaneIndex(_itemBoxLanes[i]);
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
