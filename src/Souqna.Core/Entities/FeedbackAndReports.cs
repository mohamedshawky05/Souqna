using System;
using Souqna.Core.Enums;

namespace Souqna.Core.Entities
{
    public class ProductFavorite
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser? User { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Review
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int StoreId { get; set; }
        public virtual Store? Store { get; set; }

        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser? User { get; set; }

        public int Rating { get; set; } = 5; // 1 to 5
        public string Comment { get; set; } = string.Empty;
        public bool IsApproved { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Report
    {
        public int Id { get; set; }
        public string ReporterUserId { get; set; } = string.Empty;
        public virtual ApplicationUser? Reporter { get; set; }

        public ReportTargetType TargetType { get; set; }
        public int TargetId { get; set; }

        public string Reason { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReportStatus Status { get; set; } = ReportStatus.Pending;
        public string? AdminNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }

    public class Notification
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser? User { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? LinkUrl { get; set; }
        public string Type { get; set; } = "info"; // order, subscription, verification, promo
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
