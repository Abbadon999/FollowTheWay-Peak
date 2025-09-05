using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using FollowTheWay.Models;
using FollowTheWay.Utils;

namespace FollowTheWay.Services
{
    /// <summary>
    /// Service for managing local climb data storage and operations
    /// </summary>
    public class ClimbDataService : IDisposable
    {
        private const string CLIMBS_FOLDER = "Climbs";
        private const string CACHE_FOLDER = "Cache";
        private const string METADATA_FILE = "metadata.json";
        private const string PAYLOAD_FILE = "payload.dat";

        private readonly string _dataDirectory;
        private readonly string _climbsDirectory;
        private readonly string _cacheDirectory;
        private readonly Dictionary<Guid, ClimbData> _climbCache;
        private readonly object _lockObject = new object();

        private bool _disposed = false;

        public ClimbDataService()
        {
            _dataDirectory = Path.Combine(Application.persistentDataPath, "FollowTheWay");
            _climbsDirectory = Path.Combine(_dataDirectory, CLIMBS_FOLDER);
            _cacheDirectory = Path.Combine(_dataDirectory, CACHE_FOLDER);
            _climbCache = new Dictionary<Guid, ClimbData>();

            InitializeDirectories();
            LoadClimbCache();

            Plugin.Log.LogInfo($"ClimbDataService initialized. Data directory: {_dataDirectory}");
        }

        #region Initialization

        private void InitializeDirectories()
        {
            try
            {
                Directory.CreateDirectory(_dataDirectory);
                Directory.CreateDirectory(_climbsDirectory);
                Directory.CreateDirectory(_cacheDirectory);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to initialize directories: {ex.Message}");
                throw;
            }
        }

        private void LoadClimbCache()
        {
            try
            {
                var climbDirectories = Directory.GetDirectories(_climbsDirectory);

                foreach (var climbDir in climbDirectories)
                {
                    try
                    {
                        var metadataFile = Path.Combine(climbDir, METADATA_FILE);
                        if (File.Exists(metadataFile))
                        {
                            var json = File.ReadAllText(metadataFile);
                            var climbData = JsonConvert.DeserializeObject<ClimbData>(json, CommonJsonSettings.Default);

                            if (climbData != null)
                            {
                                _climbCache[climbData.Id] = climbData;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning($"Failed to load climb from {climbDir}: {ex.Message}");
                    }
                }

                Plugin.Log.LogInfo($"Loaded {_climbCache.Count} climbs from cache");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to load climb cache: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Save climb data locally
        /// </summary>
        public async Task<bool> SaveClimbAsync(ClimbData climbData)
        {
            if (climbData == null)
            {
                Plugin.Log.LogError("Cannot save null climb data");
                return false;
            }

            try
            {
                lock (_lockObject)
                {
                    var climbDirectory = Path.Combine(_climbsDirectory, climbData.Id.ToString());
                    Directory.CreateDirectory(climbDirectory);

                    // Save metadata
                    var metadataFile = Path.Combine(climbDirectory, METADATA_FILE);
                    var metadataJson = JsonConvert.SerializeObject(climbData, CommonJsonSettings.Pretty);
                    File.WriteAllText(metadataFile, metadataJson);

                    // Save compressed payload if points exist
                    if (climbData.Points != null && climbData.Points.Any())
                    {
                        var payloadFile = Path.Combine(climbDirectory, PAYLOAD_FILE);
                        var compressedData = ClimbDataCrusher.CompressClimbData(climbData);
                        File.WriteAllBytes(payloadFile, compressedData);
                    }

                    // Update cache
                    _climbCache[climbData.Id] = climbData;
                }

                Plugin.Log.LogInfo($"Saved climb locally: {climbData.Title} ({climbData.Id})");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to save climb {climbData.Id}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load climb data by ID
        /// </summary>
        public async Task<ClimbData> LoadClimbAsync(Guid climbId)
        {
            try
            {
                lock (_lockObject)
                {
                    // Check cache first
                    if (_climbCache.TryGetValue(climbId, out var cachedClimb))
                    {
                        // If points are not loaded, try to load them from payload
                        if (cachedClimb.Points == null || !cachedClimb.Points.Any())
                        {
                            var climbWithPoints = LoadClimbWithPayload(climbId);
                            if (climbWithPoints != null)
                            {
                                _climbCache[climbId] = climbWithPoints;
                                return climbWithPoints;
                            }
                        }
                        return cachedClimb;
                    }

                    // Load from disk
                    return LoadClimbFromDisk(climbId);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to load climb {climbId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get all locally stored climbs (metadata only)
        /// </summary>
        public List<ClimbData> GetAllClimbs()
        {
            lock (_lockObject)
            {
                return _climbCache.Values.ToList();
            }
        }

        /// <summary>
        /// Delete climb data locally
        /// </summary>
        public async Task<bool> DeleteClimbAsync(Guid climbId)
        {
            try
            {
                lock (_lockObject)
                {
                    var climbDirectory = Path.Combine(_climbsDirectory, climbId.ToString());

                    if (Directory.Exists(climbDirectory))
                    {
                        Directory.Delete(climbDirectory, true);
                    }

                    _climbCache.Remove(climbId);
                }

                Plugin.Log.LogInfo($"Deleted climb locally: {climbId}");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to delete climb {climbId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if climb exists locally
        /// </summary>
        public bool HasClimb(Guid climbId)
        {
            lock (_lockObject)
            {
                return _climbCache.ContainsKey(climbId);
            }
        }

        /// <summary>
        /// Get climb count
        /// </summary>
        public int GetClimbCount()
        {
            lock (_lockObject)
            {
                return _climbCache.Count;
            }
        }

        /// <summary>
        /// Search climbs by title or author
        /// </summary>
        public List<ClimbData> SearchClimbs(string query)
        {
            if (string.IsNullOrEmpty(query))
                return GetAllClimbs();

            lock (_lockObject)
            {
                var lowerQuery = query.ToLower();
                return _climbCache.Values
                    .Where(c => (c.Title?.ToLower().Contains(lowerQuery) == true) ||
                               (c.Author?.ToLower().Contains(lowerQuery) == true) ||
                               (c.PlayerName?.ToLower().Contains(lowerQuery) == true))
                    .ToList();
            }
        }

        /// <summary>
        /// Get climbs by difficulty
        /// </summary>
        public List<ClimbData> GetClimbsByDifficulty(string difficulty)
        {
            lock (_lockObject)
            {
                return _climbCache.Values
                    .Where(c => string.Equals(c.Difficulty, difficulty, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        /// <summary>
        /// Get recent climbs
        /// </summary>
        public List<ClimbData> GetRecentClimbs(int count = 10)
        {
            lock (_lockObject)
            {
                return _climbCache.Values
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// Clear all local climb data
        /// </summary>
        public async Task<bool> ClearAllClimbsAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    if (Directory.Exists(_climbsDirectory))
                    {
                        Directory.Delete(_climbsDirectory, true);
                        Directory.CreateDirectory(_climbsDirectory);
                    }

                    _climbCache.Clear();
                }

                Plugin.Log.LogInfo("Cleared all local climb data");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to clear climb data: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Private Methods

        private ClimbData LoadClimbFromDisk(Guid climbId)
        {
            var climbDirectory = Path.Combine(_climbsDirectory, climbId.ToString());
            var metadataFile = Path.Combine(climbDirectory, METADATA_FILE);

            if (!File.Exists(metadataFile))
                return null;

            try
            {
                var json = File.ReadAllText(metadataFile);
                var climbData = JsonConvert.DeserializeObject<ClimbData>(json, CommonJsonSettings.Default);

                if (climbData != null)
                {
                    _climbCache[climbId] = climbData;
                }

                return climbData;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to load climb metadata from disk {climbId}: {ex.Message}");
                return null;
            }
        }

        private ClimbData LoadClimbWithPayload(Guid climbId)
        {
            var climbDirectory = Path.Combine(_climbsDirectory, climbId.ToString());
            var metadataFile = Path.Combine(climbDirectory, METADATA_FILE);
            var payloadFile = Path.Combine(climbDirectory, PAYLOAD_FILE);

            if (!File.Exists(metadataFile))
                return null;

            try
            {
                // Load metadata
                var json = File.ReadAllText(metadataFile);
                var climbData = JsonConvert.DeserializeObject<ClimbData>(json, CommonJsonSettings.Default);

                if (climbData == null)
                    return null;

                // Load payload if exists
                if (File.Exists(payloadFile))
                {
                    var compressedData = File.ReadAllBytes(payloadFile);
                    var decompressedClimb = ClimbDataCrusher.DecompressClimbData(compressedData);

                    if (decompressedClimb?.Points != null)
                    {
                        climbData.Points = decompressedClimb.Points;
                        climbData.PointsCount = decompressedClimb.Points.Count;
                    }
                }

                return climbData;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to load climb with payload {climbId}: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Refresh climb cache from disk
        /// </summary>
        public void RefreshCache()
        {
            lock (_lockObject)
            {
                _climbCache.Clear();
                LoadClimbCache();
            }
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            lock (_lockObject)
            {
                var totalSize = 0L;
                var climbsWithPayload = 0;

                foreach (var climbId in _climbCache.Keys)
                {
                    var climbDirectory = Path.Combine(_climbsDirectory, climbId.ToString());
                    var payloadFile = Path.Combine(climbDirectory, PAYLOAD_FILE);

                    if (File.Exists(payloadFile))
                    {
                        totalSize += new FileInfo(payloadFile).Length;
                        climbsWithPayload++;
                    }
                }

                return new CacheStatistics
                {
                    TotalClimbs = _climbCache.Count,
                    ClimbsWithPayload = climbsWithPayload,
                    TotalSizeBytes = totalSize,
                    CacheDirectory = _climbsDirectory
                };
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lockObject)
                {
                    _climbCache.Clear();
                }

                Plugin.Log.LogInfo("ClimbDataService disposed");
                _disposed = true;
            }
        }

        #endregion
    }

    #region Data Models

    public class CacheStatistics
    {
        public int TotalClimbs { get; set; }
        public int ClimbsWithPayload { get; set; }
        public long TotalSizeBytes { get; set; }
        public string CacheDirectory { get; set; }

        public string TotalSizeFormatted => FormatBytes(TotalSizeBytes);

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        public override string ToString()
        {
            return $"Climbs: {TotalClimbs} ({ClimbsWithPayload} with payload), Size: {TotalSizeFormatted}";
        }
    }

    #endregion
}