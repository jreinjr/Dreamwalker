# Dreamwalker

A Unity client for real-time AI-powered video streaming powered by Daydream Scope. Dreamwalker captures camera input, streams it to a Scope backend server for AI processing (image-to-image diffusion), and displays the processed video in real-time.

## Getting Started

Run Scope locally or on Runpod. On Runpod, be sure to add your HF_TOKEN to environment variables. Use the URL of your server (on Runpod, something like https://[some letters]-8000.proxy.runpod.net/). Watch the Runpod server logs while you connect from Android to monitor progress. Change the prompt, pipeline, and settings; uses your Android phone camera (either front/back). You should be able to test first in Unity Editor & then build & run on Android.

## Features

- **Real-time Camera Streaming**: Captures device camera and streams via WebRTC
- **AI Video Processing**: Connects to Scope backend for real-time image transformation
- **Multiple Pipelines**: Supports various AI pipelines including passthrough mode
- **Prompt Control**: Text prompts to guide AI generation
- **VACE Support**: Video-aware content enhancement
- **LoRA Support**: Load custom LoRA models for style transfer
- **Cross-Platform**: Targets Android mobile devices and PC

## Requirements

- Unity 2022.3 LTS or newer (tested in Unity 6000.0.60f1
- [Unity WebRTC Package](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/index.html) (included)
- A running [Scope](daydream.live) backend server

## Project Structure

```
Assets/
├── Scripts/
│   ├── CameraCapture.cs           # Camera input handling
│   ├── Networking/
│   │   ├── ScopeApiClient.cs      # REST API client for Scope backend
│   │   └── ScopeWebRTCManager.cs  # WebRTC connection management
│   ├── Models/
│   │   ├── ApiModels.cs           # API request/response models
│   │   └── StreamSettings.cs      # Stream configuration
│   └── UI/
│       ├── MainController.cs      # Main application controller
│       ├── MenuController.cs      # Menu navigation
│       ├── ServerMenu.cs          # Server connection UI
│       └── ScopeMenu.cs           # AI settings UI
├── UI/
│   ├── MainUI.uxml                # UI Toolkit layout
│   └── MainUI.uss                 # UI Toolkit styles
└── Settings/
    ├── Mobile_RPAsset.asset       # URP settings for mobile
    └── PC_RPAsset.asset           # URP settings for PC
```

## Configuration

### Stream Settings

- **Resolution**: Output resolution (width x height)
- **Pipeline**: Select AI processing pipeline
- **Prompts**: Text prompts for AI guidance
- **Noise Scale**: Control AI generation strength
- **Denoising Steps**: Quality vs. speed tradeoff
- **VACE**: Enable video-aware processing

### Building

#### Android
1. Switch platform to Android in Build Settings
2. Ensure camera permissions in `AndroidManifest.xml`
3. Build and deploy to device

#### PC
1. Switch platform to Windows/Mac/Linux
2. Build and run

## Architecture

```
┌─────────────────┐     WebRTC      ┌─────────────────┐
│   Dreamwalker   │ ◄─────────────► │  Scope Server   │
│  (Unity Client) │                 │   (AI Backend)  │
└─────────────────┘                 └─────────────────┘
        │                                   │
        ▼                                   ▼
   Camera Input                      AI Processing
   UI Controls                       Video Encoding
   Video Display                     Pipeline Management
```
