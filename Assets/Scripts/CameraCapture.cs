using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Experimental.Rendering;
using Unity.WebRTC;

/// <summary>
/// Handles camera capture, permissions, and front/back camera switching.
/// Displays the camera feed on a UI Toolkit VisualElement.
/// </summary>
public class CameraCapture : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Camera Settings")]
    [SerializeField] private int targetWidth = 576;
    [SerializeField] private int targetHeight = 320;
    [SerializeField] private int requestedFPS = 15; // Lower FPS for better quality and bandwidth

    // Aspect ratio for 16:9
    private const float TARGET_ASPECT = 16f / 9f;

    private WebCamTexture webCamTexture;
    private RenderTexture renderTexture;
    private RenderTexture croppedTexture;
    private Material cropMaterial;
    private VisualElement cameraPreviewElement;
    private Label statusText;
    private bool useFrontCamera = false;
    private bool isInitialized = false;

    // Crop parameters calculated when camera starts
    private Vector4 cropParams; // x=offsetX, y=offsetY, z=scaleX, w=scaleY

    /// <summary>
    /// Event fired when camera is ready and streaming
    /// </summary>
    public event System.Action<RenderTexture> OnCameraReady;

    /// <summary>
    /// Current cropped camera texture (640x360, 16:9)
    /// </summary>
    public RenderTexture CroppedTexture => croppedTexture;

    /// <summary>
    /// Raw camera texture (original resolution)
    /// </summary>
    public WebCamTexture RawCameraTexture => webCamTexture;

    /// <summary>
    /// Whether the camera is currently active
    /// </summary>
    public bool IsActive => webCamTexture != null && webCamTexture.isPlaying;

    private void Start()
    {
        StartCoroutine(InitializeCamera());
    }

    private IEnumerator InitializeCamera()
    {
        // Get UI elements
        var root = uiDocument.rootVisualElement;
        // Try v2 element name first (camera-pip), fall back to v1 (camera-preview)
        cameraPreviewElement = root.Q<VisualElement>("camera-pip");
        if (cameraPreviewElement == null)
        {
            cameraPreviewElement = root.Q<VisualElement>("camera-preview");
        }
        statusText = root.Q<Label>("status-text");

        UpdateStatus("Requesting camera permission...");

        // Request camera permission
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            UpdateStatus("Camera permission denied");
            Debug.LogError("Camera permission denied by user");
            yield break;
        }

        UpdateStatus("Camera permission granted");

        // Check for available cameras
        if (WebCamTexture.devices.Length == 0)
        {
            UpdateStatus("No camera found");
            Debug.LogError("No camera devices found");
            yield break;
        }

        // Log available cameras
        foreach (var device in WebCamTexture.devices)
        {
            Debug.Log($"Camera found: {device.name} (Front: {device.isFrontFacing})");
        }

        // Create the output texture ONCE here - this is the texture WebRTC binds to
        // It must persist across camera switches to keep the VideoStreamTrack alive
        CreateOutputTexture();

        isInitialized = true;
        StartCamera();
    }

    /// <summary>
    /// Creates the persistent output RenderTexture used by WebRTC.
    /// This is created once and reused across camera switches.
    /// </summary>
    private void CreateOutputTexture()
    {
        // Get WebRTC-supported graphics format for this platform
        var supportedFormat = WebRTC.GetSupportedGraphicsFormat(SystemInfo.graphicsDeviceType);
        Debug.Log($"[CameraCapture] Creating persistent output texture - WebRTC format: {supportedFormat}, device: {SystemInfo.graphicsDeviceType}");

        croppedTexture = new RenderTexture(targetWidth, targetHeight, 0, supportedFormat);
        croppedTexture.antiAliasing = 1;
        croppedTexture.Create();

        Debug.Log($"[CameraCapture] Persistent output texture created: {targetWidth}x{targetHeight}");
    }

    /// <summary>
    /// Starts or restarts the camera with current settings
    /// </summary>
    public void StartCamera()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Camera not yet initialized");
            return;
        }

        StopCamera();

        // Find the appropriate camera
        string cameraName = GetCameraName(useFrontCamera);
        if (string.IsNullOrEmpty(cameraName))
        {
            UpdateStatus("No suitable camera found");
            return;
        }

        UpdateStatus($"Starting camera: {cameraName}");

        // Request higher resolution from camera for quality, we'll crop/scale to target
        // Request 1280x720 or higher, camera will give us closest available
        webCamTexture = new WebCamTexture(cameraName, 1280, 720, requestedFPS);
        webCamTexture.Play();

        StartCoroutine(WaitForCameraReady());
    }

    private IEnumerator WaitForCameraReady()
    {
        // Wait for the camera to start
        float timeout = 5f;
        float elapsed = 0f;

        while (!webCamTexture.didUpdateThisFrame && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!webCamTexture.didUpdateThisFrame)
        {
            UpdateStatus("Camera timeout");
            Debug.LogError("Camera failed to start within timeout");
            yield break;
        }

        // Camera is ready
        int camWidth = webCamTexture.width;
        int camHeight = webCamTexture.height;
        Debug.Log($"Camera started: {camWidth}x{camHeight} @ {webCamTexture.requestedFPS}fps");

        // Calculate crop parameters to achieve 16:9 aspect ratio
        // Scale to fit smaller dimension, then crop the excess
        float camAspect = (float)camWidth / camHeight;

        if (camAspect > TARGET_ASPECT)
        {
            // Camera is wider than 16:9 - crop sides
            float scale = TARGET_ASPECT / camAspect;
            float offset = (1f - scale) / 2f;
            cropParams = new Vector4(offset, 0f, scale, 1f);
        }
        else if (camAspect < TARGET_ASPECT)
        {
            // Camera is taller than 16:9 - crop top/bottom
            float scale = camAspect / TARGET_ASPECT;
            float offset = (1f - scale) / 2f;
            cropParams = new Vector4(0f, offset, 1f, scale);
        }
        else
        {
            // Already 16:9
            cropParams = new Vector4(0f, 0f, 1f, 1f);
        }

        Debug.Log($"Crop params: offset=({cropParams.x:F3}, {cropParams.y:F3}), scale=({cropParams.z:F3}, {cropParams.w:F3})");

        // Create RenderTexture for raw camera frames (intermediate buffer - recreated per camera)
        if (renderTexture != null) renderTexture.Release();
        renderTexture = new RenderTexture(camWidth, camHeight, 0, RenderTextureFormat.ARGB32);
        renderTexture.Create();

        // Note: croppedTexture is created once in CreateOutputTexture() and persists across camera switches
        // This is critical for WebRTC - the VideoStreamTrack is bound to this texture
        Debug.Log($"[CameraCapture] Created intermediate RenderTexture: {camWidth}x{camHeight}, output: {croppedTexture.width}x{croppedTexture.height}");

        UpdateStatus($"Camera Active ({targetWidth}x{targetHeight})");

        // Set the cropped texture as background for display
        if (cameraPreviewElement != null)
        {
            cameraPreviewElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(croppedTexture));

            // Adjust rotation/flip based on camera orientation
            AdjustCameraRotation();
        }

        OnCameraReady?.Invoke(croppedTexture);
    }

    private void Update()
    {
        // Blit webcam texture to render texture, then crop to output
        if (webCamTexture != null && webCamTexture.isPlaying &&
            renderTexture != null && croppedTexture != null)
        {
            // First blit raw camera to renderTexture
            Graphics.Blit(webCamTexture, renderTexture);

            // Then blit with crop/scale to croppedTexture
            // Using scale and offset to crop the image
            Vector2 scale = new Vector2(cropParams.z, cropParams.w);
            Vector2 offset = new Vector2(cropParams.x, cropParams.y);
            Graphics.Blit(renderTexture, croppedTexture, scale, offset);
        }
    }

    private void AdjustCameraRotation()
    {
        if (webCamTexture == null || cameraPreviewElement == null) return;

        // Get the video rotation angle
        int angle = -webCamTexture.videoRotationAngle;

        // For front camera, we may need to flip horizontally
        if (useFrontCamera)
        {
            cameraPreviewElement.style.scale = new StyleScale(new Scale(new Vector2(-1, 1)));
        }
        else
        {
            cameraPreviewElement.style.scale = new StyleScale(new Scale(new Vector2(1, 1)));
        }

        // Apply rotation
        cameraPreviewElement.style.rotate = new StyleRotate(new Rotate(angle));
    }

    /// <summary>
    /// Stops the current camera
    /// </summary>
    public void StopCamera()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
            webCamTexture = null;
        }
    }

    /// <summary>
    /// Toggles between front and back camera
    /// </summary>
    public void ToggleCamera()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Camera not yet initialized");
            return;
        }

        useFrontCamera = !useFrontCamera;
        Debug.Log($"Switching to {(useFrontCamera ? "front" : "back")} camera");
        StartCamera();
    }

    private string GetCameraName(bool front)
    {
        foreach (var device in WebCamTexture.devices)
        {
            if (device.isFrontFacing == front)
            {
                return device.name;
            }
        }

        // Fallback: return first available camera if preferred type not found
        if (WebCamTexture.devices.Length > 0)
        {
            return WebCamTexture.devices[0].name;
        }

        return null;
    }

    private void UpdateStatus(string message)
    {
        Debug.Log($"[CameraCapture] {message}");
        if (statusText != null)
        {
            statusText.text = $"Status: {message}";
        }
    }

    private void OnDestroy()
    {
        StopCamera();
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
        if (croppedTexture != null)
        {
            croppedTexture.Release();
            Destroy(croppedTexture);
            croppedTexture = null;
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // App going to background - stop camera
            if (webCamTexture != null && webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
            }
        }
        else
        {
            // App resuming - restart camera
            if (webCamTexture != null && !webCamTexture.isPlaying)
            {
                webCamTexture.Play();
            }
        }
    }
}
