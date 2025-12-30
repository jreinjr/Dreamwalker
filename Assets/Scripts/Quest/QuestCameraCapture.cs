using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Unity.WebRTC;
using Meta.XR;

namespace Dreamwalker.Quest
{
    /// <summary>
    /// Handles Quest 3 passthrough camera capture for WebRTC streaming.
    /// Wraps Meta.XR.PassthroughCameraAccess to provide a similar interface to CameraCapture.cs
    /// </summary>
    public class QuestCameraCapture : MonoBehaviour, ICameraCapture
    {
        [Header("Camera Settings")]
        [SerializeField] private int targetWidth = 576;
        [SerializeField] private int targetHeight = 320;

        [Header("Passthrough Camera Reference")]
        [Tooltip("Reference to the PassthroughCameraAccess component. If not set, will search for one.")]
        [SerializeField] private PassthroughCameraAccess passthroughCamera;

        // Aspect ratio for 16:9
        private const float TARGET_ASPECT = 16f / 9f;

        private RenderTexture croppedTexture;
        private bool isInitialized = false;

        // Crop parameters calculated when camera starts
        private Vector4 cropParams; // x=offsetX, y=offsetY, z=scaleX, w=scaleY

        /// <summary>
        /// Event fired when camera is ready and streaming
        /// </summary>
        public event Action<RenderTexture> OnCameraReady;

        /// <summary>
        /// Current cropped camera texture (576x320, 16:9)
        /// </summary>
        public RenderTexture CroppedTexture => croppedTexture;

        /// <summary>
        /// Whether the camera is currently active and providing frames
        /// </summary>
        public bool IsActive => passthroughCamera != null && passthroughCamera.IsPlaying;

        /// <summary>
        /// Current resolution of the passthrough camera
        /// </summary>
        public Vector2Int SourceResolution => passthroughCamera != null ? passthroughCamera.CurrentResolution : Vector2Int.zero;

        private void Start()
        {
            StartCoroutine(InitializeCamera());
        }

        private IEnumerator InitializeCamera()
        {
            Debug.Log("[QuestCameraCapture] Starting initialization...");

            // Find PassthroughCameraAccess if not assigned
            if (passthroughCamera == null)
            {
                passthroughCamera = FindObjectOfType<PassthroughCameraAccess>();
                if (passthroughCamera == null)
                {
                    Debug.LogError("[QuestCameraCapture] No PassthroughCameraAccess found in scene. Please add the Passthrough Camera Access Building Block.");
                    yield break;
                }
            }

            Debug.Log("[QuestCameraCapture] Found PassthroughCameraAccess, waiting for it to start playing...");

            // Wait for passthrough camera to be ready
            float timeout = 10f;
            float elapsed = 0f;

            while (!passthroughCamera.IsPlaying && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!passthroughCamera.IsPlaying)
            {
                Debug.LogError("[QuestCameraCapture] Passthrough camera failed to start within timeout");
                yield break;
            }

            Debug.Log($"[QuestCameraCapture] Passthrough camera ready: {passthroughCamera.CurrentResolution.x}x{passthroughCamera.CurrentResolution.y}");

            // Create the output texture for WebRTC
            CreateOutputTexture();

            // Calculate crop parameters based on passthrough resolution
            CalculateCropParameters();

            isInitialized = true;
            Debug.Log("[QuestCameraCapture] Initialization complete");

            OnCameraReady?.Invoke(croppedTexture);
        }

        /// <summary>
        /// Creates the persistent output RenderTexture used by WebRTC.
        /// </summary>
        private void CreateOutputTexture()
        {
            // Get WebRTC-supported graphics format for this platform
            var supportedFormat = WebRTC.GetSupportedGraphicsFormat(SystemInfo.graphicsDeviceType);
            Debug.Log($"[QuestCameraCapture] Creating output texture - WebRTC format: {supportedFormat}, device: {SystemInfo.graphicsDeviceType}");

            croppedTexture = new RenderTexture(targetWidth, targetHeight, 0, supportedFormat);
            croppedTexture.antiAliasing = 1;
            croppedTexture.Create();

            Debug.Log($"[QuestCameraCapture] Output texture created: {targetWidth}x{targetHeight}");
        }

        /// <summary>
        /// Calculates crop parameters to achieve 16:9 aspect ratio from passthrough camera.
        /// Quest 3 passthrough is typically 1280x960 (4:3), we crop to 16:9.
        /// </summary>
        private void CalculateCropParameters()
        {
            var resolution = passthroughCamera.CurrentResolution;
            int camWidth = resolution.x;
            int camHeight = resolution.y;

            float camAspect = (float)camWidth / camHeight;

            if (camAspect > TARGET_ASPECT)
            {
                // Camera is wider than 16:9 - crop sides
                float scale = TARGET_ASPECT / camAspect;
                float offset = (1f - scale) / 2f;
                cropParams = new Vector4(offset, 0f, scale, 1f);
            }
            else if (camAspect < TARGET_ASPECT)
            {
                // Camera is taller than 16:9 - crop top/bottom (typical for Quest 3's 4:3)
                float scale = camAspect / TARGET_ASPECT;
                float offset = (1f - scale) / 2f;
                cropParams = new Vector4(0f, offset, 1f, scale);
            }
            else
            {
                // Already 16:9
                cropParams = new Vector4(0f, 0f, 1f, 1f);
            }

            Debug.Log($"[QuestCameraCapture] Crop params: offset=({cropParams.x:F3}, {cropParams.y:F3}), scale=({cropParams.z:F3}, {cropParams.w:F3})");
        }

        private void Update()
        {
            if (!isInitialized || passthroughCamera == null || croppedTexture == null)
                return;

            // Only process when we have a new frame
            if (!passthroughCamera.IsPlaying)
                return;

            // Get the passthrough camera texture
            Texture sourceTexture = passthroughCamera.GetTexture();
            if (sourceTexture == null)
                return;

            // Blit with crop/scale to croppedTexture
            Vector2 scale = new Vector2(cropParams.z, cropParams.w);
            Vector2 offset = new Vector2(cropParams.x, cropParams.y);
            Graphics.Blit(sourceTexture, croppedTexture, scale, offset);
        }

        /// <summary>
        /// Switches between left and right passthrough cameras.
        /// Note: This requires recreating the PassthroughCameraAccess component.
        /// </summary>
        public void ToggleCamera()
        {
            if (passthroughCamera == null)
            {
                Debug.LogWarning("[QuestCameraCapture] No passthrough camera to toggle");
                return;
            }

            // Toggle camera position
            var currentPosition = passthroughCamera.CameraPosition;
            var newPosition = currentPosition == PassthroughCameraAccess.CameraPositionType.Left
                ? PassthroughCameraAccess.CameraPositionType.Right
                : PassthroughCameraAccess.CameraPositionType.Left;

            Debug.Log($"[QuestCameraCapture] Toggling camera from {currentPosition} to {newPosition}");

            // Need to disable and re-enable with new position
            passthroughCamera.enabled = false;
            passthroughCamera.CameraPosition = newPosition;
            passthroughCamera.enabled = true;

            // Recalculate crop params in case resolution differs
            StartCoroutine(WaitAndRecalculateCrop());
        }

        private IEnumerator WaitAndRecalculateCrop()
        {
            // Wait for camera to restart
            yield return new WaitUntil(() => passthroughCamera.IsPlaying);
            CalculateCropParameters();
        }

        private void OnDestroy()
        {
            if (croppedTexture != null)
            {
                croppedTexture.Release();
                Destroy(croppedTexture);
                croppedTexture = null;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // PassthroughCameraAccess handles its own pause/resume
            // But we log for debugging
            Debug.Log($"[QuestCameraCapture] Application pause: {pauseStatus}");
        }
    }
}
