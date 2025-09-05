using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using FollowTheWay.Models;
using FollowTheWay.Utils;
using FollowTheWay.Config;

namespace FollowTheWay.Services
{
    public class VPSApiService : IDisposable
    {
        private const string BASE_URL = "https://followtheway.ru/api/v1";
        private const int TIMEOUT_SECONDS = 30;
        private const int MAX_RETRIES = 3;

        private readonly string _apiKey;
        private bool _disposed = false;

        public VPSApiService()
        {
            _apiKey = ApiKeys.FollowTheWayApiKey;

            if (string.IsNullOrEmpty(_apiKey))
            {
                Plugin.Log.LogError("FollowTheWay API key not configured! Please check ApiKeys.cs");
                throw new InvalidOperationException("API key not configured");
            }

            Plugin.Log.LogInfo($"VPSApiService initialized with server: {BASE_URL}");
        }

        #region Health Check

        public async Task<bool> CheckServerHealthAsync()
        {
            try
            {
                using var request = UnityWebRequest.Get($"{BASE_URL}/health");
                request.timeout = TIMEOUT_SECONDS;

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonConvert.DeserializeObject<HealthResponse>(request.downloadHandler.text);
                    Plugin.Log.LogInfo($"Server health check successful: {response?.ok}");
                    return response?.ok == true;
                }

                Plugin.Log.LogWarning($"Health check failed: {request.error}");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Health check error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Climb Operations

        public async Task<ApiResponse<ClimbData>> UploadClimbAsync(ClimbData climbData)
        {
            if (climbData == null)
                return ApiResponse<ClimbData>.CreateFailure("Climb data is null");

            try
            {
                // First, create the climb metadata
                var createResponse = await CreateClimbMetadataAsync(climbData);
                if (!createResponse.IsSuccess)
                    return createResponse;

                var climbId = createResponse.Data.Id;

                // Then upload the payload (compressed climb points)
                var payloadResponse = await UploadClimbPayloadAsync(climbId, climbData);
                if (!payloadResponse.IsSuccess)
                {
                    Plugin.Log.LogWarning($"Payload upload failed for climb {climbId}, but metadata was created");
                }

                Plugin.Log.LogInfo($"Climb uploaded successfully: {climbId}");
                return createResponse;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Upload climb error: {ex.Message}");
                return ApiResponse<ClimbData>.CreateFailure($"Upload failed: {ex.Message}");
            }
        }

        private async Task<ApiResponse<ClimbData>> CreateClimbMetadataAsync(ClimbData climbData)
        {
            var payload = new
            {
                id = climbData.Id,
                title = climbData.Title ?? $"Climb {DateTime.Now:yyyy-MM-dd HH:mm}",
                author = climbData.Author ?? "Unknown",
                playerName = climbData.PlayerName ?? climbData.Author,
                modVersion = Plugin.ModVersion,
                map = climbData.Map ?? "Peak",
                biomeName = climbData.BiomeName ?? "Unknown",
                difficulty = climbData.Difficulty ?? "Medium",
                ascentLevel = climbData.AscentLevel,
                duration = climbData.Duration?.TotalSeconds,
                lengthMeters = climbData.LengthMeters,
                gameVersion = climbData.GameVersion ?? "1.0",
                climbCode = GenerateClimbCode(),
                startAltitude = climbData.StartAltitude,
                endAltitude = climbData.EndAltitude,
                pointsCount = climbData.Points?.Count ?? 0,
                tagsJson = JsonConvert.SerializeObject(climbData.Tags ?? new List<string>()),
                isVerified = false
            };

            using var request = CreatePostRequest($"{BASE_URL}/climbs", payload);
            return await ExecuteRequestAsync<ClimbData>(request);
        }

        private async Task<ApiResponse<object>> UploadClimbPayloadAsync(Guid climbId, ClimbData climbData)
        {
            try
            {
                // Compress climb data using ClimbDataCrusher
                var compressedData = ClimbDataCrusher.Compress(climbData);

                using var request = new UnityWebRequest($"{BASE_URL}/climbs/{climbId}/payload", "POST");
                request.uploadHandler = new UploadHandlerRaw(compressedData);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/octet-stream");
                request.SetRequestHeader("X-Api-Key", _apiKey);
                request.timeout = TIMEOUT_SECONDS * 2; // Longer timeout for file uploads

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Plugin.Log.LogInfo($"Payload uploaded successfully for climb {climbId}");
                    return ApiResponse<object>.CreateSuccess(new { uploaded = true });
                }
                else
                {
                    Plugin.Log.LogError($"Payload upload failed: {request.error}");
                    return ApiResponse<object>.CreateFailure($"Payload upload failed: {request.error}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Payload upload error: {ex.Message}");
                return ApiResponse<object>.CreateFailure($"Payload upload error: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<ClimbData>>> GetClimbsAsync(int skip = 0, int take = 20, string searchQuery = null, string sortBy = "latest")
        {
            try
            {
                var url = $"{BASE_URL}/climbs?skip={skip}&take={take}&sort={sortBy}";
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    url += $"&q={UnityWebRequest.EscapeURL(searchQuery)}";
                }

                using var request = CreateGetRequest(url);
                var response = await ExecuteRequestAsync<ClimbListResponse>(request);

                if (response.IsSuccess && response.Data != null)
                {
                    var climbs = response.Data.Items?.ConvertAll(ConvertToClimbData) ?? new List<ClimbData>();
                    return ApiResponse<List<ClimbData>>.CreateSuccess(climbs);
                }

                return ApiResponse<List<ClimbData>>.CreateFailure(response.ErrorMessage);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Get climbs error: {ex.Message}");
                return ApiResponse<List<ClimbData>>.CreateFailure($"Failed to get climbs: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ClimbData>> GetClimbAsync(Guid climbId)
        {
            try
            {
                using var request = CreateGetRequest($"{BASE_URL}/climbs/{climbId}");
                return await ExecuteRequestAsync<ClimbData>(request);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Get climb error: {ex.Message}");
                return ApiResponse<ClimbData>.CreateFailure($"Failed to get climb: {ex.Message}");
            }
        }

        public async Task<ApiResponse<byte[]>> DownloadClimbPayloadAsync(Guid climbId)
        {
            try
            {
                using var request = CreateGetRequest($"{BASE_URL}/climbs/{climbId}/payload");

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var data = request.downloadHandler.data;
                    Plugin.Log.LogInfo($"Payload downloaded successfully for climb {climbId}, size: {data.Length} bytes");
                    return ApiResponse<byte[]>.CreateSuccess(data);
                }
                else
                {
                    Plugin.Log.LogError($"Payload download failed: {request.error}");
                    return ApiResponse<byte[]>.CreateFailure($"Download failed: {request.error}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Download payload error: {ex.Message}");
                return ApiResponse<byte[]>.CreateFailure($"Download failed: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> DeleteClimbAsync(Guid climbId)
        {
            try
            {
                using var request = CreateDeleteRequest($"{BASE_URL}/climbs/{climbId}");
                var response = await ExecuteRequestAsync<object>(request);
                return ApiResponse<bool>.CreateSuccess(response.IsSuccess);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Delete climb error: {ex.Message}");
                return ApiResponse<bool>.CreateFailure($"Failed to delete climb: {ex.Message}");
            }
        }

        #endregion

        #region Statistics

        public async Task<ApiResponse<ServerStats>> GetServerStatsAsync()
        {
            try
            {
                using var request = CreateGetRequest($"{BASE_URL}/metrics-lite");
                var response = await ExecuteRequestAsync<ServerStatsResponse>(request);

                if (response.IsSuccess && response.Data != null)
                {
                    var stats = new ServerStats
                    {
                        TotalClimbs = response.Data.Climbs,
                        TotalPayloads = response.Data.Payloads,
                        TotalApiKeys = response.Data.Keys,
                        LastUpdated = response.Data.Timestamp
                    };
                    return ApiResponse<ServerStats>.CreateSuccess(stats);
                }

                return ApiResponse<ServerStats>.CreateFailure(response.ErrorMessage);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Get server stats error: {ex.Message}");
                return ApiResponse<ServerStats>.CreateFailure($"Failed to get stats: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private UnityWebRequest CreateGetRequest(string url)
        {
            var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("X-Api-Key", _apiKey);
            request.SetRequestHeader("User-Agent", $"FollowTheWay/{Plugin.ModVersion}");
            request.timeout = TIMEOUT_SECONDS;
            return request;
        }

        private UnityWebRequest CreatePostRequest(string url, object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Api-Key", _apiKey);
            request.SetRequestHeader("User-Agent", $"FollowTheWay/{Plugin.ModVersion}");
            request.timeout = TIMEOUT_SECONDS;
            return request;
        }

        private UnityWebRequest CreateDeleteRequest(string url)
        {
            var request = UnityWebRequest.Delete(url);
            request.SetRequestHeader("X-Api-Key", _apiKey);
            request.SetRequestHeader("User-Agent", $"FollowTheWay/{Plugin.ModVersion}");
            request.timeout = TIMEOUT_SECONDS;
            return request;
        }

        private async Task<ApiResponse<T>> ExecuteRequestAsync<T>(UnityWebRequest request)
        {
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var responseText = request.downloadHandler.text;

                        if (string.IsNullOrEmpty(responseText))
                        {
                            return ApiResponse<T>.CreateSuccess(default);
                        }

                        var data = JsonConvert.DeserializeObject<T>(responseText);
                        return ApiResponse<T>.CreateSuccess(data);
                    }
                    else
                    {
                        var errorMessage = $"Request failed: {request.error} (Status: {request.responseCode})";

                        if (attempt < MAX_RETRIES && IsRetryableError(request.responseCode))
                        {
                            Plugin.Log.LogWarning($"Attempt {attempt} failed, retrying... {errorMessage}");
                            await Task.Delay(1000 * attempt); // Exponential backoff
                            continue;
                        }

                        Plugin.Log.LogError(errorMessage);
                        return ApiResponse<T>.CreateFailure(errorMessage);
                    }
                }
                catch (Exception ex)
                {
                    if (attempt < MAX_RETRIES)
                    {
                        Plugin.Log.LogWarning($"Attempt {attempt} failed with exception, retrying... {ex.Message}");
                        await Task.Delay(1000 * attempt);
                        continue;
                    }

                    Plugin.Log.LogError($"Request failed after {MAX_RETRIES} attempts: {ex.Message}");
                    return ApiResponse<T>.CreateFailure($"Request failed: {ex.Message}");
                }
            }

            return ApiResponse<T>.CreateFailure("Max retries exceeded");
        }

        private bool IsRetryableError(long responseCode)
        {
            return responseCode >= 500 || responseCode == 429; // Server errors or rate limiting
        }

        private ClimbData ConvertToClimbData(ClimbListItem item)
        {
            return new ClimbData
            {
                Id = item.Id,
                Title = item.Title,
                Author = item.Author,
                PlayerName = item.PlayerName,
                ModVersion = item.ModVersion,
                Map = item.Map,
                BiomeName = item.BiomeName,
                Difficulty = item.Difficulty,
                AscentLevel = item.AscentLevel,
                Duration = item.Duration.HasValue ? TimeSpan.FromSeconds(item.Duration.Value) : (TimeSpan?)null,
                LengthMeters = item.LengthMeters,
                GameVersion = item.GameVersion,
                ClimbCode = item.ClimbCode,
                StartAltitude = item.StartAltitude,
                EndAltitude = item.EndAltitude,
                PointsCount = item.PointsCount,
                Downloads = item.Downloads,
                Likes = item.Likes,
                CreatedAt = item.CreatedAtUtc,
                LastDownloadedAt = item.LastDownloadedAtUtc,
                IsVerified = item.IsVerified,
                Tags = string.IsNullOrEmpty(item.TagsJson) ? new List<string>() :
                       JsonConvert.DeserializeObject<List<string>>(item.TagsJson) ?? new List<string>()
            };
        }

        private string GenerateClimbCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new System.Random();
            var code = new char[6];

            for (int i = 0; i < code.Length; i++)
            {
                code[i] = chars[random.Next(chars.Length)];
            }

            return new string(code);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                Plugin.Log.LogInfo("VPSApiService disposed");
                _disposed = true;
            }
        }

        #endregion

        #region Response Models

        private class HealthResponse
        {
            [JsonProperty("ok")]
            public bool ok { get; set; }

            [JsonProperty("ts")]
            public DateTime ts { get; set; }
        }

        private class ClimbListResponse
        {
            [JsonProperty("total")]
            public int Total { get; set; }

            [JsonProperty("items")]
            public List<ClimbListItem> Items { get; set; }
        }

        private class ClimbListItem
        {
            [JsonProperty("id")]
            public Guid Id { get; set; }

            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("author")]
            public string Author { get; set; }

            [JsonProperty("playerName")]
            public string PlayerName { get; set; }

            [JsonProperty("modVersion")]
            public string ModVersion { get; set; }

            [JsonProperty("map")]
            public string Map { get; set; }

            [JsonProperty("biomeName")]
            public string BiomeName { get; set; }

            [JsonProperty("difficulty")]
            public string Difficulty { get; set; }

            [JsonProperty("ascentLevel")]
            public int AscentLevel { get; set; }

            [JsonProperty("duration")]
            public double? Duration { get; set; }

            [JsonProperty("lengthMeters")]
            public double? LengthMeters { get; set; }

            [JsonProperty("gameVersion")]
            public string GameVersion { get; set; }

            [JsonProperty("climbCode")]
            public string ClimbCode { get; set; }

            [JsonProperty("startAltitude")]
            public float? StartAltitude { get; set; }

            [JsonProperty("endAltitude")]
            public float? EndAltitude { get; set; }

            [JsonProperty("pointsCount")]
            public int? PointsCount { get; set; }

            [JsonProperty("downloads")]
            public long Downloads { get; set; }

            [JsonProperty("likes")]
            public long Likes { get; set; }

            [JsonProperty("createdAtUtc")]
            public DateTime CreatedAtUtc { get; set; }

            [JsonProperty("lastDownloadedAtUtc")]
            public DateTime? LastDownloadedAtUtc { get; set; }

            [JsonProperty("isVerified")]
            public bool IsVerified { get; set; }

            [JsonProperty("tagsJson")]
            public string TagsJson { get; set; }
        }

        private class ServerStatsResponse
        {
            [JsonProperty("climbs")]
            public int Climbs { get; set; }

            [JsonProperty("payloads")]
            public int Payloads { get; set; }

            [JsonProperty("keys")]
            public int Keys { get; set; }

            [JsonProperty("ts")]
            public DateTime Timestamp { get; set; }
        }

        #endregion
    }
}