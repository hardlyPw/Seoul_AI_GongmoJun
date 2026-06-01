using UnityEngine;
using Unity.Netcode;

public class StaticObstacle : ObstacleBase
{
    [SerializeField] private int laneIndex = 0;

    public override void OnNetworkSpawn()
    {
        var pos = transform.position;
        pos.z = LaneManager.Instance.GetLaneZ(laneIndex);
        transform.position = pos;
    }
}