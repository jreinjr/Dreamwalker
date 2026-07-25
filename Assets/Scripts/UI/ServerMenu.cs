using UnityEngine;
using UnityEngine.UIElements;
using System;
using Dreamwalker.Networking;

namespace Dreamwalker.UI
{
    /// <summary>
    /// Handles the Server Settings menu UI and connection logic.
    /// </summary>
    public class ServerMenu : MonoBehaviour
    {
        private const string PREFS_KEY_SERVER_URL = "DreamwalkerServerUrl";

        [Header("References")]
        [SerializeField] private ScopeWebRTCManager webRTCManager;

        // UI Elements
        private TextField serverUrlField;
        private Button connectButton;
        private Label connectionStatusLabel;

        // State
        private bool isConnecting = false;
        private string currentServerUrl = "";

        // Events
        public event Action<string> OnConnectionRequested;
        public event Action OnDisconnectRequested;

        public void Initialize(VisualElement root)
        {
            if (root == null) return;

            serverUrlField = root.Q<TextField>("server-url");
            connectButton = root.Q<Button>("btn-connect");
            connectionStatusLabel = root.Q<Label>("connection-status");

            // Load saved server URL from PlayerPrefs
            if (PlayerPrefs.HasKey(PREFS_KEY_SERVER_URL))
            {
                string savedUrl = PlayerPrefs.GetString(PREFS_KEY_SERVER_URL);
                if (serverUrlField != null && !string.IsNullOrEmpty(savedUrl))
                {
                    serverUrlField.value = savedUrl;
                    currentServerUrl = savedUrl;
                }
            }

            BindEvents();
            UpdateConnectionStatus(ConnectionStatus.Disconnected);
        }

        private void BindEvents()
        {
            if (connectButton != null)
            {
                connectButton.clicked += OnConnectButtonClicked;
            }

            if (serverUrlField != null)
            {
                serverUrlField.RegisterValueChangedCallback(OnServerUrlChanged);
            }
        }

        private void OnDestroy()
        {
            if (connectButton != null)
            {
                connectButton.clicked -= OnConnectButtonClicked;
            }
        }

        private void OnServerUrlChanged(ChangeEvent<string> evt)
        {
            currentServerUrl = evt.newValue;
        }

        private void OnConnectButtonClicked()
        {
            if (isConnecting) return;

            string url = serverUrlField?.value?.Trim();
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
                // Disconnect
                OnDisconnectRequested?.Invoke();
                return;
            }

            // Save URL to PlayerPrefs for next session
            PlayerPrefs.SetString(PREFS_KEY_SERVER_URL, url);
            PlayerPrefs.Save();

            currentServerUrl = url;
            OnConnectionRequested?.Invoke(url);
        }

        public void UpdateConnectionStatus(ConnectionStatus status, string message = null)
        {
            if (connectionStatusLabel == null) return;

            switch (status)
            {
                case ConnectionStatus.Disconnected:
                    connectionStatusLabel.text = "Disconnected";
                    connectionStatusLabel.style.color = new Color(1f, 0.8f, 0.4f); // Orange
                    if (connectButton != null) connectButton.text = "Connect";
                    isConnecting = false;
                    break;

                case ConnectionStatus.Connecting:
                    connectionStatusLabel.text = message ?? "Connecting...";
                    connectionStatusLabel.style.color = new Color(0.6f, 0.8f, 1f); // Light blue
                    if (connectButton != null) connectButton.text = "Cancel";
                    isConnecting = true;
                    break;

                case ConnectionStatus.Connected:
                    connectionStatusLabel.text = message ?? "Connected";
                    connectionStatusLabel.style.color = new Color(0.4f, 1f, 0.4f); // Green
                    if (connectButton != null) connectButton.text = "Disconnect";
                    isConnecting = false;
                    break;

                case ConnectionStatus.Error:
                    connectionStatusLabel.text = message ?? "Error";
                    connectionStatusLabel.style.color = new Color(1f, 0.4f, 0.4f); // Red
                    if (connectButton != null) connectButton.text = "Retry";
                    isConnecting = false;
                    break;
            }
        }

        public string GetServerUrl()
        {
            return serverUrlField?.value ?? "";
        }

        public void SetServerUrl(string url)
        {
            if (serverUrlField != null)
            {
                serverUrlField.value = url;
                currentServerUrl = url;
            }
        }
    }

    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }
}
