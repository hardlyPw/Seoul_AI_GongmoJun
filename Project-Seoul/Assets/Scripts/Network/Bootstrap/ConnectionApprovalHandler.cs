using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Bootstrap
{
    [RequireComponent(typeof(NetworkManager))]
    public class ConnectionApprovalHandler : MonoBehaviour
    {
        [SerializeField] private int maxPlayers = 4;

        private NetworkManager _nm;

        private void Awake()
        {
            _nm = GetComponent<NetworkManager>();
            _nm.ConnectionApprovalCallback = ApprovalCheck;
        }

        private void OnDestroy()
        {
            if (_nm != null) _nm.ConnectionApprovalCallback = null;
        }

        private void ApprovalCheck(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            int currentCount = _nm.ConnectedClientsIds.Count;

            if (currentCount >= maxPlayers)
            {
                response.Approved = false;
                response.Reason = $"Room is full ({currentCount}/{maxPlayers})";
                Debug.Log($"[Approval] Rejected: room full");
                return;
            }

            response.Approved        = true;
            response.CreatePlayerObject = false;
            response.Pending         = false;

            Debug.Log($"[Approval] Approved client {request.ClientNetworkId} ({currentCount + 1}/{maxPlayers})");
        }
    }
}
