using FollowTheWay.Detection;
using FollowTheWay.Managers;
using FollowTheWay.Models;
using FollowTheWay.Services;
using FollowTheWay.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace FollowTheWay.ModMenu
{
    public class ModMenuManager : IDisposable
    {
        private bool _isMenuVisible = false;
        private bool _isInitialized = false;
        private Rect _menuRect;
        private Vector2 _scrollPosition;
        private int _selectedTab = 0;

        // UI Controllers
        private ModMenuUIController _uiController;
        private TabManager _tabManager;
        private SettingsController _settingsController;

        // Menu state
        private readonly Dictionary<string, bool> _tabStates;
        private readonly Dictionary<string, object> _menuData;

        // Constants
        private const float MENU_WIDTH = 800f;
        private const float MENU_HEIGHT = 600f;
        private const string MENU_TITLE = "FollowTheWay v" + Plugin.ModVersion;

        // Events
        public event Action OnMenuOpened;
        public event Action OnMenuClosed;
        public event Action<int> OnTabChanged;

        // Properties
        public bool IsMenuVisible => _isMenuVisible;
        public bool IsInitialized => _isInitialized;
        public int SelectedTab => _selectedTab;

        public ModMenuManager()
        {
            _tabStates = new Dictionary<string, bool>();
            _menuData = new Dictionary<string, object>();

            InitializeMenu();
            Plugin.Log.LogInfo("ModMenuManager initialized");
        }

        private void InitializeMenu()
        {
            try
            {
                // Calculate menu position (center of screen)
                _menuRect = new Rect(
                    (Screen.width - MENU_WIDTH) / 2f,
                    (Screen.height - MENU_HEIGHT) / 2f,
                    MENU_WIDTH,
                    MENU_HEIGHT
                );

                // Initialize controllers
                _uiController = new ModMenuUIController();
                _tabManager = new TabManager();
                _settingsController = new SettingsController();

                // Setup tabs
                SetupTabs();

                // Load saved settings
                LoadMenuSettings();

                _isInitialized = true;
                Plugin.Log.LogInfo("ModMenu initialized successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to initialize ModMenu: {ex.Message}");
            }
        }

        private void SetupTabs()
        {
            _tabManager.AddTab("Recording", "🎬 Recording", () => DrawRecordingTab());
            _tabManager.AddTab("Climbs", "🏔️ Climbs", () => DrawClimbsTab());
            _tabManager.AddTab("Visualization", "🎨 Visualization", () => DrawVisualizationTab());
            _tabManager.AddTab("CloudSync", "☁️ Cloud Sync", () => DrawCloudSyncTab());
            _tabManager.AddTab("Detection", "🛡️ Detection", () => DrawDetectionTab());
            _tabManager.AddTab("Settings", "⚙️ Settings", () => DrawSettingsTab());
            _tabManager.AddTab("About", "ℹ️ About", () => DrawAboutTab());
        }

        #region Menu Control

        public void ToggleMenu()
        {
            if (_isMenuVisible)
                CloseMenu();
            else
                OpenMenu();
        }

        public void OpenMenu()
        {
            if (!_isInitialized)
            {
                Plugin.Log.LogWarning("Cannot open menu: Not initialized");
                return;
            }

            _isMenuVisible = true;

            // Pause game if needed
            if (Plugin.Settings.PauseGameWhenMenuOpen)
            {
                Time.timeScale = 0f;
            }

            // Show cursor
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            Plugin.Log.LogInfo("ModMenu opened");
            OnMenuOpened?.Invoke();
        }

        public void CloseMenu()
        {
            _isMenuVisible = false;

            // Resume game
            Time.timeScale = 1f;

            // Hide cursor (restore game state)
            if (Plugin.Settings.HideCursorWhenMenuClosed)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }

            // Save settings
            SaveMenuSettings();

            Plugin.Log.LogInfo("ModMenu closed");
            OnMenuClosed?.Invoke();
        }

        #endregion

        #region GUI Drawing

        public void OnGUI()
        {
            if (!_isMenuVisible || !_isInitialized) return;

            try
            {
                // Apply UI skin/style
                _uiController.ApplyMenuStyle();

                // Draw main menu window
                _menuRect = GUI.Window(12345, _menuRect, DrawMenuWindow, MENU_TITLE);

                // Clamp window to screen
                _menuRect.x = Mathf.Clamp(_menuRect.x, 0, Screen.width - _menuRect.width);
                _menuRect.y = Mathf.Clamp(_menuRect.y, 0, Screen.height - _menuRect.height);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error drawing ModMenu GUI: {ex.Message}");
            }
        }

        private void DrawMenuWindow(int windowId)
        {
            GUILayout.BeginVertical();

            // Header
            DrawMenuHeader();

            // Tab buttons
            DrawTabButtons();

            // Content area with scroll
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

            // Draw selected tab content
            _tabManager.DrawSelectedTab(_selectedTab);

            GUILayout.EndScrollView();

            // Footer
            DrawMenuFooter();

            GUILayout.EndVertical();

            // Make window draggable
            GUI.DragWindow();
        }

        private void DrawMenuHeader()
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label($"FollowTheWay Peak Mod v{Plugin.ModVersion}", _uiController.HeaderStyle);

            GUILayout.FlexibleSpace();

            // Close button
            if (GUILayout.Button("✕", _uiController.CloseButtonStyle, GUILayout.Width(30), GUILayout.Height(25)))
            {
                CloseMenu();
            }

            GUILayout.EndHorizontal();

            // Status line
            DrawStatusLine();
        }

        private void DrawStatusLine()
        {
            GUILayout.BeginHorizontal();

            // Recording status
            var recordingStatus = Plugin.ClimbRecording?.IsRecording == true ? "🔴 Recording" : "⚫ Not Recording";
            GUILayout.Label($"Status: {recordingStatus}", _uiController.StatusStyle);

            GUILayout.FlexibleSpace();

            // Connection status
            var connectionStatus = Plugin.VPSApi?.IsConnected == true ? "🟢 Connected" : "🔴 Disconnected";
            GUILayout.Label($"Server: {connectionStatus}", _uiController.StatusStyle);

            GUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        private void DrawTabButtons()
        {
            GUILayout.BeginHorizontal();

            var tabs = _tabManager.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                var isSelected = i == _selectedTab;
                var style = isSelected ? _uiController.SelectedTabStyle : _uiController.TabStyle;

                if (GUILayout.Button(tab.DisplayName, style))
                {
                    SelectTab(i);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        private void DrawMenuFooter()
        {
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();

            // Quick actions
            if (Plugin.ClimbRecording?.IsRecording == true)
            {
                if (GUILayout.Button("⏹️ Stop Recording", _uiController.ActionButtonStyle))
                {
                    var climb = Plugin.ClimbRecording.StopRecording();
                    if (climb != null)
                    {
                        Plugin.Log.LogInfo($"Recording stopped: {climb.Title}");
                    }
                }

                if (Plugin.ClimbRecording.IsPaused)
                {
                    if (GUILayout.Button("▶️ Resume", _uiController.ActionButtonStyle))
                    {
                        Plugin.ClimbRecording.ResumeRecording();
                    }
                }
                else
                {
                    if (GUILayout.Button("⏸️ Pause", _uiController.ActionButtonStyle))
                    {
                        Plugin.ClimbRecording.PauseRecording();
                    }
                }
            }
            else
            {
                if (GUILayout.Button("🎬 Start Recording", _uiController.ActionButtonStyle))
                {
                    Plugin.ClimbRecording?.StartRecording();
                }
            }

            GUILayout.FlexibleSpace();

            // Version info
            GUILayout.Label($"API: {Plugin.Settings.ServerUrl}", _uiController.FooterStyle);

            GUILayout.EndHorizontal();
        }

        #endregion

        #region Tab Content

        private void DrawRecordingTab()
        {
            GUILayout.Label("🎬 Recording Controls", _uiController.SectionHeaderStyle);

            var recording = Plugin.ClimbRecording;
            if (recording == null)
            {
                GUILayout.Label("Recording manager not available", _uiController.ErrorStyle);
                return;
            }

            // Recording status
            GUILayout.BeginVertical("box");
            GUILayout.Label("Current Status:", _uiController.LabelStyle);

            if (recording.IsRecording)
            {
                GUILayout.Label($"🔴 Recording: {recording.CurrentClimb?.Title ?? "Unnamed"}", _uiController.SuccessStyle);
                GUILayout.Label($"Points recorded: {recording.RecordedPointsCount}", _uiController.InfoStyle);
                GUILayout.Label($"Duration: {recording.RecordingDuration:mm\\:ss}", _uiController.InfoStyle);

                if (recording.IsPaused)
                {
                    GUILayout.Label("⏸️ PAUSED", _uiController.WarningStyle);
                }
            }
            else
            {
                GUILayout.Label("⚫ Not recording", _uiController.InfoStyle);
            }
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Recording controls
            GUILayout.Label("Controls:", _uiController.SectionHeaderStyle);

            GUILayout.BeginHorizontal();

            if (!recording.IsRecording)
            {
                if (GUILayout.Button("🎬 Start Recording", _uiController.ButtonStyle))
                {
                    var title = $"Climb {DateTime.Now:HH:mm}";
                    recording.StartRecording(title, Environment.UserName);
                }
            }
            else
            {
                if (GUILayout.Button("⏹️ Stop Recording", _uiController.ButtonStyle))
                {
                    var climb = recording.StopRecording();
                    if (climb != null)
                    {
                        // Auto-upload if enabled
                        if (Plugin.Settings.AutoUploadClimbs)
                        {
                            Plugin.ClimbUpload?.UploadClimbAsync(climb);
                        }
                    }
                }

                if (recording.IsPaused)
                {
                    if (GUILayout.Button("▶️ Resume", _uiController.ButtonStyle))
                    {
                        recording.ResumeRecording();
                    }
                }
                else
                {
                    if (GUILayout.Button("⏸️ Pause", _uiController.ButtonStyle))
                    {
                        recording.PauseRecording();
                    }
                }

                if (GUILayout.Button("❌ Cancel", _uiController.DangerButtonStyle))
                {
                    recording.CancelRecording();
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Recording settings
            DrawRecordingSettings();
        }

        private void DrawRecordingSettings()
        {
            GUILayout.Label("Recording Settings:", _uiController.SectionHeaderStyle);

            GUILayout.BeginVertical("box");

            var settings = Plugin.ClimbRecording?.GetSettings();
            if (settings != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Recording Interval:", GUILayout.Width(150));
                var intervalStr = GUILayout.TextField(settings.RecordingInterval.ToString("F2"), GUILayout.Width(80));
                if (float.TryParse(intervalStr, out float interval))
                {
                    settings.RecordingInterval = Mathf.Clamp(interval, 0.01f, 1f);
                }
                GUILayout.Label("seconds", GUILayout.Width(60));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Min Distance:", GUILayout.Width(150));
                var distanceStr = GUILayout.TextField(settings.MinDistanceThreshold.ToString("F2"), GUILayout.Width(80));
                if (float.TryParse(distanceStr, out float distance))
                {
                    settings.MinDistanceThreshold = Mathf.Clamp(distance, 0.01f, 5f);
                }
                GUILayout.Label("meters", GUILayout.Width(60));
                GUILayout.EndHorizontal();

                settings.DetectFlying = GUILayout.Toggle(settings.DetectFlying, "Detect Flying/Cheating");
                settings.AutoUpload = GUILayout.Toggle(settings.AutoUpload, "Auto-upload completed climbs");

                if (GUILayout.Button("Apply Settings", _uiController.ButtonStyle))
                {
                    Plugin.ClimbRecording?.UpdateSettings(settings);
                    Plugin.Log.LogInfo("Recording settings updated");
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawClimbsTab()
        {
            GUILayout.Label("🏔️ Climb Management", _uiController.SectionHeaderStyle);

            // This would be implemented by ClimbsTabController
            // For now, show basic info
            GUILayout.Label("Local climbs, server browser, and climb management will be here.", _uiController.InfoStyle);

            if (GUILayout.Button("Refresh Climbs", _uiController.ButtonStyle))
            {
                Plugin.ClimbDownload?.RefreshClimbsAsync();
            }
        }

        private void DrawVisualizationTab()
        {
            GUILayout.Label("🎨 Visualization Settings", _uiController.SectionHeaderStyle);

            var viz = Plugin.ClimbVisualization;
            if (viz == null)
            {
                GUILayout.Label("Visualization manager not available", _uiController.ErrorStyle);
                return;
            }

            // Visualization status
            GUILayout.BeginVertical("box");
            if (viz.IsVisualizationActive)
            {
                GUILayout.Label($"🎨 Visualizing: {viz.CurrentClimb?.Title ?? "Unknown"}", _uiController.SuccessStyle);

                if (viz.IsAnimating)
                {
                    GUILayout.Label($"▶️ Animation Progress: {viz.AnimationProgress:P0}", _uiController.InfoStyle);
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("⏹️ Stop Visualization", _uiController.ButtonStyle))
                {
                    viz.StopVisualization();
                }

                if (viz.IsAnimating)
                {
                    if (GUILayout.Button("⏸️ Pause Animation", _uiController.ButtonStyle))
                    {
                        viz.PauseAnimation();
                    }
                }
                else
                {
                    if (GUILayout.Button("▶️ Start Animation", _uiController.ButtonStyle))
                    {
                        viz.StartAnimation();
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("⚫ No active visualization", _uiController.InfoStyle);
            }
            GUILayout.EndVertical();

            // Visualization settings would go here
            DrawVisualizationSettings();
        }

        private void DrawVisualizationSettings()
        {
            GUILayout.Label("Visualization Settings:", _uiController.SectionHeaderStyle);
            GUILayout.Label("Path colors, markers, effects settings will be here.", _uiController.InfoStyle);
        }

        private void DrawCloudSyncTab()
        {
            GUILayout.Label("☁️ Cloud Synchronization", _uiController.SectionHeaderStyle);

            // Server connection status
            GUILayout.BeginVertical("box");
            GUILayout.Label("Server Connection:", _uiController.LabelStyle);

            var api = Plugin.VPSApi;
            if (api?.IsConnected == true)
            {
                GUILayout.Label("🟢 Connected to followtheway.ru", _uiController.SuccessStyle);
            }
            else
            {
                GUILayout.Label("🔴 Disconnected", _uiController.ErrorStyle);

                if (GUILayout.Button("🔄 Reconnect", _uiController.ButtonStyle))
                {
                    // Attempt reconnection
                    Plugin.VPSApi?.TestConnectionAsync();
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Upload/Download controls
            GUILayout.Label("Sync Controls:", _uiController.SectionHeaderStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("⬆️ Upload Local Climbs", _uiController.ButtonStyle))
            {
                // Upload all local climbs
                Plugin.Log.LogInfo("Starting bulk upload...");
            }

            if (GUILayout.Button("⬇️ Download Server Climbs", _uiController.ButtonStyle))
            {
                Plugin.ClimbDownload?.RefreshClimbsAsync();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawDetectionTab()
        {
            GUILayout.Label("🛡️ Cheat Detection", _uiController.SectionHeaderStyle);

            var detection = Plugin.FlyDetection;
            if (detection == null)
            {
                GUILayout.Label("Detection system not available", _uiController.ErrorStyle);
                return;
            }

            // Detection status
            GUILayout.BeginVertical("box");
            GUILayout.Label($"Status: {(detection.IsMonitoring ? "🟢 Active" : "🔴 Inactive")}",
                          detection.IsMonitoring ? _uiController.SuccessStyle : _uiController.ErrorStyle);
            GUILayout.Label($"Events detected: {detection.DetectionEventCount}", _uiController.InfoStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Detection controls
            GUILayout.BeginHorizontal();
            if (detection.IsMonitoring)
            {
                if (GUILayout.Button("⏹️ Stop Detection", _uiController.ButtonStyle))
                {
                    detection.StopMonitoring();
                }
            }
            else
            {
                if (GUILayout.Button("▶️ Start Detection", _uiController.ButtonStyle))
                {
                    detection.StartMonitoring();
                }
            }

            if (GUILayout.Button("🗑️ Clear Events", _uiController.ButtonStyle))
            {
                detection.ClearEvents();
            }
            GUILayout.EndHorizontal();

            // Recent events
            var recentEvents = detection.GetRecentEvents(10);
            if (recentEvents.Count > 0)
            {
                GUILayout.Label("Recent Detection Events:", _uiController.SectionHeaderStyle);
                GUILayout.BeginVertical("box");

                foreach (var evt in recentEvents.Take(5))
                {
                    var color = evt.SeverityLevel >= FlyingSeverity.High ? _uiController.ErrorStyle : _uiController.WarningStyle;
                    GUILayout.Label($"{evt.Timestamp:HH:mm:ss} - {evt.SeverityLevel} - Speed: {evt.Velocity.magnitude:F1} m/s", color);
                }

                GUILayout.EndVertical();
            }
        }

        private void DrawSettingsTab()
        {
            GUILayout.Label("⚙️ Mod Settings", _uiController.SectionHeaderStyle);

            _settingsController.DrawSettings();
        }

        private void DrawAboutTab()
        {
            GUILayout.Label("ℹ️ About FollowTheWay", _uiController.SectionHeaderStyle);

            GUILayout.BeginVertical("box");
            GUILayout.Label($"FollowTheWay Peak Mod v{Plugin.ModVersion}", _uiController.HeaderStyle);
            GUILayout.Label("Author: ABBADON", _uiController.InfoStyle);
            GUILayout.Label("Server: followtheway.ru", _uiController.InfoStyle);
            GUILayout.Space(10);
            GUILayout.Label("A climbing route recording and sharing mod for Peak.", _uiController.InfoStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            if (GUILayout.Button("🌐 Visit Website", _uiController.ButtonStyle))
            {
                Application.OpenURL("https://followtheway.ru");
            }
        }

        #endregion

        #region Tab Management

        private void SelectTab(int tabIndex)
        {
            if (tabIndex >= 0 && tabIndex < _tabManager.GetTabs().Count)
            {
                _selectedTab = tabIndex;
                OnTabChanged?.Invoke(tabIndex);
            }
        }

        #endregion

        #region Settings

        private void LoadMenuSettings()
        {
            try
            {
                // Load menu position and state from PlayerPrefs or config file
                if (PlayerPrefs.HasKey("FollowTheWay_MenuX"))
                {
                    _menuRect.x = PlayerPrefs.GetFloat("FollowTheWay_MenuX");
                    _menuRect.y = PlayerPrefs.GetFloat("FollowTheWay_MenuY");
                }

                _selectedTab = PlayerPrefs.GetInt("FollowTheWay_SelectedTab", 0);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to load menu settings: {ex.Message}");
            }
        }

        private void SaveMenuSettings()
        {
            try
            {
                PlayerPrefs.SetFloat("FollowTheWay_MenuX", _menuRect.x);
                PlayerPrefs.SetFloat("FollowTheWay_MenuY", _menuRect.y);
                PlayerPrefs.SetInt("FollowTheWay_SelectedTab", _selectedTab);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to save menu settings: {ex.Message}");
            }
        }

        #endregion

        #region Input Handling

        public void HandleInput()
        {
            try
            {
                // Toggle menu with F1 key (configurable)
                if (Input.GetKeyDown(Plugin.Settings.MenuToggleKey))
                {
                    ToggleMenu();
                }

                // Close menu with Escape
                if (_isMenuVisible && Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseMenu();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error handling input: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isMenuVisible)
            {
                CloseMenu();
            }

            _uiController?.Dispose();
            _tabManager?.Dispose();
            _settingsController?.Dispose();

            Plugin.Log.LogInfo("ModMenuManager disposed");
        }

        #endregion
    }
}