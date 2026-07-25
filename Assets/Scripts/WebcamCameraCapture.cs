using System;
using System.Collections;
using UnityEngine;
using Unity.WebRTC;

namespace Dreamwalker
{
    /// <summary>
    /// Simple webcam capture for desktop. Implements ICameraCapture for WebRTC streaming.
    /// </summary>
    public class WebcamCameraCapture : MonoBehaviour, ICameraCapture
    {
        [Header("Camera Settings")]
        [SerializeField] private int targetWidth = 576;
        [SerializeField] private int targetHeight = 320;
        [SerializeField] private int requestedFPS = 30;

        private const float TARGET_ASPECT = 16f / 9f;

        private WebCamTexture webCamTexture;
        private RenderTexture renderTexture;
        private RenderTexture croppedTexture;
        private Vector4 cropParams;

        public event Action<RenderTexture> OnCameraReady;
        public RenderTexture CroppedTexture => croppedTexture;
        public bool IsActive => webCamTexture != null && webCamTexture.isPlaying;

        private void Start()
        {
            StartCoroutine(InitializeCamera());
        }

        private IEnumerator InitializeCamera()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogError("[WebcamCapture] Camera permission denied");
                yield break;
            }

            if (WebCamTexture.devices.Length == 0)
            {
                Debug.LogError("[WebcamCapture] No camera devices found");
                yield break;
            }

            // Log available cameras and use the first one
            foreach (var device in WebCamTexture.devices)
            {
                Debug.Log($"[WebcamCapture] Camera found: {device.name}");
            }

            CreateOutputTexture();
            StartCamera();
        }

        private void CreateOutputTexture()
        {
            var supportedFormat = WebRTC.GetSupportedGraphicsFormat(SystemInfo.graphicsDeviceType);
            croppedTexture = new RenderTexture(targetWidth, targetHeight, 0, supportedFormat);
            croppedTexture.Create();
            Debug.Log($"[WebcamCapture] Output texture created: {targetWidth}x{targetHeight}");
        }

        private void StartCamera()
        {
            StopCamera();

            string cameraName = WebCamTexture.devices[0].name;
            Debug.Log($"[WebcamCapture] Starting camera: {cameraName}");

            webCamTexture = new WebCamTexture(cameraName, 1280, 720, requestedFPS);
            webCamTexture.Play();

            StartCoroutine(WaitForCameraReady());
        }

        private IEnumerator WaitForCameraReady()
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (!webCamTexture.didUpdateThisFrame && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!webCamTexture.didUpdateThisFrame)
            {
                Debug.LogError("[WebcamCapture] Camera timeout");
                yield break;
            }

            int camWidth = webCamTexture.width;
            int camHeight = webCamTexture.height;
            Debug.Log($"[WebcamCapture] Camera started: {camWidth}x{camHeight}");

            // Calculate crop parameters for 16:9 aspect ratio
            float camAspect = (float)camWidth / camHeight;
            if (camAspect > TARGET_ASPECT)
            {
                float scale = TARGET_ASPECT / camAspect;
                float offset = (1f - scale) / 2f;
                cropParams = new Vector4(offset, 0f, scale, 1f);
            }
            else if (camAspect < TARGET_ASPECT)
            {
                float scale = camAspect / TARGET_ASPECT;
                float offset = (1f - scale) / 2f;
                cropParams = new Vector4(0f, offset, 1f, scale);
            }
            else
            {
                cropParams = new Vector4(0f, 0f, 1f, 1f);
            }

            renderTexture = new RenderTexture(camWidth, camHeight, 0, RenderTextureFormat.ARGB32);
            renderTexture.Create();

            OnCameraReady?.Invoke(croppedTexture);
        }

        private void Update()
        {
            if (webCamTexture != null && webCamTexture.isPlaying &&
                renderTexture != null && croppedTexture != null)
            {
                Graphics.Blit(webCamTexture, renderTexture);
                Vector2 scale = new Vector2(cropParams.z, cropParams.w);
                Vector2 offset = new Vector2(cropParams.x, cropParams.y);
                Graphics.Blit(renderTexture, croppedTexture, scale, offset);
            }
        }

        public void ToggleCamera()
        {
            // No-op for simple desktop webcam - only one camera used
        }

        private void StopCamera()
        {
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
                Destroy(webCamTexture);
                webCamTexture = null;
            }
        }

        private void OnDestroy()
        {
            StopCamera();
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
            if (croppedTexture != null)
            {
                croppedTexture.Release();
                Destroy(croppedTexture);
            }
        }
    }
}
