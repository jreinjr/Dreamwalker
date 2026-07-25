using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.WebRTC;
using Dreamwalker.Models;
using Oculus.Interaction;

namespace Dreamwalker.Networking
{
    /// <summary>
    /// Manages WebRTC connection to Scope backend with Inspector-configurable settings
    /// </summary>
    public class ScopeWebRTCManager : MonoBehaviour
    {
        public enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected,
            Failed
        }

        // ========== Serialized Inspector Fields ==========

        [Header("Dependencies")]
        [SerializeField] private ScopeApiClient apiClient;
        [SerializeField, Interface(typeof(ICameraCapture))] private UnityEngine.Object _cameraCapture;
        public ICameraCapture CameraCapture { get; private set; }

        [Header("Output")]
        [SerializeField] private UnityEngine.UI.RawImage outputDisplay;

        [Header("Connection Settings")]
        [SerializeField] private bool autoConnectOnStart = false;
        [SerializeField] private float connectionTimeout = 30f;

        [Header("Bitrate Settings")]
        [SerializeField] private int maxSendBitrateKbps = 5000;
        [SerializeField] private int minSendBitrateKbps = 1000;
        [SerializeField] private int maxReceiveBitrateKbps = 5000;
        [SerializeField] private int maxFramerate = 30;

        [Header("Runtime Status")]
        [SerializeField, ReadOnly] private ConnectionState currentState = ConnectionState.Disconnected;
        [SerializeField, ReadOnly] private string currentSessionId = "";
        [SerializeField, ReadOnly] private float displayFps = 0f;
        [SerializeField, ReadOnly] private float displayBitrateMbps = 0f;
        [SerializeField, ReadOnly] private int displayFramesSent = 0;
        [SerializeField, ReadOnly] private int displayFramesReceived = 0;
        [SerializeField, ReadOnly] private float connectionDuration = 0f;

        // ========== Private Fields ==========

        // Static flag to ensure WebRTC is initialized only once
        private static bool webRTCInitialized = false;

        // WebRTC state
        private RTCPeerConnection peerConnection;
        private RTCDataChannel dataChannel;
        private MediaStream localStream;
        private VideoStreamTrack videoTrack;
        private string sessionId;
        private ConnectionState connectionState = ConnectionState.Disconnected;
        private List<RTCIceCandidate> pendingCandidates = new List<RTCIceCandidate>();
        private bool candidatesSent = false;
        private Coroutine statsCoroutine;

        // Received video
        private Texture receivedVideoTexture;

        // Stats tracking
        private float currentFps = 0;
        private float currentBitrate = 0;
        private ulong lastBytesReceived = 0;
        private int lastFramesReceived = 0;
        private int framesSentCount = 0;
        private float connectionStartTime = 0f;

        // Initialization state
        private RenderTexture activeSourceTexture;
        private StreamSettings currentSettings;
        private bool isInitialized = false;

        // Frame logging
        private int receivedFrameCount = 0;
        private float lastFrameLogTime = 0f;

        // ========== Events ==========

        public event Action<ConnectionState> OnConnectionStateChanged;
        public event Action<Texture> OnVideoReceived;
        public event Action<Texture2D> OnVideoFrameReceived;
        public event Action<string> OnError;
        public event Action<float, float> OnStatsUpdated; // fps, bitrate
        public event Action OnConnected;
        public event Action OnDisconnected;

        // ========== Public Properties ==========

        public ScopeApiClient ApiClient => apiClient;
        public ConnectionState State => connectionState;
        public Texture ReceivedVideo => receivedVideoTexture;
        public bool IsConnected => connectionState == ConnectionState.Connected;

        public float CurrentFps => currentFps;
        public float CurrentBitrate => currentBitrate;
        public int FramesSent => framesSentCount;
        public int FramesReceived => receivedFrameCount;
        public float ConnectionDuration => connectionState == ConnectionState.Connected ? Time.time - connectionStartTime : 0f;
        public string SessionId => sessionId;

        // ========== Stats Data Class ==========

        /// <summary>
        /// Stats data for display
        /// </summary>
        public class WebRTCStats
        {
            public float fps;
            public float bitrateMbps;
        }

        // ========== Unity Lifecycle ==========

        private void Awake()
        {
            CameraCapture = _cameraCapture as ICameraCapture;

            // Start the WebRTC update loop - this is CRITICAL for video encoding to work!
            if (!webRTCInitialized)
            {
                StartCoroutine(WebRTC.Update());
                webRTCInitialized = true;
                Debug.Log("[WebRTC] WebRTC.Update() coroutine started");
            }
        }

        private void Start()
        {
            if (autoConnectOnStart)
            {
                StartCoroutine(AutoConnectCoroutine());
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void Update()
        {
            // Update display stats
            if (connectionState == ConnectionState.Connected)
            {
                displayFps = currentFps;
                displayBitrateMbps = currentBitrate;
                displayFramesReceived = receivedFrameCount;
                connectionDuration = Time.time - connectionStartTime;
            }
        }

        private IEnumerator AutoConnectCoroutine()
        {
            yield return new WaitForSeconds(2f);
            yield return Connect();
        }

        // ========== Initialization ==========

        /// <summary>
        /// Initialize the WebRTC manager with required dependencies.
        /// Call this before Connect() when using from scripts.
        /// </summary>
        public void Initialize(ScopeApiClient client, RenderTexture cameraTexture, StreamSettings settings)
        {
            apiClient = client;
            activeSourceTexture = cameraTexture;
            currentSettings = settings;
            isInitialized = true;
        }

        // ========== Connection Methods ==========

        /// <summary>
        /// Connects to the Scope server.
        /// If not initialized via Initialize(), uses Inspector-configured settings from ApiClient.
        /// </summary>
        public IEnumerator Connect()
        {
            // If not initialized via Initialize(), use Inspector settings
            if (!isInitialized)
            {
                // Get or find API client
                if (apiClient == null)
                {
                    apiClient = GetComponent<ScopeApiClient>();
                    if (apiClient == null)
                    {
                        OnError?.Invoke("No ScopeApiClient assigned or found on GameObject");
                        yield break;
                    }
                }

                // Ensure API client has its URL configured
                if (string.IsNullOrEmpty(apiClient.BaseUrl))
                {
                    apiClient.SetServerUrl(apiClient.ServerUrl);
                }

                // Get source texture from camera capture
                if (CameraCapture != null)
                {
                    activeSourceTexture = CameraCapture.CroppedTexture;
                }

                // Build settings from API client's Inspector fields
                currentSettings = apiClient.ToStreamSettings();

                // Upload reference image if set, before connecting
                if (apiClient.HasReferenceImage)
                {
                    Debug.Log("[WebRTC] Uploading VACE reference image before connection...");
                    string uploadedPath = null;
                    string uploadError = null;
                    string filename = $"vace_ref_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";

                    yield return apiClient.UploadTexture(apiClient.VaceReferenceImage, filename, (response, error) =>
                    {
                        if (response != null)
                        {
                            uploadedPath = response.path;
                            Debug.Log($"[WebRTC] Reference image uploaded to: {uploadedPath}");
                        }
                        else
                        {
                            uploadError = error;
                        }
                    });

                    if (!string.IsNullOrEmpty(uploadedPath))
                    {
                        currentSettings.vaceReferenceImages.Add(uploadedPath);
                    }
                    else if (uploadError != null)
                    {
                        Debug.LogWarning($"[WebRTC] Failed to upload reference image: {uploadError}");
                    }
                }
            }

            // Validate we have what we need
            if (apiClient == null)
            {
                OnError?.Invoke("No ScopeApiClient available");
                yield break;
            }

            if (activeSourceTexture == null && CameraCapture != null)
            {
                activeSourceTexture = CameraCapture.CroppedTexture;
            }

            if (activeSourceTexture == null)
            {
                OnError?.Invoke("No camera capture or source texture available");
                yield break;
            }

            if (currentSettings == null)
            {
                currentSettings = apiClient.ToStreamSettings();
            }

            if (connectionState == ConnectionState.Connecting || connectionState == ConnectionState.Connected)
            {
                Debug.LogWarning("[WebRTC] Already connecting or connected");
                yield break;
            }

            yield return ConnectCoroutine(currentSettings);
        }

        /// <summary>
        /// Connects to the Scope server (legacy method with parameters)
        /// </summary>
        public void Connect(string serverUrl, StreamSettings settings)
        {
            if (connectionState == ConnectionState.Connecting || connectionState == ConnectionState.Connected)
            {
                Debug.LogWarning("[WebRTC] Already connecting or connected");
                return;
            }

            // Create or get API client
            if (apiClient == null)
            {
                apiClient = gameObject.AddComponent<ScopeApiClient>();
            }
            apiClient.SetServerUrl(serverUrl);
            currentSettings = settings;
            isInitialized = true;
            StartCoroutine(ConnectCoroutine(settings));
        }

        /// <summary>
        /// Send updated parameters to the server
        /// </summary>
        public void SendParameters(StreamSettings settings)
        {
            if (settings == null) return;

            currentSettings = settings;
            var parameters = new RuntimeParameters
            {
                prompts = settings.prompts?.ToArray() ?? new PromptItem[0],
                prompt_interpolation_method = settings.promptInterpolationMethod,
                denoising_step_list = settings.denoisingSteps,
                noise_scale = settings.noiseScale,
                noise_controller = settings.noiseController,
                manage_cache = settings.manageCache,
                paused = settings.paused
            };

            SendParameterUpdate(parameters);
        }

        /// <summary>
        /// Get current stats for display
        /// </summary>
        public WebRTCStats GetStats()
        {
            return new WebRTCStats
            {
                fps = currentFps,
                bitrateMbps = currentBitrate
            };
        }

        /// <summary>
        /// Disconnects from the server
        /// </summary>
        public void Disconnect()
        {
            if (statsCoroutine != null)
            {
                StopCoroutine(statsCoroutine);
                statsCoroutine = null;
            }

            if (dataChannel != null)
            {
                dataChannel.Close();
                dataChannel = null;
            }

            if (peerConnection != null)
            {
                peerConnection.Close();
                peerConnection.Dispose();
                peerConnection = null;
            }

            if (videoTrack != null)
            {
                videoTrack.Dispose();
                videoTrack = null;
            }

            if (localStream != null)
            {
                localStream.Dispose();
                localStream = null;
            }

            sessionId = null;
            currentSessionId = "";
            pendingCandidates.Clear();
            candidatesSent = false;
            receivedVideoTexture = null;
            isInitialized = false;

            // Reset display stats
            displayFps = 0f;
            displayBitrateMbps = 0f;
            displayFramesSent = 0;
            displayFramesReceived = 0;
            connectionDuration = 0f;

            SetConnectionState(ConnectionState.Disconnected);
        }

        /// <summary>
        /// Sends parameter update over data channel
        /// </summary>
        public void SendParameterUpdate(RuntimeParameters parameters)
        {
            if (dataChannel == null || dataChannel.ReadyState != RTCDataChannelState.Open)
            {
                Debug.LogWarning("[WebRTC] Data channel not ready");
                return;
            }

            string json = SerializeParametersFiltered(parameters);
            dataChannel.Send(json);
            Debug.Log($"[WebRTC] Sent parameter update: {json}");
        }

        /// <summary>
        /// Serializes RuntimeParameters to JSON, excluding null/empty values (matches frontend behavior)
        /// </summary>
        private string SerializeParametersFiltered(RuntimeParameters p)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (p.prompts != null && p.prompts.Length > 0)
            {
                var promptsJson = new System.Collections.Generic.List<string>();
                foreach (var prompt in p.prompts)
                {
                    promptsJson.Add($"{{\"text\":\"{EscapeJson(prompt.text)}\",\"weight\":{prompt.weight}}}");
                }
                parts.Add($"\"prompts\":[{string.Join(",", promptsJson)}]");
            }

            if (!string.IsNullOrEmpty(p.prompt_interpolation_method))
                parts.Add($"\"prompt_interpolation_method\":\"{p.prompt_interpolation_method}\"");

            if (p.transition != null)
                parts.Add($"\"transition\":{JsonUtility.ToJson(p.transition)}");

            if (p.denoising_step_list != null && p.denoising_step_list.Length > 0)
                parts.Add($"\"denoising_step_list\":[{string.Join(",", p.denoising_step_list)}]");

            if (p.noise_scale.HasValue)
                parts.Add($"\"noise_scale\":{p.noise_scale.Value}");

            if (p.noise_controller.HasValue)
                parts.Add($"\"noise_controller\":{(p.noise_controller.Value ? "true" : "false")}");

            if (p.manage_cache.HasValue)
                parts.Add($"\"manage_cache\":{(p.manage_cache.Value ? "true" : "false")}");

            if (p.reset_cache.HasValue)
                parts.Add($"\"reset_cache\":{(p.reset_cache.Value ? "true" : "false")}");

            if (p.kv_cache_attention_bias.HasValue)
                parts.Add($"\"kv_cache_attention_bias\":{p.kv_cache_attention_bias.Value}");

            if (p.lora_scales != null && p.lora_scales.Length > 0)
            {
                var loraJson = new System.Collections.Generic.List<string>();
                foreach (var lora in p.lora_scales)
                {
                    loraJson.Add($"{{\"path\":\"{EscapeJson(lora.path)}\",\"scale\":{lora.scale}}}");
                }
                parts.Add($"\"lora_scales\":[{string.Join(",", loraJson)}]");
            }

            if (p.paused.HasValue)
                parts.Add($"\"paused\":{(p.paused.Value ? "true" : "false")}");

            // Only include vace_ref_images if it has values (matches frontend behavior)
            if (p.vace_ref_images != null && p.vace_ref_images.Length > 0)
            {
                var escaped = new System.Collections.Generic.List<string>();
                foreach (var img in p.vace_ref_images)
                {
                    escaped.Add($"\"{EscapeJson(img)}\"");
                }
                parts.Add($"\"vace_ref_images\":[{string.Join(",", escaped)}]");
            }

            if (p.vace_context_scale.HasValue)
                parts.Add($"\"vace_context_scale\":{p.vace_context_scale.Value}");

            return "{" + string.Join(",", parts) + "}";
        }

        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        /// <summary>
        /// Modifies SDP to add bandwidth constraint for video
        /// </summary>
        private string ModifySdpBandwidth(string sdp, int maxBitrateKbps)
        {
            if (string.IsNullOrEmpty(sdp) || maxBitrateKbps <= 0)
                return sdp;

            var lines = sdp.Split('\n').ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith("m=video"))
                {
                    // Insert b=AS (Application Specific) bandwidth limit after m=video line
                    lines.Insert(i + 1, $"b=AS:{maxBitrateKbps}");
                    Debug.Log($"[WebRTC] Added bandwidth constraint to SDP: {maxBitrateKbps} kbps");
                    break;
                }
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Sends reset_cache command to backend to reset pipeline state
        /// </summary>
        public void SendResetCache()
        {
            if (dataChannel == null || dataChannel.ReadyState != RTCDataChannelState.Open)
            {
                Debug.LogWarning("[WebRTC] Data channel not ready for reset");
                return;
            }

            var resetParams = new RuntimeParameters
            {
                reset_cache = true
            };

            string json = JsonUtility.ToJson(resetParams);
            dataChannel.Send(json);
            Debug.Log($"[WebRTC] Sent reset_cache command: {json}");
        }

        // ========== Private Connection Logic ==========

        private IEnumerator ConnectCoroutine(StreamSettings settings)
        {
            SetConnectionState(ConnectionState.Connecting);
            connectionStartTime = Time.time;

            // Step 1: Check server health
            bool healthOk = false;
            string healthError = null;
            yield return apiClient.CheckHealth((ok, error) =>
            {
                healthOk = ok;
                healthError = error;
            });

            if (!healthOk)
            {
                OnError?.Invoke($"Server not reachable: {healthError}");
                SetConnectionState(ConnectionState.Failed);
                yield break;
            }

            Debug.Log("[WebRTC] Server health check passed");

            // Step 2: Load pipeline with Inspector settings
            Debug.Log("[WebRTC] Loading pipeline with Inspector settings...");
            var loadRequest = apiClient.ToPipelineLoadRequest();
            Debug.Log($"[WebRTC] Pipeline load request: {loadRequest.pipeline_id}, vace_enabled={loadRequest.load_params.vace_enabled}");

            bool loadOk = false;
            string loadError = null;
            yield return apiClient.LoadPipeline(loadRequest, (success, error) =>
            {
                loadOk = success;
                loadError = error;
            });

            if (!loadOk)
            {
                OnError?.Invoke($"Pipeline load failed: {loadError}");
                SetConnectionState(ConnectionState.Failed);
                yield break;
            }

            // Step 3: Wait for pipeline to be ready
            Debug.Log("[WebRTC] Waiting for pipeline to load...");
            bool pipelineReady = false;
            string pipelineError = null;
            float pipelineTimeout = 120f; // 2 minute timeout for pipeline loading
            float pipelineElapsed = 0f;

            while (!pipelineReady && pipelineError == null && pipelineElapsed < pipelineTimeout)
            {
                yield return apiClient.GetPipelineStatus((status, error) =>
                {
                    if (error != null)
                    {
                        pipelineError = error;
                        return;
                    }

                    if (status.status == "loaded")
                    {
                        pipelineReady = true;
                        Debug.Log("[WebRTC] Pipeline loaded successfully");
                    }
                    else if (status.status == "error")
                    {
                        pipelineError = status.error ?? "Pipeline loading failed";
                    }
                    else
                    {
                        Debug.Log($"[WebRTC] Pipeline status: {status.status}");
                    }
                });

                if (!pipelineReady && pipelineError == null)
                {
                    yield return new WaitForSeconds(0.5f);
                    pipelineElapsed += 0.5f;
                }
            }

            if (pipelineError != null)
            {
                OnError?.Invoke($"Pipeline error: {pipelineError}");
                SetConnectionState(ConnectionState.Failed);
                yield break;
            }

            if (!pipelineReady)
            {
                OnError?.Invoke("Pipeline loading timed out");
                SetConnectionState(ConnectionState.Failed);
                yield break;
            }

            // Step 4: Get ICE servers
            IceServersResponse iceServers = null;
            yield return apiClient.GetIceServers((response, error) =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"[WebRTC] Failed to get ICE servers: {error}, using default");
                }
                iceServers = response;
            });

            // Step 5: Create peer connection
            var config = new RTCConfiguration();
            if (iceServers?.iceServers != null && iceServers.iceServers.Length > 0)
            {
                config.iceServers = new RTCIceServer[iceServers.iceServers.Length];
                for (int i = 0; i < iceServers.iceServers.Length; i++)
                {
                    config.iceServers[i] = new RTCIceServer
                    {
                        urls = iceServers.iceServers[i].urls,
                        username = iceServers.iceServers[i].username,
                        credential = iceServers.iceServers[i].credential
                    };
                }
            }
            else
            {
                // Fallback to Google STUN
                config.iceServers = new RTCIceServer[]
                {
                    new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
                };
            }

            peerConnection = new RTCPeerConnection(ref config);
            Debug.Log("[WebRTC] Peer connection created");

            // Set up event handlers
            peerConnection.OnIceCandidate = OnIceCandidate;
            peerConnection.OnIceConnectionChange = OnIceConnectionChange;
            peerConnection.OnTrack = OnTrack;
            peerConnection.OnConnectionStateChange = OnPeerConnectionStateChange;

            // Step 6: Create data channel for parameter updates
            var dataChannelInit = new RTCDataChannelInit { ordered = true };
            dataChannel = peerConnection.CreateDataChannel("parameters", dataChannelInit);
            dataChannel.OnOpen = () => Debug.Log("[WebRTC] Data channel opened");
            dataChannel.OnClose = () => Debug.Log("[WebRTC] Data channel closed");
            dataChannel.OnMessage = OnDataChannelMessage;
            Debug.Log("[WebRTC] Data channel created");

            // Step 7: Add local video track using MediaStream
            RenderTexture videoSource = activeSourceTexture;
            if (videoSource == null && CameraCapture != null)
            {
                videoSource = CameraCapture.CroppedTexture;
            }

            if (videoSource != null)
            {
                Debug.Log($"[WebRTC] Creating VideoStreamTrack from RenderTexture: {videoSource.width}x{videoSource.height}, format={videoSource.format}, isCreated={videoSource.IsCreated()}");

                // Create video track from RenderTexture
                videoTrack = new VideoStreamTrack(videoSource);

                Debug.Log($"[WebRTC] VideoStreamTrack created: kind={videoTrack.Kind}, id={videoTrack.Id}, enabled={videoTrack.Enabled}, readyState={videoTrack.ReadyState}");

                // Ensure track is enabled
                if (!videoTrack.Enabled)
                {
                    videoTrack.Enabled = true;
                    Debug.Log("[WebRTC] Enabled video track");
                }

                // Create MediaStream and add track
                localStream = new MediaStream();
                localStream.AddTrack(videoTrack);

                // Add track to peer connection with MediaStream association
                var sender = peerConnection.AddTrack(videoTrack, localStream);
                Debug.Log($"[WebRTC] Track added to peer connection, sender={sender != null}");

                // Configure send bitrate
                if (sender != null)
                {
                    var parameters = sender.GetParameters();
                    foreach (var encoding in parameters.encodings)
                    {
                        encoding.maxBitrate = (ulong)(maxSendBitrateKbps * 1000);
                        encoding.minBitrate = (ulong)(minSendBitrateKbps * 1000);
                        encoding.maxFramerate = (uint)maxFramerate;
                    }
                    var error = sender.SetParameters(parameters);
                    if (error.errorType == RTCErrorType.None)
                    {
                        Debug.Log($"[WebRTC] Send bitrate configured: {minSendBitrateKbps}-{maxSendBitrateKbps} kbps, {maxFramerate} fps");
                    }
                    else
                    {
                        Debug.LogWarning($"[WebRTC] Failed to set send parameters: {error.errorType} - {error.message}");
                    }
                }

                // Find the transceiver for this sender to set codec preferences
                RTCRtpTransceiver transceiver = null;
                foreach (var t in peerConnection.GetTransceivers())
                {
                    if (t.Sender == sender)
                    {
                        transceiver = t;
                        break;
                    }
                }

                if (transceiver != null)
                {
                    // Ensure bidirectional video
                    transceiver.Direction = RTCRtpTransceiverDirection.SendRecv;
                    Debug.Log($"[WebRTC] Transceiver direction set to SendRecv");

                    // Set VP8 codec preference (required by aiortc)
                    var codecs = RTCRtpSender.GetCapabilities(TrackKind.Video).codecs;
                    var vp8Codecs = new List<RTCRtpCodecCapability>();
                    foreach (var codec in codecs)
                    {
                        if (codec.mimeType.ToLower().Contains("vp8"))
                        {
                            vp8Codecs.Add(codec);
                        }
                    }
                    if (vp8Codecs.Count > 0)
                    {
                        transceiver.SetCodecPreferences(vp8Codecs.ToArray());
                        Debug.Log($"[WebRTC] VP8 codec set as preference ({vp8Codecs.Count} codecs available)");
                    }
                    else
                    {
                        Debug.LogWarning("[WebRTC] No VP8 codecs available!");
                    }
                }
                else
                {
                    Debug.LogWarning("[WebRTC] Could not find transceiver for video sender");
                }

                Debug.Log($"[WebRTC] Video track added via AddTrack with MediaStream (texture: {videoSource.width}x{videoSource.height})");
            }
            else
            {
                Debug.LogWarning("[WebRTC] No camera texture available");
            }

            // Step 8: Create offer
            var offerOp = peerConnection.CreateOffer();
            yield return offerOp;

            if (offerOp.IsError)
            {
                OnError?.Invoke($"Failed to create offer: {offerOp.Error.message}");
                SetConnectionState(ConnectionState.Failed);
                yield break;
            }

            var offer = offerOp.Desc;
            Debug.Log($"[WebRTC] Offer created: {offer.sdp.Substring(0, Math.Min(200, offer.sdp.Length))}...");

            // Set local description
            var setLocalOp = peerConnection.SetLocalDescription(ref offer);
            yield return setLocalOp;

            if (setLocalOp.IsError)
            {
                OnError?.Invoke($"Failed to set local description: {setLocalOp.Error.message}");
                SetConnectionState(ConnectionState.Failed);
                yield break;
            }

            Debug.Log("[WebRTC] Local description set");

            // Step 9: Send offer to server
            var initialParams = settings.ToInitialParameters();
            Debug.Log($"[WebRTC] Initial parameters: input_mode={initialParams.input_mode}, pipeline={settings.pipelineId}, prompts={initialParams.prompts?.Length ?? 0}");

            var offerRequest = new WebRTCOfferRequest
            {
                sdp = offer.sdp,
                type = "offer",
                initialParameters = initialParams
            };

            WebRTCOfferResponse answerResponse = null;
            string offerError = null;
            yield return apiClient.SendOffer(offerRequest, (response, error) =>
            {
                answerResponse = response;
                offerError = error;
            });

            if (offerError != null || answerResponse == null)
            {
                OnError?.Invoke($"Failed to send offer: {offerError}");
                SetConnectionState(ConnectionState.Failed);
                yield break;
            }

            sessionId = answerResponse.sessionId;
            currentSessionId = sessionId;
            Debug.Log($"[WebRTC] Received answer, sessionId: {sessionId}");

            // Step 10: Set remote description (with bandwidth constraint for receive)
            var answer = new RTCSessionDescription
            {
                type = RTCSdpType.Answer,
                sdp = ModifySdpBandwidth(answerResponse.sdp, maxReceiveBitrateKbps)
            };

            var setRemoteOp = peerConnection.SetRemoteDescription(ref answer);
            yield return setRemoteOp;

            if (setRemoteOp.IsError)
            {
                OnError?.Invoke($"Failed to set remote description: {setRemoteOp.Error.message}");
                SetConnectionState(ConnectionState.Failed);
                yield break;
            }

            Debug.Log("[WebRTC] Remote description set");

            // Step 11: Send any pending ICE candidates
            yield return SendPendingCandidates();

            // Start stats collection
            statsCoroutine = StartCoroutine(CollectStats());

            // Log transceiver state and manually subscribe to receiver tracks
            if (peerConnection != null)
            {
                var transceivers = peerConnection.GetTransceivers();
                Debug.Log($"[WebRTC] Connection established. Transceivers count: {transceivers.Count()}");
                foreach (var t in transceivers)
                {
                    Debug.Log($"[WebRTC] Transceiver: direction={t.Direction}, currentDirection={t.CurrentDirection}, mid={t.Mid}");
                    if (t.Receiver?.Track != null)
                    {
                        var track = t.Receiver.Track;
                        Debug.Log($"[WebRTC]   Receiver track: kind={track.Kind}, readyState={track.ReadyState}, enabled={track.Enabled}, id={track.Id}");

                        // Manually subscribe to video receiver track
                        if (track is VideoStreamTrack receiverVideoTrack)
                        {
                            Debug.Log($"[WebRTC] Manually subscribing to receiver video track (Enabled={receiverVideoTrack.Enabled})");

                            if (!receiverVideoTrack.Enabled)
                            {
                                receiverVideoTrack.Enabled = true;
                                Debug.Log($"[WebRTC] Enabled receiver video track");
                            }

                            receivedFrameCount = 0;

                            receiverVideoTrack.OnVideoReceived += tex =>
                            {
                                receivedFrameCount++;
                                receivedVideoTexture = tex;

                                // Update Inspector-assigned displays
                                UpdateOutputDisplays(tex);

                                OnVideoReceived?.Invoke(tex);

                                if (Time.time - lastFrameLogTime > 1.0f)
                                {
                                    Debug.Log($"[WebRTC] Receiving video: {tex.width}x{tex.height}, frames received: {receivedFrameCount}");
                                    lastFrameLogTime = Time.time;
                                }

                                if (tex is Texture2D tex2D)
                                {
                                    OnVideoFrameReceived?.Invoke(tex2D);
                                }
                            };

                            Debug.Log($"[WebRTC] Video receiver callback registered on transceiver receiver track");
                        }
                        else
                        {
                            Debug.LogWarning($"[WebRTC] Receiver track is not VideoStreamTrack, type: {track.GetType().Name}");
                        }
                    }
                }
            }

            // Start a coroutine to periodically check receiver track state
            StartCoroutine(MonitorReceiverTrack());
        }

        private IEnumerator MonitorReceiverTrack()
        {
            yield return new WaitForSeconds(2f);

            for (int i = 0; i < 10; i++)
            {
                if (peerConnection == null) yield break;

                foreach (var t in peerConnection.GetTransceivers())
                {
                    if (t.Receiver?.Track is VideoStreamTrack vst)
                    {
                        Debug.Log($"[WebRTC] Monitor: Receiver track state - readyState={vst.ReadyState}, enabled={vst.Enabled}, receivedFrames={receivedFrameCount}");
                    }
                    if (t.Sender?.Track is VideoStreamTrack senderTrack)
                    {
                        Debug.Log($"[WebRTC] Monitor: Sender track state - readyState={senderTrack.ReadyState}, enabled={senderTrack.Enabled}");
                    }
                }

                // Check stats for both inbound and outbound video
                var statsOp = peerConnection.GetStats();
                yield return statsOp;

                if (!statsOp.IsError)
                {
                    foreach (var stat in statsOp.Value.Stats.Values)
                    {
                        if (stat is RTCInboundRTPStreamStats inbound && inbound.kind == "video")
                        {
                            Debug.Log($"[WebRTC] Monitor: INBOUND video - framesReceived={inbound.framesReceived}, bytesReceived={inbound.bytesReceived}, framesDecoded={inbound.framesDecoded}");
                        }
                        if (stat is RTCOutboundRTPStreamStats outbound && outbound.kind == "video")
                        {
                            framesSentCount = (int)outbound.framesSent;
                            displayFramesSent = framesSentCount;
                            Debug.Log($"[WebRTC] Monitor: OUTBOUND video - framesSent={outbound.framesSent}, bytesSent={outbound.bytesSent}, framesEncoded={outbound.framesEncoded}");
                        }
                    }
                }

                if (activeSourceTexture != null)
                {
                    Debug.Log($"[WebRTC] Monitor: Source texture - {activeSourceTexture.width}x{activeSourceTexture.height}, isCreated={activeSourceTexture.IsCreated()}");
                }

                yield return new WaitForSeconds(2f);
            }
        }

        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            Debug.Log($"[WebRTC] ICE candidate: {candidate.Candidate}");

            if (string.IsNullOrEmpty(sessionId))
            {
                pendingCandidates.Add(candidate);
            }
            else if (!candidatesSent)
            {
                pendingCandidates.Add(candidate);
            }
            else
            {
                StartCoroutine(SendSingleCandidate(candidate));
            }
        }

        private IEnumerator SendPendingCandidates()
        {
            if (pendingCandidates.Count == 0 || string.IsNullOrEmpty(sessionId))
            {
                candidatesSent = true;
                yield break;
            }

            var candidates = new IceCandidate[pendingCandidates.Count];
            for (int i = 0; i < pendingCandidates.Count; i++)
            {
                candidates[i] = new IceCandidate
                {
                    candidate = pendingCandidates[i].Candidate,
                    sdpMid = pendingCandidates[i].SdpMid,
                    sdpMLineIndex = pendingCandidates[i].SdpMLineIndex ?? 0
                };
            }

            var request = new IceCandidatesRequest { candidates = candidates };

            yield return apiClient.SendIceCandidates(sessionId, request, (success, error) =>
            {
                if (success)
                {
                    Debug.Log($"[WebRTC] Sent {candidates.Length} ICE candidates");
                }
                else
                {
                    Debug.LogWarning($"[WebRTC] Failed to send ICE candidates: {error}");
                }
            });

            pendingCandidates.Clear();
            candidatesSent = true;
        }

        private IEnumerator SendSingleCandidate(RTCIceCandidate candidate)
        {
            var request = new IceCandidatesRequest
            {
                candidates = new[]
                {
                    new IceCandidate
                    {
                        candidate = candidate.Candidate,
                        sdpMid = candidate.SdpMid,
                        sdpMLineIndex = candidate.SdpMLineIndex ?? 0
                    }
                }
            };

            yield return apiClient.SendIceCandidates(sessionId, request, (success, error) =>
            {
                if (!success)
                {
                    Debug.LogWarning($"[WebRTC] Failed to send ICE candidate: {error}");
                }
            });
        }

        private void OnIceConnectionChange(RTCIceConnectionState state)
        {
            Debug.Log($"[WebRTC] ICE connection state: {state}");
        }

        private void OnPeerConnectionStateChange(RTCPeerConnectionState state)
        {
            Debug.Log($"[WebRTC] Peer connection state: {state}");

            switch (state)
            {
                case RTCPeerConnectionState.Connected:
                    SetConnectionState(ConnectionState.Connected);
                    break;
                case RTCPeerConnectionState.Failed:
                    OnError?.Invoke("Connection failed");
                    SetConnectionState(ConnectionState.Failed);
                    break;
                case RTCPeerConnectionState.Disconnected:
                case RTCPeerConnectionState.Closed:
                    SetConnectionState(ConnectionState.Disconnected);
                    break;
            }
        }

        private void OnTrack(RTCTrackEvent e)
        {
            Debug.Log($"[WebRTC] *** TRACK RECEIVED *** kind={e.Track.Kind}, id={e.Track.Id}, readyState={e.Track.ReadyState}");

            if (e.Track is VideoStreamTrack videoStreamTrack)
            {
                Debug.Log($"[WebRTC] Setting up video track receiver for VideoStreamTrack");
                receivedFrameCount = 0;

                videoStreamTrack.OnVideoReceived += tex =>
                {
                    receivedFrameCount++;
                    receivedVideoTexture = tex;

                    // Update Inspector-assigned displays
                    UpdateOutputDisplays(tex);

                    OnVideoReceived?.Invoke(tex);

                    if (Time.time - lastFrameLogTime > 1.0f)
                    {
                        Debug.Log($"[WebRTC] Receiving video: {tex.width}x{tex.height}, frames received: {receivedFrameCount}");
                        lastFrameLogTime = Time.time;
                    }

                    if (tex is Texture2D tex2D)
                    {
                        OnVideoFrameReceived?.Invoke(tex2D);
                    }
                };

                Debug.Log($"[WebRTC] OnVideoReceived callback registered");
            }
            else
            {
                Debug.LogWarning($"[WebRTC] Received non-video track: {e.Track.GetType().Name}");
            }
        }

        private void OnDataChannelMessage(byte[] data)
        {
            string message = System.Text.Encoding.UTF8.GetString(data);
            Debug.Log($"[WebRTC] Data channel message: {message}");

            if (message.Contains("stream_stopped"))
            {
                OnError?.Invoke("Stream stopped by server");
                Disconnect();
            }
        }

        /// <summary>
        /// Updates the Inspector-assigned output display with the received texture
        /// </summary>
        private void UpdateOutputDisplays(Texture tex)
        {
            if (outputDisplay != null)
            {
                outputDisplay.texture = tex;
            }
        }

        private void SetConnectionState(ConnectionState state)
        {
            if (connectionState != state)
            {
                var previousState = connectionState;
                connectionState = state;
                currentState = state;
                OnConnectionStateChanged?.Invoke(state);
                Debug.Log($"[WebRTC] Connection state changed to: {state}");

                if (state == ConnectionState.Connected && previousState != ConnectionState.Connected)
                {
                    OnConnected?.Invoke();
                }
                else if (state == ConnectionState.Disconnected && previousState == ConnectionState.Connected)
                {
                    OnDisconnected?.Invoke();
                }
            }
        }

        private IEnumerator CollectStats()
        {
            float lastStatsTime = Time.time;

            while (peerConnection != null && connectionState == ConnectionState.Connected)
            {
                yield return new WaitForSeconds(1f);

                if (peerConnection == null) yield break;

                var statsOp = peerConnection.GetStats();
                yield return statsOp;

                if (statsOp.IsError) continue;

                float timeDelta = Time.time - lastStatsTime;
                lastStatsTime = Time.time;

                foreach (var stat in statsOp.Value.Stats.Values)
                {
                    if (stat is RTCInboundRTPStreamStats inbound)
                    {
                        // Calculate FPS from frames delta
                        int framesDelta = (int)inbound.framesReceived - lastFramesReceived;
                        lastFramesReceived = (int)inbound.framesReceived;
                        currentFps = framesDelta / timeDelta;

                        // Calculate bitrate from bytes delta
                        ulong bytesDelta = inbound.bytesReceived - lastBytesReceived;
                        lastBytesReceived = inbound.bytesReceived;
                        currentBitrate = (bytesDelta * 8f / timeDelta) / 1_000_000f; // Mbps

                        break;
                    }

                    if (stat is RTCOutboundRTPStreamStats outbound && outbound.kind == "video")
                    {
                        framesSentCount = (int)outbound.framesSent;
                        displayFramesSent = framesSentCount;
                    }
                }

                OnStatsUpdated?.Invoke(currentFps, currentBitrate);
            }
        }
    }
}
