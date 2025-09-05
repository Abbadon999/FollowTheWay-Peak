using System;

namespace FollowTheWay.Models
{
    public class ServerConfig
    {
        public string ServerVersion { get; set; }
        public long MaxClimbSizeBytes { get; set; }
        public int MaxClimbsPerUser { get; set; }
        public bool CompressionEnabled { get; set; }
        public bool RequireAuthentication { get; set; }
        public string[] AllowedFileTypes { get; set; }
        public int RateLimitPerMinute { get; set; }
        public bool MaintenanceMode { get; set; }
        public string ServerMessage { get; set; }
        public string SupportedApiVersion { get; set; }
        public DateTime? LastUpdated { get; set; }

        public ServerConfig()
        {
            AllowedFileTypes = new[] { "json", "compressed" };
            LastUpdated = DateTime.UtcNow;
        }
    }
}