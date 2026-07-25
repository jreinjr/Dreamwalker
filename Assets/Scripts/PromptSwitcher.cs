using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Dreamwalker.Models;
using Dreamwalker.Networking;

namespace Dreamwalker
{
    /// <summary>
    /// Switches prompts and reference images based on head orientation.
    /// Compares CenterEyeAnchor forward direction with configured transforms.
    /// </summary>
    public class PromptSwitcher : MonoBehaviour
    {
        public enum Direction
        {
            None,
            Forward,
            Left,
            Right
        }

        [Header("Dependencies")]
        [SerializeField] private ScopeWebRTCManager webRTCManager;
        [SerializeField] private Transform centerEyeAnchor;

        [Header("Forward Direction")]
        [SerializeField] private Transform forwardTransform;
        [SerializeField, TextArea(2, 5)] private string forwardPrompt = "";
        [SerializeField] private Texture2D forwardImage;

        [Header("Left Direction")]
        [SerializeField] private Transform leftTransform;
        [SerializeField, TextArea(2, 5)] private string leftPrompt = "";
        [SerializeField] private Texture2D leftImage;

        [Header("Right Direction")]
        [SerializeField] private Transform rightTransform;
        [SerializeField, TextArea(2, 5)] private string rightPrompt = "";
        [SerializeField] private Texture2D rightImage;

        [Header("Settings")]
        [SerializeField] private float debounceDuration = 1f;

        [Header("Events")]
        public UnityEvent<Texture2D> OnReferenceImageChanged;
        public UnityEvent<Vector2> OnLookOffsetChanged;

        [Header("Runtime Status")]
        [SerializeField, ReadOnly] private Direction currentDirection = Direction.None;
        [SerializeField, ReadOnly] private float forwardDot;
        [SerializeField, ReadOnly] private float leftDot;
        [SerializeField, ReadOnly] private float rightDot;
        [SerializeField, ReadOnly] private Vector2 lookOffset;

        private Direction lastSentDirection = Direction.None;
        private float lastDirectionChangeTime;
        private bool isUpdating;

        private void Update()
        {
            if (centerEyeAnchor == null) return;

            Vector3 eyeForward = centerEyeAnchor.forward;

            forwardDot = forwardTransform != null ? Vector3.Dot(eyeForward, forwardTransform.forward) : float.MinValue;
            leftDot = leftTransform != null ? Vector3.Dot(eyeForward, leftTransform.forward) : float.MinValue;
            rightDot = rightTransform != null ? Vector3.Dot(eyeForward, rightTransform.forward) : float.MinValue;

            Direction closestDirection = GetClosestDirection();

            // Compute look offset relative to the current active target (debounce-aware)
            Transform activeTransform = GetTransformForDirection(lastSentDirection);
            if (activeTransform != null)
            {
                lookOffset = ComputeLookOffset(eyeForward, activeTransform);
                OnLookOffsetChanged?.Invoke(lookOffset);
            }

            if (closestDirection != currentDirection)
            {
                currentDirection = closestDirection;
                Debug.Log($"[PromptSwitcher] Direction changed to: {currentDirection}");
            }

            if (currentDirection != lastSentDirection &&
                currentDirection != Direction.None &&
                !isUpdating &&
                Time.time - lastDirectionChangeTime >= debounceDuration)
            {
                lastDirectionChangeTime = Time.time;
                StartCoroutine(UpdatePromptAndImage(currentDirection));
            }
        }

        private Direction GetClosestDirection()
        {
            float maxDot = float.MinValue;
            Direction closest = Direction.None;

            if (forwardTransform != null && forwardDot > maxDot)
            {
                maxDot = forwardDot;
                closest = Direction.Forward;
            }

            if (leftTransform != null && leftDot > maxDot)
            {
                maxDot = leftDot;
                closest = Direction.Left;
            }

            if (rightTransform != null && rightDot > maxDot)
            {
                maxDot = rightDot;
                closest = Direction.Right;
            }

            return closest;
        }

        private IEnumerator UpdatePromptAndImage(Direction direction)
        {
            if (webRTCManager == null || !webRTCManager.IsConnected)
            {
                Debug.LogWarning("[PromptSwitcher] WebRTC not connected, skipping update");
                yield break;
            }

            isUpdating = true;

            string prompt = GetPromptForDirection(direction);
            Texture2D image = GetImageForDirection(direction);
            var apiClient = webRTCManager.ApiClient;

            if (apiClient == null)
            {
                Debug.LogError("[PromptSwitcher] No API client available");
                isUpdating = false;
                yield break;
            }

            apiClient.CurrentPrompt = prompt;
            apiClient.VaceReferenceImage = image;

            OnReferenceImageChanged?.Invoke(image);

            string uploadedImagePath = null;

            if (image != null)
            {
                string filename = $"prompt_switcher_{direction}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";

                yield return apiClient.UploadTextureScaled(
                    image,
                    filename,
                    apiClient.Width,
                    apiClient.Height,
                    (response, error) =>
                    {
                        if (response != null)
                        {
                            uploadedImagePath = response.path;
                            Debug.Log($"[PromptSwitcher] Uploaded reference image: {uploadedImagePath}");
                        }
                        else
                        {
                            Debug.LogWarning($"[PromptSwitcher] Failed to upload image: {error}");
                        }
                    });
            }

            var parameters = new RuntimeParameters
            {
                prompts = new PromptItem[]
                {
                    new PromptItem { text = prompt, weight = apiClient.PromptWeight }
                },
                vace_ref_images = !string.IsNullOrEmpty(uploadedImagePath) ? new[] { uploadedImagePath } : null,
                vace_context_scale = apiClient.VaceContextScale
            };

            webRTCManager.SendParameterUpdate(parameters);
            Debug.Log($"[PromptSwitcher] Sent update for direction {direction}: prompt=\"{prompt}\"");

            lastSentDirection = direction;
            isUpdating = false;
        }

        private string GetPromptForDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Forward => forwardPrompt,
                Direction.Left => leftPrompt,
                Direction.Right => rightPrompt,
                _ => ""
            };
        }

        private Texture2D GetImageForDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Forward => forwardImage,
                Direction.Left => leftImage,
                Direction.Right => rightImage,
                _ => null
            };
        }

        private Transform GetTransformForDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Forward => forwardTransform,
                Direction.Left => leftTransform,
                Direction.Right => rightTransform,
                _ => null
            };
        }

        /// <summary>
        /// Computes how much the eye forward deviates from the target forward
        /// in the target's local space. Returns offset where:
        /// X > 0 means looking right of target, X < 0 means looking left
        /// Y > 0 means looking above target, Y < 0 means looking below
        /// </summary>
        private Vector2 ComputeLookOffset(Vector3 eyeForward, Transform target)
        {
            float x = Vector3.Dot(eyeForward, target.right);
            float y = Vector3.Dot(eyeForward, target.up);
            return new Vector2(x, y);
        }
    }
}
