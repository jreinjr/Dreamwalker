using System;
using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;

namespace Dreamwalker
{
    /// <summary>
    /// Captures frames from a RenderTexture for WebRTC streaming.
    /// Optionally applies a material for processing.
    /// </summary>
    public class RenderTextureCapture : MonoBehaviour, ICameraCapture
    {
        [Header("Source")]
        [SerializeField] private RenderTexture sourceTexture;
        [SerializeField] private Material material;

        [Header("Output Settings")]
        [SerializeField] private int targetWidth = 576;
        [SerializeField] private int targetHeight = 320;

        [Header("Debug")]
        [SerializeField] private RawImage debugPreview;

        public event Action<RenderTexture> OnCameraReady;
        public RenderTexture CroppedTexture => outputTexture;
        public bool IsActive => isActive && outputTexture != null;

        private RenderTexture outputTexture;
        private bool isActive = false;

        private void Start()
        {
            if (sourceTexture == null)
            {
                Debug.LogError("[RenderTextureCapture] No source texture assigned");
                return;
            }

            CreateOutputTexture();
            isActive = true;

            if (debugPreview != null)
                debugPreview.texture = outputTexture;

            OnCameraReady?.Invoke(outputTexture);
        }

        private void CreateOutputTexture()
        {
            var supportedFormat = WebRTC.GetSupportedGraphicsFormat(SystemInfo.graphicsDeviceType);
            Debug.Log($"[RenderTextureCapture] Creating output texture {targetWidth}x{targetHeight}, format: {supportedFormat}");

            outputTexture = new RenderTexture(targetWidth, targetHeight, 0, supportedFormat);
            outputTexture.antiAliasing = 1;
            outputTexture.Create();
        }

        private void LateUpdate()
        {
            if (!isActive || sourceTexture == null || outputTexture == null)
                return;

            if (material != null)
                Graphics.Blit(sourceTexture, outputTexture, material);
            else
                Graphics.Blit(sourceTexture, outputTexture);
        }

        public void ToggleCamera()
        {
            // No-op for RenderTexture source
        }

        private void OnDestroy()
        {
            isActive = false;

            if (outputTexture != null)
            {
                outputTexture.Release();
                Destroy(outputTexture);
                outputTexture = null;
            }
        }
    }
}
