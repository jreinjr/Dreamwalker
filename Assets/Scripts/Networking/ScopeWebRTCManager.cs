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
    /// Manages WebRTC connection to Scope backend
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

        [Header("Dependencies")]
        [SerializeField, Interface(typeof(ICameraCapture))] private UnityEngine.Object _cameraCapture;
        public ICameraCapture CameraCapture { get; private set; }

        // Static flag to ensure WebRTC is initialized only once
        private static bool webRTCInitialized = false;

        // Events
        public event Action<ConnectionState> OnConnectionStateChanged;
        public event Action<Texture> OnVideoReceived;
        public event Action<Texture2D> OnVideoFrameReceived;
        public event Action<string> OnError;
        public event Action<float, float> OnStatsUpdated; // fps, bitrate
        public event Action OnConnected;
        public event Action OnDisconnected;

        // State
        private ScopeApiClient apiClient;
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

        // Initialization state
        private RenderTexture sourceTexture;
        private StreamSettings currentSettings;

        public ConnectionState State => connectionState;
        public Texture ReceivedVideo => receivedVideoTexture;
        public bool IsConnected => connectionState == ConnectionState.Connected;

        /// <summary>
        /// Stats data for display
        /// </summary>
        public class WebRTCStats
        {
            public float fps;
            public float bitrateMbps;
        }

        private void Awake()
        {
            CameraCapture = _cameraCapture as ICameraCapture;
            // Start the WebRTC update loop - this is CRITICAL for video encoding to work!
            // In Unity WebRTC 3.0, initialization is automatic
            if (!webRTCInitialized)
            {
                StartCoroutine(WebRTC.Update());
                webRTCInitialized = true;
                Debug.Log("[WebRTC] WebRTC.Update() coroutine started");
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }


        /// <summary>
        /// Initialize the WebRTC manager with required dependencies
        /// </summary>
        public void Initialize(ScopeApiClient client, RenderTexture cameraTexture, StreamSettings settings)
        {
            apiClient = client;
            sourceTexture = cameraTexture;
            currentSettings = settings;
        }

        /// <summary>
        /// Connects to the Scope server using pre-initialized settings
        /// </summary>
        public IEnumerator Connect()
        {
            if (apiClient == null || sourceTexture == null || currentSettings == null)
            {
                OnError?.Invoke("WebRTC manager not initialized");
                yield break;
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
            pendingCandidates.Clear();
            candidatesSent = false;
            receivedVideoTexture = null;

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

            string json = JsonUtility.ToJson(parameters);
            dataChannel.Send(json);
            Debug.Log($"[WebRTC] Sent parameter update: {json}");
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

        private IEnumerator ConnectCoroutine(StreamSettings settings)
        {
            SetConnectionState(ConnectionState.Connecting);

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

            // Step 2: Get ICE servers
            IceServersResponse iceServers = null;
            yield return apiClient.GetIceServers((response, error) =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"[WebRTC] Failed to get ICE servers: {error}, using default");
                }
                iceServers = response;
            });

            // Step 3: Create peer connection
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

            // Step 4: Create data channel for parameter updates
            var dataChannelInit = new RTCDataChannelInit { ordered = true };
            dataChannel = peerConnection.CreateDataChannel("parameters", dataChannelInit);
            dataChannel.OnOpen = () => Debug.Log("[WebRTC] Data channel opened");
            dataChannel.OnClose = () => Debug.Log("[WebRTC] Data channel closed");
            dataChannel.OnMessage = OnDataChannelMessage;
            Debug.Log("[WebRTC] Data channel created");

            // Step 5: Add local video track using MediaStream (matching frontend approach)
            RenderTexture videoSource = sourceTexture;
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

                // Create MediaStream and add track (matching frontend's pc.addTrack(track, stream))
                localStream = new MediaStream();
                localStream.AddTrack(videoTrack);

                // Add track to peer connection with MediaStream association
                var sender = peerConnection.AddTrack(videoTrack, localStream);
                Debug.Log($"[WebRTC] Track added to peer connection, sender={sender != null}");

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
                    // Ensure bidirectional video (send camera, receive AI-processed)
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

            // Step 6: Create offer
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

            // Step 7: Send offer to server
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
            Debug.Log($"[WebRTC] Received answer, sessionId: {sessionId}");

            // Step 8: Set remote description
            var answer = new RTCSessionDescription
            {
                type = RTCSdpType.Answer,
                sdp = answerResponse.sdp
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

            // Step 9: Send any pending ICE candidates
            yield return SendPendingCandidates();

            // Start stats collection
            statsCoroutine = StartCoroutine(CollectStats());

            // Log transceiver state and manually subscribe to receiver tracks
            // OnTrack doesn't fire for tracks created via SendRecv transceiver, so we must manually subscribe
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

                        // Manually subscribe to video receiver track since OnTrack doesn't fire for SendRecv transceivers
                        if (track is VideoStreamTrack receiverVideoTrack)
                        {
                            Debug.Log($"[WebRTC] Manually subscribing to receiver video track (Enabled={receiverVideoTrack.Enabled})");

                            // Ensure track is enabled
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
                                OnVideoReceived?.Invoke(tex);

                                // Log every second to avoid spam
                                if (Time.time - lastFrameLogTime > 1.0f)
                                {
                                    Debug.Log($"[WebRTC] Receiving video: {tex.width}x{tex.height}, frames received: {receivedFrameCount}");
                                    lastFrameLogTime = Time.time;
                                }

                                // Also fire Texture2D version for compatibility
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
                            Debug.Log($"[WebRTC] Monitor: OUTBOUND video - framesSent={outbound.framesSent}, bytesSent={outbound.bytesSent}, framesEncoded={outbound.framesEncoded}");
                        }
                    }
                }

                // Also log camera texture state
                if (sourceTexture != null)
                {
                    Debug.Log($"[WebRTC] Monitor: Source texture - {sourceTexture.width}x{sourceTexture.height}, isCreated={sourceTexture.IsCreated()}");
                }

                yield return new WaitForSeconds(2f);
            }
        }

        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            Debug.Log($"[WebRTC] ICE candidate: {candidate.Candidate}");

            if (string.IsNullOrEmpty(sessionId))
            {
                // Queue candidate until we have session ID
                pendingCandidates.Add(candidate);
            }
            else if (!candidatesSent)
            {
                pendingCandidates.Add(candidate);
            }
            else
            {
                // Send immediately
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

        private int receivedFrameCount = 0;
        private float lastFrameLogTime = 0f;

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
                    OnVideoReceived?.Invoke(tex);

                    // Log every second to avoid spam
                    if (Time.time - lastFrameLogTime > 1.0f)
                    {
                        Debug.Log($"[WebRTC] Receiving video: {tex.width}x{tex.height}, frames received: {receivedFrameCount}");
                        lastFrameLogTime = Time.time;
                    }

                    // Also fire Texture2D version for compatibility
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

            // Check for stream_stopped message
            if (message.Contains("stream_stopped"))
            {
                OnError?.Invoke("Stream stopped by server");
                Disconnect();
            }
        }

        private void SetConnectionState(ConnectionState state)
        {
            if (connectionState != state)
            {
                var previousState = connectionState;
                connectionState = state;
                OnConnectionStateChanged?.Invoke(state);
                Debug.Log($"[WebRTC] Connection state changed to: {state}");

                // Fire convenience events
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
                }

                OnStatsUpdated?.Invoke(currentFps, currentBitrate);
            }
        }
    }
}
