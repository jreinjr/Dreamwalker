using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using System.Collections;
using Dreamwalker.Networking;
using Dreamwalker.Models;

namespace Dreamwalker.UI
{
    /// <summary>
    /// Main controller that integrates all UI components and manages the application flow.
    /// Coordinates between camera, WebRTC, and UI menus.
    /// </summary>
    public class MainController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Camera")]
        [SerializeField] private CameraCapture cameraCapture;

        [Header("Networking")]
        [SerializeField] private ScopeWebRTCManager webRTCManager;
        [SerializeField] private ScopeApiClient apiClient;

        // UI Controllers
        private MenuController menuController;
        private ServerMenu serverMenu;
        private ScopeMenu scopeMenu;

        // UI Elements
        private VisualElement root;
        private VisualElement cameraPip;
        private Label statusText;
        private Label statsText;

        // Video display (using Unity UI RawImage instead of UI Toolkit)
        private Canvas videoCanvas;
        private RawImage videoRawImage;

        // State
        private bool isConnected = false;
        private Coroutine statsUpdateCoroutine;

        private void Awake()
        {
            // Get or add component references
            if (menuController == null)
                menuController = GetComponent<MenuController>();
            if (menuController == null)
                menuController = gameObject.AddComponent<MenuController>();

            if (serverMenu == null)
                serverMenu = GetComponent<ServerMenu>();
            if (serverMenu == null)
                serverMenu = gameObject.AddComponent<ServerMenu>();

            if (scopeMenu == null)
                scopeMenu = GetComponent<ScopeMenu>();
            if (scopeMenu == null)
                scopeMenu = gameObject.AddComponent<ScopeMenu>();

            // Create API client if not assigned
            if (apiClient == null)
            {
                apiClient = gameObject.AddComponent<ScopeApiClient>();
            }

            // Create WebRTC manager if not assigned
            if (webRTCManager == null)
            {
                webRTCManager = gameObject.AddComponent<ScopeWebRTCManager>();
            }
        }

        private void OnEnable()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            StartCoroutine(InitializeAfterFrame());
        }

        private IEnumerator InitializeAfterFrame()
        {
            // Wait for UI to be ready
            yield return null;

            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            Debug.Log("[MainController] Initialize starting...");

            root = uiDocument.rootVisualElement;
            Debug.Log($"[MainController] Root element: {root != null}");

            // Get UI elements
            cameraPip = root.Q<VisualElement>("camera-pip");
            statusText = root.Q<Label>("status-text");
            statsText = root.Q<Label>("stats-text");

            Debug.Log($"[MainController] UI Elements - cameraPip: {cameraPip != null}");

            // Create Canvas and RawImage for video display (behind UI Toolkit)
            SetupVideoCanvas();

            // Initialize controllers
            Debug.Log("[MainController] Initializing MenuController...");
            menuController.Initialize(root);

            Debug.Log("[MainController] Initializing ServerMenu...");
            serverMenu.Initialize(root);

            Debug.Log("[MainController] Initializing ScopeMenu...");
            scopeMenu.Initialize(root);

            // Bind events
            BindEvents();

            // Setup camera PIP display
            SetupCameraPip();

            UpdateStatus("Ready - Enter server URL to connect");
            Debug.Log("[MainController] Initialize complete");
        }

        private void SetupVideoCanvas()
        {
            // Create a Canvas for the video RawImage (rendered behind UI Toolkit)
            var canvasGO = new GameObject("VideoCanvas");
            canvasGO.transform.SetParent(transform, false);

            videoCanvas = canvasGO.AddComponent<Canvas>();
            videoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            videoCanvas.sortingOrder = -1; // Behind UI Toolkit (which defaults to 0)

            var canvasScaler = canvasGO.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);

            // Create RawImage for video display
            var rawImageGO = new GameObject("VideoRawImage");
            rawImageGO.transform.SetParent(canvasGO.transform, false);

            videoRawImage = rawImageGO.AddComponent<RawImage>();
            videoRawImage.color = Color.white;

            // Make it fullscreen
            var rectTransform = videoRawImage.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // Start with black background
            videoRawImage.color = Color.black;

            Debug.Log("[MainController] Video Canvas and RawImage created");
        }

        private void BindEvents()
        {
            // Menu controller events
            menuController.OnCameraToggleRequested += OnCameraToggle;

            // Server menu events
            serverMenu.OnConnectionRequested += OnConnectionRequested;
            serverMenu.OnDisconnectRequested += OnDisconnectRequested;

            // Scope menu events
            scopeMenu.OnSettingsChanged += OnSettingsChanged;
            scopeMenu.OnPromptSent += OnPromptSent;
            scopeMenu.OnPauseToggled += OnPauseToggled;
            scopeMenu.OnResetRequested += OnResetRequested;
            scopeMenu.OnStopRequested += OnStopRequested;
            scopeMenu.OnPipelineChangeRequested += OnPipelineChangeRequested;
            scopeMenu.OnVaceToggled += OnVaceToggled;

            // WebRTC events
            if (webRTCManager != null)
            {
                webRTCManager.OnConnected += OnWebRTCConnected;
                webRTCManager.OnDisconnected += OnWebRTCDisconnected;
                webRTCManager.OnError += OnWebRTCError;
                webRTCManager.OnVideoReceived += OnVideoReceived;
            }
        }

        private void OnDisable()
        {
            UnbindEvents();

            if (statsUpdateCoroutine != null)
            {
                StopCoroutine(statsUpdateCoroutine);
            }
        }

        private void UnbindEvents()
        {
            if (menuController != null)
            {
                menuController.OnCameraToggleRequested -= OnCameraToggle;
            }

            if (serverMenu != null)
            {
                serverMenu.OnConnectionRequested -= OnConnectionRequested;
                serverMenu.OnDisconnectRequested -= OnDisconnectRequested;
            }

            if (scopeMenu != null)
            {
                scopeMenu.OnSettingsChanged -= OnSettingsChanged;
                scopeMenu.OnPromptSent -= OnPromptSent;
                scopeMenu.OnPauseToggled -= OnPauseToggled;
                scopeMenu.OnResetRequested -= OnResetRequested;
                scopeMenu.OnStopRequested -= OnStopRequested;
                scopeMenu.OnPipelineChangeRequested -= OnPipelineChangeRequested;
                scopeMenu.OnVaceToggled -= OnVaceToggled;
            }

            if (webRTCManager != null)
            {
                webRTCManager.OnConnected -= OnWebRTCConnected;
                webRTCManager.OnDisconnected -= OnWebRTCDisconnected;
                webRTCManager.OnError -= OnWebRTCError;
                webRTCManager.OnVideoReceived -= OnVideoReceived;
            }
        }

        private void SetupCameraPip()
        {
            if (cameraPip != null && cameraCapture != null)
            {
                // Display camera preview in PIP
                var croppedTexture = cameraCapture.CroppedTexture;
                if (croppedTexture != null)
                {
                    cameraPip.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(croppedTexture));
                }
            }
        }

        private void Update()
        {
            // Update camera PIP background
            if (cameraPip != null && cameraCapture != null && cameraCapture.CroppedTexture != null)
            {
                cameraPip.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(cameraCapture.CroppedTexture));
            }
        }

        #region Camera Events

        private void OnCameraToggle()
        {
            if (cameraCapture != null)
            {
                cameraCapture.ToggleCamera();
                UpdateStatus("Camera switched");
            }
        }

        #endregion

        #region Server Connection Events

        private void OnConnectionRequested(string serverUrl)
        {
            if (string.IsNullOrEmpty(serverUrl))
            {
                serverMenu.UpdateConnectionStatus(ConnectionStatus.Error, "Invalid URL");
                return;
            }

            serverMenu.UpdateConnectionStatus(ConnectionStatus.Connecting, "Connecting...");
            UpdateStatus("Connecting to " + serverUrl);

            // Set API client base URL
            apiClient.SetBaseUrl(serverUrl);

            // Start connection process
            StartCoroutine(ConnectToServer(serverUrl));
        }

        private IEnumerator ConnectToServer(string serverUrl)
        {
            // Check server health
            serverMenu.UpdateConnectionStatus(ConnectionStatus.Connecting, "Checking server...");

            bool healthOk = false;
            yield return apiClient.CheckHealth((success, message) =>
            {
                healthOk = success;
                if (!success)
                {
                    serverMenu.UpdateConnectionStatus(ConnectionStatus.Error, "Server unreachable");
                    UpdateStatus("Error: " + message);
                }
            });

            if (!healthOk) yield break;

            // Get initial settings from scope menu
            var settings = scopeMenu.CurrentSettings;

            // Load pipeline
            serverMenu.UpdateConnectionStatus(ConnectionStatus.Connecting, "Loading pipeline...");
            UpdateStatus("Loading pipeline: " + settings.pipelineId);

            // Build load params based on pipeline type (matching frontend)
            PipelineLoadParams loadParams;
            if (settings.pipelineId == "passthrough")
            {
                // Passthrough only needs resolution
                loadParams = new PipelineLoadParams
                {
                    width = settings.width,
                    height = settings.height
                };
            }
            else
            {
                // Other pipelines get full params
                loadParams = new PipelineLoadParams
                {
                    width = settings.width,
                    height = settings.height,
                    seed = settings.seed,
                    vace_enabled = settings.vaceEnabled
                };
            }

            var loadRequest = new PipelineLoadRequest
            {
                pipeline_id = settings.pipelineId,
                load_params = loadParams
            };

            Debug.Log($"[MainController] Loading pipeline: {loadRequest.pipeline_id}, resolution: {loadParams.width}x{loadParams.height}");

            bool loadOk = false;
            yield return apiClient.LoadPipeline(loadRequest, (success, error) =>
            {
                loadOk = success;
                if (!success)
                {
                    serverMenu.UpdateConnectionStatus(ConnectionStatus.Error, "Pipeline load failed");
                    UpdateStatus("Error: " + error);
                }
            });

            if (!loadOk) yield break;

            // Poll until pipeline is loaded
            serverMenu.UpdateConnectionStatus(ConnectionStatus.Connecting, "Waiting for pipeline...");
            bool pipelineReady = false;
            string pipelineError = null;

            while (!pipelineReady && pipelineError == null)
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
                    }
                    else if (status.status == "error")
                    {
                        pipelineError = status.error ?? "Pipeline loading failed";
                    }
                    else
                    {
                        UpdateStatus("Pipeline status: " + status.status);
                    }
                });

                if (!pipelineReady && pipelineError == null)
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }

            if (pipelineError != null)
            {
                serverMenu.UpdateConnectionStatus(ConnectionStatus.Error, pipelineError);
                UpdateStatus("Error: " + pipelineError);
                yield break;
            }

            // Initialize WebRTC connection
            serverMenu.UpdateConnectionStatus(ConnectionStatus.Connecting, "Establishing WebRTC...");

            if (cameraCapture != null && cameraCapture.CroppedTexture != null)
            {
                webRTCManager.Initialize(apiClient, cameraCapture.CroppedTexture, settings);
                yield return webRTCManager.Connect();
            }
            else
            {
                serverMenu.UpdateConnectionStatus(ConnectionStatus.Error, "Camera not ready");
                UpdateStatus("Error: Camera not initialized");
            }
        }

        private void OnDisconnectRequested()
        {
            if (webRTCManager != null)
            {
                webRTCManager.Disconnect();
            }

            serverMenu.UpdateConnectionStatus(ConnectionStatus.Disconnected);
            UpdateStatus("Disconnected");
            isConnected = false;

            if (statsUpdateCoroutine != null)
            {
                StopCoroutine(statsUpdateCoroutine);
                statsUpdateCoroutine = null;
            }

            UpdateStats("");
        }

        #endregion

        #region WebRTC Events

        private void OnWebRTCConnected()
        {
            isConnected = true;
            serverMenu.UpdateConnectionStatus(ConnectionStatus.Connected);
            UpdateStatus("Connected - Streaming");

            // Start stats update
            statsUpdateCoroutine = StartCoroutine(UpdateStatsCoroutine());

            // Fetch available pipelines and LoRAs
            StartCoroutine(FetchServerInfo());
        }

        private void OnWebRTCDisconnected()
        {
            isConnected = false;
            serverMenu.UpdateConnectionStatus(ConnectionStatus.Disconnected);
            UpdateStatus("Disconnected");

            if (statsUpdateCoroutine != null)
            {
                StopCoroutine(statsUpdateCoroutine);
                statsUpdateCoroutine = null;
            }

            UpdateStats("");
        }

        private void OnWebRTCError(string error)
        {
            serverMenu.UpdateConnectionStatus(ConnectionStatus.Error, error);
            UpdateStatus("Error: " + error);
        }

        private int frameCount = 0;
        private float lastFrameTime = 0f;
        private float calculatedFps = 0f;

        private void OnVideoReceived(Texture texture)
        {
            if (texture == null) return;

            frameCount++;

            // Calculate FPS
            float currentTime = Time.time;
            float deltaTime = currentTime - lastFrameTime;
            if (deltaTime > 0)
            {
                calculatedFps = 1f / deltaTime;
            }
            lastFrameTime = currentTime;

            // Log every 30 frames with detailed format info
            if (frameCount % 30 == 0)
            {
                string formatInfo = "N/A";
                if (texture is Texture2D t2d)
                {
                    formatInfo = $"Texture2D format={t2d.format}, graphicsFormat={t2d.graphicsFormat}";
                }
                else if (texture is RenderTexture rt)
                {
                    formatInfo = $"RenderTexture format={rt.format}, graphicsFormat={rt.graphicsFormat}";
                }
                Debug.Log($"[MainController] Video frame #{frameCount}: {texture.width}x{texture.height}, FPS: {calculatedFps:F1}, {formatInfo}");
            }

            // Set texture directly on RawImage - this is the approach used in Unity WebRTC samples
            if (videoRawImage != null)
            {
                videoRawImage.texture = texture;
                videoRawImage.color = Color.white; // Ensure full visibility
            }
        }

        private IEnumerator FetchServerInfo()
        {
            // Fetch available pipelines
            yield return apiClient.GetPipelineSchemas((schemas, error) =>
            {
                if (schemas != null && schemas.pipelines != null)
                {
                    var pipelineNames = new System.Collections.Generic.List<string>();
                    foreach (var pipeline in schemas.pipelines)
                    {
                        pipelineNames.Add(pipeline.id);
                    }
                    scopeMenu.SetAvailablePipelines(pipelineNames);
                }
            });

            // Fetch available LoRAs
            yield return apiClient.GetLoRAList((loraResponse, error) =>
            {
                if (loraResponse != null && loraResponse.loras != null)
                {
                    var loraNames = new System.Collections.Generic.List<string>();
                    foreach (var lora in loraResponse.loras)
                    {
                        loraNames.Add(lora.name);
                    }
                    scopeMenu.SetAvailableLoras(loraNames);
                }
            });
        }

        private IEnumerator UpdateStatsCoroutine()
        {
            while (isConnected)
            {
                if (webRTCManager != null)
                {
                    var stats = webRTCManager.GetStats();
                    if (stats != null)
                    {
                        UpdateStats($"FPS: {stats.fps:F1} | Bitrate: {stats.bitrateMbps:F2} Mbps");
                    }
                }
                yield return new WaitForSeconds(1f);
            }
        }

        #endregion

        #region Scope Settings Events

        private void OnSettingsChanged(StreamSettings settings)
        {
            if (isConnected && webRTCManager != null)
            {
                webRTCManager.SendParameters(settings);
            }
        }

        private void OnPromptSent(string prompt)
        {
            UpdateStatus("Prompt: " + (prompt.Length > 30 ? prompt.Substring(0, 30) + "..." : prompt));
        }

        private void OnPauseToggled(bool paused)
        {
            UpdateStatus(paused ? "Paused" : "Streaming");
        }

        private void OnResetRequested()
        {
            if (isConnected && webRTCManager != null)
            {
                // Send reset_cache command to backend
                webRTCManager.SendResetCache();
                UpdateStatus("Cache reset");
                Debug.Log("[MainController] Reset cache requested");
            }
        }

        private void OnStopRequested()
        {
            OnDisconnectRequested();
        }

        private void OnPipelineChangeRequested(string newPipelineId)
        {
            // Only handle if we're connected - otherwise settings will be used on next connect
            if (!isConnected)
            {
                Debug.Log($"[MainController] Pipeline changed to {newPipelineId} (will apply on next connect)");
                return;
            }

            Debug.Log($"[MainController] Pipeline change requested to {newPipelineId} - reconnecting...");
            UpdateStatus($"Switching to {newPipelineId}...");

            // Disconnect, load new pipeline, and reconnect
            StartCoroutine(SwitchPipelineCoroutine(newPipelineId));
        }

        private void OnVaceToggled(bool enabled)
        {
            // Only handle if we're connected - otherwise settings will be used on next connect
            if (!isConnected)
            {
                Debug.Log($"[MainController] VACE {(enabled ? "enabled" : "disabled")} (will apply on next connect)");
                return;
            }

            Debug.Log($"[MainController] VACE toggled to {enabled} - requires pipeline reload, reconnecting...");
            UpdateStatus($"Reloading with VACE {(enabled ? "ON" : "OFF")}...");

            // VACE is a pipeline-load-time setting, so we need to reload the pipeline
            // Reuse SwitchPipelineCoroutine with the current pipeline ID
            StartCoroutine(SwitchPipelineCoroutine(scopeMenu.CurrentSettings.pipelineId));
        }

        private IEnumerator SwitchPipelineCoroutine(string newPipelineId)
        {
            // Disconnect WebRTC
            if (webRTCManager != null)
            {
                webRTCManager.Disconnect();
            }

            isConnected = false;
            if (statsUpdateCoroutine != null)
            {
                StopCoroutine(statsUpdateCoroutine);
                statsUpdateCoroutine = null;
            }

            serverMenu.UpdateConnectionStatus(ConnectionStatus.Connecting, "Loading new pipeline...");

            // Get settings (already updated by ScopeMenu)
            var settings = scopeMenu.CurrentSettings;

            // Build load params for new pipeline
            PipelineLoadParams loadParams;
            if (newPipelineId == "passthrough")
            {
                loadParams = new PipelineLoadParams
                {
                    width = settings.width,
                    height = settings.height
                };
            }
            else
            {
                loadParams = new PipelineLoadParams
                {
                    width = settings.width,
                    height = settings.height,
                    seed = settings.seed,
                    vace_enabled = settings.vaceEnabled
                };
            }

            var loadRequest = new PipelineLoadRequest
            {
                pipeline_id = newPipelineId,
                load_params = loadParams
            };

            Debug.Log($"[MainController] Loading pipeline: {newPipelineId}, resolution: {loadParams.width}x{loadParams.height}");

            bool loadOk = false;
            yield return apiClient.LoadPipeline(loadRequest, (success, error) =>
            {
                loadOk = success;
                if (!success)
                {
                    serverMenu.UpdateConnectionStatus(ConnectionStatus.Error, "Pipeline load failed");
                    UpdateStatus("Error: " + error);
                }
            });

            if (!loadOk) yield break;

            // Poll until pipeline is loaded
            serverMenu.UpdateConnectionStatus(ConnectionStatus.Connecting, "Waiting for pipeline...");
            bool pipelineReady = false;
            string pipelineError = null;

            while (!pipelineReady && pipelineError == null)
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
                    }
                    else if (status.status == "error")
                    {
                        pipelineError = status.error ?? "Pipeline loading failed";
                    }
                    else
                    {
                        UpdateStatus("Pipeline status: " + status.status);
                    }
                });

                if (!pipelineReady && pipelineError == null)
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }

            if (pipelineError != null)
            {
                serverMenu.UpdateConnectionStatus(ConnectionStatus.Error, pipelineError);
                UpdateStatus("Error: " + pipelineError);
                yield break;
            }

            // Reconnect WebRTC
            serverMenu.UpdateConnectionStatus(ConnectionStatus.Connecting, "Reconnecting WebRTC...");

            if (cameraCapture != null && cameraCapture.CroppedTexture != null)
            {
                webRTCManager.Initialize(apiClient, cameraCapture.CroppedTexture, settings);
                yield return webRTCManager.Connect();
            }
            else
            {
                serverMenu.UpdateConnectionStatus(ConnectionStatus.Error, "Camera not ready");
                UpdateStatus("Error: Camera not initialized");
            }
        }

        #endregion

        #region UI Updates

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = "Status: " + message;
            }
        }

        private void UpdateStats(string stats)
        {
            if (statsText != null)
            {
                statsText.text = stats;
            }
        }

        #endregion

        private void OnDestroy()
        {
            // Clean up video canvas
            if (videoCanvas != null)
            {
                Destroy(videoCanvas.gameObject);
            }
        }
    }
}
