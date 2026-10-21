using System;
using System.Collections.Generic;

namespace Souqna.Core.Entities
{
    public class Store
    {
        public int Id { get; set; }
        public string SellerId { get; set; } = string.Empty;
        public virtual ApplicationUser? Seller { get; set; }

        public string StoreName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? CoverUrl { get; set; }

        public string City { get; set; } = "القاهرة";
        public string Address { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;

        public bool IsVerified { get; set; } = false;
        public string? VerificationBadge { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual StoreAddress? StoreAddress { get; set; }
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
        public virtual ICollection<Video> Videos { get; set; } = new List<Video>();
        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public virtual ICollection<IdentityVerification> VerificationRequests { get; set; } = new List<IdentityVerification>();
        public virtual ICollection<StoreOrder> StoreOrders { get; set; } = new List<StoreOrder>();
        public virtual ICollection<Coupon> Coupons { get; set; } = new List<Coupon>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }

    public class StoreAddress
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public virtual Store? Store { get; set; }

        public string Governorate { get; set; } = "القاهرة";
        public string City { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string? Building { get; set; }
        public string? PostalCode { get; set; }
    }
}
