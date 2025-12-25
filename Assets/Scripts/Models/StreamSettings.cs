using System;
using System.Collections.Generic;

namespace Dreamwalker.Models
{
    /// <summary>
    /// Holds all stream settings that can be configured via UI
    /// </summary>
    [Serializable]
    public class StreamSettings
    {
        // Pipeline settings
        public string pipelineId = "longlive";  // Default to longlive for AI video
        public int width = 576;
        public int height = 320;
        public int seed = 42;
        public string inputMode = "video"; // Default to video mode for camera input

        // Denoising
        public float[] denoisingSteps = { 1000f, 750f, 500f, 250f };

        // Noise control
        public float noiseScale = 0.8f;
        public bool noiseController = true;

        // Cache
        public bool manageCache = true;

        // Prompts - fun Christmas default!
        public List<PromptItem> prompts = new List<PromptItem>
        {
            new PromptItem { text = "Inside Santa's workshop, snowy winter decorations, colorful presents and happy elves. Christmas decorations with beautiful colored lights.", weight = 1.0f }
        };
        public string promptInterpolationMethod = "slerp";

        // LoRA
        public List<LoRAConfig> loras = new List<LoRAConfig>();
        public string loraMergeStrategy = "permanent_merge";

        // VACE
        public bool vaceEnabled = true;
        public float vaceContextScale = 1.0f;

        // KV Cache
        public float kvCacheAttentionBias = 0.5f;

        // Playback
        public bool paused = false;

        /// <summary>
        /// Creates InitialParameters for WebRTC connection
        /// </summary>
        public InitialParameters ToInitialParameters()
        {
            // Passthrough mode only needs input_mode - no AI parameters
            if (pipelineId == "passthrough")
            {
                return new InitialParameters
                {
                    input_mode = inputMode
                };
            }

            // AI pipelines need full parameters
            var defaultPrompt = new PromptItem
            {
                text = "A 3D animated scene. A panda sitting in the grass, looking around.",
                weight = 1.0f
            };

            return new InitialParameters
            {
                input_mode = inputMode,
                prompts = prompts.Count > 0 && !string.IsNullOrEmpty(prompts[0].text)
                    ? prompts.ToArray()
                    : new[] { defaultPrompt },
                prompt_interpolation_method = promptInterpolationMethod,
                denoising_step_list = denoisingSteps,
                noise_scale = noiseScale,
                noise_controller = noiseController,
                manage_cache = manageCache,
                reset_cache = false,
                kv_cache_attention_bias = kvCacheAttentionBias,
                lora_scales = GetLoRAScales(),
                vace_enabled = vaceEnabled,
                vace_context_scale = vaceContextScale
            };
        }

        /// <summary>
        /// Creates PipelineLoadParams for loading a pipeline
        /// </summary>
        public PipelineLoadParams ToPipelineLoadParams()
        {
            return new PipelineLoadParams
            {
                width = width,
                height = height,
                seed = seed,
                vace_enabled = vaceEnabled,
                loras = loras.Count > 0 ? loras.ToArray() : null,
                lora_merge_mode = loraMergeStrategy
            };
        }

        private LoRAScale[] GetLoRAScales()
        {
            if (loras.Count == 0) return null;

            var scales = new LoRAScale[loras.Count];
            for (int i = 0; i < loras.Count; i++)
            {
                scales[i] = new LoRAScale
                {
                    path = loras[i].path,
                    scale = loras[i].scale
                };
            }
            return scales;
        }

        /// <summary>
        /// Randomizes the seed
        /// </summary>
        public void RandomizeSeed()
        {
            seed = UnityEngine.Random.Range(0, int.MaxValue);
        }
    }
}
