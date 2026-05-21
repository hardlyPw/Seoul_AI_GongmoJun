using UnityEngine;

// 패럴렉스 배경 스크롤. 각 레이어의 parallaxFactor: 0=고정, 1=카메라와 동일하게 이동
public class BackgroundScroller : MonoBehaviour
{
    [SerializeField] private Transform[] layers;
    [SerializeField] private float[]     parallaxFactors;
    [SerializeField] private Transform   cameraTransform;

    private float[] _originX;

    private void Start()
    {
        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;

        _originX = new float[layers.Length];
        for (int i = 0; i < layers.Length; i++)
            _originX[i] = layers[i].position.x;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;
        float camX = cameraTransform.position.x;

        for (int i = 0; i < layers.Length; i++)
        {
            float factor = i < parallaxFactors.Length ? parallaxFactors[i] : 0.5f;
            Vector3 pos  = layers[i].position;
            pos.x        = _originX[i] + camX * factor;
            layers[i].position = pos;
        }
    }
}
