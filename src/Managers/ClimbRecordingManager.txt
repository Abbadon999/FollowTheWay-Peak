using FollowTheWay.Detection;
using FollowTheWay.Models;
using FollowTheWay.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

namespace FollowTheWay.Managers
{
    public class ClimbRecordingManager : IDisposable
    {
        private bool _isRecording = false;
        private bool _isPaused = false;
        private ClimbData _currentClimb;
        private List<ClimbPoint> _recordedPoints;
        private float _lastRecordTime;
        private Vector3 _lastPosition;
        private DateTime _recordingStartTime;
        private RecordingSettings _settings;

        // Game object references (will be populated via reflection/patches)
        private Transform _playerTransform;
        private Rigidbody _playerRigidbody;
        private bool _playerGrounded;

        // Events
        public event Action<ClimbData> OnClimbRecordingStarted;
        public event Action<ClimbData> OnClimbRecordingStopped;
        public event Action<ClimbData> OnClimbRecordingPaused;
        public event Action<ClimbData> OnClimbRecordingResumed;
        public event Action<ClimbPoint> OnPointRecorded;

        // Properties
        public bool IsRecording => _isRecording;
        public bool IsPaused => _isPaused;
        public ClimbData CurrentClimb => _currentClimb;
        public int RecordedPointsCount => _recordedPoints?.Count ?? 0;
        public TimeSpan RecordingDuration => _isRecording ? DateTime.UtcNow - _recordingStartTime : TimeSpan.Zero;

        public ClimbRecordingManager()
        {
            _settings = new RecordingSettings();
            _recordedPoints = new List<ClimbPoint>();

            Plugin.Log.LogInfo("ClimbRecordingManager initialized");
        }

        public void Update()
        {
            if (!_isRecording || _isPaused) return;

            try
            {
                UpdatePlayerReferences();

                if (_playerTransform == null) return;

                var currentTime = Time.time;
                var currentPosition = _playerTransform.position;

                // Check if we should record a new point
                if (ShouldRecordPoint(currentTime, currentPosition))
                {
                    RecordPoint(currentTime, currentPosition);
                }

                // Auto-stop recording if player is idle for too long
                CheckForAutoStop();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in ClimbRecordingManager.Update: {ex.Message}");
            }
        }

        #region Recording Control

        public bool StartRecording(string title = null, string author = null)
        {
            if (_isRecording)
            {
                Plugin.Log.LogWarning("Recording is already in progress");
                return false;
            }

            try
            {
                UpdatePlayerReferences();

                if (_playerTransform == null)
                {
                    Plugin.Log.LogError("Cannot start recording: Player transform not found");
                    return false;
                }

                // Initialize new climb data
                _currentClimb = new ClimbData
                {
                    Id = Guid.NewGuid(),
                    Title = title ?? $"Climb {DateTime.Now:yyyy-MM-dd HH:mm}",
                    Author = author ?? Environment.UserName,
                    PlayerName = author ?? Environment.UserName,
                    ModVersion = Plugin.ModVersion,
                    Map = DetectCurrentMap(),
                    GameVersion = DetectGameVersion(),
                    StartTime = DateTime.UtcNow,
                    RecordingSettings = _settings
                };

                // Reset recording state
                _recordedPoints.Clear();
                _lastRecordTime = Time.time;
                _lastPosition = _playerTransform.position;
                _recordingStartTime = DateTime.UtcNow;
                _isRecording = true;
                _isPaused = false;

                // Record initial point
                RecordPoint(Time.time, _playerTransform.position, isStartPoint: true);

                Plugin.Log.LogInfo($"Started recording climb: {_currentClimb.Title}");
                OnClimbRecordingStarted?.Invoke(_currentClimb);

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to start recording: {ex.Message}");
                return false;
            }
        }

        public ClimbData StopRecording()
        {
            if (!_isRecording)
            {
                Plugin.Log.LogWarning("No recording in progress");
                return null;
            }

            try
            {
                // Record final point
                if (_playerTransform != null)
                {
                    RecordPoint(Time.time, _playerTransform.position, isEndPoint: true);
                }

                // Finalize climb data
                _currentClimb.EndTime = DateTime.UtcNow;
                _currentClimb.Duration = _currentClimb.EndTime - _currentClimb.StartTime;
                _currentClimb.Points = new List<ClimbPoint>(_recordedPoints);
                _currentClimb.PointsCount = _recordedPoints.Count;
                _currentClimb.LengthMeters = _currentClimb.TotalDistance;

                // Calculate additional metadata
                CalculateClimbMetadata(_currentClimb);

                // Validate climb data
                var validationErrors = _currentClimb.GetValidationErrors();
                if (validationErrors.Count > 0)
                {
                    Plugin.Log.LogWarning($"Climb validation warnings: {string.Join(", ", validationErrors)}");
                }

                var completedClimb = _currentClimb;

                // Reset state
                _isRecording = false;
                _isPaused = false;
                _currentClimb = null;

                Plugin.Log.LogInfo($"Stopped recording climb. Points: {completedClimb.Points.Count}, Duration: {completedClimb.Duration:mm\\:ss}");
                OnClimbRecordingStopped?.Invoke(completedClimb);

                return completedClimb;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to stop recording: {ex.Message}");
                _isRecording = false;
                return null;
            }
        }

        public void PauseRecording()
        {
            if (!_isRecording || _isPaused) return;

            _isPaused = true;
            Plugin.Log.LogInfo("Recording paused");
            OnClimbRecordingPaused?.Invoke(_currentClimb);
        }

        public void ResumeRecording()
        {
            if (!_isRecording || !_isPaused) return;

            _isPaused = false;
            _lastRecordTime = Time.time; // Reset timing to avoid gaps
            Plugin.Log.LogInfo("Recording resumed");
            OnClimbRecordingResumed?.Invoke(_currentClimb);
        }

        public void CancelRecording()
        {
            if (!_isRecording) return;

            Plugin.Log.LogInfo("Recording cancelled");
            _isRecording = false;
            _isPaused = false;
            _currentClimb = null;
            _recordedPoints.Clear();
        }

        #endregion

        #region Point Recording

        private bool ShouldRecordPoint(float currentTime, Vector3 currentPosition)
        {
            // Time-based recording
            if (currentTime - _lastRecordTime < _settings.RecordingInterval)
                return false;

            // Distance-based recording
            if (Vector3.Distance(currentPosition, _lastPosition) < _settings.MinDistanceThreshold)
                return false;

            return true;
        }

        private void RecordPoint(float timestamp, Vector3 position, bool isStartPoint = false, bool isEndPoint = false)
        {
            try
            {
                var velocity = _playerRigidbody != null ? _playerRigidbody.velocity : Vector3.zero;
                var isGrounded = _playerGrounded;
                var isFlying = _settings.DetectFlying && DetectFlying(velocity, isGrounded);

                var point = new ClimbPoint(position, timestamp, velocity, isGrounded)
                {
                    IsFlying = isFlying,
                    Metadata = new Dictionary<string, object>()
                };

                // Add metadata
                if (isStartPoint) point.Metadata["isStartPoint"] = true;
                if (isEndPoint) point.Metadata["isEndPoint"] = true;

                // Add altitude
                point.Metadata["altitude"] = position.y;

                // Add speed
                point.Metadata["speed"] = velocity.magnitude;

                _recordedPoints.Add(point);
                _lastRecordTime = timestamp;
                _lastPosition = position;

                OnPointRecorded?.Invoke(point);

                // Log suspicious activity
                if (isFlying)
                {
                    Plugin.FlyDetection?.LogFlyingDetected(position, velocity);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to record point: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private void UpdatePlayerReferences()
        {
            if (_playerTransform != null) return;

            try
            {
                // Try to find player objects (this will need to be adapted based on Peak's actual structure)
                var playerObject = GameObject.FindWithTag("Player");
                if (playerObject == null)
                {
                    playerObject = GameObject.Find("Player");
                }

                if (playerObject != null)
                {
                    _playerTransform = playerObject.transform;
                    _playerRigidbody = playerObject.GetComponent<Rigidbody>();

                    Plugin.Log.LogInfo("Player references updated");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to update player references: {ex.Message}");
            }
        }

        private bool DetectFlying(Vector3 velocity, bool isGrounded)
        {
            if (isGrounded) return false;

            // Simple flying detection - can be enhanced
            var verticalSpeed = Mathf.Abs(velocity.y);
            var horizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

            // Suspicious if moving too fast while not grounded
            return verticalSpeed > 15f || horizontalSpeed > 20f;
        }

        private void CheckForAutoStop()
        {
            if (_recordedPoints.Count < 2) return;

            var lastPoint = _recordedPoints.Last();
            var timeSinceLastPoint = Time.time - lastPoint.Timestamp;

            // Auto-stop if idle for 5 minutes
            if (timeSinceLastPoint > 300f)
            {
                Plugin.Log.LogInfo("Auto-stopping recording due to inactivity");
                StopRecording();
            }
        }

        private string DetectCurrentMap()
        {
            // This would need to be implemented based on Peak's actual scene/map system
            try
            {
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                return activeScene.name ?? "Peak";
            }
            catch
            {
                return "Peak";
            }
        }

        private string DetectGameVersion()
        {
            // This would need to be implemented based on Peak's version system
            return Application.version ?? "1.0";
        }

        private void CalculateClimbMetadata(ClimbData climb)
        {
            if (climb.Points == null || climb.Points.Count < 2) return;

            try
            {
                // Calculate altitudes
                climb.StartAltitude = climb.Points.First().Position.y;
                climb.EndAltitude = climb.Points.Last().Position.y;

                // Detect biome and calculate difficulty
                climb.DetectBiome();
                climb.CalculateAscentLevel();

                Plugin.Log.LogInfo($"Climb metadata calculated - Distance: {climb.TotalDistance:F1}m, " +
                                 $"Elevation: {climb.EndAltitude - climb.StartAltitude:F1}m, " +
                                 $"Difficulty: {climb.Difficulty}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to calculate climb metadata: {ex.Message}");
            }
        }

        #endregion

        #region Settings

        public void UpdateSettings(RecordingSettings newSettings)
        {
            _settings = newSettings ?? new RecordingSettings();
            Plugin.Log.LogInfo("Recording settings updated");
        }

        public RecordingSettings GetSettings()
        {
            return _settings;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isRecording)
            {
                StopRecording();
            }

            _recordedPoints?.Clear();
            Plugin.Log.LogInfo("ClimbRecordingManager disposed");
        }

        #endregion
    }
}