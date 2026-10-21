using System;
using System.Collections.Generic;
using Souqna.Core.Enums;

namespace Souqna.Core.Entities
{
    public class Video
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public virtual Store? Store { get; set; }

        public int? ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string VideoUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }

        public int DurationSeconds { get; set; }
        public long FileSizeBytes { get; set; }
        public VideoStatus Status { get; set; } = VideoStatus.Published;

        public int ViewsCount { get; set; } = 0;
        public int LikesCount { get; set; } = 0;
        public bool IsSponsored { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<VideoLike> Likes { get; set; } = new List<VideoLike>();
        public virtual ICollection<VideoView> Views { get; set; } = new List<VideoView>();
    }

    public class VideoLike
    {
        public int Id { get; set; }
        public int VideoId { get; set; }
        public virtual Video? Video { get; set; }

        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class VideoView
    {
        public int Id { get; set; }
        public int VideoId { get; set; }
        public virtual Video? Video { get; set; }

        public string? ViewerKey { get; set; } // IP hash or User ID to avoid duplicate rapid view spam
        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
    }
}
