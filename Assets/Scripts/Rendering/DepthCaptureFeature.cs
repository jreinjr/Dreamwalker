using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Dreamwalker
{
    /// <summary>
    /// URP Renderer Feature that captures the depth texture to a RenderTexture.
    /// Requires Compatibility Mode (RenderGraph disabled) in Project Settings > Graphics.
    /// </summary>
    public class DepthCaptureFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("The RenderTexture to write depth to")]
            public RenderTexture outputTexture;

            [Tooltip("Material for depth visualization (required)")]
            public Material material;

            [Tooltip("When to capture the depth")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public Settings settings = new Settings();
        private DepthCapturePass depthCapturePass;

        public override void Create()
        {
            depthCapturePass = new DepthCapturePass(settings);
            depthCapturePass.renderPassEvent = settings.renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.outputTexture == null || settings.material == null)
                return;

            if (renderingData.cameraData.cameraType != CameraType.Game)
                return;

            renderer.EnqueuePass(depthCapturePass);
        }

        private class DepthCapturePass : ScriptableRenderPass
        {
            private Settings settings;
            private static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");

            public DepthCapturePass(Settings settings)
            {
                this.settings = settings;
                profilingSampler = new ProfilingSampler("DepthCapture");
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (settings.outputTexture == null || settings.material == null)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get("DepthCapture");

                // Blit _CameraDepthTexture through the material to the output
                cmd.Blit(CameraDepthTextureId, settings.outputTexture, settings.material);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
