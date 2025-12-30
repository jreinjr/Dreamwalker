using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Dreamwalker.Quest
{
    /// <summary>
    /// Creates and manages the worldspace Canvas UI for Quest VR.
    /// Provides references to all UI elements for other controllers.
    /// </summary>
    public class QuestWorldspaceUI : MonoBehaviour
    {
        [Header("UI Positioning")]
        [SerializeField] private float distanceFromUser = 1.5f;
        [SerializeField] private float heightOffset = 1.3f;
        [SerializeField] private Vector2 canvasSize = new Vector2(0.6f, 0.45f); // 60cm x 45cm

        [Header("Video Display")]
        [SerializeField] private Vector2 videoDisplaySize = new Vector2(0.5f, 0.28f); // 50cm x 28cm (16:9)

        [Header("Main Canvas (assign if using prefab)")]
        [SerializeField] private Canvas mainCanvas;
        [SerializeField] private RectTransform mainCanvasRect;

        [Header("Top Bar")]
        [SerializeField] private Button serverButton;
        [SerializeField] private Button scopeButton;
        [SerializeField] private TextMeshProUGUI topStatusText;

        [Header("Server Menu Panel")]
        [SerializeField] private GameObject serverMenuPanel;
        [SerializeField] private TMP_InputField serverUrlInput;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private TextMeshProUGUI serverStatusText;

        [Header("Scope Menu Panel")]
        [SerializeField] private GameObject scopeMenuPanel;
        [SerializeField] private TMP_Dropdown pipelineDropdown;
        [SerializeField] private TMP_InputField promptInput;
        [SerializeField] private Button sendPromptButton;
        [SerializeField] private TMP_InputField seedInput;
        [SerializeField] private Button randomizeSeedButton;
        [SerializeField] private Slider denoiseSlider;
        [SerializeField] private TextMeshProUGUI denoiseValueText;
        [SerializeField] private Slider noiseScaleSlider;
        [SerializeField] private TextMeshProUGUI noiseScaleValueText;
        [SerializeField] private Toggle noiseControllerToggle;
        [SerializeField] private Toggle manageCacheToggle;
        [SerializeField] private TMP_Dropdown loraDropdown;
        [SerializeField] private Slider loraScaleSlider;
        [SerializeField] private TextMeshProUGUI loraScaleValueText;
        [SerializeField] private Toggle vaceToggle;
        [SerializeField] private Slider vaceContextScaleSlider;
        [SerializeField] private TextMeshProUGUI vaceScaleValueText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button stopButton;

        [Header("Bottom Status Bar")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI statsText;

        [Header("Video/Camera Displays")]
        [SerializeField] private RawImage videoDisplay;
        [SerializeField] private RawImage cameraPIP;

        // Public accessors
        public Canvas MainCanvas { get => mainCanvas; set => mainCanvas = value; }
        public RectTransform MainCanvasRect { get => mainCanvasRect; set => mainCanvasRect = value; }
        public Button ServerButton { get => serverButton; set => serverButton = value; }
        public Button ScopeButton { get => scopeButton; set => scopeButton = value; }
        public TextMeshProUGUI TopStatusText { get => topStatusText; set => topStatusText = value; }
        public GameObject ServerMenuPanel { get => serverMenuPanel; set => serverMenuPanel = value; }
        public TMP_InputField ServerUrlInput { get => serverUrlInput; set => serverUrlInput = value; }
        public Button ConnectButton { get => connectButton; set => connectButton = value; }
        public Button DisconnectButton { get => disconnectButton; set => disconnectButton = value; }
        public TextMeshProUGUI ServerStatusText { get => serverStatusText; set => serverStatusText = value; }
        public GameObject ScopeMenuPanel { get => scopeMenuPanel; set => scopeMenuPanel = value; }
        public TMP_Dropdown PipelineDropdown { get => pipelineDropdown; set => pipelineDropdown = value; }
        public TMP_InputField PromptInput { get => promptInput; set => promptInput = value; }
        public Button SendPromptButton { get => sendPromptButton; set => sendPromptButton = value; }
        public TMP_InputField SeedInput { get => seedInput; set => seedInput = value; }
        public Button RandomizeSeedButton { get => randomizeSeedButton; set => randomizeSeedButton = value; }
        public Slider DenoiseSlider { get => denoiseSlider; set => denoiseSlider = value; }
        public TextMeshProUGUI DenoiseValueText { get => denoiseValueText; set => denoiseValueText = value; }
        public Slider NoiseScaleSlider { get => noiseScaleSlider; set => noiseScaleSlider = value; }
        public TextMeshProUGUI NoiseScaleValueText { get => noiseScaleValueText; set => noiseScaleValueText = value; }
        public Toggle NoiseControllerToggle { get => noiseControllerToggle; set => noiseControllerToggle = value; }
        public Toggle ManageCacheToggle { get => manageCacheToggle; set => manageCacheToggle = value; }
        public TMP_Dropdown LoRADropdown { get => loraDropdown; set => loraDropdown = value; }
        public Slider LoRAScaleSlider { get => loraScaleSlider; set => loraScaleSlider = value; }
        public TextMeshProUGUI LoRAScaleValueText { get => loraScaleValueText; set => loraScaleValueText = value; }
        public Toggle VACEToggle { get => vaceToggle; set => vaceToggle = value; }
        public Slider VACEContextScaleSlider { get => vaceContextScaleSlider; set => vaceContextScaleSlider = value; }
        public TextMeshProUGUI VACEScaleValueText { get => vaceScaleValueText; set => vaceScaleValueText = value; }
        public Button PauseButton { get => pauseButton; set => pauseButton = value; }
        public Button ResetButton { get => resetButton; set => resetButton = value; }
        public Button StopButton { get => stopButton; set => stopButton = value; }
        public TextMeshProUGUI StatusText { get => statusText; set => statusText = value; }
        public TextMeshProUGUI StatsText { get => statsText; set => statsText = value; }
        public RawImage VideoDisplay { get => videoDisplay; set => videoDisplay = value; }
        public RawImage CameraPIP { get => cameraPIP; set => cameraPIP = value; }

        // Colors
        private readonly Color panelColor = new Color(0.12f, 0.12f, 0.16f, 0.95f);
        private readonly Color buttonColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        private readonly Color buttonHighlightColor = new Color(0.3f, 0.4f, 0.6f, 1f);
        private readonly Color accentColor = new Color(0.4f, 0.6f, 1f, 1f);
        private readonly Color textColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        private void Awake()
        {
            CreateUI();
        }

        private void CreateUI()
        {
            // // Create main canvas
            // CreateMainCanvas();

            // // Create video display (background)
            // CreateVideoDisplay();

            // // Create main panel with all UI elements
            // CreateMainPanel();

            // // Create camera PIP
            // CreateCameraPIP();

            // Hide menus by default
            if (ServerMenuPanel != null) ServerMenuPanel.SetActive(false);
            if (ScopeMenuPanel != null) ScopeMenuPanel.SetActive(false);
        }

        private void CreateMainCanvas()
        {
            // Create canvas GameObject
            var canvasGO = new GameObject("WorldspaceCanvas");
            canvasGO.transform.SetParent(transform);

            MainCanvas = canvasGO.AddComponent<Canvas>();
            MainCanvas.renderMode = RenderMode.WorldSpace;

            var canvasScaler = canvasGO.AddComponent<CanvasScaler>();
            canvasScaler.dynamicPixelsPerUnit = 100;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Set up rect transform
            MainCanvasRect = MainCanvas.GetComponent<RectTransform>();
            MainCanvasRect.sizeDelta = canvasSize * 1000; // Convert meters to UI units
            MainCanvasRect.localScale = Vector3.one * 0.001f; // Scale down to world units

            // Position in front of user
            canvasGO.transform.localPosition = new Vector3(0, heightOffset, distanceFromUser);
            canvasGO.transform.localRotation = Quaternion.identity;

            Debug.Log($"[QuestWorldspaceUI] Canvas created at position {canvasGO.transform.localPosition}");
        }

        private void CreateVideoDisplay()
        {
            var videoGO = new GameObject("VideoDisplay");
            videoGO.transform.SetParent(MainCanvas.transform, false);

            VideoDisplay = videoGO.AddComponent<RawImage>();
            VideoDisplay.color = Color.black;

            var rect = VideoDisplay.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = videoDisplaySize * 1000;
            rect.anchoredPosition = new Vector2(0, 20); // Slightly above center

            // Put behind UI
            videoGO.transform.SetAsFirstSibling();
        }

        private void CreateMainPanel()
        {
            var panelGO = CreatePanel("MainPanel", MainCanvas.transform);
            var panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Make panel background transparent
            panelGO.GetComponent<Image>().color = Color.clear;

            // Create top bar
            CreateTopBar(panelGO.transform);

            // Create server menu panel
            CreateServerMenuPanel(panelGO.transform);

            // Create scope menu panel
            CreateScopeMenuPanel(panelGO.transform);

            // Create bottom status bar
            CreateBottomBar(panelGO.transform);
        }

        private void CreateTopBar(Transform parent)
        {
            var topBar = CreatePanel("TopBar", parent);
            var topBarRect = topBar.GetComponent<RectTransform>();
            topBarRect.anchorMin = new Vector2(0, 1);
            topBarRect.anchorMax = new Vector2(1, 1);
            topBarRect.pivot = new Vector2(0.5f, 1);
            topBarRect.sizeDelta = new Vector2(0, 40);
            topBarRect.anchoredPosition = Vector2.zero;
            topBar.GetComponent<Image>().color = panelColor;

            var layout = topBar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // Server button
            ServerButton = CreateButton("Server", topBar.transform, new Vector2(80, 30));

            // Scope button
            ScopeButton = CreateButton("Scope", topBar.transform, new Vector2(80, 30));

            // Spacer
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(topBar.transform, false);
            var spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1;

            // Status text
            TopStatusText = CreateText("Ready", topBar.transform, 14);
            TopStatusText.alignment = TextAlignmentOptions.Right;
            var statusLayout = TopStatusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredWidth = 200;
        }

        private void CreateServerMenuPanel(Transform parent)
        {
            ServerMenuPanel = CreatePanel("ServerMenuPanel", parent);
            var rect = ServerMenuPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(200, -80);
            rect.anchoredPosition = new Vector2(10, -50);
            ServerMenuPanel.GetComponent<Image>().color = panelColor;

            var layout = ServerMenuPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Header
            var header = CreateText("SERVER", ServerMenuPanel.transform, 16);
            header.fontStyle = FontStyles.Bold;
            header.alignment = TextAlignmentOptions.Center;
            var headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 25;

            // URL Input
            ServerUrlInput = CreateInputField("Server URL...", ServerMenuPanel.transform);
            var urlLayout = ServerUrlInput.gameObject.AddComponent<LayoutElement>();
            urlLayout.preferredHeight = 35;

            // Connect button
            ConnectButton = CreateButton("Connect", ServerMenuPanel.transform, new Vector2(0, 35));
            var connectLayout = ConnectButton.gameObject.AddComponent<LayoutElement>();
            connectLayout.preferredHeight = 35;

            // Disconnect button
            DisconnectButton = CreateButton("Disconnect", ServerMenuPanel.transform, new Vector2(0, 35));
            var disconnectLayout = DisconnectButton.gameObject.AddComponent<LayoutElement>();
            disconnectLayout.preferredHeight = 35;
            DisconnectButton.gameObject.SetActive(false);

            // Status label
            ServerStatusText = CreateText("Disconnected", ServerMenuPanel.transform, 12);
            ServerStatusText.color = new Color(1f, 0.6f, 0.2f); // Orange for disconnected
            var statusLayout = ServerStatusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredHeight = 20;
        }

        private void CreateScopeMenuPanel(Transform parent)
        {
            ScopeMenuPanel = CreatePanel("ScopeMenuPanel", parent);
            var rect = ScopeMenuPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(220, -80);
            rect.anchoredPosition = new Vector2(10, -50);
            ScopeMenuPanel.GetComponent<Image>().color = panelColor;

            // Add scroll view for long content
            var scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(ScopeMenuPanel.transform, false);
            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            var scrollRectTransform = scrollGO.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(5, 5);
            scrollRectTransform.offsetMax = new Vector2(-5, -5);

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewport = viewportGO.AddComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;
            viewportGO.AddComponent<Image>().color = Color.clear;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var content = contentGO.GetComponent<RectTransform>();
            if (content == null) content = contentGO.AddComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 600);
            content.anchoredPosition = Vector2.zero;

            var layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var contentSizeFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = content;
            scrollRect.viewport = viewport;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            // Header
            var header = CreateText("SCOPE", contentGO.transform, 16);
            header.fontStyle = FontStyles.Bold;
            header.alignment = TextAlignmentOptions.Center;
            AddLayoutElement(header.gameObject, 25);

            // Pipeline dropdown
            CreateLabel("Pipeline", contentGO.transform);
            PipelineDropdown = CreateDropdown(contentGO.transform);
            AddLayoutElement(PipelineDropdown.gameObject, 35);

            // Prompt input
            CreateLabel("Prompt", contentGO.transform);
            PromptInput = CreateInputField("Enter prompt...", contentGO.transform, true);
            AddLayoutElement(PromptInput.gameObject, 60);

            // Send button
            SendPromptButton = CreateButton("Send", contentGO.transform, new Vector2(0, 30));
            AddLayoutElement(SendPromptButton.gameObject, 30);

            // Seed input with randomize button
            CreateLabel("Seed", contentGO.transform);
            var seedRow = CreateHorizontalRow(contentGO.transform);
            SeedInput = CreateInputField("42", seedRow.transform);
            SeedInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            var seedInputLayout = SeedInput.gameObject.AddComponent<LayoutElement>();
            seedInputLayout.flexibleWidth = 1;
            RandomizeSeedButton = CreateButton("Rand", seedRow.transform, new Vector2(50, 30));
            AddLayoutElement(seedRow, 35);

            // Denoise slider
            CreateLabel("Denoise Steps", contentGO.transform);
            var denoiseRow = CreateHorizontalRow(contentGO.transform);
            DenoiseSlider = CreateSlider(denoiseRow.transform, 1, 4, true);
            DenoiseSlider.value = 4;
            var denoiseSliderLayout = DenoiseSlider.gameObject.AddComponent<LayoutElement>();
            denoiseSliderLayout.flexibleWidth = 1;
            DenoiseValueText = CreateText("4", denoiseRow.transform, 12);
            var denoiseTextLayout = DenoiseValueText.gameObject.AddComponent<LayoutElement>();
            denoiseTextLayout.preferredWidth = 30;
            AddLayoutElement(denoiseRow, 30);

            // Noise Scale slider
            CreateLabel("Noise Scale", contentGO.transform);
            var noiseRow = CreateHorizontalRow(contentGO.transform);
            NoiseScaleSlider = CreateSlider(noiseRow.transform, 0, 1, false);
            NoiseScaleSlider.value = 0.8f;
            var noiseSliderLayout = NoiseScaleSlider.gameObject.AddComponent<LayoutElement>();
            noiseSliderLayout.flexibleWidth = 1;
            NoiseScaleValueText = CreateText("0.80", noiseRow.transform, 12);
            var noiseTextLayout = NoiseScaleValueText.gameObject.AddComponent<LayoutElement>();
            noiseTextLayout.preferredWidth = 40;
            AddLayoutElement(noiseRow, 30);

            // Toggles
            NoiseControllerToggle = CreateToggle("Noise Controller", contentGO.transform);
            NoiseControllerToggle.isOn = true;
            AddLayoutElement(NoiseControllerToggle.gameObject, 25);

            ManageCacheToggle = CreateToggle("Manage Cache", contentGO.transform);
            ManageCacheToggle.isOn = true;
            AddLayoutElement(ManageCacheToggle.gameObject, 25);

            // LoRA dropdown and scale
            CreateLabel("LoRA", contentGO.transform);
            LoRADropdown = CreateDropdown(contentGO.transform);
            AddLayoutElement(LoRADropdown.gameObject, 35);

            var loraScaleRow = CreateHorizontalRow(contentGO.transform);
            var loraScaleLabel = CreateText("Scale:", loraScaleRow.transform, 12);
            var loraLabelLayout = loraScaleLabel.gameObject.AddComponent<LayoutElement>();
            loraLabelLayout.preferredWidth = 40;
            LoRAScaleSlider = CreateSlider(loraScaleRow.transform, 0, 2, false);
            LoRAScaleSlider.value = 1f;
            var loraSliderLayout = LoRAScaleSlider.gameObject.AddComponent<LayoutElement>();
            loraSliderLayout.flexibleWidth = 1;
            LoRAScaleValueText = CreateText("1.00", loraScaleRow.transform, 12);
            var loraTextLayout = LoRAScaleValueText.gameObject.AddComponent<LayoutElement>();
            loraTextLayout.preferredWidth = 40;
            AddLayoutElement(loraScaleRow, 30);

            // VACE toggle and scale
            VACEToggle = CreateToggle("VACE", contentGO.transform);
            VACEToggle.isOn = true;
            AddLayoutElement(VACEToggle.gameObject, 25);

            var vaceScaleRow = CreateHorizontalRow(contentGO.transform);
            var vaceScaleLabel = CreateText("Context:", vaceScaleRow.transform, 12);
            var vaceLabelLayout = vaceScaleLabel.gameObject.AddComponent<LayoutElement>();
            vaceLabelLayout.preferredWidth = 50;
            VACEContextScaleSlider = CreateSlider(vaceScaleRow.transform, 0, 2, false);
            VACEContextScaleSlider.value = 1f;
            var vaceSliderLayout = VACEContextScaleSlider.gameObject.AddComponent<LayoutElement>();
            vaceSliderLayout.flexibleWidth = 1;
            VACEScaleValueText = CreateText("1.00", vaceScaleRow.transform, 12);
            var vaceTextLayout = VACEScaleValueText.gameObject.AddComponent<LayoutElement>();
            vaceTextLayout.preferredWidth = 40;
            AddLayoutElement(vaceScaleRow, 30);

            // Control buttons row
            var buttonRow = CreateHorizontalRow(contentGO.transform);
            var buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 5;
            PauseButton = CreateButton("Pause", buttonRow.transform, new Vector2(60, 30));
            ResetButton = CreateButton("Reset", buttonRow.transform, new Vector2(60, 30));
            StopButton = CreateButton("Stop", buttonRow.transform, new Vector2(60, 30));
            AddLayoutElement(buttonRow, 35);
        }

        private void CreateBottomBar(Transform parent)
        {
            var bottomBar = CreatePanel("BottomBar", parent);
            var bottomBarRect = bottomBar.GetComponent<RectTransform>();
            bottomBarRect.anchorMin = new Vector2(0, 0);
            bottomBarRect.anchorMax = new Vector2(1, 0);
            bottomBarRect.pivot = new Vector2(0.5f, 0);
            bottomBarRect.sizeDelta = new Vector2(0, 30);
            bottomBarRect.anchoredPosition = Vector2.zero;
            bottomBar.GetComponent<Image>().color = panelColor;

            var layout = bottomBar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // Status text
            StatusText = CreateText("Status: Ready", bottomBar.transform, 12);
            var statusLayout = StatusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredWidth = 300;

            // Spacer
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(bottomBar.transform, false);
            var spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1;

            // Stats text
            StatsText = CreateText("", bottomBar.transform, 12);
            StatsText.alignment = TextAlignmentOptions.Right;
            var statsLayout = StatsText.gameObject.AddComponent<LayoutElement>();
            statsLayout.preferredWidth = 200;
        }

        private void CreateCameraPIP()
        {
            var pipGO = CreatePanel("CameraPIP", MainCanvas.transform);
            var pipRect = pipGO.GetComponent<RectTransform>();
            pipRect.anchorMin = new Vector2(1, 0);
            pipRect.anchorMax = new Vector2(1, 0);
            pipRect.pivot = new Vector2(1, 0);
            pipRect.sizeDelta = new Vector2(120, 68); // 16:9 aspect
            pipRect.anchoredPosition = new Vector2(-10, 40);

            CameraPIP = pipGO.AddComponent<RawImage>();
            CameraPIP.color = new Color(0.2f, 0.2f, 0.2f);
        }

        #region UI Creation Helpers

        private GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = panelColor;
            return go;
        }

        private Button CreateButton(string text, Transform parent, Vector2 size)
        {
            var go = new GameObject(text + "Button");
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = buttonColor;

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = buttonHighlightColor;
            colors.pressedColor = accentColor;
            button.colors = colors;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private TextMeshProUGUI CreateText(string text, Transform parent, int fontSize)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Left;
            return tmp;
        }

        private void CreateLabel(string text, Transform parent)
        {
            var label = CreateText(text, parent, 11);
            label.color = new Color(0.7f, 0.7f, 0.7f);
            AddLayoutElement(label.gameObject, 18);
        }

        private TMP_InputField CreateInputField(string placeholder, Transform parent, bool multiline = false)
        {
            var go = new GameObject("InputField");
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.2f);

            var inputField = go.AddComponent<TMP_InputField>();

            // Text area
            var textAreaGO = new GameObject("Text Area");
            textAreaGO.transform.SetParent(go.transform, false);
            var textAreaRect = textAreaGO.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(8, 4);
            textAreaRect.offsetMax = new Vector2(-8, -4);

            // Placeholder
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            var placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
            placeholderText.text = placeholder;
            placeholderText.fontSize = 12;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f);
            placeholderText.alignment = TextAlignmentOptions.Left;
            var placeholderRect = placeholderGO.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            // Text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textAreaGO.transform, false);
            var textComponent = textGO.AddComponent<TextMeshProUGUI>();
            textComponent.fontSize = 12;
            textComponent.color = textColor;
            textComponent.alignment = TextAlignmentOptions.Left;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            inputField.textViewport = textAreaRect;
            inputField.textComponent = textComponent;
            inputField.placeholder = placeholderText;

            if (multiline)
            {
                inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
                textComponent.enableWordWrapping = true;
            }

            return inputField;
        }

        private TMP_Dropdown CreateDropdown(Transform parent)
        {
            var go = new GameObject("Dropdown");
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.2f);

            var dropdown = go.AddComponent<TMP_Dropdown>();

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.fontSize = 12;
            label.color = textColor;
            label.alignment = TextAlignmentOptions.Left;
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 2);
            labelRect.offsetMax = new Vector2(-25, -2);

            // Arrow
            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(go.transform, false);
            var arrow = arrowGO.AddComponent<Image>();
            arrow.color = textColor;
            var arrowRect = arrowGO.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.sizeDelta = new Vector2(15, 15);
            arrowRect.anchoredPosition = new Vector2(-15, 0);

            // Template (minimal setup)
            var templateGO = new GameObject("Template");
            templateGO.transform.SetParent(go.transform, false);
            var templateRect = templateGO.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.sizeDelta = new Vector2(0, 150);
            templateGO.AddComponent<Image>().color = panelColor;
            templateGO.AddComponent<ScrollRect>();
            templateGO.SetActive(false);

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(templateGO.transform, false);
            var viewportRect = viewportGO.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;
            viewportGO.AddComponent<Image>().color = Color.clear;

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 28);

            // Item
            var itemGO = new GameObject("Item");
            itemGO.transform.SetParent(contentGO.transform, false);
            var itemRect = itemGO.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 25);
            var itemToggle = itemGO.AddComponent<Toggle>();
            itemGO.AddComponent<Image>().color = Color.clear;

            var itemLabelGO = new GameObject("Item Label");
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            var itemLabel = itemLabelGO.AddComponent<TextMeshProUGUI>();
            itemLabel.fontSize = 12;
            itemLabel.color = textColor;
            var itemLabelRect = itemLabelGO.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(10, 0);
            itemLabelRect.offsetMax = new Vector2(-10, 0);

            dropdown.captionText = label;
            dropdown.itemText = itemLabel;
            dropdown.template = templateRect;

            return dropdown;
        }

        private Slider CreateSlider(Transform parent, float min, float max, bool wholeNumbers)
        {
            var go = new GameObject("Slider");
            go.transform.SetParent(parent, false);

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var bgImage = bgGO.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.2f);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Fill Area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(go.transform, false);
            var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);

            // Fill
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillImage = fillGO.AddComponent<Image>();
            fillImage.color = accentColor;
            var fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.sizeDelta = new Vector2(10, 0);

            // Handle Slide Area
            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(go.transform, false);
            var handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0, 0);
            handleAreaRect.anchorMax = new Vector2(1, 1);
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            // Handle
            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleImage = handleGO.AddComponent<Image>();
            handleImage.color = Color.white;
            var handleRect = handleGO.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(15, 0);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;

            return slider;
        }

        private Toggle CreateToggle(string labelText, Transform parent)
        {
            var go = new GameObject(labelText + "Toggle");
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8;
            layout.childControlWidth = false;
            layout.childControlHeight = true;

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var bgImage = bgGO.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.2f);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(20, 20);

            // Checkmark
            var checkGO = new GameObject("Checkmark");
            checkGO.transform.SetParent(bgGO.transform, false);
            var checkImage = checkGO.AddComponent<Image>();
            checkImage.color = accentColor;
            var checkRect = checkGO.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;

            // Label
            var label = CreateText(labelText, go.transform, 12);

            var toggle = go.AddComponent<Toggle>();
            toggle.graphic = checkImage;
            toggle.targetGraphic = bgImage;

            return toggle;
        }

        private GameObject CreateHorizontalRow(Transform parent)
        {
            var go = new GameObject("Row");
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            return go;
        }

        private void AddLayoutElement(GameObject go, float preferredHeight)
        {
            var layout = go.GetComponent<LayoutElement>();
            if (layout == null) layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
        }

        #endregion

        /// <summary>
        /// Positions the UI in front of the given camera/transform
        /// </summary>
        public void PositionInFrontOf(Transform target)
        {
            if (MainCanvas == null) return;

            Vector3 forward = target.forward;
            forward.y = 0;
            forward.Normalize();

            MainCanvas.transform.position = target.position + forward * distanceFromUser + Vector3.up * heightOffset;
            MainCanvas.transform.rotation = Quaternion.LookRotation(forward);
        }
    }
}
