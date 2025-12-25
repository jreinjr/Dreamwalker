using System;
using System.Collections.Generic;

namespace Dreamwalker.Models
{
    // ========== WebRTC Models ==========

    [Serializable]
    public class IceServer
    {
        public string[] urls;
        public string username;
        public string credential;
    }

    [Serializable]
    public class IceServersResponse
    {
        public IceServer[] iceServers;
    }

    [Serializable]
    public class WebRTCOfferRequest
    {
        public string sdp;
        public string type;
        public InitialParameters initialParameters;
    }

    [Serializable]
    public class WebRTCOfferResponse
    {
        public string sdp;
        public string type;
        public string sessionId;
    }

    [Serializable]
    public class IceCandidate
    {
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }

    [Serializable]
    public class IceCandidatesRequest
    {
        public IceCandidate[] candidates;
    }

    // ========== Pipeline Models ==========

    [Serializable]
    public class PipelineLoadRequest
    {
        public string pipeline_id;
        public PipelineLoadParams load_params;
    }

    [Serializable]
    public class PipelineLoadParams
    {
        public int height = 360;
        public int width = 640;
        public int? seed;
        public string quantization;
        public bool vace_enabled = false;
        public LoRAConfig[] loras;
        public string lora_merge_mode;
    }

    [Serializable]
    public class PipelineStatusResponse
    {
        public string status; // "not_loaded", "loading", "loaded", "error"
        public string pipeline_id;
        public PipelineLoadParams load_params;
        public LoRAAdapter[] loaded_lora_adapters;
        public string error;
    }

    [Serializable]
    public class LoRAConfig
    {
        public string name;  // Display name for UI
        public string path;  // File path on server
        public float scale = 1.0f;
        public string merge_mode;
    }

    [Serializable]
    public class LoRAAdapter
    {
        public string path;
        public float scale;
    }

    // ========== Hardware Info ==========

    [Serializable]
    public class HardwareInfoResponse
    {
        public float vram_gb;
        public bool spout_available;
    }

    // ========== Model Download ==========

    [Serializable]
    public class ModelStatusResponse
    {
        public string status; // "not_downloaded", "downloading", "downloaded", "error"
        public float percentage;
        public string current_artifact;
        public string error;
    }

    [Serializable]
    public class ModelDownloadRequest
    {
        public string pipeline_id;
    }

    // ========== LoRA List ==========

    [Serializable]
    public class LoRAFileInfo
    {
        public string name;
        public string path;
        public float size_mb;
    }

    [Serializable]
    public class LoRAListResponse
    {
        public LoRAFileInfo[] loras;
    }

    // ========== Pipeline Schemas ==========

    [Serializable]
    public class PipelineSchemaInfo
    {
        public string id;
        public string name;
        public string[] supported_modes;
        public string default_mode;
        public PipelineDefaults defaults;
    }

    [Serializable]
    public class PipelineDefaults
    {
        public int width;
        public int height;
        public float[] denoising_step_list;
    }

    [Serializable]
    public class PipelineSchemasResponse
    {
        public PipelineSchemaInfo[] pipelines;
    }

    // ========== Initial/Runtime Parameters ==========

    [Serializable]
    public class InitialParameters
    {
        public string input_mode = "video";
        public PromptItem[] prompts;
        public string prompt_interpolation_method = "slerp";
        public float[] denoising_step_list;
        public float noise_scale = 0.8f;
        public bool noise_controller = true;
        public bool manage_cache = true;
        public bool reset_cache = false;
        public float kv_cache_attention_bias = 0.5f;
        public LoRAScale[] lora_scales;
        public bool vace_enabled = false;
        public float vace_context_scale = 1.0f;
    }

    [Serializable]
    public class PromptItem
    {
        public string text;
        public float weight = 1.0f;
    }

    [Serializable]
    public class LoRAScale
    {
        public string path;
        public float scale;
    }

    // Runtime parameter update (sent via data channel)
    [Serializable]
    public class RuntimeParameters
    {
        public PromptItem[] prompts;
        public string prompt_interpolation_method;
        public PromptTransition transition;
        public float[] denoising_step_list;
        public float? noise_scale;
        public bool? noise_controller;
        public bool? manage_cache;
        public bool? reset_cache;
        public float? kv_cache_attention_bias;
        public LoRAScale[] lora_scales;
        public bool? paused;
        public float? vace_context_scale;
    }

    [Serializable]
    public class PromptTransition
    {
        public PromptItem[] target_prompts;
        public int num_steps = 4;
        public string temporal_interpolation_method = "slerp";
    }

    // ========== Health Check ==========

    [Serializable]
    public class HealthResponse
    {
        public string status;
        public string timestamp;
    }
}
