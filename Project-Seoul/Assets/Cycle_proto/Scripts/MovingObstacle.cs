using UnityEngine;

public class MovingObstacle : MonoBehaviour
{
    public float moveSpeed = 5f;     // 좌우 이동 속도
    public float minZ = -5f;         // 최우측 라인 끝
    public float maxZ = 5f;          // 최좌측 라인 끝
    private int direction = 1;       // 이동 방향

    void Update()
    {
        float currentZ = transform.position.z;
        currentZ += direction * moveSpeed * Time.deltaTime;

        // 끝에 도달하면 방향 반전
        if (currentZ >= maxZ)
        {
            currentZ = maxZ;
            direction = -1;
        }
        else if (currentZ <= minZ)
        {
            currentZ = minZ;
            direction = 1;
        }

        // X와 Y는 유지하고 Z축 위치만 갱신
        transform.position = new Vector3(transform.position.x, transform.position.y, currentZ);
    }
}