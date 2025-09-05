using System;
using System.Threading.Tasks;
using FollowTheWay.Models;
using FollowTheWay.Utils;

namespace FollowTheWay.Services
{
    public class ServerConfigService
    {
        private readonly VPSApiService _apiService;
        private readonly ModLogger _logger;
        private ServerConfig _cachedConfig;
        private DateTime _lastConfigUpdate;
        private readonly TimeSpan _configCacheTimeout = TimeSpan.FromMinutes(5);

        public ServerConfigService(VPSApiService apiService)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = new ModLogger("ServerConfigService");
            _lastConfigUpdate = DateTime.MinValue;
        }

        public async Task<ServerConfig> GetServerConfigAsync(bool forceRefresh = false)
        {
            try
            {
                // Return cached config if it's still valid and not forcing refresh
                if (!forceRefresh && _cachedConfig != null &&
                    DateTime.UtcNow - _lastConfigUpdate < _configCacheTimeout)
                {
                    _logger.LogDebug("Returning cached server config");
                    return _cachedConfig;
                }

                _logger.LogInfo("Fetching server configuration");

                var response = await _apiService.GetServerConfigAsync();

                if (response.Success && response.Data != null)
                {
                    _cachedConfig = response.Data;
                    _lastConfigUpdate = DateTime.UtcNow;

                    _logger.LogInfo("Successfully retrieved server configuration");
                    _logger.LogDebug($"Server version: {_cachedConfig.ServerVersion}");
                    _logger.LogDebug($"Max climb size: {_cachedConfig.MaxClimbSizeBytes} bytes");

                    return _cachedConfig;
                }
                else
                {
                    _logger.LogError($"Failed to get server config: {response.ErrorMessage}");

                    // Return cached config if available, even if expired
                    if (_cachedConfig != null)
                    {
                        _logger.LogWarning("Using expired cached server config");
                        return _cachedConfig;
                    }

                    // Return default config as fallback
                    return GetDefaultConfig();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception getting server config: {ex.Message}");

                // Return cached config if available
                if (_cachedConfig != null)
                {
                    _logger.LogWarning("Using cached server config due to exception");
                    return _cachedConfig;
                }

                // Return default config as fallback
                return GetDefaultConfig();
            }
        }

        public ServerConfig GetCachedConfig()
        {
            return _cachedConfig ?? GetDefaultConfig();
        }

        public bool IsConfigCached()
        {
            return _cachedConfig != null && DateTime.UtcNow - _lastConfigUpdate < _configCacheTimeout;
        }

        public void ClearCache()
        {
            _logger.LogInfo("Clearing server config cache");
            _cachedConfig = null;
            _lastConfigUpdate = DateTime.MinValue;
        }

        private ServerConfig GetDefaultConfig()
        {
            _logger.LogDebug("Using default server configuration");

            return new ServerConfig
            {
                ServerVersion = "1.0.0",
                MaxClimbSizeBytes = 10 * 1024 * 1024, // 10MB default
                MaxClimbsPerUser = 100,
                CompressionEnabled = true,
                RequireAuthentication = true,
                AllowedFileTypes = new[] { "json", "compressed" },
                RateLimitPerMinute = 60,
                MaintenanceMode = false,
                ServerMessage = "Welcome to FollowTheWay!",
                SupportedApiVersion = "1.0"
            };
        }

        public async Task<bool> IsServerAvailableAsync()
        {
            try
            {
                var config = await GetServerConfigAsync();
                return config != null && !config.MaintenanceMode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetServerVersionAsync()
        {
            try
            {
                var config = await GetServerConfigAsync();
                return config?.ServerVersion ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}