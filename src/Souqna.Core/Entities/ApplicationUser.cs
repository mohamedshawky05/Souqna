using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Souqna.Core.Enums;

namespace Souqna.Core.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public UserRole UserType { get; set; } = UserRole.Customer;
        public string? AvatarUrl { get; set; }
        public bool IsSuspended { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Store? Store { get; set; }
        public virtual ICollection<DeliveryAddress> DeliveryAddresses { get; set; } = new List<DeliveryAddress>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<ProductFavorite> Favorites { get; set; } = new List<ProductFavorite>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}

