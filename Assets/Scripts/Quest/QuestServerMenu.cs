using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Dreamwalker.Networking;
using Dreamwalker.UI;

namespace Dreamwalker.Quest
{
    /// <summary>
    /// Handles the Server Settings menu for Quest worldspace UI.
    /// Unity UI version of ServerMenu.cs.
    /// </summary>
    public class QuestServerMenu : MonoBehaviour
    {
        private const string PREFS_KEY_SERVER_URL = "QuestServerUrl";
        private const string DEFAULT_SERVER_URL = "http://10.0.0.92:8000";

        [Header("UI References")]
        [SerializeField] private TMP_InputField serverUrlInput;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        [SerializeField] private TextMeshProUGUI connectButtonText;

        [Header("References")]
        [SerializeField] private ScopeWebRTCManager webRTCManager;

        // State
        private bool isConnecting = false;
        private string currentServerUrl = "";

        // Colors
        private readonly Color disconnectedColor = new Color(1f, 0.6f, 0.2f); // Orange
        private readonly Color connectingColor = new Color(0.6f, 0.8f, 1f);   // Light blue
        private readonly Color connectedColor = new Color(0.4f, 1f, 0.4f);    // Green
        private readonly Color errorColor = new Color(1f, 0.4f, 0.4f);        // Red

        // Events
        public event Action<string> OnConnectionRequested;
        public event Action OnDisconnectRequested;

        /// <summary>
        /// Gets the current server URL.
        /// </summary>
        public string CurrentServerUrl => currentServerUrl;

        /// <summary>
        /// Initialize with UI references from QuestWorldspaceUI.
        /// </summary>
        public void Initialize(QuestWorldspaceUI ui)
        {
            serverUrlInput = ui.ServerUrlInput;
            connectButton = ui.ConnectButton;
            disconnectButton = ui.DisconnectButton;
            connectionStatusText = ui.ServerStatusText;

            if (connectButton != null)
                connectButtonText = connectButton.GetComponentInChildren<TextMeshProUGUI>();

            Initialize();
        }

        /// <summary>
        /// Initialize with existing serialized references.
        /// </summary>
        public void Initialize()
        {
            // Load saved server URL from PlayerPrefs, default to DEFAULT_SERVER_URL
            string savedUrl = PlayerPrefs.GetString(PREFS_KEY_SERVER_URL, DEFAULT_SERVER_URL);
            if (serverUrlInput != null && !string.IsNullOrEmpty(savedUrl))
            {
                serverUrlInput.text = savedUrl;
                currentServerUrl = savedUrl;
            }

            BindEvents();
            UpdateConnectionStatus(ConnectionStatus.Disconnected);

            Debug.Log($"[QuestServerMenu] Initialized with URL: {currentServerUrl}");
        }

        private void BindEvents()
        {
            if (connectButton != null)
                connectButton.onClick.AddListener(OnConnectButtonClicked);

            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(OnDisconnectButtonClicked);

            if (serverUrlInput != null)
                serverUrlInput.onValueChanged.AddListener(OnServerUrlChanged);
        }

        private void OnDestroy()
        {
            if (connectButton != null)
                connectButton.onClick.RemoveListener(OnConnectButtonClicked);

            if (disconnectButton != null)
                disconnectButton.onClick.RemoveListener(OnDisconnectButtonClicked);

            if (serverUrlInput != null)
                serverUrlInput.onValueChanged.RemoveListener(OnServerUrlChanged);
        }

        private void OnServerUrlChanged(string newValue)
        {
            currentServerUrl = newValue;
        }

        private void OnConnectButtonClicked()
        {
            if (isConnecting) return;

            string url = serverUrlInput?.text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                UpdateConnectionStatus(ConnectionStatus.Error, "Please enter a server URL");
                return;
            }

            // Normalize URL format
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "http://" + url;
            }

            // Check if already connected to this server
            if (webRTCManager != null && webRTCManager.IsConnected)
            {
                // Already connected - this shouldn't happen as disconnect button should be shown
                OnDisconnectRequested?.Invoke();
                return;
            }

            // Save URL to PlayerPrefs for next session
            PlayerPrefs.SetString(PREFS_KEY_SERVER_URL, url);
            PlayerPrefs.Save();

            currentServerUrl = url;
            Debug.Log($"[QuestServerMenu] Connection requested to: {url}");
            OnConnectionRequested?.Invoke(url);
        }

        private void OnDisconnectButtonClicked()
        {
            Debug.Log("[QuestServerMenu] Disconnect requested");
            OnDisconnectRequested?.Invoke();
        }

        public void UpdateConnectionStatus(ConnectionStatus status, string message = null)
        {
            if (connectionStatusText == null) return;

            switch (status)
            {
                case ConnectionStatus.Disconnected:
                    connectionStatusText.text = "Disconnected";
                    connectionStatusText.color = disconnectedColor;
                    UpdateButtonStates(false, false);
                    isConnecting = false;
                    break;

                case ConnectionStatus.Connecting:
                    connectionStatusText.text = message ?? "Connecting...";
                    connectionStatusText.color = connectingColor;
                    UpdateButtonStates(false, true); // Show neither button during connecting
                    isConnecting = true;
                    break;

                case ConnectionStatus.Connected:
                    connectionStatusText.text = message ?? "Connected";
                    connectionStatusText.color = connectedColor;
                    UpdateButtonStates(true, false);
                    isConnecting = false;
                    break;

                case ConnectionStatus.Error:
                    connectionStatusText.text = message ?? "Error";
                    connectionStatusText.color = errorColor;
                    UpdateButtonStates(false, false);
                    isConnecting = false;
                    break;
            }
        }

        private void UpdateButtonStates(bool isConnected, bool isConnecting)
        {
            if (connectButton != null)
            {
                connectButton.gameObject.SetActive(!isConnected && !isConnecting);
            }

            if (disconnectButton != null)
            {
                disconnectButton.gameObject.SetActive(isConnected);
            }
        }

        public string GetServerUrl()
        {
            return serverUrlInput?.text ?? "";
        }

        public void SetServerUrl(string url)
        {
            if (serverUrlInput != null)
            {
                serverUrlInput.text = url;
                currentServerUrl = url;
            }
        }

        /// <summary>
        /// Triggers a connection attempt to the default/saved server URL.
        /// Used for auto-connect functionality.
        /// </summary>
        public void TriggerAutoConnect()
        {
            if (string.IsNullOrEmpty(currentServerUrl))
            {
                currentServerUrl = DEFAULT_SERVER_URL;
                if (serverUrlInput != null)
                    serverUrlInput.text = currentServerUrl;
            }
            OnConnectButtonClicked();
        }
    }
}
