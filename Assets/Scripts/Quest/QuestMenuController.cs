using UnityEngine;
using UnityEngine.UI;
using System;

namespace Dreamwalker.Quest
{
    /// <summary>
    /// Manages the worldspace menu system with mutually exclusive menu visibility.
    /// Only one menu can be open at a time.
    /// Unity UI version of MenuController.cs.
    /// </summary>
    public class QuestMenuController : MonoBehaviour
    {
        public enum MenuType
        {
            None,
            Server,
            Scope
        }

        [Header("UI References")]
        [SerializeField] private Button serverButton;
        [SerializeField] private Button scopeButton;
        [SerializeField] private GameObject serverMenuPanel;
        [SerializeField] private GameObject scopeMenuPanel;

        // Current state
        private MenuType currentMenu = MenuType.None;

        // Button colors
        private readonly Color normalColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        private readonly Color activeColor = new Color(0.3f, 0.4f, 0.6f, 1f);

        // Events
        public event Action<MenuType> OnMenuChanged;

        public MenuType CurrentMenu => currentMenu;

        /// <summary>
        /// Initialize with UI references from QuestWorldspaceUI.
        /// </summary>
        public void Initialize(QuestWorldspaceUI ui)
        {
            serverButton = ui.ServerButton;
            scopeButton = ui.ScopeButton;
            serverMenuPanel = ui.ServerMenuPanel;
            scopeMenuPanel = ui.ScopeMenuPanel;

            Initialize();
        }

        /// <summary>
        /// Initialize with existing serialized references.
        /// </summary>
        public void Initialize()
        {
            BindEvents();
            CloseAllMenus();
            Debug.Log("[QuestMenuController] Initialized");
        }

        private void BindEvents()
        {
            if (serverButton != null)
                serverButton.onClick.AddListener(OnServerButtonClicked);

            if (scopeButton != null)
                scopeButton.onClick.AddListener(OnScopeButtonClicked);
        }

        private void OnDestroy()
        {
            if (serverButton != null)
                serverButton.onClick.RemoveListener(OnServerButtonClicked);

            if (scopeButton != null)
                scopeButton.onClick.RemoveListener(OnScopeButtonClicked);
        }

        private void OnServerButtonClicked()
        {
            Debug.Log("[QuestMenuController] Server button clicked");
            ToggleMenu(MenuType.Server);
        }

        private void OnScopeButtonClicked()
        {
            Debug.Log("[QuestMenuController] Scope button clicked");
            ToggleMenu(MenuType.Scope);
        }

        /// <summary>
        /// Toggle a menu. If it's already open, close it. Otherwise, open it (closing any other open menu).
        /// </summary>
        public void ToggleMenu(MenuType menu)
        {
            if (currentMenu == menu)
            {
                CloseCurrentMenu();
            }
            else
            {
                OpenMenu(menu);
            }
        }

        /// <summary>
        /// Open a specific menu, closing any other open menu first.
        /// </summary>
        public void OpenMenu(MenuType menu)
        {
            Debug.Log($"[QuestMenuController] OpenMenu: {menu}");
            CloseAllMenus();

            currentMenu = menu;

            switch (menu)
            {
                case MenuType.Server:
                    if (serverMenuPanel != null)
                        serverMenuPanel.SetActive(true);
                    SetButtonActive(serverButton, true);
                    break;

                case MenuType.Scope:
                    if (scopeMenuPanel != null)
                        scopeMenuPanel.SetActive(true);
                    SetButtonActive(scopeButton, true);
                    break;
            }

            OnMenuChanged?.Invoke(currentMenu);
        }

        /// <summary>
        /// Close the currently open menu.
        /// </summary>
        public void CloseCurrentMenu()
        {
            CloseAllMenus();
            currentMenu = MenuType.None;
            OnMenuChanged?.Invoke(currentMenu);
        }

        private void CloseAllMenus()
        {
            if (serverMenuPanel != null)
                serverMenuPanel.SetActive(false);

            if (scopeMenuPanel != null)
                scopeMenuPanel.SetActive(false);

            SetButtonActive(serverButton, false);
            SetButtonActive(scopeButton, false);
        }

        private void SetButtonActive(Button button, bool active)
        {
            if (button == null) return;

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = active ? activeColor : normalColor;
            }
        }

        /// <summary>
        /// Check if any menu is currently open.
        /// </summary>
        public bool IsAnyMenuOpen()
        {
            return currentMenu != MenuType.None;
        }
    }
}
