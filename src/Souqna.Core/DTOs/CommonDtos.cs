using System;
using System.Collections.Generic;
using Souqna.Core.Enums;

namespace Souqna.Core.DTOs
{
    public class SubscriptionPlanCheckResult
    {
        public bool IsAllowed { get; set; }
        public string? Reason { get; set; }
        public int CurrentCount { get; set; }
        public int MaxAllowed { get; set; }
    }

    public class SubscriptionUsageDto
    {
        public string PlanNameAr { get; set; } = string.Empty;
        public string PlanNameEn { get; set; } = string.Empty;
        public SubscriptionStatus Status { get; set; }
        public DateTime EndDate { get; set; }
        public int ActiveProductsCount { get; set; }
        public int MaxProducts { get; set; }
        public int PublishedVideosCount { get; set; }
        public int MaxVideos { get; set; }
        public int MaxVideoDurationSeconds { get; set; }
        public bool AllowsCoupons { get; set; }
        public bool AllowsCsvExportImport { get; set; }
        public bool HasAdvancedAnalytics { get; set; }
    }

    public class SubscriptionPaymentResult
    {
        public bool Success { get; set; }
        public string TransactionReference { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class NotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
        public string Type { get; set; } = "info";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class VideoValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public int DurationSeconds { get; set; }
        public long FileSizeBytes { get; set; }
        public string? RelativeVideoUrl { get; set; }
        public string? RelativeThumbnailUrl { get; set; }
    }

    public class IdentityVerificationSubmissionDto
    {
        public string NationalIdNumber { get; set; } = string.Empty;
        public string FullLegalName { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public bool PrivacyNoticeAcknowledged { get; set; }
        public Stream? FrontIdStream { get; set; }
        public string FrontIdFileName { get; set; } = string.Empty;
        public string FrontIdContentType { get; set; } = "image/jpeg";
        public Stream? BackIdStream { get; set; }
        public string BackIdFileName { get; set; } = string.Empty;
        public string BackIdContentType { get; set; } = "image/jpeg";
    }

    public class CheckoutDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string City { get; set; } = "القاهرة";
        public string Area { get; set; } = string.Empty;
        public string StreetAddress { get; set; } = string.Empty;
        public string? BuildingNumber { get; set; }
        public string? FloorNumber { get; set; }
        public string? ApartmentNumber { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;
        public string? Notes { get; set; }
    }

    public class CartItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;
        public int StockQuantity { get; set; }
    }

    public class CartDto
    {
        public int Id { get; set; }
        public List<CartItemDto> Items { get; set; } = new List<CartItemDto>();
        public decimal Subtotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal Total { get; set; }
        public int TotalItems { get; set; }
    }
}
