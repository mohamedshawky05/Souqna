using System.IO;
using System.Threading.Tasks;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Core.Enums;

namespace Souqna.Core.Interfaces
{
    public interface ISmsService
    {
        Task<bool> SendOtpAsync(string phoneNumber, string otpCode);
    }

    public interface IStorageService
    {
        Task<string> SavePublicFileAsync(Stream stream, string originalFileName, string folder);
        Task<string> SavePrivateDocumentAsync(Stream stream, string originalFileName);
        Task<(Stream Stream, string ContentType)?> GetPrivateDocumentAsync(string storageKey);
        Task<bool> DeleteFileAsync(string relativePathOrKey);
    }

    public interface IPaymentService
    {
        Task<SubscriptionPaymentResult> ProcessSubscriptionPaymentAsync(int subscriptionId, decimal amount, string currency, PaymentMethod paymentMethod);
        Task<bool> VerifyWebhookSignatureAsync(string payload, string signature);
    }

    public interface IVideoProcessingService
    {
        Task<VideoValidationResult> ValidateAndProcessVideoAsync(Stream stream, string originalFileName, int maxAllowedSeconds);
    }

    public interface ISubscriptionService
    {
        Task<SubscriptionPlanCheckResult> CanAddProductAsync(int storeId);
        Task<SubscriptionPlanCheckResult> CanUploadVideoAsync(int storeId, int requestedDurationSeconds);
        Task<SubscriptionUsageDto> GetStoreUsageAsync(int storeId);
        Task<Subscription> SubscribeToPlanAsync(int storeId, int planId, BillingInterval interval);
        Task<bool> CancelSubscriptionAsync(int storeId);
        Task<Subscription> GetActiveSubscriptionAsync(int storeId);
    }

    public interface INotificationService
    {
        Task<IReadOnlyList<NotificationDto>> GetRecentAsync(string userId, int count = 10);
        Task<int> GetUnreadCountAsync(string userId);
        Task<bool> MarkAsReadAsync(int notificationId, string userId);
        Task<int> MarkAllAsReadAsync(string userId);
        Task<Notification> CreateNotificationAsync(
            string userId,
            string title,
            string message,
            string type,
            string? actionUrl = null);
        Task PublishAsync(Notification notification);
    }

    public interface IOrderService
    {
        Task<Order> CreateOrderFromCartAsync(string? userId, string? anonymousId, CheckoutDto checkoutDto);
        Task<bool> UpdateStoreOrderStatusAsync(int storeOrderId, int storeId, OrderStatus newStatus, string? trackingNumber);
    }

    public interface IPhoneVerificationService
    {
        Task<bool> RequestOtpAsync(string phoneNumber);
        Task<bool> VerifyOtpAsync(string phoneNumber, string code);
    }

    public interface IIdentityVerificationService
    {
        Task<IdentityVerification> SubmitVerificationAsync(int storeId, IdentityVerificationSubmissionDto dto);
        Task<bool> ReviewVerificationAsync(int verificationId, string reviewerId, VerificationStatus decision, string? notes);
    }
}
