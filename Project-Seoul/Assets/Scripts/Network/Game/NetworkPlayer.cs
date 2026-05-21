using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    [RequireComponent(typeof(PlayerController))]
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private GameObject ownerVisualMarker;

        [Header("Camera")]
        [SerializeField] private bool attachCameraOnSpawn = true;

        public override void OnNetworkSpawn()
        {
            if (controller == null) controller = GetComponent<PlayerController>();

            if (IsOwner)
            {
                controller.Initialize(new PlayerInputProvider());
                if (ownerVisualMarker != null) ownerVisualMarker.SetActive(true);

                if (attachCameraOnSpawn) AttachCameraToSelf();
            }
            else
            {
                controller.Initialize(new NullInputProvider());
                if (ownerVisualMarker != null) ownerVisualMarker.SetActive(false);
            }

            Debug.Log($"[NetworkPlayer] Spawned. OwnerClientId={OwnerClientId} IsOwner={IsOwner} LocalClientId={NetworkManager.Singleton.LocalClientId} pos={transform.position}");
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
