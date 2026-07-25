using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using Dreamwalker.Models;

namespace Dreamwalker.Quest
{
    /// <summary>
    /// Handles the Scope Settings menu for Quest worldspace UI.
    /// Unity UI version of ScopeMenu.cs.
    /// </summary>
    public class QuestScopeMenu : MonoBehaviour
    {
        [Header("UI References - Pipeline")]
        [SerializeField] private TMP_Dropdown pipelineDropdown;

        [Header("UI References - Prompt")]
        [SerializeField] private TMP_InputField promptInput;
        [SerializeField] private Button sendPromptButton;

        [Header("UI References - Seed")]
        [SerializeField] private TMP_InputField seedInput;
        [SerializeField] private Button randomizeSeedButton;

        [Header("UI References - Sliders")]
        [SerializeField] private Slider denoiseSlider;
        [SerializeField] private TextMeshProUGUI denoiseValueText;
        [SerializeField] private Slider noiseScaleSlider;
        [SerializeField] private TextMeshProUGUI noiseScaleValueText;

        [Header("UI References - Toggles")]
        [SerializeField] private Toggle noiseControllerToggle;
        [SerializeField] private Toggle manageCacheToggle;

        [Header("UI References - LoRA")]
        [SerializeField] private TMP_Dropdown loraDropdown;
        [SerializeField] private Slider loraScaleSlider;
        [SerializeField] private TextMeshProUGUI loraScaleValueText;

        [Header("UI References - VACE")]
        [SerializeField] private Toggle vaceToggle;
        [SerializeField] private Slider vaceContextScaleSlider;
        [SerializeField] private TextMeshProUGUI vaceScaleValueText;

        [Header("UI References - Control Buttons")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button stopButton;

        // State
        private StreamSettings currentSettings = new StreamSettings();
        private bool isPaused = false;
        private List<string> availablePipelines = new List<string>
        {
            "longlive",
            "streamdiffusionv2",
            "krea-realtime-video",
            "reward-forcing",
            "passthrough"
        };
        private List<string> availableLoras = new List<string> { "None" };

        // Events
        public event Action<StreamSettings> OnSettingsChanged;
        public event Action<string> OnPromptSent;
        public event Action<bool> OnPauseToggled;
        public event Action OnResetRequested;
        public event Action OnStopRequested;
        public event Action<string> OnPipelineChangeRequested;
        public event Action<bool> OnVaceToggled;

        public StreamSettings CurrentSettings => currentSettings;

        /// <summary>
        /// Initialize with UI references from QuestWorldspaceUI.
        /// </summary>
        public void Initialize(QuestWorldspaceUI ui)
        {
            pipelineDropdown = ui.PipelineDropdown;
            promptInput = ui.PromptInput;
            sendPromptButton = ui.SendPromptButton;
            seedInput = ui.SeedInput;
            randomizeSeedButton = ui.RandomizeSeedButton;
            denoiseSlider = ui.DenoiseSlider;
            denoiseValueText = ui.DenoiseValueText;
            noiseScaleSlider = ui.NoiseScaleSlider;
            noiseScaleValueText = ui.NoiseScaleValueText;
            noiseControllerToggle = ui.NoiseControllerToggle;
            manageCacheToggle = ui.ManageCacheToggle;
            loraDropdown = ui.LoRADropdown;
            loraScaleSlider = ui.LoRAScaleSlider;
            loraScaleValueText = ui.LoRAScaleValueText;
            vaceToggle = ui.VACEToggle;
            vaceContextScaleSlider = ui.VACEContextScaleSlider;
            vaceScaleValueText = ui.VACEScaleValueText;
            pauseButton = ui.PauseButton;
            resetButton = ui.ResetButton;
            stopButton = ui.StopButton;

            Initialize();
        }

        /// <summary>
        /// Initialize with existing serialized references.
        /// </summary>
        public void Initialize()
        {
            BindEvents();
            InitializeDefaults();
            Debug.Log("[QuestScopeMenu] Initialized");
        }

        private void BindEvents()
        {
            // Buttons
            if (sendPromptButton != null)
                sendPromptButton.onClick.AddListener(SendPrompt);

            if (randomizeSeedButton != null)
                randomizeSeedButton.onClick.AddListener(RandomizeSeed);

            if (pauseButton != null)
                pauseButton.onClick.AddListener(TogglePause);

            if (resetButton != null)
                resetButton.onClick.AddListener(RequestReset);

            if (stopButton != null)
                stopButton.onClick.AddListener(RequestStop);

            // Dropdowns
            if (pipelineDropdown != null)
                pipelineDropdown.onValueChanged.AddListener(OnPipelineChanged);

            if (loraDropdown != null)
                loraDropdown.onValueChanged.AddListener(OnLoraChanged);

            // Sliders
            if (denoiseSlider != null)
                denoiseSlider.onValueChanged.AddListener(OnDenoiseChanged);

            if (noiseScaleSlider != null)
                noiseScaleSlider.onValueChanged.AddListener(OnNoiseChanged);

            if (loraScaleSlider != null)
                loraScaleSlider.onValueChanged.AddListener(OnLoraScaleChanged);

            if (vaceContextScaleSlider != null)
                vaceContextScaleSlider.onValueChanged.AddListener(OnVaceScaleChanged);

            // Toggles
            if (noiseControllerToggle != null)
                noiseControllerToggle.onValueChanged.AddListener(OnNoiseControllerChanged);

            if (manageCacheToggle != null)
                manageCacheToggle.onValueChanged.AddListener(OnCacheChanged);

            if (vaceToggle != null)
                vaceToggle.onValueChanged.AddListener(OnVaceChanged);

            // Input fields
            if (seedInput != null)
                seedInput.onEndEdit.AddListener(OnSeedChanged);
        }

        private void OnDestroy()
        {
            // Buttons
            if (sendPromptButton != null)
                sendPromptButton.onClick.RemoveListener(SendPrompt);

            if (randomizeSeedButton != null)
                randomizeSeedButton.onClick.RemoveListener(RandomizeSeed);

            if (pauseButton != null)
                pauseButton.onClick.RemoveListener(TogglePause);

            if (resetButton != null)
                resetButton.onClick.RemoveListener(RequestReset);

            if (stopButton != null)
                stopButton.onClick.RemoveListener(RequestStop);

            // Dropdowns
            if (pipelineDropdown != null)
                pipelineDropdown.onValueChanged.RemoveListener(OnPipelineChanged);

            if (loraDropdown != null)
                loraDropdown.onValueChanged.RemoveListener(OnLoraChanged);

            // Sliders
            if (denoiseSlider != null)
                denoiseSlider.onValueChanged.RemoveListener(OnDenoiseChanged);

            if (noiseScaleSlider != null)
                noiseScaleSlider.onValueChanged.RemoveListener(OnNoiseChanged);

            if (loraScaleSlider != null)
                loraScaleSlider.onValueChanged.RemoveListener(OnLoraScaleChanged);

            if (vaceContextScaleSlider != null)
                vaceContextScaleSlider.onValueChanged.RemoveListener(OnVaceScaleChanged);

            // Toggles
            if (noiseControllerToggle != null)
                noiseControllerToggle.onValueChanged.RemoveListener(OnNoiseControllerChanged);

            if (manageCacheToggle != null)
                manageCacheToggle.onValueChanged.RemoveListener(OnCacheChanged);

            if (vaceToggle != null)
                vaceToggle.onValueChanged.RemoveListener(OnVaceChanged);

            // Input fields
            if (seedInput != null)
                seedInput.onEndEdit.RemoveListener(OnSeedChanged);
        }

        private void InitializeDefaults()
        {
            // Pipeline dropdown
            if (pipelineDropdown != null)
            {
                pipelineDropdown.ClearOptions();
                pipelineDropdown.AddOptions(availablePipelines);
                int index = availablePipelines.IndexOf(currentSettings.pipelineId);
                if (index >= 0) pipelineDropdown.value = index;
            }

            // LoRA dropdown
            if (loraDropdown != null)
            {
                loraDropdown.ClearOptions();
                loraDropdown.AddOptions(availableLoras);
                loraDropdown.value = 0;
            }

            // Seed
            if (seedInput != null)
            {
                seedInput.text = currentSettings.seed.ToString();
            }

            // Denoise slider (1-4)
            if (denoiseSlider != null)
            {
                denoiseSlider.minValue = 1;
                denoiseSlider.maxValue = 4;
                denoiseSlider.wholeNumbers = true;
                denoiseSlider.value = currentSettings.denoisingSteps?.Length ?? 4;
            }
            UpdateDenoiseLabel(currentSettings.denoisingSteps?.Length ?? 4);

            // Noise scale slider (0-1)
            if (noiseScaleSlider != null)
            {
                noiseScaleSlider.minValue = 0;
                noiseScaleSlider.maxValue = 1;
                noiseScaleSlider.value = currentSettings.noiseScale;
            }
            UpdateNoiseLabel(currentSettings.noiseScale);

            // LoRA scale slider (0-2)
            if (loraScaleSlider != null)
            {
                loraScaleSlider.minValue = 0;
                loraScaleSlider.maxValue = 2;
                loraScaleSlider.value = 1f;
            }
            UpdateLoraScaleLabel(1f);

            // VACE scale slider (0-2)
            if (vaceContextScaleSlider != null)
            {
                vaceContextScaleSlider.minValue = 0;
                vaceContextScaleSlider.maxValue = 2;
                vaceContextScaleSlider.value = currentSettings.vaceContextScale;
            }
            UpdateVaceScaleLabel(currentSettings.vaceContextScale);

            // Toggles
            if (noiseControllerToggle != null)
                noiseControllerToggle.isOn = currentSettings.noiseController;

            if (manageCacheToggle != null)
                manageCacheToggle.isOn = currentSettings.manageCache;

            if (vaceToggle != null)
                vaceToggle.isOn = currentSettings.vaceEnabled;

            // Prompt
            if (promptInput != null && currentSettings.prompts.Count > 0)
            {
                promptInput.text = currentSettings.prompts[0].text;
            }
        }

        #region Event Handlers

        private void SendPrompt()
        {
            string prompt = promptInput?.text?.Trim();
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
                seedInput.text = newSeed.ToString();
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
                var buttonText = pauseButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                    buttonText.text = isPaused ? "Resume" : "Pause";
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

        private void OnPipelineChanged(int index)
        {
            if (index < 0 || index >= availablePipelines.Count) return;

            string oldPipeline = currentSettings.pipelineId;
            string newPipeline = availablePipelines[index];
            currentSettings.pipelineId = newPipeline;

            // Update resolution based on pipeline
            switch (newPipeline)
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

            Debug.Log($"[QuestScopeMenu] Pipeline changed to {newPipeline}, resolution: {currentSettings.width}x{currentSettings.height}");

            if (oldPipeline != newPipeline)
            {
                OnPipelineChangeRequested?.Invoke(newPipeline);
            }

            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnSeedChanged(string value)
        {
            if (int.TryParse(value, out int seed))
            {
                currentSettings.seed = seed;
                OnSettingsChanged?.Invoke(currentSettings);
            }
        }

        private void OnDenoiseChanged(float value)
        {
            int steps = Mathf.RoundToInt(value);
            UpdateDenoiseLabel(steps);

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

        private void OnNoiseChanged(float value)
        {
            currentSettings.noiseScale = value;
            UpdateNoiseLabel(value);
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnNoiseControllerChanged(bool value)
        {
            currentSettings.noiseController = value;
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnCacheChanged(bool value)
        {
            currentSettings.manageCache = value;
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnLoraChanged(int index)
        {
            if (index < 0 || index >= availableLoras.Count) return;

            string loraName = availableLoras[index];
            if (loraName == "None" || string.IsNullOrEmpty(loraName))
            {
                currentSettings.loras = new List<LoRAConfig>();
            }
            else
            {
                float scale = loraScaleSlider?.value ?? 1.0f;
                currentSettings.loras = new List<LoRAConfig>
                {
                    new LoRAConfig { name = loraName, scale = scale }
                };
            }
            OnSettingsChanged?.Invoke(currentSettings);
        }

        private void OnLoraScaleChanged(float value)
        {
            UpdateLoraScaleLabel(value);
            if (currentSettings.loras != null && currentSettings.loras.Count > 0)
            {
                currentSettings.loras[0].scale = value;
                OnSettingsChanged?.Invoke(currentSettings);
            }
        }

        private void OnVaceChanged(bool value)
        {
            currentSettings.vaceEnabled = value;
            OnVaceToggled?.Invoke(value);
        }

        private void OnVaceScaleChanged(float value)
        {
            currentSettings.vaceContextScale = value;
            UpdateVaceScaleLabel(value);
            OnSettingsChanged?.Invoke(currentSettings);
        }

        #endregion

        #region Label Updates

        private void UpdateDenoiseLabel(int steps)
        {
            if (denoiseValueText != null)
            {
                denoiseValueText.text = $"{steps}";
            }
        }

        private void UpdateNoiseLabel(float value)
        {
            if (noiseScaleValueText != null)
            {
                noiseScaleValueText.text = value.ToString("F2");
            }
        }

        private void UpdateLoraScaleLabel(float value)
        {
            if (loraScaleValueText != null)
            {
                loraScaleValueText.text = value.ToString("F2");
            }
        }

        private void UpdateVaceScaleLabel(float value)
        {
            if (vaceScaleValueText != null)
            {
                vaceScaleValueText.text = value.ToString("F2");
            }
        }

        #endregion

        #region Public Methods

        public void SetAvailablePipelines(List<string> pipelines)
        {
            availablePipelines = pipelines;
            if (pipelineDropdown != null)
            {
                pipelineDropdown.ClearOptions();
                pipelineDropdown.AddOptions(availablePipelines);
            }
        }

        public void SetAvailableLoras(List<string> loras)
        {
            availableLoras = new List<string> { "None" };
            availableLoras.AddRange(loras);
            if (loraDropdown != null)
            {
                loraDropdown.ClearOptions();
                loraDropdown.AddOptions(availableLoras);
            }
        }

        public void ApplySettings(StreamSettings settings)
        {
            currentSettings = settings;

            if (pipelineDropdown != null)
            {
                int index = availablePipelines.IndexOf(settings.pipelineId);
                if (index >= 0) pipelineDropdown.value = index;
            }

            if (seedInput != null)
            {
                seedInput.text = settings.seed.ToString();
            }

            if (denoiseSlider != null)
            {
                denoiseSlider.value = settings.denoisingSteps?.Length ?? 4;
            }

            if (noiseScaleSlider != null)
            {
                noiseScaleSlider.value = settings.noiseScale;
            }

            if (noiseControllerToggle != null)
            {
                noiseControllerToggle.isOn = settings.noiseController;
            }

            if (manageCacheToggle != null)
            {
                manageCacheToggle.isOn = settings.manageCache;
            }

            if (vaceToggle != null)
            {
                vaceToggle.isOn = settings.vaceEnabled;
            }

            if (vaceContextScaleSlider != null)
            {
                vaceContextScaleSlider.value = settings.vaceContextScale;
            }

            if (promptInput != null && settings.prompts != null && settings.prompts.Count > 0)
            {
                promptInput.text = settings.prompts[0].text;
            }
        }

        #endregion
    }
}
