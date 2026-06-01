using System.Collections;
using UnityEngine;

// 횡단보도 신호등 기믹. blockingWall이 minLane~maxLane 범위를 자동으로 커버.
public class CrosswalkGimmick : MonoBehaviour
{
    [SerializeField] private float      greenDuration = 3f;
    [SerializeField] private float      redDuration   = 4f;
    [SerializeField] private GameObject greenLight;
    [SerializeField] private GameObject redLight;
    [SerializeField] private Collider   blockingWall;
    [SerializeField] private int        minLane = 3;
    [SerializeField] private int        maxLane = 6;

    public bool IsGreen { get; private set; }

    private void Start()
    {
        var lm = LaneManager.Instance;

        if (blockingWall != null)
        {
            var wallPos = blockingWall.transform.position;
            wallPos.z                          = lm.GetLaneCenterZ(minLane, maxLane);
            blockingWall.transform.position    = wallPos;

            if (blockingWall is BoxCollider box)
            {
                var size = box.size;
                size.z   = lm.GetLaneSpanZ(minLane, maxLane);
                box.size = size;
            }
        }

        StartCoroutine(TrafficCycle());
    }

    private IEnumerator TrafficCycle()
    {
        while (true)
        {
            SetGreen(true);
            yield return new WaitForSeconds(greenDuration);
            SetGreen(false);
            yield return new WaitForSeconds(redDuration);
        }
    }

    private void SetGreen(bool green)
    {
        IsGreen = green;
        if (greenLight)   greenLight.SetActive(green);
        if (redLight)     redLight.SetActive(!green);
        if (blockingWall) blockingWall.enabled = !green;
    }
}
