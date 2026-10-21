using System;
using System.Collections.Generic;
using Souqna.Core.Enums;

namespace Souqna.Core.Entities
{
    public class Cart
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }

        public string? AnonymousId { get; set; } // Session / Cookie key for guests
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }

    public class CartItem
    {
        public int Id { get; set; }
        public int CartId { get; set; }
        public virtual Cart? Cart { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public int? VariantId { get; set; }
    }

    public class DeliveryAddress
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser? User { get; set; }

        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string City { get; set; } = "القاهرة";
        public string Area { get; set; } = string.Empty;
        public string StreetAddress { get; set; } = string.Empty;
        public string? BuildingNumber { get; set; }
        public string? FloorNumber { get; set; }
        public string? ApartmentNumber { get; set; }
        public bool IsDefault { get; set; } = false;
    }

    public class Order
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser? User { get; set; }

        public decimal Subtotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal DiscountAmount { get; set; } = 0m;
        public decimal TotalAmount { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public string DeliveryAddressJson { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Multi-vendor split suborders
        public virtual ICollection<StoreOrder> StoreOrders { get; set; } = new List<StoreOrder>();
    }

    public class StoreOrder
    {
        public int Id { get; set; }
        public int ParentOrderId { get; set; }
        public virtual Order? ParentOrder { get; set; }

        public int StoreId { get; set; }
        public virtual Store? Store { get; set; }

        public decimal Subtotal { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal TotalAmount { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public string? TrackingNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public int StoreOrderId { get; set; }
        public virtual StoreOrder? StoreOrder { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public string? VariantDetails { get; set; }
    }

    public class Coupon
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public virtual Store? Store { get; set; }

        public string Code { get; set; } = string.Empty;
        public decimal DiscountPercentage { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public decimal? MinOrderAmount { get; set; }
        public DateTime ExpirationDate { get; set; }
        public int UsageLimit { get; set; } = 100;
        public int TimesUsed { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }
}
