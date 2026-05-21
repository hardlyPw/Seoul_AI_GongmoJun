using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    [RequireComponent(typeof(PlayerController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        public static readonly List<NetworkPlayer> All = new();

        [Header("References")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private GameObject ownerVisualMarker;

        [Header("Camera")]
        [SerializeField] private bool attachCameraOnSpawn = true;

        public NetworkVariable<int> Score = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public void AddScore(int amount)
        {
            if (!IsServer) return;
            Score.Value += amount;
            Debug.Log($"[NetworkPlayer] clientId={OwnerClientId} score={Score.Value} (+{amount})");
        }

        public override void OnNetworkSpawn()
        {
            if (!All.Contains(this)) All.Add(this);

            if (controller == null) controller = GetComponent<PlayerController>();

            if (IsOwner)
            {
                controller.Initialize(new NullInputProvider());
                if (ownerVisualMarker != null) ownerVisualMarker.SetActive(true);
                if (attachCameraOnSpawn) AttachCameraToSelf();
            }
            else
            {
                controller.Initialize(new NullInputProvider());
                if (ownerVisualMarker != null) ownerVisualMarker.SetActive(false);
            }

            Debug.Log($"[NetworkPlayer] Spawned. OwnerClientId={OwnerClientId} IsOwner={IsOwner} LocalClientId={NetworkManager.Singleton.LocalClientId} pos={transform.position}");

            if (NetworkRaceManager.Instance != null)
            {
                NetworkRaceManager.Instance.State.OnValueChanged += OnRaceStateChanged;
                OnRaceStateChanged(default, NetworkRaceManager.Instance.State.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);

            if (NetworkRaceManager.Instance != null)
            {
                NetworkRaceManager.Instance.State.OnValueChanged -= OnRaceStateChanged;
            }
        }

        private void OnRaceStateChanged(RaceState previous, RaceState current)
        {
            if (!IsOwner) return;

            if (current == RaceState.Racing)
            {
                controller.Initialize(new PlayerInputProvider());
            }
            else
            {
                controller.Initialize(new NullInputProvider());
            }
        }

        private void AttachCameraToSelf()
        {
            var mainCam = Camera.main;
            if (mainCam == null) return;

            var follow = mainCam.GetComponent<CameraFollow>();
            if (follow != null)
            {
                follow.SetTarget(transform);
            }
            else
            {
                mainCam.transform.SetParent(transform);
                mainCam.transform.localPosition = new Vector3(0f, 3f, -6f);
                mainCam.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            }
        }
    }
}
