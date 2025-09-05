using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace FollowTheWay.Models
{
    [Serializable]
    public class ClimbData
    {
        [JsonProperty("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonProperty("title")]
        public string Title { get; set; } = "";

        [JsonProperty("author")]
        public string Author { get; set; } = "";

        [JsonProperty("playerName")]
        public string PlayerName { get; set; } = "";

        [JsonProperty("modVersion")]
        public string ModVersion { get; set; } = "";

        [JsonProperty("map")]
        public string Map { get; set; } = "Peak";

        [JsonProperty("biomeName")]
        public string BiomeName { get; set; } = "";

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; } = "Medium";

        [JsonProperty("ascentLevel")]
        public int AscentLevel { get; set; } = 1;

        [JsonProperty("duration")]
        public TimeSpan? Duration { get; set; }

        [JsonProperty("lengthMeters")]
        public double? LengthMeters { get; set; }

        [JsonProperty("gameVersion")]
        public string GameVersion { get; set; } = "1.0";

        [JsonProperty("climbCode")]
        public string ClimbCode { get; set; } = "";

        [JsonProperty("startAltitude")]
        public float? StartAltitude { get; set; }

        [JsonProperty("endAltitude")]
        public float? EndAltitude { get; set; }

        [JsonProperty("pointsCount")]
        public int? PointsCount { get; set; }

        [JsonProperty("downloads")]
        public long Downloads { get; set; } = 0;

        [JsonProperty("likes")]
        public long Likes { get; set; } = 0;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [JsonProperty("lastDownloadedAt")]
        public DateTime? LastDownloadedAt { get; set; }

        [JsonProperty("isVerified")]
        public bool IsVerified { get; set; } = false;

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        // Climb recording data
        [JsonProperty("points")]
        public List<ClimbPoint> Points { get; set; } = new List<ClimbPoint>();

        [JsonProperty("startTime")]
        public DateTime StartTime { get; set; }

        [JsonProperty("endTime")]
        public DateTime EndTime { get; set; }

        [JsonProperty("recordingSettings")]
        public RecordingSettings RecordingSettings { get; set; } = new RecordingSettings();

        // Calculated properties
        [JsonIgnore]
        public bool HasPoints => Points != null && Points.Count > 0;

        [JsonIgnore]
        public Vector3 StartPosition => HasPoints ? Points[0].Position : Vector3.zero;

        [JsonIgnore]
        public Vector3 EndPosition => HasPoints ? Points[Points.Count - 1].Position : Vector3.zero;

        [JsonIgnore]
        public float TotalDistance
        {
            get
            {
                if (!HasPoints || Points.Count < 2) return 0f;

                float distance = 0f;
                for (int i = 1; i < Points.Count; i++)
                {
                    distance += Vector3.Distance(Points[i - 1].Position, Points[i].Position);
                }
                return distance;
            }
        }

        [JsonIgnore]
        public float AverageSpeed
        {
            get
            {
                if (Duration == null || Duration.Value.TotalSeconds <= 0) return 0f;
                return TotalDistance / (float)Duration.Value.TotalSeconds;
            }
        }

        [JsonIgnore]
        public float MaxSpeed
        {
            get
            {
                if (!HasPoints || Points.Count < 2) return 0f;

                float maxSpeed = 0f;
                for (int i = 1; i < Points.Count; i++)
                {
                    var timeDiff = Points[i].Timestamp - Points[i - 1].Timestamp;
                    if (timeDiff > 0)
                    {
                        var distance = Vector3.Distance(Points[i - 1].Position, Points[i].Position);
                        var speed = distance / timeDiff;
                        maxSpeed = Mathf.Max(maxSpeed, speed);
                    }
                }
                return maxSpeed;
            }
        }

        // Validation methods
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Title) &&
                   !string.IsNullOrEmpty(Author) &&
                   HasPoints &&
                   Duration.HasValue &&
                   Duration.Value.TotalSeconds > 0;
        }

        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(Title))
                errors.Add("Title is required");

            if (string.IsNullOrEmpty(Author))
                errors.Add("Author is required");

            if (!HasPoints)
                errors.Add("Climb must have recorded points");

            if (!Duration.HasValue || Duration.Value.TotalSeconds <= 0)
                errors.Add("Duration must be greater than 0");

            if (Points != null && Points.Count > 0)
            {
                // Check for suspicious data
                if (MaxSpeed > 50f) // 50 m/s seems unrealistic for climbing
                    errors.Add("Maximum speed seems unrealistic (possible cheating)");

                if (TotalDistance < 10f) // Very short climbs might be invalid
                    errors.Add("Climb distance seems too short");
            }

            return errors;
        }

        // Helper methods for biome detection
        public void DetectBiome()
        {
            if (!HasPoints) return;

            // Simple biome detection based on altitude and position
            var avgAltitude = 0f;
            var avgX = 0f;
            var avgZ = 0f;

            foreach (var point in Points)
            {
                avgAltitude += point.Position.y;
                avgX += point.Position.x;
                avgZ += point.Position.z;
            }

            avgAltitude /= Points.Count;
            avgX /= Points.Count;
            avgZ /= Points.Count;

            // Basic biome classification (this would need to be adjusted based on actual game map)
            if (avgAltitude > 2000f)
                BiomeName = "High Mountain";
            else if (avgAltitude > 1000f)
                BiomeName = "Mountain";
            else if (avgAltitude > 500f)
                BiomeName = "Hills";
            else if (Mathf.Abs(avgX) < 500f && Mathf.Abs(avgZ) < 500f)
                BiomeName = "Valley";
            else
                BiomeName = "Plains";
        }

        // Helper method for ascent level calculation
        public void CalculateAscentLevel()
        {
            if (!HasPoints) return;

            var elevationGain = EndPosition.y - StartPosition.y;
            var distance = TotalDistance;
            var duration = Duration?.TotalMinutes ?? 0;

            // Calculate difficulty based on multiple factors
            var difficultyScore = 0;

            // Elevation gain factor
            if (elevationGain > 1000f) difficultyScore += 3;
            else if (elevationGain > 500f) difficultyScore += 2;
            else if (elevationGain > 200f) difficultyScore += 1;

            // Distance factor
            if (distance > 5000f) difficultyScore += 2;
            else if (distance > 2000f) difficultyScore += 1;

            // Duration factor (longer climbs are generally harder)
            if (duration > 60) difficultyScore += 2;
            else if (duration > 30) difficultyScore += 1;

            // Speed factor (very fast climbs might indicate easier routes or cheating)
            var avgSpeed = AverageSpeed;
            if (avgSpeed < 2f) difficultyScore += 1; // Slow = difficult
            else if (avgSpeed > 10f) difficultyScore -= 1; // Too fast = suspicious

            // Convert to 1-5 scale
            AscentLevel = Mathf.Clamp(1 + (difficultyScore / 2), 1, 5);

            // Set difficulty string based on ascent level
            Difficulty = AscentLevel switch
            {
                1 => "Easy",
                2 => "Medium",
                3 => "Hard",
                4 => "Very Hard",
                5 => "Extreme",
                _ => "Medium"
            };
        }

        // Create a copy for upload (without sensitive data)
        public ClimbData CreateUploadCopy()
        {
            var copy = new ClimbData
            {
                Id = this.Id,
                Title = this.Title,
                Author = this.Author,
                PlayerName = this.PlayerName ?? this.Author,
                ModVersion = this.ModVersion,
                Map = this.Map,
                BiomeName = this.BiomeName,
                Difficulty = this.Difficulty,
                AscentLevel = this.AscentLevel,
                Duration = this.Duration,
                LengthMeters = this.LengthMeters,
                GameVersion = this.GameVersion,
                ClimbCode = this.ClimbCode,
                StartAltitude = this.StartAltitude,
                EndAltitude = this.EndAltitude,
                PointsCount = this.Points?.Count,
                Tags = new List<string>(this.Tags ?? new List<string>()),
                Points = new List<ClimbPoint>(this.Points ?? new List<ClimbPoint>()),
                StartTime = this.StartTime,
                EndTime = this.EndTime,
                RecordingSettings = this.RecordingSettings
            };

            return copy;
        }
    }

    [Serializable]
    public class ClimbPoint
    {
        [JsonProperty("position")]
        public Vector3 Position { get; set; }

        [JsonProperty("timestamp")]
        public float Timestamp { get; set; }

        [JsonProperty("velocity")]
        public Vector3 Velocity { get; set; }

        [JsonProperty("isGrounded")]
        public bool IsGrounded { get; set; }

        [JsonProperty("isFlying")]
        public bool IsFlying { get; set; }

        [JsonProperty("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        public ClimbPoint() { }

        public ClimbPoint(Vector3 position, float timestamp)
        {
            Position = position;
            Timestamp = timestamp;
        }

        public ClimbPoint(Vector3 position, float timestamp, Vector3 velocity, bool isGrounded = true)
        {
            Position = position;
            Timestamp = timestamp;
            Velocity = velocity;
            IsGrounded = isGrounded;
        }
    }

    [Serializable]
    public class RecordingSettings
    {
        [JsonProperty("recordingInterval")]
        public float RecordingInterval { get; set; } = 0.1f; // 10 times per second

        [JsonProperty("minDistanceThreshold")]
        public float MinDistanceThreshold { get; set; } = 0.5f; // Minimum distance to record new point

        [JsonProperty("recordVelocity")]
        public bool RecordVelocity { get; set; } = true;

        [JsonProperty("recordGroundState")]
        public bool RecordGroundState { get; set; } = true;

        [JsonProperty("detectFlying")]
        public bool DetectFlying { get; set; } = true;

        [JsonProperty("compressionLevel")]
        public int CompressionLevel { get; set; } = 1; // 0 = none, 1 = light, 2 = heavy
    }
}