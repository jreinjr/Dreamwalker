using UnityEngine;
using UnityEngine.UIElements;
using System;

namespace Dreamwalker.UI
{
    /// <summary>
    /// Manages the left-anchored menu system with mutually exclusive menu visibility.
    /// Only one menu can be open at a time.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        public enum MenuType
        {
            None,
            Server,
            Scope
        }

        // UI Element references
        private VisualElement root;
        private Button btnServer;
        private Button btnScope;
        private Button btnCameraToggle;
        private VisualElement serverMenu;
        private VisualElement scopeMenu;
        private Button btnServerClose;
        private Button btnScopeClose;

        // Current state
        private MenuType currentMenu = MenuType.None;

        // Events
        public event Action OnCameraToggleRequested;
        public event Action<MenuType> OnMenuChanged;

        public MenuType CurrentMenu => currentMenu;

        private void OnDisable()
        {
            UnbindEvents();
        }

        public void Initialize(VisualElement rootElement)
        {
            root = rootElement;
            BindElements();
        }

        private void BindElements()
        {
            if (root == null)
            {
                Debug.LogError("[MenuController] Root is null, cannot bind elements!");
                return;
            }

            // Top-right buttons
            btnServer = root.Q<Button>("btn-server");
            btnScope = root.Q<Button>("btn-scope");
            btnCameraToggle = root.Q<Button>("btn-camera-toggle");

            Debug.Log($"[MenuController] Buttons - Server: {btnServer != null}, Scope: {btnScope != null}, Camera: {btnCameraToggle != null}");

            // Menu panels
            serverMenu = root.Q<VisualElement>("server-menu");
            scopeMenu = root.Q<VisualElement>("scope-menu");

            Debug.Log($"[MenuController] Menus - Server: {serverMenu != null}, Scope: {scopeMenu != null}");

            // Close buttons
            btnServerClose = root.Q<Button>("btn-server-close");
            btnScopeClose = root.Q<Button>("btn-scope-close");

            BindEvents();

            // Start with all menus closed
            CloseAllMenus();

            Debug.Log("[MenuController] BindElements complete");
        }

        private void BindEvents()
        {
            if (btnServer != null)
                btnServer.clicked += OnServerButtonClicked;

            if (btnScope != null)
                btnScope.clicked += OnScopeButtonClicked;

            if (btnCameraToggle != null)
                btnCameraToggle.clicked += OnCameraToggleClicked;

            if (btnServerClose != null)
                btnServerClose.clicked += CloseCurrentMenu;

            if (btnScopeClose != null)
                btnScopeClose.clicked += CloseCurrentMenu;
        }

        private void UnbindEvents()
        {
            if (btnServer != null)
                btnServer.clicked -= OnServerButtonClicked;

            if (btnScope != null)
                btnScope.clicked -= OnScopeButtonClicked;

            if (btnCameraToggle != null)
                btnCameraToggle.clicked -= OnCameraToggleClicked;

            if (btnServerClose != null)
                btnServerClose.clicked -= CloseCurrentMenu;

            if (btnScopeClose != null)
                btnScopeClose.clicked -= CloseCurrentMenu;
        }

        private void OnServerButtonClicked()
        {
            Debug.Log("[MenuController] Server button clicked!");
            ToggleMenu(MenuType.Server);
        }

        private void OnScopeButtonClicked()
        {
            Debug.Log("[MenuController] Scope button clicked!");
            ToggleMenu(MenuType.Scope);
        }

        private void OnCameraToggleClicked()
        {
            OnCameraToggleRequested?.Invoke();
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
            Debug.Log($"[MenuController] OpenMenu: {menu}");
            CloseAllMenus();

            currentMenu = menu;

            switch (menu)
            {
                case MenuType.Server:
                    if (serverMenu != null)
                    {
                        serverMenu.AddToClassList("menu-open");
                        serverMenu.BringToFront();
                        Debug.Log("[MenuController] Server menu opened");
                    }
                    if (btnServer != null)
                        btnServer.AddToClassList("button-active");
                    break;

                case MenuType.Scope:
                    if (scopeMenu != null)
                    {
                        scopeMenu.AddToClassList("menu-open");
                        scopeMenu.BringToFront();
                        Debug.Log("[MenuController] Scope menu opened");
                    }
                    if (btnScope != null)
                        btnScope.AddToClassList("button-active");
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
            if (serverMenu != null)
            {
                serverMenu.RemoveFromClassList("menu-open");
            }

            if (scopeMenu != null)
            {
                scopeMenu.RemoveFromClassList("menu-open");
            }

            if (btnServer != null)
                btnServer.RemoveFromClassList("button-active");

            if (btnScope != null)
                btnScope.RemoveFromClassList("button-active");
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
