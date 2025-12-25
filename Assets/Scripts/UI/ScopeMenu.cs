using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using Dreamwalker.Models;

namespace Dreamwalker.UI
{
    /// <summary>
    /// Handles the Scope Settings menu UI for controlling AI generation parameters.
    /// </summary>
    public class ScopeMenu : MonoBehaviour
    {
        // UI Elements
        private DropdownField inputModeDropdown;
        private TextField promptInput;
        private Button sendPromptButton;
        private DropdownField pipelineDropdown;
        private IntegerField seedInput;
        private Button randomizeSeedButton;
        private SliderInt denoiseSlider;
        private Label denoiseValueLabel;
        private Slider noiseSlider;
        private Label noiseValueLabel;
        private Toggle noiseControllerToggle;
        private Toggle cacheToggle;
        private DropdownField loraDropdown;
        private Slider loraScaleSlider;
        private Label loraScaleValueLabel;
        private Toggle vaceToggle;
        private Slider vaceScaleSlider;
        private Label vaceScaleValueLabel;
        private Button pauseButton;
        private Button resetButton;
        private Button stopButton;

        // State
        private StreamSettings currentSettings = new StreamSettings();
        private bool isPaused = false;
        private List<string> availablePipelines = new List<string>
        {
            "longlive",  // Default - good for video mode
            "streamdiffusionv2",
            "krea-realtime-video",
            "reward-forcing",
            "passthrough"  // For testing without AI
        };
        private List<string> availableLoras = new List<string> { "None" };

        // Events
        public event Action<StreamSettings> OnSettingsChanged;
        public event Action<string> OnPromptSent;
        public event Action<bool> OnPauseToggled;
        public event Action OnResetRequested;
        public event Action OnStopRequested;
        public event Action<string> OnPipelineChangeRequested; // Fires when pipeline changes while connected
        public event Action<bool> OnVaceToggled; // Fires when VACE is toggled - requires reconnection

        public StreamSettings CurrentSettings => currentSettings;

        public void Initialize(VisualElement root)
        {
            if (root == null) return;

            BindElements(root);
            BindEvents();
            InitializeDefaults();
        }

        private void BindElements(VisualElement root)
        {
            inputModeDropdown = root.Q<DropdownField>("input-mode-dropdown");
            promptInput = root.Q<TextField>("prompt-input");
            sendPromptButton = root.Q<Button>("btn-send-prompt");
            pipelineDropdown = root.Q<DropdownField>("pipeline-dropdown");
            seedInput = root.Q<IntegerField>("seed-input");
            randomizeSeedButton = root.Q<Button>("btn-randomize-seed");
            denoiseSlider = root.Q<SliderInt>("denoise-slider");
            denoiseValueLabel = root.Q<Label>("denoise-value");
            noiseSlider = root.Q<Slider>("noise-slider");
            noiseValueLabel = root.Q<Label>("noise-value");
            noiseControllerToggle = root.Q<Toggle>("noise-controller-toggle");
            cacheToggle = root.Q<Toggle>("cache-toggle");
            loraDropdown = root.Q<DropdownField>("lora-dropdown");
            loraScaleSlider = root.Q<Slider>("lora-scale-slider");
            loraScaleValueLabel = root.Q<Label>("lora-scale-value");
            vaceToggle = root.Q<Toggle>("vace-toggle");
            vaceScaleSlider = root.Q<Slider>("vace-scale-slider");
            vaceScaleValueLabel = root.Q<Label>("vace-scale-value");
            pauseButton = root.Q<Button>("btn-pause");
            resetButton = root.Q<Button>("btn-reset");
            stopButton = root.Q<Button>("btn-stop");
        }

        private void BindEvents()
        {
            sendPromptButton?.RegisterCallback<ClickEvent>(_ => SendPrompt());
            randomizeSeedButton?.RegisterCallback<ClickEvent>(_ => RandomizeSeed());
            pauseButton?.RegisterCallback<ClickEvent>(_ => TogglePause());
            resetButton?.RegisterCallback<ClickEvent>(_ => RequestReset());
            stopButton?.RegisterCallback<ClickEvent>(_ => RequestStop());

            inputModeDropdown?.RegisterValueChangedCallback(OnInputModeChanged);
            pipelineDropdown?.RegisterValueChangedCallback(OnPipelineChanged);
            seedInput?.RegisterValueChangedCallback(OnSeedChanged);
            denoiseSlider?.RegisterValueChangedCallback(OnDenoiseChanged);
            noiseSlider?.RegisterValueChangedCallback(OnNoiseChanged);
            noiseControllerToggle?.RegisterValueChangedCallback(OnNoiseControllerChanged);
            cacheToggle?.RegisterValueChangedCallback(OnCacheChanged);
            loraDropdown?.RegisterValueChangedCallback(OnLoraChanged);
            loraScaleSlider?.RegisterValueChangedCallback(OnLoraScaleChanged);
            vaceToggle?.RegisterValueChangedCallback(OnVaceChanged);
            vaceScaleSlider?.RegisterValueChangedCallback(OnVaceScaleChanged);
        }

        private void InitializeDefaults()
        {
            // Input mode dropdown - default to Video for camera input
            if (inputModeDropdown != null)
            {
                inputModeDropdown.choices = new List<string> { "Video", "Text" };
                inputModeDropdown.value = currentSettings.inputMode == "text" ? "Text" : "Video";
            }

            // Pipeline dropdown
            if (pipelineDropdown != null)
            {
                pipelineDropdown.choices = availablePipelines;
                pipelineDropdown.value = currentSettings.pipelineId;
            }

            // LoRA dropdown
            if (loraDropdown != null)
            {
                loraDropdown.choices = availableLoras;
                loraDropdown.value = "None";
            }

            // Initial values
            UpdateDenoiseLabel(currentSettings.denoisingSteps.Length);
            UpdateNoiseLabel(currentSettings.noiseScale);
            UpdateLoraScaleLabel(1.0f);
            UpdateVaceScaleLabel(currentSettings.vaceContextScale);

            // VACE toggle - explicitly sync with settings (default OFF)
            if (vaceToggle != null)
            {
                vaceToggle.value = currentSettings.vaceEnabled;
            }

            // Show default prompt in input field
            if (promptInput != null && currentSettings.prompts.Count > 0)
            {
                promptInput.value = currentSettings.prompts[0].text;
            }

            // Update visibility based on input mode
            UpdateUIForInputMode();
        }

        private void SendPrompt()
        {
            string prompt = promptInput?.value?.Trim();
            if (!string.IsNullOrEmpty(prompt))
            {
                currentSettings.prompts = new List<PromptItem>
                {
                    new PromptItem { text = prompt, weight = 1.0f }
                };
                OnPromptSent?.Invoke(prompt);
                OnSettingsChanged?.Invoke(currentSettings);
            }
        }

        private void RandomizeSeed()
        {
            int newSeed = UnityEngine.Random.Range(0, int.MaxValue);
            if (seedInput != null)
            {
                seedInput.value = newSeed;
            }
            currentSettings.seed = newSeed;
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void TogglePause()
        {
            isPaused = !isPaused;
            currentSettings.paused = isPaused;
            if (pauseButton != null)
            {
                pauseButton.text = isPaused ? "Resume" : "Pause";
            }
            OnPauseToggled?.Invoke(isPaused);
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void RequestReset()
        {
            OnResetRequested?.Invoke();
        }

        private void RequestStop()
        {
            OnStopRequested?.Invoke();
        }

        private void OnInputModeChanged(ChangeEvent<string> evt)
        {
            currentSettings.inputMode = evt.newValue.ToLower();
            UpdateUIForInputMode();
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void UpdateUIForInputMode()
        {
            bool isVideoMode = currentSettings.inputMode == "video";

            // Noise controls are only relevant in video mode
            if (noiseSlider != null)
                noiseSlider.SetEnabled(isVideoMode);
            if (noiseControllerToggle != null)
                noiseControllerToggle.SetEnabled(isVideoMode);
        }

        private void OnPipelineChanged(ChangeEvent<string> evt)
        {
            string oldPipeline = currentSettings.pipelineId;
            currentSettings.pipelineId = evt.newValue;

            // Update resolution based on pipeline
            switch (evt.newValue)
            {
                case "longlive":
                    currentSettings.width = 576;
                    currentSettings.height = 320;
                    break;
                case "streamdiffusionv2":
                    currentSettings.width = 512;
                    currentSettings.height = 512;
                    break;
                case "krea-realtime-video":
                    currentSettings.width = 1024;
                    currentSettings.height = 576;
                    break;
                default:
                    currentSettings.width = 640;
                    currentSettings.height = 360;
                    break;
            }

            // Fire pipeline change event if the pipeline actually changed
            // This allows MainController to handle reconnection
            if (oldPipeline != evt.newValue)
            {
                OnPipelineChangeRequested?.Invoke(evt.newValue);
            }

            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnSeedChanged(ChangeEvent<int> evt)
        {
            currentSettings.seed = evt.newValue;
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnDenoiseChanged(ChangeEvent<int> evt)
        {
            int steps = evt.newValue;
            UpdateDenoiseLabel(steps);

            // Generate denoising step list based on number of steps
            float[] stepList;
            switch (steps)
            {
                case 1:
                    stepList = new float[] { 1000 };
                    break;
                case 2:
                    stepList = new float[] { 1000, 500 };
                    break;
                case 3:
                    stepList = new float[] { 1000, 666, 333 };
                    break;
                case 4:
                default:
                    stepList = new float[] { 1000, 750, 500, 250 };
                    break;
            }
            currentSettings.denoisingSteps = stepList;
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnNoiseChanged(ChangeEvent<float> evt)
        {
            currentSettings.noiseScale = evt.newValue;
            UpdateNoiseLabel(evt.newValue);
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnNoiseControllerChanged(ChangeEvent<bool> evt)
        {
            currentSettings.noiseController = evt.newValue;
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnCacheChanged(ChangeEvent<bool> evt)
        {
            currentSettings.manageCache = evt.newValue;
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnLoraChanged(ChangeEvent<string> evt)
        {
            if (evt.newValue == "None" || string.IsNullOrEmpty(evt.newValue))
            {
                currentSettings.loras = new List<LoRAConfig>();
            }
            else
            {
                float scale = loraScaleSlider?.value ?? 1.0f;
                currentSettings.loras = new List<LoRAConfig>
                {
                    new LoRAConfig { name = evt.newValue, scale = scale }
                };
            }
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnLoraScaleChanged(ChangeEvent<float> evt)
        {
            UpdateLoraScaleLabel(evt.newValue);
            if (currentSettings.loras != null && currentSettings.loras.Count > 0)
            {
                currentSettings.loras[0].scale = evt.newValue;
                OnSettingsChanged?.Invoke(currentSettings);
            }
        }

        private void OnVaceChanged(ChangeEvent<bool> evt)
        {
            currentSettings.vaceEnabled = evt.newValue;
            // VACE toggle requires pipeline reload - fire special event for reconnection
            // Don't fire OnSettingsChanged as VACE can't be changed mid-stream
            OnVaceToggled?.Invoke(evt.newValue);
        }

        private void OnVaceScaleChanged(ChangeEvent<float> evt)
        {
            currentSettings.vaceContextScale = evt.newValue;
            UpdateVaceScaleLabel(evt.newValue);
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void UpdateDenoiseLabel(int steps)
        {
            if (denoiseValueLabel != null)
            {
                denoiseValueLabel.text = $"{steps} step{(steps > 1 ? "s" : "")}";
            }
        }

        private void UpdateNoiseLabel(float value)
        {
            if (noiseValueLabel != null)
            {
                noiseValueLabel.text = value.ToString("F2");
            }
        }

        private void UpdateLoraScaleLabel(float value)
        {
            if (loraScaleValueLabel != null)
            {
                loraScaleValueLabel.text = value.ToString("F2");
            }
        }

        private void UpdateVaceScaleLabel(float value)
        {
            if (vaceScaleValueLabel != null)
            {
                vaceScaleValueLabel.text = value.ToString("F2");
            }
        }

        public void SetAvailablePipelines(List<string> pipelines)
        {
            availablePipelines = pipelines;
            if (pipelineDropdown != null)
            {
                pipelineDropdown.choices = availablePipelines;
            }
        }

        public void SetAvailableLoras(List<string> loras)
        {
            availableLoras = new List<string> { "None" };
            availableLoras.AddRange(loras);
            if (loraDropdown != null)
            {
                loraDropdown.choices = availableLoras;
            }
        }

        public void ApplySettings(StreamSettings settings)
        {
            currentSettings = settings;

            if (pipelineDropdown != null && availablePipelines.Contains(settings.pipelineId))
            {
                pipelineDropdown.value = settings.pipelineId;
            }

            if (seedInput != null)
            {
                seedInput.value = settings.seed;
            }

            if (denoiseSlider != null)
            {
                denoiseSlider.value = settings.denoisingSteps?.Length ?? 4;
            }

            if (noiseSlider != null)
            {
                noiseSlider.value = settings.noiseScale;
            }

            if (noiseControllerToggle != null)
            {
                noiseControllerToggle.value = settings.noiseController;
            }

            if (cacheToggle != null)
            {
                cacheToggle.value = settings.manageCache;
            }

            if (vaceToggle != null)
            {
                vaceToggle.value = settings.vaceEnabled;
            }

            if (vaceScaleSlider != null)
            {
                vaceScaleSlider.value = settings.vaceContextScale;
            }

            if (promptInput != null && settings.prompts != null && settings.prompts.Count > 0)
            {
                promptInput.value = settings.prompts[0].text;
            }
        }
    }
}
