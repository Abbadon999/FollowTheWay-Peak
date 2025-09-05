using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using FollowTheWay.Utils;

namespace FollowTheWay.Services
{
    public class AssetBundleService
    {
        private readonly ModLogger _logger;
        private readonly Dictionary<string, AssetBundle> _loadedBundles;
        private readonly string _bundlePath;

        public AssetBundleService()
        {
            _logger = new ModLogger("AssetBundleService");
            _loadedBundles = new Dictionary<string, AssetBundle>();
            _bundlePath = Path.Combine(UnityEngine.Application.persistentDataPath, "FollowTheWay", "AssetBundles");

            // Ensure bundle directory exists
            if (!Directory.Exists(_bundlePath))
            {
                Directory.CreateDirectory(_bundlePath);
                _logger.LogInfo($"Created asset bundle directory: {_bundlePath}");
            }
        }

        public AssetBundle LoadBundle(string bundleName)
        {
            if (string.IsNullOrEmpty(bundleName))
            {
                _logger.LogWarning("Attempted to load bundle with null or empty name");
                return null;
            }

            try
            {
                // Check if already loaded
                if (_loadedBundles.ContainsKey(bundleName))
                {
                    _logger.LogDebug($"Bundle already loaded: {bundleName}");
                    return _loadedBundles[bundleName];
                }

                string bundleFilePath = Path.Combine(_bundlePath, bundleName);

                if (!File.Exists(bundleFilePath))
                {
                    _logger.LogWarning($"Bundle file not found: {bundleFilePath}");
                    return null;
                }

                _logger.LogInfo($"Loading asset bundle: {bundleName}");

                AssetBundle bundle = AssetBundle.LoadFromFile(bundleFilePath);

                if (bundle != null)
                {
                    _loadedBundles[bundleName] = bundle;
                    _logger.LogInfo($"Successfully loaded bundle: {bundleName}");
                }
                else
                {
                    _logger.LogError($"Failed to load bundle: {bundleName}");
                }

                return bundle;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception loading bundle {bundleName}: {ex.Message}");
                return null;
            }
        }

        public T LoadAsset<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(bundleName) || string.IsNullOrEmpty(assetName))
            {
                _logger.LogWarning("Attempted to load asset with null or empty bundle/asset name");
                return null;
            }

            try
            {
                AssetBundle bundle = LoadBundle(bundleName);

                if (bundle == null)
                {
                    _logger.LogError($"Cannot load asset {assetName} - bundle {bundleName} not loaded");
                    return null;
                }

                _logger.LogDebug($"Loading asset {assetName} from bundle {bundleName}");

                T asset = bundle.LoadAsset<T>(assetName);

                if (asset != null)
                {
                    _logger.LogDebug($"Successfully loaded asset: {assetName}");
                }
                else
                {
                    _logger.LogWarning($"Asset not found: {assetName} in bundle {bundleName}");
                }

                return asset;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception loading asset {assetName} from bundle {bundleName}: {ex.Message}");
                return null;
            }
        }

        public void UnloadBundle(string bundleName, bool unloadAllLoadedObjects = false)
        {
            if (string.IsNullOrEmpty(bundleName))
            {
                _logger.LogWarning("Attempted to unload bundle with null or empty name");
                return;
            }

            try
            {
                if (_loadedBundles.ContainsKey(bundleName))
                {
                    _logger.LogInfo($"Unloading bundle: {bundleName}");

                    AssetBundle bundle = _loadedBundles[bundleName];
                    bundle.Unload(unloadAllLoadedObjects);

                    _loadedBundles.Remove(bundleName);

                    _logger.LogInfo($"Successfully unloaded bundle: {bundleName}");
                }
                else
                {
                    _logger.LogWarning($"Bundle not loaded, cannot unload: {bundleName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception unloading bundle {bundleName}: {ex.Message}");
            }
        }

        public void UnloadAllBundles(bool unloadAllLoadedObjects = false)
        {
            try
            {
                _logger.LogInfo("Unloading all asset bundles");

                var bundleNames = new List<string>(_loadedBundles.Keys);

                foreach (string bundleName in bundleNames)
                {
                    UnloadBundle(bundleName, unloadAllLoadedObjects);
                }

                _logger.LogInfo("All asset bundles unloaded");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception unloading all bundles: {ex.Message}");
            }
        }

        public bool IsBundleLoaded(string bundleName)
        {
            return !string.IsNullOrEmpty(bundleName) && _loadedBundles.ContainsKey(bundleName);
        }

        public string[] GetLoadedBundleNames()
        {
            var names = new string[_loadedBundles.Count];
            _loadedBundles.Keys.CopyTo(names, 0);
            return names;
        }

        public int GetLoadedBundleCount()
        {
            return _loadedBundles.Count;
        }

        public void Dispose()
        {
            _logger.LogInfo("Disposing AssetBundleService");
            UnloadAllBundles(true);
        }
    }
}