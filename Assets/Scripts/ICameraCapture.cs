using System;
using UnityEngine;

namespace Dreamwalker
{
    /// <summary>
    /// Interface for camera capture implementations.
    /// Allows different camera sources (Android WebCam, Quest Passthrough, etc.)
    /// to be used interchangeably with the WebRTC streaming system.
    /// </summary>
    public interface ICameraCapture
    {
        /// <summary>
        /// Event fired when camera is ready and streaming.
        /// </summary>
        event Action<RenderTexture> OnCameraReady;

        /// <summary>
        /// The cropped/processed camera texture ready for WebRTC streaming.
        /// </summary>
        RenderTexture CroppedTexture { get; }

        /// <summary>
        /// Whether the camera is currently active and providing frames.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Toggle between available cameras (if supported).
        /// </summary>
        void ToggleCamera();
    }
}
