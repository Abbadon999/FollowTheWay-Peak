using System;

namespace FollowTheWay.Models
{
    public class UploadQueueItem
    {
        public ClimbData ClimbData { get; set; }
        public DateTime QueuedAt { get; set; }
        public int RetryCount { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime? LastAttempt { get; set; }

        public UploadQueueItem()
        {
            QueuedAt = DateTime.UtcNow;
            RetryCount = 0;
        }
    }
}