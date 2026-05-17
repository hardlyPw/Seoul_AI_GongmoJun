using UnityEngine;

public class LevelSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject scoreItemPrefab;
    public GameObject obstacleStaticPrefab;
    public GameObject obstacleMovingPrefab; 
    public GameObject puddlePrefab;
    public GameObject itemBoxPrefab;
    public GameObject jumpPadPrefab;        

    [Header("Spawn Rules")]
    public float startX = 20f;  // 시작지점
    public float endX = 500f;   // 골지점
    public float minIntervalX = 5f;     // 최소 간격
    public float maxIntervalX = 15f;    // 최대 간격

    [Header("Lanes")]
    public float[] laneZPositions = { -5f, -3f, -1f, 1f, 3f, 5f };

    void Start() { GenerateLevel(); }

    public void ReconstructLevel()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        GenerateLevel();
    }

    private void GenerateLevel()
    {
        float currentX = startX;

        while (currentX < endX)
        {
            currentX += Random.Range(minIntervalX, maxIntervalX);
            if (currentX >= endX) break;

            float spawnZ = laneZPositions[Random.Range(0, laneZPositions.Length)];
            float chance = Random.value;

            // 자동 레벨디자인
            if (chance < 0.3f)
            {
                Instantiate(scoreItemPrefab, new Vector3(currentX, 0.5f, spawnZ), Quaternion.identity, this.transform);
            }
            else if (chance < 0.45f)
            {
                Instantiate(puddlePrefab, new Vector3(currentX, 0.5f, spawnZ), Quaternion.identity, this.transform);
            }
            else if (chance < 0.6f)
            {
                Instantiate(obstacleStaticPrefab, new Vector3(currentX, 1.0f, spawnZ), Quaternion.identity, this.transform);
            }
            else if (chance < 0.75f)
            {
                Instantiate(obstacleMovingPrefab, new Vector3(currentX, 1.0f, spawnZ), Quaternion.identity, this.transform);
            }
            else if (chance < 0.85f)
            {
                Instantiate(itemBoxPrefab, new Vector3(currentX, 0.7f, spawnZ), Quaternion.identity, this.transform);
            }
            else
            {
                // 점프대 생성
                Instantiate(jumpPadPrefab, new Vector3(currentX, 0.5f, spawnZ), Quaternion.identity, this.transform);

                // 점프대 생성 시 젤리 포물선 배치
                Instantiate(scoreItemPrefab, new Vector3(currentX + 5f, 4.0f, spawnZ), Quaternion.identity, this.transform);
                Instantiate(scoreItemPrefab, new Vector3(currentX + 8f, 5.0f, spawnZ), Quaternion.identity, this.transform);
                Instantiate(scoreItemPrefab, new Vector3(currentX + 11f, 4.0f, spawnZ), Quaternion.identity, this.transform);
            }
        }
    }
}