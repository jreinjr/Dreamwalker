using UnityEngine;
using UnityEngine.UI;

namespace Dreamwalker.UI
{
    /// <summary>
    /// Nudges a RawImage based on a Vector2 offset.
    /// Attach to a GameObject with a RawImage and RectTransform.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    [RequireComponent(typeof(RectTransform))]
    public class RawImageNudge : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Vector2 scale = new Vector2(100f, 100f);
        [SerializeField] private float smoothing = 10f;

        [Header("Runtime Status")]
        [SerializeField, ReadOnly] private Vector2 currentOffset;
        [SerializeField, ReadOnly] private Vector2 targetOffset;

        private RectTransform rectTransform;
        private Vector2 originalAnchoredPosition;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalAnchoredPosition = rectTransform.anchoredPosition;
        }

        private void Update()
        {
            if (smoothing > 0)
            {
                currentOffset = Vector2.Lerp(currentOffset, targetOffset, Time.deltaTime * smoothing);
            }
            else
            {
                currentOffset = targetOffset;
            }

            rectTransform.anchoredPosition = originalAnchoredPosition + currentOffset * scale;
        }

        /// <summary>
        /// Sets the nudge offset. Values are typically in range -1 to 1.
        /// </summary>
        public void NudgeOffset(Vector2 offset)
        {
            targetOffset = offset;
        }

        /// <summary>
        /// Resets the nudge to center position.
        /// </summary>
        public void ResetNudge()
        {
            targetOffset = Vector2.zero;
            currentOffset = Vector2.zero;
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }
    }
}
