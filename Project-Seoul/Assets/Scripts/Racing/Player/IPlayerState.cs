using UnityEngine;

public interface IPlayerState
{
    void EnterState(PlayerController player);
    void UpdateState(PlayerController player);
    void FixedUpdateState(PlayerController player);
    void OnCollisionCheck(PlayerController player, UnityEngine.Collider other);
    void ExitState(PlayerController player);
}