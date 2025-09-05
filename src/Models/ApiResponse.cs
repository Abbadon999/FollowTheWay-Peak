using System;
using Newtonsoft.Json;

namespace FollowTheWay.Models
{
    /// <summary>
    /// Generic API response wrapper for all FollowTheWay API calls
    /// </summary>
    /// <typeparam name="T">Type of data returned by the API</typeparam>
    [Serializable]
    public class ApiResponse<T>
    {
        [JsonProperty("success")]
        public bool IsSuccess { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; }

        [JsonProperty("error")]
        public string ErrorMessage { get; set; }

        [JsonProperty("errorCode")]
        public string ErrorCode { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        // Constructors
        public ApiResponse() { }

        public ApiResponse(bool success, T data = default, string errorMessage = null, string errorCode = null)
        {
            IsSuccess = success;
            Data = data;
            ErrorMessage = errorMessage;
            ErrorCode = errorCode;
            Timestamp = DateTime.UtcNow;
            RequestId = Guid.NewGuid().ToString("N").Substring(0, 8); // Short request ID
        }

        // Static factory methods
        public static ApiResponse<T> CreateSuccess(T data)
        {
            return new ApiResponse<T>(true, data);
        }

        public static ApiResponse<T> CreateFailure(string errorMessage, string errorCode = null)
        {
            return new ApiResponse<T>(false, default, errorMessage, errorCode);
        }

        public static ApiResponse<T> CreateFailure(Exception exception)
        {
            return new ApiResponse<T>(false, default, exception.Message, exception.GetType().Name);
        }

        // Helper methods
        public bool IsFailure => !IsSuccess;

        public override string ToString()
        {
            if (IsSuccess)
            {
                return $"Success: {Data}";
            }
            else
            {
                return $"Error: {ErrorMessage} ({ErrorCode})";
            }
        }
    }

    /// <summary>
    /// Server statistics model
    /// </summary>
    [Serializable]
    public class ServerStats
    {
        [JsonProperty("totalClimbs")]
        public int TotalClimbs { get; set; }

        [JsonProperty("totalPayloads")]
        public int TotalPayloads { get; set; }

        [JsonProperty("totalApiKeys")]
        public int TotalApiKeys { get; set; }

        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        [JsonProperty("serverVersion")]
        public string ServerVersion { get; set; }

        [JsonProperty("uptime")]
        public TimeSpan? Uptime { get; set; }

        public override string ToString()
        {
            return $"Climbs: {TotalClimbs}, Payloads: {TotalPayloads}, Keys: {TotalApiKeys}";
        }
    }

    /// <summary>
    /// Update message for real-time notifications
    /// </summary>
    [Serializable]
    public class UpdateMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("data")]
        public object Data { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonProperty("source")]
        public string Source { get; set; } = "FollowTheWay";

        // Message types
        public const string CLIMB_UPLOADED = "climb_uploaded";
        public const string CLIMB_DOWNLOADED = "climb_downloaded";
        public const string CLIMB_DELETED = "climb_deleted";
        public const string SERVER_STATS_UPDATED = "server_stats_updated";
        public const string MOD_UPDATE_AVAILABLE = "mod_update_available";
        public const string MAINTENANCE_MODE = "maintenance_mode";

        public static UpdateMessage ClimbUploaded(ClimbData climb)
        {
            return new UpdateMessage
            {
                Type = CLIMB_UPLOADED,
                Data = new { climbId = climb.Id, title = climb.Title, author = climb.Author }
            };
        }

        public static UpdateMessage ClimbDownloaded(Guid climbId, string title)
        {
            return new UpdateMessage
            {
                Type = CLIMB_DOWNLOADED,
                Data = new { climbId, title }
            };
        }

        public static UpdateMessage ClimbDeleted(Guid climbId)
        {
            return new UpdateMessage
            {
                Type = CLIMB_DELETED,
                Data = new { climbId }
            };
        }

        public static UpdateMessage ServerStatsUpdated(ServerStats stats)
        {
            return new UpdateMessage
            {
                Type = SERVER_STATS_UPDATED,
                Data = stats
            };
        }

        public static UpdateMessage ModUpdateAvailable(string version, string downloadUrl)
        {
            return new UpdateMessage
            {
                Type = MOD_UPDATE_AVAILABLE,
                Data = new { version, downloadUrl }
            };
        }

        public static UpdateMessage MaintenanceMode(bool enabled, string message = null)
        {
            return new UpdateMessage
            {
                Type = MAINTENANCE_MODE,
                Data = new { enabled, message }
            };
        }
    }

    /// <summary>
    /// Upload queue item for managing upload operations
    /// </summary>
    [Serializable]
    public class UploadQueueItem
    {
        [JsonProperty("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonProperty("climbData")]
        public ClimbData ClimbDataItem { get; set; }

        [JsonProperty("status")]
        public UploadStatus Status { get; set; } = UploadStatus.Pending;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("lastAttempt")]
        public DateTime? LastAttempt { get; set; }

        [JsonProperty("attemptCount")]
        public int AttemptCount { get; set; } = 0;

        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; set; }

        [JsonProperty("priority")]
        public int Priority { get; set; } = 0; // Higher = more priority

        public bool CanRetry => AttemptCount < 3 && Status == UploadStatus.Failed;
        public bool IsExpired => CreatedAt.AddHours(24) < DateTime.UtcNow; // Expire after 24 hours
    }

    /// <summary>
    /// Upload status enumeration
    /// </summary>
    public enum UploadStatus
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4,
        Expired = 5
    }

    /// <summary>
    /// Server configuration model
    /// </summary>
    [Serializable]
    public class ServerConfig
    {
        [JsonProperty("serverUrl")]
        public string ServerUrl { get; set; } = "https://followtheway.ru";

        [JsonProperty("apiVersion")]
        public string ApiVersion { get; set; } = "v1";

        [JsonProperty("maxUploadSize")]
        public long MaxUploadSize { get; set; } = 20 * 1024 * 1024; // 20MB

        [JsonProperty("maxClimbPoints")]
        public int MaxClimbPoints { get; set; } = 50000;

        [JsonProperty("compressionEnabled")]
        public bool CompressionEnabledValue { get; set; } = true;

        [JsonProperty("retryAttempts")]
        public int RetryAttempts { get; set; } = 3;

        [JsonProperty("timeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 30;

        [JsonProperty("rateLimitPerMinute")]
        public int RateLimitPerMinuteValue { get; set; } = 30;

        [JsonProperty("maintenanceMode")]
        public bool MaintenanceModeValue { get; set; } = false;

        [JsonProperty("maintenanceMessage")]
        public string MaintenanceMessage { get; set; }

        [JsonProperty("supportedGameVersions")]
        public string[] SupportedGameVersions { get; set; } = { "1.0" };

        [JsonProperty("requiredModVersion")]
        public string RequiredModVersion { get; set; } = "0.0.1";

        [JsonProperty("features")]
        public ServerFeatures Features { get; set; } = new ServerFeatures();

        public string GetFullApiUrl() => $"{ServerUrl.TrimEnd('/')}/api/{ApiVersion}";
    }

    /// <summary>
    /// Server features configuration
    /// </summary>
    [Serializable]
    public class ServerFeatures
    {
        [JsonProperty("uploadEnabled")]
        public bool UploadEnabled { get; set; } = true;

        [JsonProperty("downloadEnabled")]
        public bool DownloadEnabled { get; set; } = true;

        [JsonProperty("searchEnabled")]
        public bool SearchEnabled { get; set; } = true;

        [JsonProperty("statisticsEnabled")]
        public bool StatisticsEnabled { get; set; } = true;

        [JsonProperty("leaderboardEnabled")]
        public bool LeaderboardEnabled { get; set; } = true;

        [JsonProperty("socialFeaturesEnabled")]
        public bool SocialFeaturesEnabled { get; set; } = false;

        [JsonProperty("moderationEnabled")]
        public bool ModerationEnabled { get; set; } = true;

        [JsonProperty("antiCheatEnabled")]
        public bool AntiCheatEnabled { get; set; } = true;
    }
}