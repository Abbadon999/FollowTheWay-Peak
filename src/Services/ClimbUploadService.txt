using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FollowTheWay.Models;
using FollowTheWay.Utils;

namespace FollowTheWay.Services
{
    public class ClimbUploadService
    {
        private readonly VPSApiService _apiService;
        private readonly ModLogger _logger;
        private readonly Queue<UploadQueueItem> _uploadQueue;
        private bool _isUploading;

        public ClimbUploadService(VPSApiService apiService)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = new ModLogger("ClimbUploadService");
            _uploadQueue = new Queue<UploadQueueItem>();
            _isUploading = false;
        }

        public void QueueUpload(ClimbData climbData)
        {
            if (climbData == null)
            {
                _logger.LogWarning("Attempted to queue null climb data for upload");
                return;
            }

            var queueItem = new UploadQueueItem
            {
                ClimbData = climbData,
                QueuedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            _uploadQueue.Enqueue(queueItem);
            _logger.LogInfo($"Queued climb for upload: {climbData.ClimbName}");

            if (!_isUploading)
            {
                ProcessUploadQueue();
            }
        }

        private async void ProcessUploadQueue()
        {
            if (_isUploading || _uploadQueue.Count == 0)
                return;

            _isUploading = true;

            try
            {
                while (_uploadQueue.Count > 0)
                {
                    var item = _uploadQueue.Dequeue();
                    await UploadClimbData(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing upload queue: {ex.Message}");
            }
            finally
            {
                _isUploading = false;
            }
        }

        private async Task UploadClimbData(UploadQueueItem item)
        {
            try
            {
                _logger.LogInfo($"Uploading climb: {item.ClimbData.ClimbName}");

                var response = await _apiService.UploadClimbAsync(item.ClimbData);

                if (response.Success)
                {
                    _logger.LogInfo($"Successfully uploaded climb: {item.ClimbData.ClimbName}");
                }
                else
                {
                    _logger.LogError($"Failed to upload climb: {item.ClimbData.ClimbName} - {response.ErrorMessage}");

                    // Retry logic
                    if (item.RetryCount < 3)
                    {
                        item.RetryCount++;
                        _uploadQueue.Enqueue(item);
                        _logger.LogInfo($"Retrying upload for climb: {item.ClimbData.ClimbName} (Attempt {item.RetryCount})");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception during climb upload: {ex.Message}");

                if (item.RetryCount < 3)
                {
                    item.RetryCount++;
                    _uploadQueue.Enqueue(item);
                }
            }
        }

        public int GetQueueCount()
        {
            return _uploadQueue.Count;
        }

        public bool IsUploading()
        {
            return _isUploading;
        }
    }
}