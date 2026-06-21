namespace Seoul.Network.Game
{
    // 현재 활성 날씨가 PlayerController/스태미너 등에 적용할 모디파이어.
    // WeatherGimmick이 날씨 결정 시 Apply()로 갱신.
    // 소비처: PlayerStunState(FallDuration), PlayerController.StartRecoveryWindow(Recovery), PlayerRunState(SprintDrain).
    public static class WeatherModifiers
    {
        public static WeatherType Current { get; private set; } = WeatherType.Clear;

        // 비: 넘어짐 더 오래 + 회복 더 느림
        public static float FallDurationMult { get; private set; } = 1f;
        public static float RecoveryTimeMult { get; private set; } = 1f;

        // 황사/미세먼지: 스프린트 스태미너 소모 2배
        public static float SprintDrainMult { get; private set; } = 1f;

        public static void Apply(WeatherType weather)
        {
            Current = weather;
            FallDurationMult = 1f;
            RecoveryTimeMult = 1f;
            SprintDrainMult  = 1f;

            switch (weather)
            {
                case WeatherType.Rain:
                    FallDurationMult = 2f;
                    RecoveryTimeMult = 2f;
                    break;
                case WeatherType.Dust:
                    SprintDrainMult = 2f;
                    break;
                // Typhoon은 ItemBox 셔플 → WeatherGimmick에서 직접 처리
                // Clear는 기본값(1f) 유지
            }
        }

        public static void Clear() => Apply(WeatherType.Clear);

        public static string KoreanName(WeatherType w)
        {
            switch (w)
            {
                case WeatherType.Rain:    return "비";
                case WeatherType.Typhoon: return "태풍";
                case WeatherType.Dust:    return "황사";
                case WeatherType.Clear:
                default:                  return "맑음";
            }
        }
    }
}
