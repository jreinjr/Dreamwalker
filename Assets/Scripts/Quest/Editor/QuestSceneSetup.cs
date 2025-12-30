#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;

namespace Dreamwalker.Quest.Editor
{
    /// <summary>
    /// Editor utility to set up the Quest Camera WebRTC scene.
    /// </summary>
    public class QuestSceneSetup : EditorWindow
    {
        [MenuItem("Dreamwalker/Setup Quest Camera WebRTC Scene")]
        public static void ShowWindow()
        {
            GetWindow<QuestSceneSetup>("Quest Scene Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("Quest Camera WebRTC Scene Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "This will set up a new scene with all the required components for Quest 3 passthrough camera streaming via WebRTC.\n\n" +
                "Prerequisites:\n" +
                "- Meta XR SDK Core installed\n" +
                "- Meta XR MR Utility Kit installed\n" +
                "- Unity WebRTC package installed\n" +
                "- TextMeshPro installed",
                MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Create New Scene", GUILayout.Height(40)))
            {
                CreateQuestScene();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Setup Current Scene", GUILayout.Height(30)))
            {
                SetupCurrentScene();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "After setup, you MUST:\n" +
                "1. Add Camera Rig Building Block\n" +
                "   (Meta > Building Blocks > Camera Rig)\n" +
                "2. Add Passthrough Camera Access Building Block\n" +
                "   (Meta > Building Blocks > Passthrough Camera Access)\n" +
                "3. Enable Passthrough in OVRManager\n" +
                "4. Delete the placeholder GameObjects",
                MessageType.Warning);
        }

        private void CreateQuestScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupCurrentScene();

            // Ensure Scenes directory exists
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            // Save scene
            string scenePath = "Assets/Scenes/QuestCameraWebRTC.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log($"[QuestSceneSetup] Scene created and saved to {scenePath}");
            EditorUtility.DisplayDialog("Scene Created",
                $"Scene saved to:\n{scenePath}\n\n" +
                "IMPORTANT: You must now add the Meta XR Building Blocks:\n" +
                "1. Camera Rig\n" +
                "2. Passthrough Camera Access\n\n" +
                "Then delete the placeholder GameObjects.",
                "OK");
        }

        private void SetupCurrentScene()
        {
            // Create placeholder for OVRCameraRig (user should use Building Block)
            var cameraRigGO = new GameObject("=== ADD CAMERA RIG BUILDING BLOCK ===");
            var mainCamera = new GameObject("MainCamera");
            mainCamera.transform.SetParent(cameraRigGO.transform);
            mainCamera.AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
            mainCamera.transform.localPosition = new Vector3(0, 1.6f, 0);

            // Create Directional Light
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Create Quest Streaming System
            var systemGO = new GameObject("QuestStreamingSystem");

            // Add QuestCameraCapture
            systemGO.AddComponent<QuestCameraCapture>();

            // Add WorldspaceUI
            systemGO.AddComponent<QuestWorldspaceUI>();

            // Add networking components
            systemGO.AddComponent<Dreamwalker.Networking.ScopeWebRTCManager>();
            systemGO.AddComponent<Dreamwalker.Networking.ScopeApiClient>();

            // Add UI controllers
            systemGO.AddComponent<QuestMenuController>();
            systemGO.AddComponent<QuestServerMenu>();
            systemGO.AddComponent<QuestScopeMenu>();

            // Add main controller
            systemGO.AddComponent<QuestMainController>();

            // Create PassthroughCameraAccess placeholder
            var ptCameraGO = new GameObject("=== ADD PASSTHROUGH CAMERA ACCESS BUILDING BLOCK ===");

            // Try to add the PassthroughCameraAccess component if available
            Type ptCameraType = Type.GetType("Meta.XR.PassthroughCameraAccess, Meta.XR.MRUtilityKit");
            if (ptCameraType != null)
            {
                ptCameraGO.AddComponent(ptCameraType);
                Debug.Log("[QuestSceneSetup] Added PassthroughCameraAccess component");
            }
            else
            {
                Debug.LogWarning("[QuestSceneSetup] PassthroughCameraAccess type not found. Please add it manually via Building Blocks.");
            }

            Debug.Log("[QuestSceneSetup] Scene setup complete. Please add OVR Building Blocks for proper VR functionality.");

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            // Select the system object
            Selection.activeGameObject = systemGO;
        }
    }
}
#endif
