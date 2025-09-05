using FollowTheWay.Models;
using FollowTheWay.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;

namespace FollowTheWay.Detection
{
    public class FlyDetectionLogger : IDisposable
    {
        private readonly List<FlyDetectionEvent> _detectionEvents;
        private readonly FlyDetectionConfig _config;
        private Vector3 _lastPlayerPosition;
        private float _lastCheckTime;
        private bool _isMonitoring;

        // Tracking variables
        private float _consecutiveFlyTime;
        private int _flyEventCount;
        private DateTime _sessionStartTime;

        // Events
        public event Action<FlyDetectionEvent> OnFlyingDetected;
        public event Action<FlyDetectionSummary> OnSuspiciousActivity;

        // Properties
        public bool IsMonitoring => _isMonitoring;
        public int DetectionEventCount => _detectionEvents.Count;
        public FlyDetectionSummary SessionSummary => GenerateSessionSummary();

        public FlyDetectionLogger()
        {
            _detectionEvents = new List<FlyDetectionEvent>();
            _config = new FlyDetectionConfig();
            _sessionStartTime = DateTime.UtcNow;

            Plugin.Log.LogInfo("FlyDetectionLogger initialized");
        }

        public void Update()
        {
            if (!_isMonitoring) return;

            try
            {
                var currentTime = Time.time;
                if (currentTime - _lastCheckTime < _config.CheckInterval)
                    return;

                CheckForFlying();
                _lastCheckTime = currentTime;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in FlyDetectionLogger.Update: {ex.Message}");
            }
        }

        #region Monitoring Control

        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            _sessionStartTime = DateTime.UtcNow;
            _flyEventCount = 0;
            _consecutiveFlyTime = 0f;

            Plugin.Log.LogInfo("Fly detection monitoring started");
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _isMonitoring = false;

            // Generate session summary
            var summary = GenerateSessionSummary();
            if (summary.IsSuspicious)
            {
                OnSuspiciousActivity?.Invoke(summary);
            }

            Plugin.Log.LogInfo($"Fly detection monitoring stopped. Events detected: {_flyEventCount}");
        }

        #endregion

        #region Detection Logic

        private void CheckForFlying()
        {
            var playerTransform = GetPlayerTransform();
            if (playerTransform == null) return;

            var currentPosition = playerTransform.position;
            var currentTime = Time.time;

            if (_lastPlayerPosition != Vector3.zero)
            {
                var deltaTime = currentTime - _lastCheckTime;
                var movement = currentPosition - _lastPlayerPosition;
                var velocity = movement / deltaTime;

                var flyingResult = AnalyzeMovement(currentPosition, velocity, deltaTime);

                if (flyingResult.IsFlying)
                {
                    LogFlyingEvent(flyingResult);
                }
            }

            _lastPlayerPosition = currentPosition;
        }

        private FlyingAnalysisResult AnalyzeMovement(Vector3 position, Vector3 velocity, float deltaTime)
        {
            var result = new FlyingAnalysisResult
            {
                Position = position,
                Velocity = velocity,
                Timestamp = Time.time,
                DeltaTime = deltaTime
            };

            // Check various flying indicators
            result.VerticalSpeedViolation = CheckVerticalSpeed(velocity);
            result.HorizontalSpeedViolation = CheckHorizontalSpeed(velocity);
            result.AltitudeViolation = CheckAltitude(position);
            result.AccelerationViolation = CheckAcceleration(velocity);
            result.GroundDistanceViolation = CheckGroundDistance(position);

            // Determine if this constitutes flying
            result.IsFlying = DetermineFlyingStatus(result);
            result.ConfidenceLevel = CalculateConfidence(result);
            result.SeverityLevel = CalculateSeverity(result);

            return result;
        }

        private bool CheckVerticalSpeed(Vector3 velocity)
        {
            var verticalSpeed = Mathf.Abs(velocity.y);
            return verticalSpeed > _config.MaxVerticalSpeed;
        }

        private bool CheckHorizontalSpeed(Vector3 velocity)
        {
            var horizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
            return horizontalSpeed > _config.MaxHorizontalSpeed;
        }

        private bool CheckAltitude(Vector3 position)
        {
            // Check if player is at an impossible altitude
            return position.y > _config.MaxReasonableAltitude;
        }

        private bool CheckAcceleration(Vector3 currentVelocity)
        {
            // This would require tracking previous velocity
            // For now, just check for instant high speeds
            return currentVelocity.magnitude > _config.MaxInstantSpeed;
        }

        private bool CheckGroundDistance(Vector3 position)
        {
            // Raycast down to check distance to ground
            try
            {
                if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, _config.MaxGroundCheckDistance))
                {
                    return hit.distance > _config.MaxGroundDistance;
                }
                else
                {
                    // No ground found within reasonable distance
                    return true;
                }
            }
            catch
            {
                return false; // Assume grounded if raycast fails
            }
        }

        private bool DetermineFlyingStatus(FlyingAnalysisResult result)
        {
            var violationCount = 0;

            if (result.VerticalSpeedViolation) violationCount++;
            if (result.HorizontalSpeedViolation) violationCount++;
            if (result.AltitudeViolation) violationCount++;
            if (result.AccelerationViolation) violationCount++;
            if (result.GroundDistanceViolation) violationCount++;

            // Require multiple violations for higher confidence
            return violationCount >= _config.MinViolationsForFlying;
        }

        private float CalculateConfidence(FlyingAnalysisResult result)
        {
            var confidence = 0f;

            if (result.VerticalSpeedViolation) confidence += 0.3f;
            if (result.HorizontalSpeedViolation) confidence += 0.2f;
            if (result.AltitudeViolation) confidence += 0.2f;
            if (result.AccelerationViolation) confidence += 0.2f;
            if (result.GroundDistanceViolation) confidence += 0.1f;

            return Mathf.Clamp01(confidence);
        }

        private FlyingSeverity CalculateSeverity(FlyingAnalysisResult result)
        {
            var speed = result.Velocity.magnitude;

            if (speed > _config.MaxHorizontalSpeed * 2f)
                return FlyingSeverity.Extreme;
            else if (speed > _config.MaxHorizontalSpeed * 1.5f)
                return FlyingSeverity.High;
            else if (speed > _config.MaxHorizontalSpeed)
                return FlyingSeverity.Medium;
            else
                return FlyingSeverity.Low;
        }

        #endregion

        #region Event Logging

        public void LogFlyingDetected(Vector3 position, Vector3 velocity)
        {
            var analysisResult = new FlyingAnalysisResult
            {
                Position = position,
                Velocity = velocity,
                Timestamp = Time.time,
                IsFlying = true,
                ConfidenceLevel = 1f,
                SeverityLevel = FlyingSeverity.High
            };

            LogFlyingEvent(analysisResult);
        }

        private void LogFlyingEvent(FlyingAnalysisResult analysis)
        {
            var flyEvent = new FlyDetectionEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                GameTime = analysis.Timestamp,
                Position = analysis.Position,
                Velocity = analysis.Velocity,
                ConfidenceLevel = analysis.ConfidenceLevel,
                SeverityLevel = analysis.SeverityLevel,
                Violations = GetViolationsList(analysis),
                SessionId = GetCurrentSessionId()
            };

            _detectionEvents.Add(flyEvent);
            _flyEventCount++;
            _consecutiveFlyTime += analysis.DeltaTime;

            // Log to console based on severity
            LogEventToConsole(flyEvent);

            // Trigger event
            OnFlyingDetected?.Invoke(flyEvent);

            // Check for suspicious patterns
            CheckForSuspiciousPatterns();
        }

        private List<string> GetViolationsList(FlyingAnalysisResult analysis)
        {
            var violations = new List<string>();

            if (analysis.VerticalSpeedViolation)
                violations.Add($"Vertical speed: {analysis.Velocity.y:F1} m/s");

            if (analysis.HorizontalSpeedViolation)
                violations.Add($"Horizontal speed: {new Vector3(analysis.Velocity.x, 0, analysis.Velocity.z).magnitude:F1} m/s");

            if (analysis.AltitudeViolation)
                violations.Add($"Altitude: {analysis.Position.y:F1} m");

            if (analysis.AccelerationViolation)
                violations.Add($"Instant speed: {analysis.Velocity.magnitude:F1} m/s");

            if (analysis.GroundDistanceViolation)
                violations.Add("Too far from ground");

            return violations;
        }

        private void LogEventToConsole(FlyDetectionEvent flyEvent)
        {
            var message = $"Flying detected - Confidence: {flyEvent.ConfidenceLevel:P0}, " +
                         $"Severity: {flyEvent.SeverityLevel}, " +
                         $"Speed: {flyEvent.Velocity.magnitude:F1} m/s, " +
                         $"Position: {flyEvent.Position}";

            switch (flyEvent.SeverityLevel)
            {
                case FlyingSeverity.Extreme:
                    Plugin.Log.LogError($"[EXTREME FLYING] {message}");
                    break;
                case FlyingSeverity.High:
                    Plugin.Log.LogWarning($"[HIGH FLYING] {message}");
                    break;
                case FlyingSeverity.Medium:
                    Plugin.Log.LogWarning($"[MEDIUM FLYING] {message}");
                    break;
                case FlyingSeverity.Low:
                    Plugin.Log.LogInfo($"[LOW FLYING] {message}");
                    break;
            }
        }

        #endregion

        #region Pattern Analysis

        private void CheckForSuspiciousPatterns()
        {
            var recentEvents = _detectionEvents
                .Where(e => (DateTime.UtcNow - e.Timestamp).TotalMinutes < 5)
                .ToList();

            if (recentEvents.Count >= _config.SuspiciousEventThreshold)
            {
                var summary = GenerateSessionSummary();
                OnSuspiciousActivity?.Invoke(summary);
            }
        }

        private FlyDetectionSummary GenerateSessionSummary()
        {
            var sessionDuration = DateTime.UtcNow - _sessionStartTime;
            var recentEvents = _detectionEvents
                .Where(e => (DateTime.UtcNow - e.Timestamp).TotalHours < 1)
                .ToList();

            return new FlyDetectionSummary
            {
                SessionId = GetCurrentSessionId(),
                SessionDuration = sessionDuration,
                TotalEvents = _detectionEvents.Count,
                RecentEvents = recentEvents.Count,
                HighSeverityEvents = recentEvents.Count(e => e.SeverityLevel >= FlyingSeverity.High),
                AverageConfidence = recentEvents.Any() ? recentEvents.Average(e => e.ConfidenceLevel) : 0f,
                MaxSpeed = recentEvents.Any() ? recentEvents.Max(e => e.Velocity.magnitude) : 0f,
                ConsecutiveFlyTime = _consecutiveFlyTime,
                IsSuspicious = DetermineIfSuspicious(recentEvents),
                SuspiciousReasons = GetSuspiciousReasons(recentEvents)
            };
        }

        private bool DetermineIfSuspicious(List<FlyDetectionEvent> events)
        {
            if (events.Count >= _config.SuspiciousEventThreshold) return true;
            if (events.Count(e => e.SeverityLevel == FlyingSeverity.Extreme) >= 3) return true;
            if (_consecutiveFlyTime > 30f) return true; // 30 seconds of continuous flying

            return false;
        }

        private List<string> GetSuspiciousReasons(List<FlyDetectionEvent> events)
        {
            var reasons = new List<string>();

            if (events.Count >= _config.SuspiciousEventThreshold)
                reasons.Add($"High frequency of flying events ({events.Count} in recent period)");

            var extremeEvents = events.Count(e => e.SeverityLevel == FlyingSeverity.Extreme);
            if (extremeEvents >= 3)
                reasons.Add($"Multiple extreme flying events ({extremeEvents})");

            if (_consecutiveFlyTime > 30f)
                reasons.Add($"Extended continuous flying ({_consecutiveFlyTime:F1} seconds)");

            var maxSpeed = events.Any() ? events.Max(e => e.Velocity.magnitude) : 0f;
            if (maxSpeed > 50f)
                reasons.Add($"Extremely high speed detected ({maxSpeed:F1} m/s)");

            return reasons;
        }

        #endregion

        #region Helper Methods

        private Transform GetPlayerTransform()
        {
            try
            {
                var playerObject = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
                return playerObject?.transform;
            }
            catch
            {
                return null;
            }
        }

        private string GetCurrentSessionId()
        {
            // Generate a session ID based on start time
            return _sessionStartTime.ToString("yyyyMMdd_HHmmss");
        }

        #endregion

        #region Public API

        public List<FlyDetectionEvent> GetRecentEvents(int minutes = 60)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
            return _detectionEvents.Where(e => e.Timestamp >= cutoff).ToList();
        }

        public void ClearEvents()
        {
            _detectionEvents.Clear();
            _flyEventCount = 0;
            _consecutiveFlyTime = 0f;
            Plugin.Log.LogInfo("Fly detection events cleared");
        }

        public void UpdateConfig(FlyDetectionConfig newConfig)
        {
            if (newConfig != null)
            {
                // Update config properties
                _config.MaxVerticalSpeed = newConfig.MaxVerticalSpeed;
                _config.MaxHorizontalSpeed = newConfig.MaxHorizontalSpeed;
                _config.MaxReasonableAltitude = newConfig.MaxReasonableAltitude;
                _config.MaxInstantSpeed = newConfig.MaxInstantSpeed;
                _config.MaxGroundDistance = newConfig.MaxGroundDistance;
                _config.MaxGroundCheckDistance = newConfig.MaxGroundCheckDistance;
                _config.MinViolationsForFlying = newConfig.MinViolationsForFlying;
                _config.SuspiciousEventThreshold = newConfig.SuspiciousEventThreshold;
                _config.CheckInterval = newConfig.CheckInterval;

                Plugin.Log.LogInfo("Fly detection config updated");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            StopMonitoring();
            _detectionEvents.Clear();
            Plugin.Log.LogInfo("FlyDetectionLogger disposed");
        }

        #endregion
    }

    #region Data Models

    public class FlyDetectionEvent
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public float GameTime { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public float ConfidenceLevel { get; set; }
        public FlyingSeverity SeverityLevel { get; set; }
        public List<string> Violations { get; set; } = new List<string>();
        public string SessionId { get; set; }
    }

    public class FlyDetectionSummary
    {
        public string SessionId { get; set; }
        public TimeSpan SessionDuration { get; set; }
        public int TotalEvents { get; set; }
        public int RecentEvents { get; set; }
        public int HighSeverityEvents { get; set; }
        public float AverageConfidence { get; set; }
        public float MaxSpeed { get; set; }
        public float ConsecutiveFlyTime { get; set; }
        public bool IsSuspicious { get; set; }
        public List<string> SuspiciousReasons { get; set; } = new List<string>();
    }

    public class FlyingAnalysisResult
    {
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public float Timestamp { get; set; }
        public float DeltaTime { get; set; }
        public bool IsFlying { get; set; }
        public float ConfidenceLevel { get; set; }
        public FlyingSeverity SeverityLevel { get; set; }

        // Violation flags
        public bool VerticalSpeedViolation { get; set; }
        public bool HorizontalSpeedViolation { get; set; }
        public bool AltitudeViolation { get; set; }
        public bool AccelerationViolation { get; set; }
        public bool GroundDistanceViolation { get; set; }
    }

    public class FlyDetectionConfig
    {
        public float MaxVerticalSpeed { get; set; } = 15f; // m/s
        public float MaxHorizontalSpeed { get; set; } = 20f; // m/s
        public float MaxReasonableAltitude { get; set; } = 5000f; // meters
        public float MaxInstantSpeed { get; set; } = 30f; // m/s
        public float MaxGroundDistance { get; set; } = 50f; // meters
        public float MaxGroundCheckDistance { get; set; } = 100f; // meters
        public int MinViolationsForFlying { get; set; } = 2;
        public int SuspiciousEventThreshold { get; set; } = 10;
        public float CheckInterval { get; set; } = 0.1f; // seconds
    }

    public enum FlyingSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Extreme = 4
    }

    #endregion
}