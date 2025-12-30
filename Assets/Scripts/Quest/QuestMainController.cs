using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Dreamwalker.Networking;
using Dreamwalker.Models;
using Dreamwalker.UI;

namespace Dreamwalker.Quest
{
    /// <summary>
    /// Main controller for Quest VR that integrates camera, WebRTC, and worldspace UI.
    /// Auto-connects to the default server on startup.
    /// </summary>
    public class QuestMainController : MonoBehaviour
    {
        private const string DEFAULT_SERVER_URL = "http://10.0.0.92:8000";

        [Header("Camera")]
        [SerializeField] private QuestCameraCapture cameraCapture;

        [Header("UI")]
        [SerializeField] private QuestWorldspaceUI worldspaceUI;

        [Header("Networking")]
        [SerializeField] private ScopeWebRTCManager webRTCManager;
        [SerializeField] private ScopeApiClient apiClient;

        [Header("Auto-Connect")]
        [SerializeField] private bool autoConnect = true;
        [SerializeField] private float autoConnectDelay = 2f;

        // UI Controllers
        private QuestMenuController menuController;
        private QuestServerMenu serverMenu;
        private QuestScopeMenu scopeMenu;

        // State
        private bool isConnected = false;
        private Coroutine statsUpdateCoroutine;

        private void Awake()
        {
            // Get or create components
            if (cameraCapture == null)
                cameraCapture = GetComponent<QuestCameraCapture>();

            if (worldspaceUI == null)
                worldspaceUI = GetComponent<QuestWorldspaceUI>();

            if (webRTCManager == null)
                webRTCManager = GetComponent<ScopeWebRTCManager>();
            if (webRTCManager == null)
                webRTCManager = gameObject.AddComponent<ScopeWebRTCManager>();

            if (apiClient == null)
                apiClient = GetComponent<ScopeApiClient>();
            if (apiClient == null)
                apiClient = gameObject.AddComponent<ScopeApiClient>();

            // Create UI controllers
            menuController = GetComponent<QuestMenuController>();
            if (menuController == null)
                menuController = gameObject.AddComponent<QuestMenuController>();

            serverMenu = GetComponent<QuestServerMenu>();
            if (serverMenu == null)
                serverMenu = gameObject.AddComponent<QuestServerMenu>();

            scopeMenu = GetComponent<QuestScopeMenu>();
            if (scopeMenu == null)
                scopeMenu = gameObject.AddComponent<QuestScopeMenu>();
        }

        private IEnumerator Start()
        {
            Debug.Log("[QuestMainController] Starting...");

            // Wait a frame for UI to be created
            yield return null;

            // Validate UI references
            ValidateUIReferences();

            // Initialize UI controllers
            if (worldspaceUI != null)
            {
                menuController.Initialize(worldspaceUI);
                serverMenu.Initialize(worldspaceUI);
                scopeMenu.Initialize(worldspaceUI);

                // Set up video displays
                SetupVideoDisplays();
            }

            // Bind events
            BindEvents();

            UpdateStatus("Initializing camera...");

            // Wait for camera to be ready
            yield return new WaitUntil(() => cameraCapture != null && cameraCapture.IsActive);

            Debug.Log("[QuestMainController] Camera ready");
            UpdateStatus("Camera ready");

            // Set up camera PIP
            if (worldspaceUI != null && worldspaceUI.CameraPIP != null && cameraCapture.CroppedTexture != null)
            {
                worldspaceUI.CameraPIP.texture = cameraCapture.CroppedTexture;
            }

            // Auto-connect if enabled
            if (autoConnect)
            {
                UpdateStatus("Auto-connecting in " + autoConnectDelay + "s...");
                yield return new WaitForSeconds(autoConnectDelay);

                string serverUrl = PlayerPrefs.GetString("QuestServerUrl", DEFAULT_SERVER_URL);
                Debug.Log($"[QuestMainController] Auto-connecting to {serverUrl}");
                serverMenu.SetServerUrl(serverUrl);

                // Use OnConnectionRequested to ensure proper setup (apiClient.SetBaseUrl, status updates, etc.)
                OnConnectionRequested(serverUrl);
            }
            else
            {
                UpdateStatus("Ready - Open Server menu to connect");
            }
        }

        private void ValidateUIReferences()
        {
            if (worldspaceUI == null)
            {
                Debug.LogError("[QuestMainController] worldspaceUI is not assigned! Assign the QuestWorldspaceUI component.");
                return;
            }

            // Check critical UI references
            int missingCount = 0;

            if (worldspaceUI.VideoDisplay == null)
            {
                Debug.LogWarning("[QuestMainController] Missing: VideoDisplay (RawImage) - video won't be displayed");
                missingCount++;
            }

            if (worldspaceUI.CameraPIP == null)
            {
                Debug.LogWarning("[QuestMainController] Missing: CameraPIP (RawImage) - camera preview won't be shown");
                missingCount++;
            }

            if (worldspaceUI.StatusText == null)
            {
                Debug.LogWarning("[QuestMainController] Missing: StatusText (TextMeshProUGUI) - status won't be displayed");
                missingCount++;
            }

            if (worldspaceUI.StatsText == null)
            {
                Debug.LogWarning("[QuestMainController] Missing: StatsText (TextMeshProUGUI) - stats won't be displayed");
                missingCount++;
            }

            if (worldspaceUI.ServerMenuPanel == null)
            {
                Debug.LogWarning("[QuestMainController] Missing: ServerMenuPanel (GameObject)");
                missingCount++;
            }

            if (worldspaceUI.ScopeMenuPanel == null)
            {
                Debug.LogWarning("[QuestMainController] Missing: ScopeMenuPanel (GameObject)");
                missingCount++;
            }

            if (missingCount > 0)
            {
                Debug.LogWarning($"[QuestMainController] {missingCount} UI references missing! Assign them in the QuestWorldspaceUI inspector.");
            }
            else
            {
                Debug.Log("[QuestMainController] All critical UI references validated successfully.");
            }
        }

        private void SetupVideoDisplays()
        {
            // Video display will be set when we receive video from WebRTC
            if (worldspaceUI != null && worldspaceUI.VideoDisplay != null)
            {
                worldspaceUI.VideoDisplay.color = Color.black;
            }
        }

        private void BindEvents()
        {
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

            Debug.Log("[QuestMainController] Events bound");
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

        private void Update()
        {
            // Update camera PIP continuously
            if (worldspaceUI != null && worldspaceUI.CameraPIP != null &&
                cameraCapture != null && cameraCapture.CroppedTexture != null)
            {
                worldspaceUI.CameraPIP.texture = cameraCapture.CroppedTexture;
            }
        }

        #region Server Connection

        private void OnConnectionRequested(string serverUrl)
        {
            if (string.IsNullOrEmpty(serverUrl))
            {
                serverMenu.UpdateConnectionStatus(ConnectionStatus.Error, "Invalid URL");
                return;
            }

            serverMenu.UpdateConnectionStatus(ConnectionStatus.Connecting, "Connecting...");
            UpdateStatus("Connecting to " + serverUrl);

            apiClient.SetBaseUrl(serverUrl);
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

            // Get settings from scope menu
            var settings = scopeMenu.CurrentSettings;

            // Load pipeline
            serverMenu.UpdateConnectionStatus(ConnectionStatus.Connecting, "Loading pipeline...");
            UpdateStatus("Loading pipeline: " + settings.pipelineId);

            PipelineLoadParams loadParams;
            if (settings.pipelineId == "passthrough")
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
                pipeline_id = settings.pipelineId,
                load_params = loadParams
            };

            Debug.Log($"[QuestMainController] Loading pipeline: {loadRequest.pipeline_id}, resolution: {loadParams.width}x{loadParams.height}");

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

            statsUpdateCoroutine = StartCoroutine(UpdateStatsCoroutine());
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

        private void OnVideoReceived(Texture texture)
        {
            if (texture == null) return;

            frameCount++;

            // Log periodically
            if (frameCount % 60 == 0)
            {
                float currentTime = Time.time;
                float fps = 1f / (currentTime - lastFrameTime);
                Debug.Log($"[QuestMainController] Video frame #{frameCount}: {texture.width}x{texture.height}, FPS: {fps:F1}");
            }
            lastFrameTime = Time.time;

            // Display on video display
            if (worldspaceUI == null)
            {
                if (frameCount == 1) Debug.LogWarning("[QuestMainController] worldspaceUI is null - cannot display video!");
                return;
            }

            if (worldspaceUI.VideoDisplay == null)
            {
                if (frameCount == 1) Debug.LogWarning("[QuestMainController] worldspaceUI.VideoDisplay is null - assign the RawImage in the inspector!");
                return;
            }

            worldspaceUI.VideoDisplay.texture = texture;
            worldspaceUI.VideoDisplay.color = Color.white;
        }

        private IEnumerator FetchServerInfo()
        {
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
                        UpdateStats($"FPS: {stats.fps:F1} | {stats.bitrateMbps:F2} Mbps");
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
            UpdateStatus("Prompt: " + (prompt.Length > 25 ? prompt.Substring(0, 25) + "..." : prompt));
        }

        private void OnPauseToggled(bool paused)
        {
            UpdateStatus(paused ? "Paused" : "Streaming");
        }

        private void OnResetRequested()
        {
            if (isConnected && webRTCManager != null)
            {
                webRTCManager.SendResetCache();
                UpdateStatus("Cache reset");
                Debug.Log("[QuestMainController] Reset cache requested");
            }
        }

        private void OnStopRequested()
        {
            OnDisconnectRequested();
        }

        private void OnPipelineChangeRequested(string newPipelineId)
        {
            if (!isConnected)
            {
                Debug.Log($"[QuestMainController] Pipeline changed to {newPipelineId} (will apply on next connect)");
                return;
            }

            Debug.Log($"[QuestMainController] Pipeline change requested to {newPipelineId} - reconnecting...");
            UpdateStatus($"Switching to {newPipelineId}...");
            StartCoroutine(SwitchPipelineCoroutine(newPipelineId));
        }

        private void OnVaceToggled(bool enabled)
        {
            if (!isConnected)
            {
                Debug.Log($"[QuestMainController] VACE {(enabled ? "enabled" : "disabled")} (will apply on next connect)");
                return;
            }

            Debug.Log($"[QuestMainController] VACE toggled to {enabled} - requires pipeline reload, reconnecting...");
            UpdateStatus($"Reloading with VACE {(enabled ? "ON" : "OFF")}...");
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

            var settings = scopeMenu.CurrentSettings;

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

            Debug.Log($"[QuestMainController] Loading pipeline: {newPipelineId}, resolution: {loadParams.width}x{loadParams.height}");

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
            if (worldspaceUI != null && worldspaceUI.StatusText != null)
            {
                worldspaceUI.StatusText.text = "Status: " + message;
            }
            Debug.Log($"[QuestMainController] {message}");
        }

        private void UpdateStats(string stats)
        {
            if (worldspaceUI != null && worldspaceUI.StatsText != null)
            {
                worldspaceUI.StatsText.text = stats;
            }
        }

        #endregion
    }
}
