using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;

namespace Souqna.Infrastructure.Services
{
    public class IdentityVerificationService : IIdentityVerificationService
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly IStorageService _storageService;
        private readonly ILogger<IdentityVerificationService> _logger;
        private readonly INotificationService _notificationService;

        public IdentityVerificationService(
            SouqnaDbContext dbContext,
            IStorageService storageService,
            ILogger<IdentityVerificationService> logger,
            INotificationService notificationService)
        {
            _dbContext = dbContext;
            _storageService = storageService;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<IdentityVerification> SubmitVerificationAsync(int storeId, IdentityVerificationSubmissionDto dto)
        {
            var store = await _dbContext.Stores.FindAsync(storeId)
                ?? throw new InvalidOperationException("المتجر غير موجود");

            var verification = new IdentityVerification
            {
                StoreId = storeId,
                NationalIdNumber = dto.NationalIdNumber,
                FullLegalName = dto.FullLegalName,
                DateOfBirth = dto.DateOfBirth,
                PrivacyNoticeAcknowledged = dto.PrivacyNoticeAcknowledged,
                Status = VerificationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            // Save Front ID to private storage
            if (dto.FrontIdStream != null)
            {
                string frontKey = await _storageService.SavePrivateDocumentAsync(dto.FrontIdStream, dto.FrontIdFileName);
                verification.Documents.Add(new VerificationDocument
                {
                    DocumentType = DocumentType.FrontNationalId,
                    StorageKey = frontKey,
                    OriginalFileName = dto.FrontIdFileName,
                    ContentType = dto.FrontIdContentType,
                    FileSizeBytes = dto.FrontIdStream.Length,
                    UploadedAt = DateTime.UtcNow
                });
            }

            // Save Back ID to private storage
            if (dto.BackIdStream != null)
            {
                string backKey = await _storageService.SavePrivateDocumentAsync(dto.BackIdStream, dto.BackIdFileName);
                verification.Documents.Add(new VerificationDocument
                {
                    DocumentType = DocumentType.BackNationalId,
                    StorageKey = backKey,
                    OriginalFileName = dto.BackIdFileName,
                    ContentType = dto.BackIdContentType,
                    FileSizeBytes = dto.BackIdStream.Length,
                    UploadedAt = DateTime.UtcNow
                });
            }

            _dbContext.IdentityVerifications.Add(verification);
            
            // Add audit log
            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = store.SellerId,
                Action = "SUBMIT_IDENTITY_VERIFICATION",
                EntityName = nameof(IdentityVerification),
                Details = $"StoreId={storeId}, LegalName={dto.FullLegalName}",
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
            return verification;
        }

        public async Task<bool> ReviewVerificationAsync(
            int verificationId, 
            string reviewerId, 
            VerificationStatus decision, 
            string? notes)
        {
            var verification = await _dbContext.IdentityVerifications
                .Include(iv => iv.Store)
                .FirstOrDefaultAsync(iv => iv.Id == verificationId);

            if (verification == null) return false;

            verification.Status = decision;
            verification.ReviewerId = reviewerId;
            verification.ReviewerNotes = notes;
            verification.ReviewedAt = DateTime.UtcNow;
            verification.UpdatedAt = DateTime.UtcNow;

            Notification? notification = null;
            if (verification.Store != null)
            {
                if (decision == VerificationStatus.Approved)
                {
                    verification.Store.IsVerified = true;
                    verification.Store.VerificationBadge = "Identity Verified";
                }
                else if (decision == VerificationStatus.Rejected)
                {
                    verification.Store.IsVerified = false;
                    verification.Store.VerificationBadge = null;
                }

                // Add notification to seller
                string title = decision == VerificationStatus.Approved ? "تم توثيق هويتك بنجاح" : "تحديث بخصوص طلب توثيق الهوية";
                string message = decision == VerificationStatus.Approved 
                    ? "تهانينا! تمت مراجعة مستنداتك وتوثيق متجرك رسمياً في منصة سوقنا."
                    : $"تمت مراجعة طلبك وكانت النتيجة: {decision}. ملاحظات الإدارة: {notes ?? "لا توجد ملاحظات"}";

                notification = new Notification
                {
                    UserId = verification.Store.SellerId,
                    Title = title,
                    Message = message,
                    LinkUrl = "/Seller/Verification",
                    Type = "verification",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Notifications.Add(notification);
            }

            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = reviewerId,
                Action = $"REVIEW_VERIFICATION_{decision}",
                EntityName = nameof(IdentityVerification),
                EntityId = verificationId.ToString(),
                Details = $"Decision={decision}, Notes={notes}",
                Timestamp = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
            if (notification != null)
                await _notificationService.PublishAsync(notification);

            return true;
        }
    }
}
