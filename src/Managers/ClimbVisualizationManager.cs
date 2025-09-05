using FollowTheWay.Models;
using FollowTheWay.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.Remoting.Messaging;
using UnityEngine;

namespace FollowTheWay.Managers
{
    public class ClimbVisualizationManager : IDisposable
    {
        private bool _isVisualizationActive = false;
        private ClimbData _currentVisualizationClimb;
        private List<GameObject> _visualizationObjects;
        private LineRenderer _pathLineRenderer;
        private GameObject _pathContainer;
        private Material _pathMaterial;
        private Material _pointMaterial;

        // Visualization settings
        private VisualizationSettings _settings;

        // Animation state
        private bool _isAnimating = false;
        private float _animationProgress = 0f;
        private float _animationSpeed = 1f;
        private int _currentAnimationPoint = 0;

        // Events
        public event Action<ClimbData> OnVisualizationStarted;
        public event Action<ClimbData> OnVisualizationStopped;
        public event Action<float> OnAnimationProgress;

        // Properties
        public bool IsVisualizationActive => _isVisualizationActive;
        public bool IsAnimating => _isAnimating;
        public ClimbData CurrentClimb => _currentVisualizationClimb;
        public float AnimationProgress => _animationProgress;

        public ClimbVisualizationManager()
        {
            _settings = new VisualizationSettings();
            _visualizationObjects = new List<GameObject>();

            InitializeMaterials();
            Plugin.Log.LogInfo("ClimbVisualizationManager initialized");
        }

        public void Update()
        {
            try
            {
                if (_isAnimating)
                {
                    UpdateAnimation();
                }

                // Update visualization objects (fade, effects, etc.)
                UpdateVisualizationEffects();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error in ClimbVisualizationManager.Update: {ex.Message}");
            }
        }

        #region Visualization Control

        public bool StartVisualization(ClimbData climbData)
        {
            if (climbData == null || !climbData.HasPoints)
            {
                Plugin.Log.LogWarning("Cannot visualize climb: No climb data or points");
                return false;
            }

            try
            {
                // Stop any existing visualization
                StopVisualization();

                _currentVisualizationClimb = climbData;
                _isVisualizationActive = true;

                // Create visualization objects
                CreateVisualizationObjects();

                Plugin.Log.LogInfo($"Started visualization for climb: {climbData.Title} ({climbData.Points.Count} points)");
                OnVisualizationStarted?.Invoke(climbData);

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to start visualization: {ex.Message}");
                return false;
            }
        }

        public void StopVisualization()
        {
            if (!_isVisualizationActive) return;

            try
            {
                // Stop animation
                StopAnimation();

                // Destroy visualization objects
                DestroyVisualizationObjects();

                var previousClimb = _currentVisualizationClimb;
                _currentVisualizationClimb = null;
                _isVisualizationActive = false;

                Plugin.Log.LogInfo("Stopped climb visualization");
                OnVisualizationStopped?.Invoke(previousClimb);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to stop visualization: {ex.Message}");
            }
        }

        public void StartAnimation(float speed = 1f)
        {
            if (!_isVisualizationActive || _currentVisualizationClimb == null)
            {
                Plugin.Log.LogWarning("Cannot start animation: No active visualization");
                return;
            }

            _isAnimating = true;
            _animationSpeed = Mathf.Max(0.1f, speed);
            _animationProgress = 0f;
            _currentAnimationPoint = 0;

            Plugin.Log.LogInfo($"Started climb animation at {speed}x speed");
        }

        public void StopAnimation()
        {
            _isAnimating = false;
            _animationProgress = 0f;
            _currentAnimationPoint = 0;
        }

        public void PauseAnimation()
        {
            _isAnimating = false;
        }

        public void ResumeAnimation()
        {
            if (_currentVisualizationClimb != null)
            {
                _isAnimating = true;
            }
        }

        public void SetAnimationProgress(float progress)
        {
            if (!_isVisualizationActive) return;

            _animationProgress = Mathf.Clamp01(progress);
            _currentAnimationPoint = Mathf.RoundToInt(_animationProgress * (_currentVisualizationClimb.Points.Count - 1));

            UpdateAnimationVisualization();
        }

        #endregion

        #region Visualization Creation

        private void CreateVisualizationObjects()
        {
            if (_currentVisualizationClimb?.Points == null) return;

            // Create container
            _pathContainer = new GameObject("FollowTheWay_ClimbPath");
            _pathContainer.transform.position = Vector3.zero;

            // Create path line
            CreatePathLine();

            // Create point markers
            CreatePointMarkers();

            // Create start/end markers
            CreateStartEndMarkers();
        }

        private void CreatePathLine()
        {
            var pathObject = new GameObject("ClimbPath_Line");
            pathObject.transform.SetParent(_pathContainer.transform);

            _pathLineRenderer = pathObject.AddComponent<LineRenderer>();
            _pathLineRenderer.material = _pathMaterial;
            _pathLineRenderer.color = _settings.PathColor;
            _pathLineRenderer.startWidth = _settings.PathWidth;
            _pathLineRenderer.endWidth = _settings.PathWidth;
            _pathLineRenderer.useWorldSpace = true;
            _pathLineRenderer.positionCount = _currentVisualizationClimb.Points.Count;

            // Set positions
            var positions = _currentVisualizationClimb.Points.Select(p => p.Position).ToArray();
            _pathLineRenderer.SetPositions(positions);

            _visualizationObjects.Add(pathObject);
        }

        private void CreatePointMarkers()
        {
            if (!_settings.ShowPointMarkers) return;

            var points = _currentVisualizationClimb.Points;
            var markerInterval = Mathf.Max(1, points.Count / _settings.MaxPointMarkers);

            for (int i = 0; i < points.Count; i += markerInterval)
            {
                var point = points[i];
                var marker = CreatePointMarker(point, i);
                _visualizationObjects.Add(marker);
            }
        }

        private GameObject CreatePointMarker(ClimbPoint point, int index)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"ClimbPoint_{index}";
            marker.transform.position = point.Position;
            marker.transform.localScale = Vector3.one * _settings.PointMarkerSize;
            marker.transform.SetParent(_pathContainer.transform);

            var renderer = marker.GetComponent<Renderer>();
            renderer.material = _pointMaterial;

            // Color based on properties
            var color = GetPointColor(point);
            renderer.material.color = color;

            // Remove collider to avoid interference
            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            return marker;
        }

        private void CreateStartEndMarkers()
        {
            if (_currentVisualizationClimb.Points.Count < 2) return;

            // Start marker
            var startMarker = CreateSpecialMarker(_currentVisualizationClimb.Points.First().Position,
                                                "Start", _settings.StartMarkerColor, _settings.StartMarkerSize);
            _visualizationObjects.Add(startMarker);

            // End marker
            var endMarker = CreateSpecialMarker(_currentVisualizationClimb.Points.Last().Position,
                                              "End", _settings.EndMarkerColor, _settings.EndMarkerSize);
            _visualizationObjects.Add(endMarker);
        }

        private GameObject CreateSpecialMarker(Vector3 position, string name, Color color, float size)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"ClimbMarker_{name}";
            marker.transform.position = position + Vector3.up * 0.5f; // Slightly elevated
            marker.transform.localScale = Vector3.one * size;
            marker.transform.SetParent(_pathContainer.transform);

            var renderer = marker.GetComponent<Renderer>();
            renderer.material = _pointMaterial;
            renderer.material.color = color;

            // Remove collider
            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            return marker;
        }

        #endregion

        #region Animation

        private void UpdateAnimation()
        {
            if (_currentVisualizationClimb?.Points == null || _currentVisualizationClimb.Points.Count < 2)
            {
                StopAnimation();
                return;
            }

            var totalDuration = (float)_currentVisualizationClimb.Duration.Value.TotalSeconds;
            var deltaTime = Time.deltaTime * _animationSpeed;

            _animationProgress += deltaTime / totalDuration;

            if (_animationProgress >= 1f)
            {
                _animationProgress = 1f;
                _isAnimating = false;
            }

            _currentAnimationPoint = Mathf.RoundToInt(_animationProgress * (_currentVisualizationClimb.Points.Count - 1));

            UpdateAnimationVisualization();
            OnAnimationProgress?.Invoke(_animationProgress);
        }

        private void UpdateAnimationVisualization()
        {
            if (_pathLineRenderer == null || _currentVisualizationClimb?.Points == null) return;

            // Update line renderer to show progress
            var visiblePoints = Mathf.Max(1, _currentAnimationPoint + 1);
            _pathLineRenderer.positionCount = visiblePoints;

            var positions = _currentVisualizationClimb.Points
                .Take(visiblePoints)
                .Select(p => p.Position)
                .ToArray();

            _pathLineRenderer.SetPositions(positions);

            // Update point marker visibility
            UpdatePointMarkerVisibility();
        }

        private void UpdatePointMarkerVisibility()
        {
            // This would update the visibility of point markers based on animation progress
            // Implementation depends on how markers are stored and managed
        }

        #endregion

        #region Helper Methods

        private void InitializeMaterials()
        {
            try
            {
                // Create path material
                _pathMaterial = new Material(Shader.Find("Sprites/Default"));
                _pathMaterial.color = _settings.PathColor;

                // Create point material
                _pointMaterial = new Material(Shader.Find("Standard"));
                _pointMaterial.color = _settings.PointMarkerColor;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to initialize materials: {ex.Message}");

                // Fallback to default materials
                _pathMaterial = new Material(Shader.Find("Diffuse"));
                _pointMaterial = new Material(Shader.find("Diffuse"));
            }
        }

        private Color GetPointColor(ClimbPoint point)
        {
            // Color based on speed
            var speed = point.Velocity.magnitude;

            if (point.IsFlying)
                return Color.red; // Suspicious points in red
            else if (speed > 10f)
                return Color.yellow; // Fast points in yellow
            else if (speed < 1f)
                return Color.blue; // Slow points in blue
            else
                return _settings.PointMarkerColor; // Normal points
        }

        private void UpdateVisualizationEffects()
        {
            if (!_isVisualizationActive) return;

            // Add pulsing effect to markers
            var time = Time.time;
            var pulse = (Mathf.Sin(time * 2f) + 1f) * 0.5f;

            foreach (var obj in _visualizationObjects)
            {
                if (obj != null && obj.name.Contains("Marker"))
                {
                    var baseScale = obj.name.Contains("Start") || obj.name.Contains("End") ?
                                   _settings.StartMarkerSize : _settings.PointMarkerSize;

                    obj.transform.localScale = Vector3.one * (baseScale * (0.8f + pulse * 0.4f));
                }
            }
        }

        private void DestroyVisualizationObjects()
        {
            foreach (var obj in _visualizationObjects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.Destroy(obj);
                }
            }

            _visualizationObjects.Clear();

            if (_pathContainer != null)
            {
                UnityEngine.Object.Destroy(_pathContainer);
                _pathContainer = null;
            }

            _pathLineRenderer = null;
        }

        #endregion

        #region Settings

        public void UpdateSettings(VisualizationSettings newSettings)
        {
            _settings = newSettings ?? new VisualizationSettings();

            // Apply settings to existing visualization
            if (_isVisualizationActive)
            {
                ApplySettingsToVisualization();
            }

            Plugin.Log.LogInfo("Visualization settings updated");
        }

        private void ApplySettingsToVisualization()
        {
            if (_pathLineRenderer != null)
            {
                _pathLineRenderer.color = _settings.PathColor;
                _pathLineRenderer.startWidth = _settings.PathWidth;
                _pathLineRenderer.endWidth = _settings.PathWidth;
            }

            // Update materials
            if (_pathMaterial != null)
                _pathMaterial.color = _settings.PathColor;

            if (_pointMaterial != null)
                _pointMaterial.color = _settings.PointMarkerColor;
        }

        public VisualizationSettings GetSettings()
        {
            return _settings;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            StopVisualization();

            if (_pathMaterial != null)
                UnityEngine.Object.Destroy(_pathMaterial);

            if (_pointMaterial != null)
                UnityEngine.Object.Destroy(_pointMaterial);

            Plugin.Log.LogInfo("ClimbVisualizationManager disposed");
        }

        #endregion
    }

    [Serializable]
    public class VisualizationSettings
    {
        public Color PathColor { get; set; } = Color.cyan;
        public float PathWidth { get; set; } = 0.1f;
        public bool ShowPointMarkers { get; set; } = true;
        public Color PointMarkerColor { get; set; } = Color.white;
        public float PointMarkerSize { get; set; } = 0.2f;
        public int MaxPointMarkers { get; set; } = 100;
        public Color StartMarkerColor { get; set; } = Color.green;
        public Color EndMarkerColor { get; set; } = Color.red;
        public float StartMarkerSize { get; set; } = 0.5f;
        public float EndMarkerSize { get; set; } = 0.5f;
        public bool EnableEffects { get; set; } = true;
        public float EffectIntensity { get; set; } = 1f;
    }
}