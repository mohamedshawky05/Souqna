namespace Souqna.Core.Enums
{
    public enum UserRole
    {
        Customer,
        Seller,
        Admin
    }

    public enum SubscriptionStatus
    {
        Pending,
        Active,
        Expired,
        Canceled,
        PaymentFailed
    }

    public enum BillingInterval
    {
        Monthly,
        Yearly
    }

    public enum VerificationStatus
    {
        NotSubmitted,
        Pending,
        UnderReview,
        Approved,
        Rejected,
        ResubmissionRequired
    }

    public enum DocumentType
    {
        FrontNationalId,
        BackNationalId,
        CommercialRegister,
        TaxCard
    }

    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Processing,
        Shipped,
        Delivered,
        Canceled,
        ReturnRequested,
        Returned,
        Refunded
    }

    public enum PaymentMethod
    {
        CashOnDelivery,
        MockCreditCard,
        ManualWhatsApp
    }
    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed
    }

    public enum VideoStatus
    {
        Draft,
        Published,
        Rejected,
        Archived
    }

    public enum ReportTargetType
    {
        Product,
        Video,
        Store
    }

    public enum ReportStatus
    {
        Pending,
        Investigating,
        Resolved,
        Dismissed
    }
}
