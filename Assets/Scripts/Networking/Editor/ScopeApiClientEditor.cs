using System;
using UnityEngine;
using UnityEditor;
using Dreamwalker.Models;
using Dreamwalker.Networking;

namespace Dreamwalker.Networking.Editor
{
    /// <summary>
    /// Custom Inspector for ScopeApiClient with buttons and controls
    /// </summary>
    [CustomEditor(typeof(ScopeApiClient))]
    public class ScopeApiClientEditor : UnityEditor.Editor
    {
        // Serialized Properties
        private SerializedProperty serverUrlProp;
        private SerializedProperty selectedPipelineProp;
        private SerializedProperty widthProp;
        private SerializedProperty heightProp;
        private SerializedProperty seedProp;
        private SerializedProperty inputModeProp;
        private SerializedProperty denoisingStepCountProp;
        private SerializedProperty noiseScaleProp;
        private SerializedProperty noiseControllerEnabledProp;
        private SerializedProperty manageCacheEnabledProp;
        private SerializedProperty loraConfigsProp;
        private SerializedProperty vaceEnabledProp;
        private SerializedProperty vaceContextScaleProp;
        private SerializedProperty vaceReferenceImageProp;
        private SerializedProperty currentPromptProp;
        private SerializedProperty promptWeightProp;
        private SerializedProperty connectionStatusProp;
        private SerializedProperty pipelineStatusProp;
        private SerializedProperty availablePipelinesProp;
        private SerializedProperty availableLorasProp;
        private SerializedProperty serverVramGbProp;

        // Foldout states
        private bool showServerSection = true;
        private bool showPipelineSection = true;
        private bool showDenoisingSection = true;
        private bool showNoiseSection = true;
        private bool showLoraSection = false;
        private bool showVaceSection = true;
        private bool showPromptSection = true;
        private bool showStatusSection = true;

        // Editor state
        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;
        private PipelineType lastPipeline;

        private void OnEnable()
        {
            // Cache serialized properties
            serverUrlProp = serializedObject.FindProperty("serverUrl");
            selectedPipelineProp = serializedObject.FindProperty("selectedPipeline");
            widthProp = serializedObject.FindProperty("width");
            heightProp = serializedObject.FindProperty("height");
            seedProp = serializedObject.FindProperty("seed");
            inputModeProp = serializedObject.FindProperty("inputMode");
            denoisingStepCountProp = serializedObject.FindProperty("denoisingStepCount");
            noiseScaleProp = serializedObject.FindProperty("noiseScale");
            noiseControllerEnabledProp = serializedObject.FindProperty("noiseControllerEnabled");
            manageCacheEnabledProp = serializedObject.FindProperty("manageCacheEnabled");
            loraConfigsProp = serializedObject.FindProperty("loraConfigs");
            vaceEnabledProp = serializedObject.FindProperty("vaceEnabled");
            vaceContextScaleProp = serializedObject.FindProperty("vaceContextScale");
            vaceReferenceImageProp = serializedObject.FindProperty("vaceReferenceImage");
            currentPromptProp = serializedObject.FindProperty("currentPrompt");
            promptWeightProp = serializedObject.FindProperty("promptWeight");
            connectionStatusProp = serializedObject.FindProperty("connectionStatus");
            pipelineStatusProp = serializedObject.FindProperty("pipelineStatus");
            availablePipelinesProp = serializedObject.FindProperty("availablePipelines");
            availableLorasProp = serializedObject.FindProperty("availableLoras");
            serverVramGbProp = serializedObject.FindProperty("serverVramGb");

            lastPipeline = (PipelineType)selectedPipelineProp.enumValueIndex;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var client = (ScopeApiClient)target;

            // ========== Server Configuration Section ==========
            showServerSection = EditorGUILayout.Foldout(showServerSection, "Server Configuration", true, EditorStyles.foldoutHeader);
            if (showServerSection)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(serverUrlProp, new GUIContent("Server URL"));

                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();

                GUI.enabled = Application.isPlaying;
                if (GUILayout.Button("Check Health", GUILayout.Height(25)))
                {
                    CheckServerHealth(client);
                }
                if (GUILayout.Button("Fetch LoRAs", GUILayout.Height(25)))
                {
                    FetchLoRAs(client);
                }
                if (GUILayout.Button("Get Hardware", GUILayout.Height(25)))
                {
                    FetchHardwareInfo(client);
                }
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();

                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play mode to use server buttons", MessageType.Info);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ========== Pipeline Settings Section ==========
            showPipelineSection = EditorGUILayout.Foldout(showPipelineSection, "Pipeline Settings", true, EditorStyles.foldoutHeader);
            if (showPipelineSection)
            {
                EditorGUI.indentLevel++;

                // Pipeline dropdown
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(selectedPipelineProp, new GUIContent("Pipeline"));
                if (EditorGUI.EndChangeCheck())
                {
                    var newPipeline = (PipelineType)selectedPipelineProp.enumValueIndex;
                    if (newPipeline != lastPipeline)
                    {
                        // Auto-update resolution
                        var (w, h) = ScopeApiClient.GetDefaultResolution(newPipeline);
                        widthProp.intValue = w;
                        heightProp.intValue = h;
                        lastPipeline = newPipeline;
                    }
                }

                // Resolution
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(widthProp, new GUIContent("Width"), GUILayout.MinWidth(100));
                EditorGUILayout.PropertyField(heightProp, new GUIContent("Height"), GUILayout.MinWidth(100));
                EditorGUILayout.EndHorizontal();

                // Seed with randomize button
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(seedProp, new GUIContent("Seed"));
                if (GUILayout.Button("Randomize", GUILayout.Width(80)))
                {
                    seedProp.intValue = UnityEngine.Random.Range(0, int.MaxValue);
                }
                EditorGUILayout.EndHorizontal();

                // Input mode
                EditorGUILayout.PropertyField(inputModeProp, new GUIContent("Input Mode"));

                // Load Pipeline button
                EditorGUILayout.Space(5);
                GUI.enabled = Application.isPlaying;
                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button("Load Pipeline", GUILayout.Height(30)))
                {
                    LoadPipeline(client);
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ========== Denoising Section ==========
            showDenoisingSection = EditorGUILayout.Foldout(showDenoisingSection, "Denoising Steps", true, EditorStyles.foldoutHeader);
            if (showDenoisingSection)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.IntSlider(denoisingStepCountProp, 1, 4, new GUIContent("Step Count"));

                // Show calculated steps
                string stepsStr = GetDenoisingStepsString(denoisingStepCountProp.intValue);
                EditorGUILayout.LabelField("Steps:", stepsStr, EditorStyles.helpBox);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ========== Noise Section ==========
            showNoiseSection = EditorGUILayout.Foldout(showNoiseSection, "Noise Control", true, EditorStyles.foldoutHeader);
            if (showNoiseSection)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.Slider(noiseScaleProp, 0f, 1f, new GUIContent("Noise Scale"));
                EditorGUILayout.PropertyField(noiseControllerEnabledProp, new GUIContent("Noise Controller"));
                EditorGUILayout.PropertyField(manageCacheEnabledProp, new GUIContent("Manage Cache"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ========== LoRA Section ==========
            showLoraSection = EditorGUILayout.Foldout(showLoraSection, "LoRA Settings", true, EditorStyles.foldoutHeader);
            if (showLoraSection)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(loraConfigsProp, new GUIContent("LoRA Configurations"), true);

                // Show available LoRAs if fetched
                if (availableLorasProp.arraySize > 0)
                {
                    EditorGUILayout.LabelField("Available LoRAs:", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < Mathf.Min(availableLorasProp.arraySize, 5); i++)
                    {
                        EditorGUILayout.LabelField(availableLorasProp.GetArrayElementAtIndex(i).stringValue);
                    }
                    if (availableLorasProp.arraySize > 5)
                    {
                        EditorGUILayout.LabelField($"... and {availableLorasProp.arraySize - 5} more");
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ========== VACE Section ==========
            showVaceSection = EditorGUILayout.Foldout(showVaceSection, "VACE Settings", true, EditorStyles.foldoutHeader);
            if (showVaceSection)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(vaceEnabledProp, new GUIContent("VACE Enabled"));

                if (vaceEnabledProp.boolValue)
                {
                    EditorGUILayout.Slider(vaceContextScaleProp, 0f, 2f, new GUIContent("Context Scale"));

                    EditorGUILayout.Space(5);
                    EditorGUILayout.PropertyField(vaceReferenceImageProp, new GUIContent("Reference Image", "Image to upload and use for VACE conditioning when sending prompt"));

                    if (vaceReferenceImageProp.objectReferenceValue != null)
                    {
                        var tex = vaceReferenceImageProp.objectReferenceValue as Texture2D;
                        if (tex != null && !tex.isReadable)
                        {
                            EditorGUILayout.HelpBox("Texture must have 'Read/Write' enabled in import settings to upload.", MessageType.Warning);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("This image will be uploaded to the server when you click 'Send Prompt Update'", MessageType.Info);
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ========== Prompt Section ==========
            showPromptSection = EditorGUILayout.Foldout(showPromptSection, "Prompt", true, EditorStyles.foldoutHeader);
            if (showPromptSection)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(currentPromptProp, new GUIContent("Prompt Text"));
                EditorGUILayout.Slider(promptWeightProp, 0f, 2f, new GUIContent("Weight"));

                // Send Prompt and Clear Cache buttons
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = Application.isPlaying;
                GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
                if (GUILayout.Button("Send Prompt Update", GUILayout.Height(25)))
                {
                    SendPromptUpdate(client);
                }
                GUI.backgroundColor = new Color(1f, 0.6f, 0.4f);
                if (GUILayout.Button("Clear Cache", GUILayout.Height(25)))
                {
                    SendClearCache(client);
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play mode and connect to send updates", MessageType.Info);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // ========== Status Section ==========
            showStatusSection = EditorGUILayout.Foldout(showStatusSection, "Runtime Status", true, EditorStyles.foldoutHeader);
            if (showStatusSection)
            {
                EditorGUI.indentLevel++;
                GUI.enabled = false;

                // Connection status with color
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Connection:", GUILayout.Width(80));
                string connStatus = connectionStatusProp.stringValue;
                Color statusColor = connStatus == "Connected" ? Color.green :
                                   connStatus == "Disconnected" ? Color.red : Color.yellow;
                var originalColor = GUI.contentColor;
                GUI.contentColor = statusColor;
                EditorGUILayout.LabelField(connStatus, EditorStyles.boldLabel);
                GUI.contentColor = originalColor;
                EditorGUILayout.EndHorizontal();

                // Pipeline status
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Pipeline:", GUILayout.Width(80));
                string pipeStatus = pipelineStatusProp.stringValue;
                statusColor = pipeStatus == "loaded" ? Color.green :
                             pipeStatus == "Loading..." ? Color.yellow :
                             pipeStatus == "Error" ? Color.red : Color.gray;
                GUI.contentColor = statusColor;
                EditorGUILayout.LabelField(pipeStatus, EditorStyles.boldLabel);
                GUI.contentColor = originalColor;
                EditorGUILayout.EndHorizontal();

                // Server VRAM
                if (serverVramGbProp.floatValue > 0)
                {
                    EditorGUILayout.LabelField($"Server VRAM: {serverVramGbProp.floatValue:F1} GB");
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

            // Repaint periodically in Play mode
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private string GetDenoisingStepsString(int count)
        {
            return count switch
            {
                1 => "[1000]",
                2 => "[1000, 500]",
                3 => "[1000, 666, 333]",
                4 => "[1000, 750, 500, 250]",
                _ => "Invalid"
            };
        }

        // ========== Button Handlers (Play Mode Only) ==========

        private void CheckServerHealth(ScopeApiClient client)
        {
            statusMessage = "Checking server health...";
            statusType = MessageType.Info;

            client.StartCoroutine(client.CheckHealth((success, message) =>
            {
                if (success)
                {
                    statusMessage = "Server is healthy!";
                    statusType = MessageType.Info;
                }
                else
                {
                    statusMessage = $"Health check failed: {message}";
                    statusType = MessageType.Error;
                }
                Repaint();
            }));
        }

        private void FetchLoRAs(ScopeApiClient client)
        {
            statusMessage = "Fetching LoRAs...";
            statusType = MessageType.Info;

            client.StartCoroutine(client.GetLoRAList((response, error) =>
            {
                if (response != null)
                {
                    int count = response.loras?.Length ?? 0;
                    statusMessage = $"Found {count} LoRAs";
                    statusType = MessageType.Info;
                }
                else
                {
                    statusMessage = $"Failed to fetch LoRAs: {error}";
                    statusType = MessageType.Error;
                }
                Repaint();
            }));
        }

        private void FetchHardwareInfo(ScopeApiClient client)
        {
            statusMessage = "Fetching hardware info...";
            statusType = MessageType.Info;

            client.StartCoroutine(client.GetHardwareInfo((response, error) =>
            {
                if (response != null)
                {
                    statusMessage = $"Server VRAM: {response.vram_gb:F1} GB";
                    statusType = MessageType.Info;
                }
                else
                {
                    statusMessage = $"Failed to fetch hardware info: {error}";
                    statusType = MessageType.Error;
                }
                Repaint();
            }));
        }

        private void LoadPipeline(ScopeApiClient client)
        {
            statusMessage = "Loading pipeline...";
            statusType = MessageType.Info;

            var loadRequest = client.ToPipelineLoadRequest();

            client.StartCoroutine(client.LoadPipeline(loadRequest, (success, error) =>
            {
                if (success)
                {
                    statusMessage = "Pipeline load initiated, polling status...";
                    statusType = MessageType.Info;
                    // Start polling for status
                    client.StartCoroutine(PollPipelineStatus(client));
                }
                else
                {
                    statusMessage = $"Pipeline load failed: {error}";
                    statusType = MessageType.Error;
                }
                Repaint();
            }));
        }

        private System.Collections.IEnumerator PollPipelineStatus(ScopeApiClient client)
        {
            float timeout = 60f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;

                bool done = false;
                client.StartCoroutine(client.GetPipelineStatus((status, error) =>
                {
                    if (status != null)
                    {
                        if (status.status == "loaded")
                        {
                            statusMessage = "Pipeline loaded successfully!";
                            statusType = MessageType.Info;
                            done = true;
                        }
                        else if (status.status == "error")
                        {
                            statusMessage = $"Pipeline error: {status.error}";
                            statusType = MessageType.Error;
                            done = true;
                        }
                    }
                    Repaint();
                }));

                if (done) yield break;
            }

            statusMessage = "Pipeline load timed out";
            statusType = MessageType.Warning;
            Repaint();
        }

        private void SendPromptUpdate(ScopeApiClient client)
        {
            // Find WebRTCManager to send via data channel
            var webRTCManager = FindObjectOfType<ScopeWebRTCManager>();
            if (webRTCManager == null)
            {
                statusMessage = "No ScopeWebRTCManager found in scene";
                statusType = MessageType.Error;
                return;
            }

            if (!webRTCManager.IsConnected)
            {
                statusMessage = "WebRTC not connected. Connect first before sending updates.";
                statusType = MessageType.Warning;
                return;
            }

            // Check if we have a reference image to upload
            if (client.HasReferenceImage)
            {
                statusMessage = "Uploading reference image...";
                statusType = MessageType.Info;
                Repaint();

                // Upload the image first, then send the prompt
                string filename = $"vace_ref_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
                client.StartCoroutine(UploadAndSendPrompt(client, webRTCManager, filename));
            }
            else
            {
                // No image, just send the prompt
                var parameters = client.ToPromptUpdateParameters(null);
                webRTCManager.SendParameterUpdate(parameters);
                statusMessage = "Sent prompt update (no reference image)";
                statusType = MessageType.Info;
                Repaint();
            }
        }

        private System.Collections.IEnumerator UploadAndSendPrompt(ScopeApiClient client, ScopeWebRTCManager webRTCManager, string filename)
        {
            string uploadedPath = null;

            yield return client.UploadTexture(client.VaceReferenceImage, filename, (response, error) =>
            {
                if (response != null)
                {
                    uploadedPath = response.path;
                    Debug.Log($"[ScopeApiClientEditor] Uploaded reference image to: {uploadedPath}");
                }
                else
                {
                    statusMessage = $"Failed to upload image: {error}";
                    statusType = MessageType.Error;
                    Repaint();
                }
            });

            if (!string.IsNullOrEmpty(uploadedPath))
            {
                // Now send the prompt with the uploaded image path
                var parameters = client.ToPromptUpdateParameters(uploadedPath);
                webRTCManager.SendParameterUpdate(parameters);
                statusMessage = $"Sent prompt with reference image";
                statusType = MessageType.Info;
                Repaint();
            }
        }

        private void SendClearCache(ScopeApiClient client)
        {
            // Find WebRTCManager to send via data channel
            var webRTCManager = FindObjectOfType<ScopeWebRTCManager>();
            if (webRTCManager == null)
            {
                statusMessage = "No ScopeWebRTCManager found in scene";
                statusType = MessageType.Error;
                return;
            }

            if (!webRTCManager.IsConnected)
            {
                statusMessage = "WebRTC not connected. Connect first before clearing cache.";
                statusType = MessageType.Warning;
                return;
            }

            webRTCManager.SendResetCache();
            statusMessage = "Cache reset command sent";
            statusType = MessageType.Info;
            Repaint();
        }
    }
}
