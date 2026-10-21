using System;
using System.Collections.Generic;
using Souqna.Core.Enums;

namespace Souqna.Core.Entities
{
    public class SubscriptionPlan
    {
        public int Id { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? DescriptionAr { get; set; }

        public decimal PriceMonthly { get; set; }
        public decimal PriceYearly { get; set; }

        public int MaxActiveProducts { get; set; }
        public int MaxPublishedVideos { get; set; }
        public int MaxVideoDurationSeconds { get; set; }
        public int StorageLimitMb { get; set; } = 500;
        public bool AllowsCoupons { get; set; } = false;
        public bool AllowsCsvExportImport { get; set; } = false;
        public bool HasAdvancedAnalytics { get; set; } = false;

        public string FeaturesJson { get; set; } = "[]";
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; } = 0;

        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }

    public class Subscription
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public virtual Store? Store { get; set; }

        public int SubscriptionPlanId { get; set; }
        public virtual SubscriptionPlan? SubscriptionPlan { get; set; }

        public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime EndDate { get; set; }
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

        public bool AutoRenew { get; set; } = false;
        public decimal PricePaid { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<SubscriptionPayment> Payments { get; set; } = new List<SubscriptionPayment>();
    }

    public class SubscriptionPayment
    {
        public int Id { get; set; }
        public int SubscriptionId { get; set; }
        public virtual Subscription? Subscription { get; set; }

        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EGP";
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.MockCreditCard;
        public string TransactionReference { get; set; } = string.Empty;
        public PaymentStatus Status { get; set; } = PaymentStatus.Completed;

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public string? ProviderPayload { get; set; }
    }
}
