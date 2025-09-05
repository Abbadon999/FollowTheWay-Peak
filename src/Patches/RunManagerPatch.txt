using FollowTheWay.Utils;
using HarmonyLib;
using System;
using System.Numerics;
using UnityEngine;

namespace FollowTheWay.Patches
{
    /// <summary>
    /// Harmony patches for Peak's run/game management system
    /// Integrates FollowTheWay recording with game events
    /// </summary>
    [HarmonyPatch]
    public static class RunManagerPatch
    {
        private static bool _wasRecordingBeforeRun = false;
        private static string _currentRunId = null;

        #region Run Start Patches

        /// <summary>
        /// Patch for when a new run/climb starts
        /// This will need to be adapted based on Peak's actual method names
        /// </summary>
        [HarmonyPatch(typeof(RunManager), "StartRun")] // Replace with actual class/method
        [HarmonyPrefix]
        public static void StartRun_Prefix()
        {
            try
            {
                Plugin.Log.LogInfo("Run started - checking recording state");

                // Generate unique run ID
                _currentRunId = Guid.NewGuid().ToString("N")[..8];

                // Check if we should auto-start recording
                if (Plugin.Settings.AutoStartRecording && Plugin.ClimbRecording != null)
                {
                    if (!Plugin.ClimbRecording.IsRecording)
                    {
                        var runTitle = $"Peak Run {DateTime.Now:HH:mm}";
                        var success = Plugin.ClimbRecording.StartRecording(runTitle, Environment.UserName);

                        if (success)
                        {
                            Plugin.Log.LogInfo($"Auto-started recording for run: {runTitle}");
                            _wasRecordingBeforeRun = false; // We started it
                        }
                    }
                    else
                    {
                        Plugin.Log.LogInfo("Recording already in progress");
                        _wasRecordingBeforeRun = true; // It was already running
                    }
                }

                // Start fly detection if enabled
                if (Plugin.Settings.EnableFlyDetection && Plugin.FlyDetection != null)
                {
                    Plugin.FlyDetection.StartMonitoring();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in StartRun_Prefix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(RunManager), "StartRun")]
        [HarmonyPostfix]
        public static void StartRun_Postfix()
        {
            try
            {
                Plugin.Log.LogInfo("Run start completed");

                // Additional setup after run starts
                if (Plugin.ClimbRecording?.IsRecording == true)
                {
                    // Add run metadata to current recording
                    var currentClimb = Plugin.ClimbRecording.CurrentClimb;
                    if (currentClimb?.Metadata == null)
                    {
                        currentClimb.Metadata = new System.Collections.Generic.Dictionary<string, object>();
                    }

                    currentClimb.Metadata["runId"] = _currentRunId;
                    currentClimb.Metadata["autoStarted"] = !_wasRecordingBeforeRun;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in StartRun_Postfix: {ex.Message}");
            }
        }

        #endregion

        #region Run End Patches

        /// <summary>
        /// Patch for when a run ends (completion or failure)
        /// </summary>
        [HarmonyPatch(typeof(RunManager), "EndRun")] // Replace with actual method
        [HarmonyPrefix]
        public static void EndRun_Prefix(bool success) // Adjust parameters based on actual method
        {
            try
            {
                Plugin.Log.LogInfo($"Run ending - Success: {success}");

                // Handle recording based on run outcome
                if (Plugin.ClimbRecording?.IsRecording == true)
                {
                    var currentClimb = Plugin.ClimbRecording.CurrentClimb;

                    // Add completion metadata
                    if (currentClimb?.Metadata != null)
                    {
                        currentClimb.Metadata["runCompleted"] = success;
                        currentClimb.Metadata["runEndTime"] = DateTime.UtcNow;
                        currentClimb.Metadata["runId"] = _currentRunId;
                    }

                    // Auto-stop recording if we auto-started it
                    if (Plugin.Settings.AutoStopRecording && !_wasRecordingBeforeRun)
                    {
                        var completedClimb = Plugin.ClimbRecording.StopRecording();

                        if (completedClimb != null)
                        {
                            Plugin.Log.LogInfo($"Auto-stopped recording: {completedClimb.Title}");

                            // Auto-upload if successful run and setting enabled
                            if (success && Plugin.Settings.AutoUploadClimbs)
                            {
                                Plugin.ClimbUpload?.UploadClimbAsync(completedClimb);
                            }
                        }
                    }
                }

                // Stop fly detection
                if (Plugin.FlyDetection?.IsMonitoring == true)
                {
                    Plugin.FlyDetection.StopMonitoring();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in EndRun_Prefix: {ex.Message}");
            }
        }

        #endregion

        #region Checkpoint Patches

        /// <summary>
        /// Patch for checkpoint/waypoint events
        /// </summary>
        [HarmonyPatch(typeof(CheckpointManager), "ReachCheckpoint")] // Replace with actual class/method
        [HarmonyPostfix]
        public static void ReachCheckpoint_Postfix(int checkpointId) // Adjust parameters
        {
            try
            {
                Plugin.Log.LogInfo($"Checkpoint reached: {checkpointId}");

                // Add checkpoint marker to recording
                if (Plugin.ClimbRecording?.IsRecording == true)
                {
                    var currentClimb = Plugin.ClimbRecording.CurrentClimb;
                    if (currentClimb?.Metadata != null)
                    {
                        var checkpointKey = $"checkpoint_{checkpointId}";
                        currentClimb.Metadata[checkpointKey] = DateTime.UtcNow;
                    }

                    // Log checkpoint in current point if available
                    var recordedPoints = Plugin.ClimbRecording.RecordedPointsCount;
                    if (recordedPoints > 0)
                    {
                        // The next recorded point will include this checkpoint info
                        // This would need access to the recording manager's internal state
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in ReachCheckpoint_Postfix: {ex.Message}");
            }
        }

        #endregion

        #region Player State Patches

        /// <summary>
        /// Patch for player respawn/reset events
        /// </summary>
        [HarmonyPatch(typeof(PlayerController), "Respawn")] // Replace with actual class/method
        [HarmonyPrefix]
        public static void Respawn_Prefix()
        {
            try
            {
                Plugin.Log.LogInfo("Player respawning");

                // Handle recording during respawn
                if (Plugin.ClimbRecording?.IsRecording == true)
                {
                    if (Plugin.Settings.PauseRecordingOnRespawn)
                    {
                        Plugin.ClimbRecording.PauseRecording();
                        Plugin.Log.LogInfo("Recording paused due to respawn");
                    }
                    else
                    {
                        // Add respawn marker
                        var currentClimb = Plugin.ClimbRecording.CurrentClimb;
                        if (currentClimb?.Metadata != null)
                        {
                            var respawnKey = $"respawn_{DateTime.UtcNow:HHmmss}";
                            currentClimb.Metadata[respawnKey] = DateTime.UtcNow;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in Respawn_Prefix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(PlayerController), "Respawn")]
        [HarmonyPostfix]
        public static void Respawn_Postfix()
        {
            try
            {
                // Resume recording after respawn if it was paused
                if (Plugin.ClimbRecording?.IsPaused == true && Plugin.Settings.PauseRecordingOnRespawn)
                {
                    Plugin.ClimbRecording.ResumeRecording();
                    Plugin.Log.LogInfo("Recording resumed after respawn");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in Respawn_Postfix: {ex.Message}");
            }
        }

        #endregion

        #region Scene/Level Patches

        /// <summary>
        /// Patch for level/scene changes
        /// </summary>
        [HarmonyPatch(typeof(SceneManager), "LoadScene")] // This might be Unity's SceneManager
        [HarmonyPrefix]
        public static void LoadScene_Prefix(string sceneName)
        {
            try
            {
                Plugin.Log.LogInfo($"Scene changing to: {sceneName}");

                // Handle recording during scene changes
                if (Plugin.ClimbRecording?.IsRecording == true)
                {
                    if (Plugin.Settings.StopRecordingOnSceneChange)
                    {
                        var completedClimb = Plugin.ClimbRecording.StopRecording();
                        if (completedClimb != null)
                        {
                            Plugin.Log.LogInfo($"Recording stopped due to scene change: {completedClimb.Title}");

                            // Auto-upload if enabled
                            if (Plugin.Settings.AutoUploadClimbs)
                            {
                                Plugin.ClimbUpload?.UploadClimbAsync(completedClimb);
                            }
                        }
                    }
                }

                // Stop visualization
                if (Plugin.ClimbVisualization?.IsVisualizationActive == true)
                {
                    Plugin.ClimbVisualization.StopVisualization();
                }

                // Stop fly detection
                if (Plugin.FlyDetection?.IsMonitoring == true)
                {
                    Plugin.FlyDetection.StopMonitoring();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in LoadScene_Prefix: {ex.Message}");
            }
        }

        #endregion

        #region Game State Patches

        /// <summary>
        /// Patch for pause/unpause events
        /// </summary>
        [HarmonyPatch(typeof(GameManager), "PauseGame")] // Replace with actual class/method
        [HarmonyPostfix]
        public static void PauseGame_Postfix(bool isPaused)
        {
            try
            {
                Plugin.Log.LogInfo($"Game pause state changed: {isPaused}");

                // Handle recording during game pause
                if (Plugin.ClimbRecording?.IsRecording == true)
                {
                    if (isPaused && Plugin.Settings.PauseRecordingWithGame)
                    {
                        Plugin.ClimbRecording.PauseRecording();
                    }
                    else if (!isPaused && Plugin.ClimbRecording.IsPaused)
                    {
                        Plugin.ClimbRecording.ResumeRecording();
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in PauseGame_Postfix: {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get current player position safely
        /// </summary>
        private static Vector3? GetPlayerPosition()
        {
            try
            {
                var playerObject = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
                return playerObject?.transform.position;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if player is in a valid state for recording
        /// </summary>
        private static bool IsPlayerInValidState()
        {
            try
            {
                var playerPosition = GetPlayerPosition();
                if (!playerPosition.HasValue) return false;

                // Add additional checks based on Peak's player state system
                // For example: not in menu, not dead, not in cutscene, etc.

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reset patch state (called when mod is disabled/reloaded)
        /// </summary>
        public static void ResetState()
        {
            _wasRecordingBeforeRun = false;
            _currentRunId = null;
            Plugin.Log.LogInfo("RunManagerPatch state reset");
        }

        #endregion
    }

    #region Settings Extension

    /// <summary>
    /// Settings related to run management patches
    /// These would be part of the main settings class
    /// </summary>
    public static class RunManagerPatchSettings
    {
        public static bool AutoStartRecording => Plugin.Settings?.AutoStartRecording ?? true;
        public static bool AutoStopRecording => Plugin.Settings?.AutoStopRecording ?? true;
        public static bool AutoUploadClimbs => Plugin.Settings?.AutoUploadClimbs ?? false;
        public static bool EnableFlyDetection => Plugin.Settings?.EnableFlyDetection ?? true;
        public static bool PauseRecordingOnRespawn => Plugin.Settings?.PauseRecordingOnRespawn ?? false;
        public static bool StopRecordingOnSceneChange => Plugin.Settings?.StopRecordingOnSceneChange ?? true;
        public static bool PauseRecordingWithGame => Plugin.Settings?.PauseRecordingWithGame ?? true;
    }

    #endregion
}