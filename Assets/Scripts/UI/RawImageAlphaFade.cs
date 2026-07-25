using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class RawImageAlphaFade : MonoBehaviour
{
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float duration = 1f;
    [Range(0f, 1f)] public float minAlpha = 0f;
    [Range(0f, 1f)] public float maxAlpha = 1f;

    private RawImage _rawImage;
    private float _time;

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();
    }

    private void Update()
    {
        if (duration <= 0f)
            return;

        _time += Time.deltaTime;
        float normalizedTime = (_time % duration) / duration;
        float curveValue = alphaCurve.Evaluate(normalizedTime);
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, curveValue);

        Color color = _rawImage.color;
        color.a = alpha;
        _rawImage.color = color;
    }
}
