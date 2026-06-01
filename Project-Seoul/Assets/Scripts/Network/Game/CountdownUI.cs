using TMPro;
using UnityEngine;

namespace Seoul.Network.Game
{
    public class CountdownUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private float goDisplayDuration = 1f;

        private float _goTimer;
        private bool  _showingGo;

        private void Reset()
        {
            countdownText = GetComponentInChildren<TMP_Text>();
        }

        private void Update()
        {
            if (NetworkRaceManager.Instance == null)
            {
                SetText(string.Empty);
                return;
            }

            var state     = NetworkRaceManager.Instance.State.Value;
            float remain  = NetworkRaceManager.Instance.CountdownRemaining.Value;

            switch (state)
            {
                case RaceState.Countdown:
                    int displayed = Mathf.CeilToInt(remain);
                    SetText(displayed > 0 ? displayed.ToString() : "GO!");
                    _showingGo = false;
                    break;

                case RaceState.Racing:
                    if (!_showingGo)
                    {
                        SetText("GO!");
                        _goTimer   = goDisplayDuration;
                        _showingGo = true;
                    }
                    else
                    {
                        _goTimer -= Time.deltaTime;
                        if (_goTimer <= 0f) SetText(string.Empty);
                    }
                    break;

                default:
                    SetText(string.Empty);
                    break;
            }
        }

        private void SetText(string s)
        {
            if (countdownText != null && countdownText.text != s)
                countdownText.text = s;
        }
    }
}
