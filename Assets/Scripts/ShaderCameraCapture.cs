using System;
using UnityEngine;
using Unity.WebRTC;

namespace Dreamwalker
{
    /// <summary>
    /// Captures frames from a shader's output for WebRTC streaming.
    /// Useful for shaders that read from global textures (e.g., Quest depth maps).
    /// </summary>
    public class ShaderCameraCapture : MonoBehaviour, ICameraCapture
    {
        [Header("Shader Settings")]
        [SerializeField] private Shader shader;

        [Header("Output Settings")]
        [SerializeField] private int targetWidth = 576;
        [SerializeField] private int targetHeight = 320;

        public event Action<RenderTexture> OnCameraReady;
        public RenderTexture CroppedTexture => outputTexture;
        public bool IsActive => isActive && outputTexture != null;

        private RenderTexture outputTexture;
        private Material material;
        private bool isActive = false;

        private void Start()
        {
            if (shader == null)
            {
                Debug.LogError("[ShaderCameraCapture] No shader assigned");
                return;
            }

            material = new Material(shader);
            CreateOutputTexture();
            isActive = true;

            Debug.Log($"[ShaderCameraCapture] Initialized with shader: {shader.name}");
            OnCameraReady?.Invoke(outputTexture);
        }

        private void CreateOutputTexture()
        {
            var supportedFormat = WebRTC.GetSupportedGraphicsFormat(SystemInfo.graphicsDeviceType);
            Debug.Log($"[ShaderCameraCapture] Creating output texture {targetWidth}x{targetHeight}, format: {supportedFormat}");

            outputTexture = new RenderTexture(targetWidth, targetHeight, 0, supportedFormat);
            outputTexture.antiAliasing = 1;
            outputTexture.Create();
        }

        private void LateUpdate()
        {
            if (!isActive || material == null || outputTexture == null)
                return;

            // Use a dummy texture as source to ensure proper UV coordinates are generated
            // The shader reads from its own global textures, but needs valid geometry/UVs from the blit
            Graphics.Blit(Texture2D.whiteTexture, outputTexture, material);
        }

        public void ToggleCamera()
        {
            // No-op for shader capture
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

            if (material != null)
            {
                Destroy(material);
                material = null;
            }
        }
    }
}
