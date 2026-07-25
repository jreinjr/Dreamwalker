using UnityEngine;
using UnityEditor;
using Dreamwalker.Networking;

namespace Dreamwalker.Networking.Editor
{
    /// <summary>
    /// Custom Inspector for ScopeWebRTCManager with connection controls and live stats
    /// </summary>
    [CustomEditor(typeof(ScopeWebRTCManager))]
    public class ScopeWebRTCManagerEditor : UnityEditor.Editor
    {
        // Serialized Properties
        private SerializedProperty apiClientProp;
        private SerializedProperty cameraCaptureProp;
        private SerializedProperty outputDisplayProp;
        private SerializedProperty autoConnectProp;
        private SerializedProperty connectionTimeoutProp;

        // Bitrate properties
        private SerializedProperty maxSendBitrateProp;
        private SerializedProperty minSendBitrateProp;
        private SerializedProperty maxReceiveBitrateProp;
        private SerializedProperty maxFramerateProp;

        // Status properties
        private SerializedProperty currentStateProp;
        private SerializedProperty currentSessionIdProp;
        private SerializedProperty displayFpsProp;
        private SerializedProperty displayBitrateProp;
        private SerializedProperty displayFramesSentProp;
        private SerializedProperty displayFramesReceivedProp;
        private SerializedProperty connectionDurationProp;

        // Foldout states
        private bool showDependenciesSection = true;
        private bool showBitrateSection = true;
        private bool showControlsSection = true;
        private bool showStatsSection = true;

        // Editor state
        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;
        private double lastRepaintTime = 0;

        private void OnEnable()
        {
            // Cache serialized properties
            apiClientProp = serializedObject.FindProperty("apiClient");
            cameraCaptureProp = serializedObject.FindProperty("_cameraCapture");
            outputDisplayProp = serializedObject.FindProperty("outputDisplay");
            autoConnectProp = serializedObject.FindProperty("autoConnectOnStart");
            connectionTimeoutProp = serializedObject.FindProperty("connectionTimeout");

            maxSendBitrateProp = serializedObject.FindProperty("maxSendBitrateKbps");
            minSendBitrateProp = serializedObject.FindProperty("minSendBitrateKbps");
            maxReceiveBitrateProp = serializedObject.FindProperty("maxReceiveBitrateKbps");
            maxFramerateProp = serializedObject.FindProperty("maxFramerate");

            currentStateProp = serializedObject.FindProperty("currentState");
            currentSessionIdProp = serializedObject.FindProperty("currentSessionId");
            displayFpsProp = serializedObject.FindProperty("displayFps");
            displayBitrateProp = serializedObject.FindProperty("displayBitrateMbps");
            displayFramesSentProp = serializedObject.FindProperty("displayFramesSent");
            displayFramesReceivedProp = serializedObject.FindProperty("displayFramesReceived");
            connectionDurationProp = serializedObject.FindProperty("connectionDuration");

            // Start update loop for live stats
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            // Repaint periodically in Play mode to show live stats
            if (Application.isPlaying && EditorApplication.timeSinceStartup - lastRepaintTime > 0.5)
            {
                lastRepaintTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var manager = (ScopeWebRTCManager)target;

            // ========== Dependencies Section ==========
            showDependenciesSection = EditorGUILayout.Foldout(showDependenciesSection, "Dependencies & Settings", true, EditorStyles.foldoutHeader);
            if (showDependenciesSection)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(apiClientProp, new GUIContent("API Client"));

                // Show server URL from ApiClient (read-only)
                var apiClient = apiClientProp.objectReferenceValue as ScopeApiClient;
                if (apiClient != null)
                {
                    GUI.enabled = false;
                    EditorGUILayout.LabelField("Server URL:", apiClient.ServerUrl);
                    GUI.enabled = true;
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(cameraCaptureProp, new GUIContent("Camera Capture"));
                EditorGUILayout.PropertyField(outputDisplayProp, new GUIContent("Output Display"));

                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(autoConnectProp, new GUIContent("Auto-Connect on Start"));
                EditorGUILayout.PropertyField(connectionTimeoutProp, new GUIContent("Timeout (seconds)"));

                // Validation warnings
                if (apiClientProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("API Client required. Assign a ScopeApiClient component.", MessageType.Warning);
                }

                if (cameraCaptureProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("Camera Capture required for video input.", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ========== Bitrate Section ==========
            showBitrateSection = EditorGUILayout.Foldout(showBitrateSection, "Bitrate Settings", true, EditorStyles.foldoutHeader);
            if (showBitrateSection)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(maxSendBitrateProp, new GUIContent("Max Send (kbps)"));
                EditorGUILayout.PropertyField(minSendBitrateProp, new GUIContent("Min Send (kbps)"));
                EditorGUILayout.PropertyField(maxReceiveBitrateProp, new GUIContent("Max Receive (kbps)"));
                EditorGUILayout.PropertyField(maxFramerateProp, new GUIContent("Max Framerate"));

                EditorGUILayout.HelpBox("Typical values:\n• 480p: 1000-2500 kbps\n• 720p: 2500-5000 kbps\n• 1080p: 5000-10000 kbps", MessageType.Info);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ========== Controls Section ==========
            showControlsSection = EditorGUILayout.Foldout(showControlsSection, "Controls", true, EditorStyles.foldoutHeader);
            if (showControlsSection)
            {
                EditorGUI.indentLevel++;

                bool isConnected = manager.IsConnected;
                bool isConnecting = manager.State == ScopeWebRTCManager.ConnectionState.Connecting;

                EditorGUILayout.BeginHorizontal();

                // Connect button
                GUI.enabled = Application.isPlaying && !isConnected && !isConnecting;
                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button("Connect", GUILayout.Height(30)))
                {
                    ConnectToServer(manager);
                }

                // Disconnect button
                GUI.enabled = Application.isPlaying && (isConnected || isConnecting);
                GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
                if (GUILayout.Button("Disconnect", GUILayout.Height(30)))
                {
                    manager.Disconnect();
                    statusMessage = "Disconnected";
                    statusType = MessageType.Info;
                }

                GUI.enabled = true;
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                // Reset Cache button
                EditorGUILayout.Space(5);
                GUI.enabled = Application.isPlaying && isConnected;
                if (GUILayout.Button("Reset Cache"))
                {
                    manager.SendResetCache();
                    statusMessage = "Cache reset command sent";
                    statusType = MessageType.Info;
                }
                GUI.enabled = true;

                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play mode to use connection controls", MessageType.Info);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ========== Stats Section ==========
            showStatsSection = EditorGUILayout.Foldout(showStatsSection, "Connection Stats", true, EditorStyles.foldoutHeader);
            if (showStatsSection)
            {
                EditorGUI.indentLevel++;
                GUI.enabled = false;

                // Connection state with color indicator
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("State:", GUILayout.Width(80));

                var state = (ScopeWebRTCManager.ConnectionState)currentStateProp.enumValueIndex;
                Color stateColor = state switch
                {
                    ScopeWebRTCManager.ConnectionState.Connected => Color.green,
                    ScopeWebRTCManager.ConnectionState.Connecting => Color.yellow,
                    ScopeWebRTCManager.ConnectionState.Failed => Color.red,
                    _ => Color.gray
                };

                var originalColor = GUI.contentColor;
                GUI.contentColor = stateColor;
                EditorGUILayout.LabelField(state.ToString(), EditorStyles.boldLabel);
                GUI.contentColor = originalColor;
                EditorGUILayout.EndHorizontal();

                // Session ID
                string sessionId = currentSessionIdProp.stringValue;
                if (!string.IsNullOrEmpty(sessionId))
                {
                    EditorGUILayout.LabelField("Session ID:", sessionId.Length > 20 ? sessionId.Substring(0, 20) + "..." : sessionId);
                }

                // Performance stats
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);

                // FPS and Bitrate on same row
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"FPS: {displayFpsProp.floatValue:F1}", GUILayout.Width(100));
                EditorGUILayout.LabelField($"Bitrate: {displayBitrateProp.floatValue:F2} Mbps");
                EditorGUILayout.EndHorizontal();

                // Frames sent/received
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Sent: {displayFramesSentProp.intValue}", GUILayout.Width(120));
                EditorGUILayout.LabelField($"Received: {displayFramesReceivedProp.intValue}");
                EditorGUILayout.EndHorizontal();

                // Connection duration
                float duration = connectionDurationProp.floatValue;
                if (duration > 0)
                {
                    string durationStr = duration < 60 ? $"{duration:F1}s" : $"{(int)(duration / 60)}m {(int)(duration % 60)}s";
                    EditorGUILayout.LabelField($"Duration: {durationStr}");
                }

                GUI.enabled = true;
                EditorGUI.indentLevel--;
            }

            // Status message box
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ConnectToServer(ScopeWebRTCManager manager)
        {
            statusMessage = "Connecting...";
            statusType = MessageType.Info;

            // Subscribe to events for status updates
            manager.OnConnected += () =>
            {
                statusMessage = "Connected successfully!";
                statusType = MessageType.Info;
                Repaint();
            };

            manager.OnError += (error) =>
            {
                statusMessage = $"Error: {error}";
                statusType = MessageType.Error;
                Repaint();
            };

            manager.OnDisconnected += () =>
            {
                statusMessage = "Disconnected";
                statusType = MessageType.Info;
                Repaint();
            };

            // Start connection
            manager.StartCoroutine(manager.Connect());
        }
    }
}
