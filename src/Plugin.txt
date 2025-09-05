using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using FollowTheWay.Services;
using FollowTheWay.Managers;
using FollowTheWay.ModMenu;
using FollowTheWay.Detection;
using FollowTheWay.Utils;

namespace FollowTheWay
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Peak.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        // Core Services
        public static VPSApiService ApiService { get; private set; }
        public static ClimbDataService DataService { get; private set; }
        public static ClimbUploadService UploadService { get; private set; }
        public static ClimbDownloadService DownloadService { get; private set; }
        public static ServerConfigService ConfigService { get; private set; }
        public static AssetBundleService AssetService { get; private set; }

        // Managers
        public static ClimbRecordingManager RecordingManager { get; private set; }
        public static ClimbVisualizationManager VisualizationManager { get; private set; }
        public static ModMenuManager MenuManager { get; private set; }

        // Detection System
        public static FlyDetectionLogger FlyDetection { get; private set; }

        // Configuration
        public static bool IsInitialized { get; private set; }
        public static string ModVersion => PluginInfo.PLUGIN_VERSION;
        public static string ServerUrl => "https://followtheway.ru";

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Log.LogInfo($"FollowTheWay v{PluginInfo.PLUGIN_VERSION} - Starting initialization...");

            try
            {
                InitializeServices();
                InitializeManagers();
                InitializeDetection();
                ApplyPatches();

                IsInitialized = true;
                Log.LogInfo("FollowTheWay - Initialization completed successfully!");
            }
            catch (System.Exception ex)
            {
                Log.LogError($"FollowTheWay - Initialization failed: {ex.Message}");
                Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private void InitializeServices()
        {
            Log.LogInfo("Initializing core services...");

            // Initialize API service with followtheway.ru configuration
            ApiService = new VPSApiService();
            ConfigService = new ServerConfigService();
            DataService = new ClimbDataService();
            UploadService = new ClimbUploadService();
            DownloadService = new ClimbDownloadService();
            AssetService = new AssetBundleService();

            Log.LogInfo("Core services initialized");
        }

        private void InitializeManagers()
        {
            Log.LogInfo("Initializing managers...");

            RecordingManager = new ClimbRecordingManager();
            VisualizationManager = new ClimbVisualizationManager();
            MenuManager = new ModMenuManager();

            Log.LogInfo("Managers initialized");
        }

        private void InitializeDetection()
        {
            Log.LogInfo("Initializing detection systems...");

            FlyDetection = new FlyDetectionLogger();

            Log.LogInfo("Detection systems initialized");
        }

        private void ApplyPatches()
        {
            Log.LogInfo("Applying Harmony patches...");

            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            _harmony.PatchAll();

            Log.LogInfo("Harmony patches applied");
        }

        private void OnDestroy()
        {
            Log.LogInfo("FollowTheWay - Shutting down...");

            try
            {
                _harmony?.UnpatchSelf();

                // Cleanup services
                ApiService?.Dispose();
                RecordingManager?.Dispose();
                VisualizationManager?.Dispose();
                MenuManager?.Dispose();
                FlyDetection?.Dispose();

                Log.LogInfo("FollowTheWay - Shutdown completed");
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Error during shutdown: {ex.Message}");
            }
        }

        private void Update()
        {
            try
            {
                // Update managers
                RecordingManager?.Update();
                VisualizationManager?.Update();
                MenuManager?.Update();
                FlyDetection?.Update();
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Error in Update: {ex.Message}");
            }
        }

        // Public API for other mods
        public static bool IsClimbRecording()
        {
            return RecordingManager?.IsRecording ?? false;
        }

        public static void StartClimbRecording()
        {
            RecordingManager?.StartRecording();
        }

        public static void StopClimbRecording()
        {
            RecordingManager?.StopRecording();
        }

        public static void ShowModMenu()
        {
            MenuManager?.ShowMenu();
        }

        public static void HideModMenu()
        {
            MenuManager?.HideMenu();
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "ABBADON.FollowTheWay.Peak";
        public const string PLUGIN_NAME = "FollowTheWay";
        public const string PLUGIN_VERSION = "0.0.1";
    }
}