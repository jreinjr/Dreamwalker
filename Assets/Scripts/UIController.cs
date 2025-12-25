using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Main UI controller that binds UI elements to functionality.
/// Handles button clicks and UI state management.
/// </summary>
public class UIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private CameraCapture cameraCapture;

    private Button btnCameraToggle; 
    private Label statusText;
    private VisualElement root;

    private void OnEnable()
    {
        // Get root element
        root = uiDocument.rootVisualElement;

        // Query UI elements
        btnCameraToggle = root.Q<Button>("btn-camera-toggle");
        statusText = root.Q<Label>("status-text");

        // Bind button events
        if (btnCameraToggle != null)
        {
            btnCameraToggle.clicked += OnCameraToggleClicked;
        }
    }

    private void OnDisable()
    {
        // Unbind button events
        if (btnCameraToggle != null)
        {
            btnCameraToggle.clicked -= OnCameraToggleClicked;
        }
    }

    private void OnCameraToggleClicked()
    {
        Debug.Log("Camera toggle button clicked");
        if (cameraCapture != null)
        {
            cameraCapture.ToggleCamera();
        }
        else
        {
            Debug.LogWarning("CameraCapture reference not set");
        }
    }

    /// <summary>
    /// Updates the status bar text
    /// </summary>
    public void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
