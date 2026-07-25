using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Dreamwalker.Models;

namespace Dreamwalker.Networking
{
    /// <summary>
    /// Available pipeline types with their default resolutions
    /// </summary>
    public enum PipelineType
    {
        Longlive,           // 576x320
        StreamDiffusionV2,  // 512x512
        KreaRealtimeVideo,  // 1024x576
        RewardForcing,      // 640x360
        Passthrough,        // Variable
        Memflow             // 640x360
    }

    /// <summary>
    /// Input mode for the pipeline
    /// </summary>
    public enum InputMode
    {
        Video,
        Text
    }

    /// <summary>
    /// HTTP client for Scope backend REST API with Inspector-configurable settings
    /// </summary>
    public class ScopeApiClient : MonoBehaviour
    {
        // ========== Serialized Inspector Fields ==========

        [Header("Server Configuration")]
        [SerializeField] private string serverUrl = "http://localhost:8000";

        [Header("Pipeline Settings")]
        [SerializeField] private PipelineType selectedPipeline = PipelineType.Longlive;
        [SerializeField] private int width = 576;
        [SerializeField] private int height = 320;
        [SerializeField] private int seed = 42;
        [SerializeField] private InputMode inputMode = InputMode.Video;

        [Header("Denoising")]
        [SerializeField, Range(1, 4)] private int denoisingStepCount = 4;

        [Header("Noise Control")]
        [SerializeField, Range(0f, 1f)] private float noiseScale = 0.8f;
        [SerializeField] private bool noiseControllerEnabled = true;

        [Header("Cache")]
        [SerializeField] private bool manageCacheEnabled = true;

        [Header("LoRA Settings")]
        [SerializeField] private List<LoRAConfig> loraConfigs = new List<LoRAConfig>();

        [Header("VACE Settings")]
        [SerializeField] private bool vaceEnabled = true;
        [SerializeField, Range(0f, 2f)] private float vaceContextScale = 1.0f;
        [SerializeField] private Texture2D vaceReferenceImage;

        [Header("Prompt")]
        [SerializeField, TextArea(2, 5)] private string currentPrompt = "A beautiful scene";
        [SerializeField, Range(0f, 2f)] private float promptWeight = 1.0f;

        [Header("Runtime Status")]
        [SerializeField, ReadOnly] private string connectionStatus = "Not Connected";
        [SerializeField, ReadOnly] private string pipelineStatus = "Not Loaded";
        [SerializeField, ReadOnly] private string[] availablePipelines = new string[0];
        [SerializeField, ReadOnly] private string[] availableLoras = new string[0];
        [SerializeField, ReadOnly] private float serverVramGb = 0f;

        // ========== Private Fields ==========
        private string baseUrl;

        // ========== Events ==========
        public event Action<string> OnStatusChanged;
        public event Action<string> OnPipelineStatusChanged;
        public event Action<string[]> OnPipelinesReceived;
        public event Action<string[]> OnLoRAsReceived;

        // ========== Public Properties ==========

        public string ServerUrl
        {
            get => serverUrl;
            set
            {
                serverUrl = value;
                SetServerUrl(value);
            }
        }

        public PipelineType SelectedPipeline
        {
            get => selectedPipeline;
            set
            {
                selectedPipeline = value;
                var (w, h) = GetDefaultResolution(value);
                width = w;
                height = h;
            }
        }

        public int Width { get => width; set => width = value; }
        public int Height { get => height; set => height = value; }
        public int Seed { get => seed; set => seed = value; }
        public InputMode CurrentInputMode { get => inputMode; set => inputMode = value; }
        public int DenoisingStepCount { get => denoisingStepCount; set => denoisingStepCount = Mathf.Clamp(value, 1, 4); }
        public float NoiseScale { get => noiseScale; set => noiseScale = Mathf.Clamp01(value); }
        public bool NoiseControllerEnabled { get => noiseControllerEnabled; set => noiseControllerEnabled = value; }
        public bool ManageCacheEnabled { get => manageCacheEnabled; set => manageCacheEnabled = value; }
        public List<LoRAConfig> LoraConfigs => loraConfigs;
        public bool VaceEnabled { get => vaceEnabled; set => vaceEnabled = value; }
        public float VaceContextScale { get => vaceContextScale; set => vaceContextScale = Mathf.Clamp(value, 0f, 2f); }
        public Texture2D VaceReferenceImage { get => vaceReferenceImage; set => vaceReferenceImage = value; }
        public string CurrentPrompt { get => currentPrompt; set => currentPrompt = value; }
        public float PromptWeight { get => promptWeight; set => promptWeight = Mathf.Clamp(value, 0f, 2f); }

        public string ConnectionStatus => connectionStatus;
        public string PipelineStatus => pipelineStatus;
        public string[] AvailablePipelines => availablePipelines;
        public string[] AvailableLoras => availableLoras;
        public float ServerVramGb => serverVramGb;
        public string BaseUrl => baseUrl;

        // ========== Constructors ==========

        /// <summary>
        /// Constructor for non-MonoBehaviour usage
        /// </summary>
        public ScopeApiClient() { }

        /// <summary>
        /// Constructor with server URL
        /// </summary>
        public ScopeApiClient(string serverUrl)
        {
            SetServerUrl(serverUrl);
        }

        // ========== Pipeline Helper Methods ==========

        /// <summary>
        /// Converts PipelineType enum to API string
        /// </summary>
        public string GetPipelineId()
        {
            return GetPipelineId(selectedPipeline);
        }

        /// <summary>
        /// Converts PipelineType enum to API string
        /// </summary>
        public static string GetPipelineId(PipelineType pipeline)
        {
            return pipeline switch
            {
                PipelineType.Longlive => "longlive",
                PipelineType.StreamDiffusionV2 => "streamdiffusionv2",
                PipelineType.KreaRealtimeVideo => "krea-realtime-video",
                PipelineType.RewardForcing => "reward-forcing",
                PipelineType.Passthrough => "passthrough",
                PipelineType.Memflow => "memflow",
                _ => "longlive"
            };
        }

        /// <summary>
        /// Converts API string to PipelineType enum
        /// </summary>
        public static PipelineType ParsePipelineId(string pipelineId)
        {
            return pipelineId?.ToLower() switch
            {
                "longlive" => PipelineType.Longlive,
                "streamdiffusionv2" => PipelineType.StreamDiffusionV2,
                "krea-realtime-video" => PipelineType.KreaRealtimeVideo,
                "reward-forcing" => PipelineType.RewardForcing,
                "passthrough" => PipelineType.Passthrough,
                "memflow" => PipelineType.Memflow,
                _ => PipelineType.Longlive
            };
        }

        /// <summary>
        /// Gets the default resolution for a pipeline type
        /// </summary>
        public static (int width, int height) GetDefaultResolution(PipelineType pipeline)
        {
            return pipeline switch
            {
                PipelineType.Longlive => (576, 320),
                PipelineType.StreamDiffusionV2 => (512, 512),
                PipelineType.KreaRealtimeVideo => (1024, 576),
                PipelineType.RewardForcing => (640, 360),
                PipelineType.Passthrough => (640, 360),
                PipelineType.Memflow => (640, 360),
                _ => (576, 320)
            };
        }

        /// <summary>
        /// Generates denoising steps array based on step count
        /// </summary>
        public float[] GenerateDenoisingSteps()
        {
            return GenerateDenoisingSteps(denoisingStepCount);
        }

        /// <summary>
        /// Generates denoising steps array based on step count
        /// </summary>
        public static float[] GenerateDenoisingSteps(int count)
        {
            return count switch
            {
                1 => new float[] { 1000f },
                2 => new float[] { 1000f, 500f },
                3 => new float[] { 1000f, 666f, 333f },
                4 => new float[] { 1000f, 750f, 500f, 250f },
                _ => new float[] { 1000f, 750f, 500f, 250f }
            };
        }

        // ========== StreamSettings Conversion ==========

        /// <summary>
        /// Converts Inspector values to StreamSettings object
        /// </summary>
        public StreamSettings ToStreamSettings()
        {
            return new StreamSettings
            {
                pipelineId = GetPipelineId(),
                width = width,
                height = height,
                seed = seed,
                inputMode = inputMode == InputMode.Video ? "video" : "text",
                denoisingSteps = GenerateDenoisingSteps(),
                noiseScale = noiseScale,
                noiseController = noiseControllerEnabled,
                manageCache = manageCacheEnabled,
                loras = new List<LoRAConfig>(loraConfigs),
                vaceEnabled = vaceEnabled,
                vaceContextScale = vaceContextScale,
                prompts = new List<PromptItem>
                {
                    new PromptItem { text = currentPrompt, weight = promptWeight }
                },
                promptInterpolationMethod = "slerp"
            };
        }

        /// <summary>
        /// Applies StreamSettings to Inspector fields
        /// </summary>
        public void ApplyStreamSettings(StreamSettings settings)
        {
            if (settings == null) return;

            selectedPipeline = ParsePipelineId(settings.pipelineId);
            width = settings.width;
            height = settings.height;
            seed = settings.seed;
            inputMode = settings.inputMode == "video" ? InputMode.Video : InputMode.Text;

            if (settings.denoisingSteps != null && settings.denoisingSteps.Length > 0)
            {
                denoisingStepCount = Mathf.Clamp(settings.denoisingSteps.Length, 1, 4);
            }

            noiseScale = settings.noiseScale;
            noiseControllerEnabled = settings.noiseController;
            manageCacheEnabled = settings.manageCache;

            if (settings.loras != null)
            {
                loraConfigs = new List<LoRAConfig>(settings.loras);
            }

            vaceEnabled = settings.vaceEnabled;
            vaceContextScale = settings.vaceContextScale;

            if (settings.prompts != null && settings.prompts.Count > 0)
            {
                currentPrompt = settings.prompts[0].text;
                promptWeight = settings.prompts[0].weight;
            }
        }

        /// <summary>
        /// Creates PipelineLoadRequest from current Inspector settings
        /// </summary>
        public PipelineLoadRequest ToPipelineLoadRequest()
        {
            return new PipelineLoadRequest
            {
                pipeline_id = GetPipelineId(),
                load_params = new PipelineLoadParams
                {
                    width = width,
                    height = height,
                    seed = seed,
                    vace_enabled = vaceEnabled,
                    loras = loraConfigs.Count > 0 ? loraConfigs.ToArray() : null,
                    lora_merge_mode = "permanent_merge"
                }
            };
        }

        // ========== Status Update Methods ==========

        /// <summary>
        /// Updates connection status (for Editor display)
        /// </summary>
        public void UpdateConnectionStatus(string status)
        {
            connectionStatus = status;
            OnStatusChanged?.Invoke(status);
        }

        /// <summary>
        /// Updates pipeline status (for Editor display)
        /// </summary>
        public void UpdatePipelineStatus(string status)
        {
            pipelineStatus = status;
            OnPipelineStatusChanged?.Invoke(status);
        }

        /// <summary>
        /// Sets available pipelines (for Editor dropdown)
        /// </summary>
        public void SetAvailablePipelines(string[] pipelines)
        {
            availablePipelines = pipelines ?? new string[0];
            OnPipelinesReceived?.Invoke(availablePipelines);
        }

        /// <summary>
        /// Sets available LoRAs (for Editor list)
        /// </summary>
        public void SetAvailableLoras(string[] loras)
        {
            availableLoras = loras ?? new string[0];
            OnLoRAsReceived?.Invoke(availableLoras);
        }

        /// <summary>
        /// Randomizes the seed
        /// </summary>
        public void RandomizeSeed()
        {
            seed = UnityEngine.Random.Range(0, int.MaxValue);
        }

        // ========== URL Configuration ==========

        /// <summary>
        /// Set the server URL (alias for SetBaseUrl)
        /// </summary>
        public void SetServerUrl(string url)
        {
            // Normalize URL - ensure http:// prefix and no trailing slash
            if (string.IsNullOrEmpty(url)) return;

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "http://" + url;
            }
            baseUrl = url.TrimEnd('/');
            serverUrl = baseUrl;
            Debug.Log($"[ScopeApiClient] Base URL set to: {baseUrl}");
        }

        /// <summary>
        /// Set the base URL for API calls
        /// </summary>
        public void SetBaseUrl(string url)
        {
            SetServerUrl(url);
        }

        // ========== Health Check ==========

        public IEnumerator CheckHealth(Action<bool, string> callback)
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                SetServerUrl(serverUrl);
            }

            using (var request = UnityWebRequest.Get($"{baseUrl}/health"))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    UpdateConnectionStatus("Connected");
                    callback?.Invoke(true, "Connected");
                }
                else
                {
                    UpdateConnectionStatus("Disconnected");
                    callback?.Invoke(false, request.error);
                }
            }
        }

        // ========== WebRTC Endpoints ==========

        public IEnumerator GetIceServers(Action<IceServersResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/webrtc/ice-servers"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<IceServersResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        public IEnumerator SendOffer(WebRTCOfferRequest offer, Action<WebRTCOfferResponse, string> callback)
        {
            string json = JsonUtility.ToJson(offer);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest($"{baseUrl}/api/v1/webrtc/offer", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<WebRTCOfferResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    Debug.LogError($"[ScopeApiClient] SendOffer failed: {request.error}\n{request.downloadHandler.text}");
                    callback?.Invoke(null, request.error);
                }
            }
        }

        public IEnumerator SendIceCandidates(string sessionId, IceCandidatesRequest candidates, Action<bool, string> callback)
        {
            string json = JsonUtility.ToJson(candidates);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest($"{baseUrl}/api/v1/webrtc/offer/{sessionId}", "PATCH"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 10;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success || request.responseCode == 204)
                {
                    callback?.Invoke(true, null);
                }
                else
                {
                    callback?.Invoke(false, request.error);
                }
            }
        }

        // ========== Pipeline Endpoints ==========

        public IEnumerator LoadPipeline(PipelineLoadRequest loadRequest, Action<bool, string> callback)
        {
            string json = JsonUtility.ToJson(loadRequest);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            UpdatePipelineStatus("Loading...");

            using (var request = new UnityWebRequest($"{baseUrl}/api/v1/pipeline/load", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(true, null);
                }
                else
                {
                    UpdatePipelineStatus("Error");
                    callback?.Invoke(false, request.error);
                }
            }
        }

        public IEnumerator GetPipelineStatus(Action<PipelineStatusResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/pipeline/status"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<PipelineStatusResponse>(request.downloadHandler.text);
                    UpdatePipelineStatus(response.status);
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        public IEnumerator GetPipelineSchemas(Action<PipelineSchemasResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/pipelines/schemas"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<PipelineSchemasResponse>(request.downloadHandler.text);

                    // Update available pipelines
                    if (response?.pipelines != null)
                    {
                        var pipelineIds = new string[response.pipelines.Length];
                        for (int i = 0; i < response.pipelines.Length; i++)
                        {
                            pipelineIds[i] = response.pipelines[i].id;
                        }
                        SetAvailablePipelines(pipelineIds);
                    }

                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        // ========== Hardware Info ==========

        public IEnumerator GetHardwareInfo(Action<HardwareInfoResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/hardware/info"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<HardwareInfoResponse>(request.downloadHandler.text);
                    serverVramGb = response.vram_gb;
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        // ========== Model Management ==========

        public IEnumerator GetModelStatus(string pipelineId, Action<ModelStatusResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/models/status?pipeline_id={pipelineId}"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<ModelStatusResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        public IEnumerator StartModelDownload(string pipelineId, Action<bool, string> callback)
        {
            var downloadRequest = new ModelDownloadRequest { pipeline_id = pipelineId };
            string json = JsonUtility.ToJson(downloadRequest);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest($"{baseUrl}/api/v1/models/download", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(true, null);
                }
                else
                {
                    callback?.Invoke(false, request.error);
                }
            }
        }

        // ========== LoRA ==========

        public IEnumerator GetLoRAList(Action<LoRAListResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/lora/list"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<LoRAListResponse>(request.downloadHandler.text);

                    // Update available LoRAs
                    if (response?.loras != null)
                    {
                        var loraNames = new string[response.loras.Length];
                        for (int i = 0; i < response.loras.Length; i++)
                        {
                            loraNames[i] = response.loras[i].name;
                        }
                        SetAvailableLoras(loraNames);
                    }

                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        // ========== Assets ==========

        /// <summary>
        /// Get list of available assets from server
        /// </summary>
        public IEnumerator GetAssets(string type, Action<AssetsResponse, string> callback)
        {
            string url = $"{baseUrl}/api/v1/assets";
            if (!string.IsNullOrEmpty(type))
            {
                url += $"?type={type}";
            }

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<AssetsResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        /// <summary>
        /// Upload an asset to the server
        /// </summary>
        public IEnumerator UploadAsset(byte[] data, string filename, Action<AssetUploadResponse, string> callback)
        {
            string encodedFilename = UnityWebRequest.EscapeURL(filename);
            string url = $"{baseUrl}/api/v1/assets?filename={encodedFilename}";

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(data);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/octet-stream");
                request.timeout = 60;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<AssetUploadResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        /// <summary>
        /// Upload a Texture2D as an asset
        /// </summary>
        public IEnumerator UploadTexture(Texture2D texture, string filename, Action<AssetUploadResponse, string> callback)
        {
            byte[] pngData = texture.EncodeToPNG();
            if (pngData == null)
            {
                callback?.Invoke(null, "Failed to encode texture to PNG");
                yield break;
            }

            yield return UploadAsset(pngData, filename, callback);
        }

        /// <summary>
        /// Upload a Texture2D as an asset, scaling it to match the current stream resolution
        /// </summary>
        public IEnumerator UploadTextureScaled(Texture2D texture, string filename, int targetWidth, int targetHeight, Action<AssetUploadResponse, string> callback)
        {
            if (texture == null)
            {
                callback?.Invoke(null, "Texture is null");
                yield break;
            }

            // Scale the texture to match stream resolution
            Texture2D scaledTexture = ScaleTexture(texture, targetWidth, targetHeight);
            Debug.Log($"[ScopeApiClient] Scaled reference image from {texture.width}x{texture.height} to {targetWidth}x{targetHeight}");

            byte[] pngData = scaledTexture.EncodeToPNG();

            // Clean up the temporary scaled texture
            if (scaledTexture != texture)
            {
                UnityEngine.Object.Destroy(scaledTexture);
            }

            if (pngData == null)
            {
                callback?.Invoke(null, "Failed to encode scaled texture to PNG");
                yield break;
            }

            yield return UploadAsset(pngData, filename, callback);
        }

        /// <summary>
        /// Scales a texture to the specified dimensions using GPU rendering
        /// </summary>
        private Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            // Create a temporary RenderTexture at the target size
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            // Store active render texture
            RenderTexture previous = RenderTexture.active;

            // Blit the source texture to the render texture (this scales it)
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            // Read the pixels from the render texture into a new Texture2D
            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            // Restore previous render texture and release temporary
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        // ========== Prompt Update Helpers ==========

        /// <summary>
        /// Creates RuntimeParameters for sending prompt update with optional VACE reference image path
        /// </summary>
        public RuntimeParameters ToPromptUpdateParameters(string uploadedImagePath = null)
        {
            return new RuntimeParameters
            {
                prompts = new PromptItem[]
                {
                    new PromptItem { text = currentPrompt, weight = promptWeight }
                },
                vace_ref_images = !string.IsNullOrEmpty(uploadedImagePath) ? new[] { uploadedImagePath } : null,
                vace_context_scale = vaceContextScale
            };
        }

        /// <summary>
        /// Creates RuntimeParameters for resetting cache
        /// </summary>
        public RuntimeParameters ToResetCacheParameters()
        {
            return new RuntimeParameters
            {
                reset_cache = true
            };
        }

        /// <summary>
        /// Check if a reference image is set
        /// </summary>
        public bool HasReferenceImage => vaceReferenceImage != null;
    }
}
