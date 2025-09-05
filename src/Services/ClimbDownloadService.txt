using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FollowTheWay.Models;
using FollowTheWay.Utils;

namespace FollowTheWay.Services
{
    public class ClimbDownloadService
    {
        private readonly VPSApiService _apiService;
        private readonly ClimbDataService _dataService;
        private readonly ModLogger _logger;
        private bool _isDownloading;

        public ClimbDownloadService(VPSApiService apiService, ClimbDataService dataService)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _logger = new ModLogger("ClimbDownloadService");
            _isDownloading = false;
        }

        public async Task<List<ClimbData>> SearchClimbsAsync(string searchTerm = "", int page = 1, int pageSize = 20)
        {
            try
            {
                _logger.LogInfo($"Searching climbs: '{searchTerm}', page {page}");

                var response = await _apiService.SearchClimbsAsync(searchTerm, page, pageSize);

                if (response.Success && response.Data != null)
                {
                    _logger.LogInfo($"Found {response.Data.Count} climbs");
                    return response.Data;
                }
                else
                {
                    _logger.LogError($"Failed to search climbs: {response.ErrorMessage}");
                    return new List<ClimbData>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception during climb search: {ex.Message}");
                return new List<ClimbData>();
            }
        }

        public async Task<ClimbData> DownloadClimbAsync(string climbId)
        {
            if (string.IsNullOrEmpty(climbId))
            {
                _logger.LogWarning("Attempted to download climb with null or empty ID");
                return null;
            }

            try
            {
                _logger.LogInfo($"Downloading climb: {climbId}");

                var response = await _apiService.GetClimbAsync(climbId);

                if (response.Success && response.Data != null)
                {
                    _logger.LogInfo($"Successfully downloaded climb: {response.Data.ClimbName}");

                    // Save to local storage
                    await _dataService.SaveClimbDataAsync(response.Data);

                    return response.Data;
                }
                else
                {
                    _logger.LogError($"Failed to download climb {climbId}: {response.ErrorMessage}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception during climb download: {ex.Message}");
                return null;
            }
        }

        public async Task<List<ClimbData>> DownloadMultipleClimbsAsync(List<string> climbIds)
        {
            if (climbIds == null || climbIds.Count == 0)
            {
                _logger.LogWarning("Attempted to download multiple climbs with null or empty ID list");
                return new List<ClimbData>();
            }

            _isDownloading = true;
            var downloadedClimbs = new List<ClimbData>();

            try
            {
                _logger.LogInfo($"Downloading {climbIds.Count} climbs");

                foreach (var climbId in climbIds)
                {
                    var climb = await DownloadClimbAsync(climbId);
                    if (climb != null)
                    {
                        downloadedClimbs.Add(climb);
                    }

                    // Small delay to avoid overwhelming the server
                    await Task.Delay(100);
                }

                _logger.LogInfo($"Successfully downloaded {downloadedClimbs.Count} out of {climbIds.Count} climbs");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception during multiple climb download: {ex.Message}");
            }
            finally
            {
                _isDownloading = false;
            }

            return downloadedClimbs;
        }

        public async Task<List<ClimbData>> GetPopularClimbsAsync(int count = 10)
        {
            try
            {
                _logger.LogInfo($"Fetching {count} popular climbs");

                var response = await _apiService.GetPopularClimbsAsync(count);

                if (response.Success && response.Data != null)
                {
                    _logger.LogInfo($"Retrieved {response.Data.Count} popular climbs");
                    return response.Data;
                }
                else
                {
                    _logger.LogError($"Failed to get popular climbs: {response.ErrorMessage}");
                    return new List<ClimbData>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception getting popular climbs: {ex.Message}");
                return new List<ClimbData>();
            }
        }

        public async Task<List<ClimbData>> GetRecentClimbsAsync(int count = 10)
        {
            try
            {
                _logger.LogInfo($"Fetching {count} recent climbs");

                var response = await _apiService.GetRecentClimbsAsync(count);

                if (response.Success && response.Data != null)
                {
                    _logger.LogInfo($"Retrieved {response.Data.Count} recent climbs");
                    return response.Data;
                }
                else
                {
                    _logger.LogError($"Failed to get recent climbs: {response.ErrorMessage}");
                    return new List<ClimbData>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception getting recent climbs: {ex.Message}");
                return new List<ClimbData>();
            }
        }

        public bool IsDownloading()
        {
            return _isDownloading;
        }
    }
}