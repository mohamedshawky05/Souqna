using System;
using System.Collections.Generic;
using Souqna.Core.Enums;

namespace Souqna.Core.Entities
{
    public class IdentityVerification
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public virtual Store? Store { get; set; }

        public string NationalIdNumber { get; set; } = string.Empty;
        public string FullLegalName { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public bool PrivacyNoticeAcknowledged { get; set; } = true;

        public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
        public string? ReviewerNotes { get; set; }
        public string? ReviewerId { get; set; }
        public DateTime? ReviewedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<VerificationDocument> Documents { get; set; } = new List<VerificationDocument>();
    }

    public class VerificationDocument
    {
        public int Id { get; set; }
        public int IdentityVerificationId { get; set; }
        public virtual IdentityVerification? IdentityVerification { get; set; }

        public DocumentType DocumentType { get; set; } = DocumentType.FrontNationalId;
        // StorageKey is the private disk path or GUID filename in App_Data/IdentityDocs/ - NEVER public URL
        public string StorageKey { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "image/jpeg";
        public long FileSizeBytes { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }

    public class PhoneVerification
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string CodeHash { get; set; } = string.Empty;
        public DateTime ExpirationUtc { get; set; }
        public int AttemptsCount { get; set; } = 0;
        public bool IsVerified { get; set; } = false;
        public DateTime LastSentUtc { get; set; } = DateTime.UtcNow;
        public DateTime? VerifiedAt { get; set; }
    }
}
