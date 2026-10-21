using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;

namespace Souqna.Infrastructure.Services
{
    public class OrderService : IOrderService
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly INotificationService _notificationService;

        public OrderService(
            SouqnaDbContext dbContext,
            INotificationService notificationService)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
        }

        public async Task<Order> CreateOrderFromCartAsync(string? userId, string? anonymousId, CheckoutDto checkoutDto)
        {
            var cart = await _dbContext.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Store)
                .FirstOrDefaultAsync(c => 
                    (!string.IsNullOrEmpty(userId) && c.UserId == userId) || 
                    (!string.IsNullOrEmpty(anonymousId) && c.AnonymousId == anonymousId));

            if (cart == null || !cart.Items.Any())
            {
                throw new InvalidOperationException("سلة المشتريات فارغة");
            }

            async Task<(Order Order, List<Notification> Notifications)> ProcessOrderAsync()
            {
                var notifications = new List<Notification>();

                // 1. Stock validation
                foreach (var item in cart.Items)
                {
                    if (item.Product == null || item.Product.IsArchived || !item.Product.IsPublished)
                    {
                        throw new InvalidOperationException($"المنتج \"{item.Product?.Name ?? "غير معروف"}\" لم يعد متاحاً.");
                    }

                    if (item.Product.StockQuantity < item.Quantity)
                    {
                        throw new InvalidOperationException($"الكمية المطلوبة من المنتج \"{item.Product.Name}\" غير متوفرة حالياً (المتاح: {item.Product.StockQuantity}).");
                    }
                }

                // 2. Group items by Store for Multi-Vendor suborders
                var storeGroups = cart.Items.GroupBy(i => i.Product!.StoreId).ToList();

                decimal parentSubtotal = 0m;
                decimal parentDeliveryFee = 0m;
                decimal flatDeliveryPerStore = 35.00m; // EGP delivery per vendor

                var parentOrder = new Order
                {
                    OrderNumber = $"SQN-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}",
                    UserId = userId ?? string.Empty,
                    PaymentMethod = checkoutDto.PaymentMethod,
                    PaymentStatus = PaymentStatus.Pending,
                    Status = OrderStatus.Pending,
                    DeliveryAddressJson = JsonSerializer.Serialize(new
                    {
                        checkoutDto.FullName,
                        checkoutDto.Phone,
                        checkoutDto.City,
                        checkoutDto.Area,
                        checkoutDto.StreetAddress,
                        checkoutDto.BuildingNumber,
                        checkoutDto.FloorNumber,
                        checkoutDto.ApartmentNumber
                    }),
                    Notes = checkoutDto.Notes,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var storeGroup in storeGroups)
                {
                    int storeId = storeGroup.Key;
                    decimal storeSubtotal = 0m;

                    var storeOrder = new StoreOrder
                    {
                        StoreId = storeId,
                        Status = OrderStatus.Pending,
                        DeliveryFee = flatDeliveryPerStore,
                        UpdatedAt = DateTime.UtcNow
                    };

                    foreach (var cartItem in storeGroup)
                    {
                        decimal itemTotal = cartItem.Product!.Price * cartItem.Quantity;
                        storeSubtotal += itemTotal;

                        // Deduct stock safely
                        cartItem.Product.StockQuantity -= cartItem.Quantity;

                        storeOrder.Items.Add(new OrderItem
                        {
                            ProductId = cartItem.ProductId,
                            ProductName = cartItem.Product.Name,
                            UnitPrice = cartItem.Product.Price,
                            Quantity = cartItem.Quantity,
                            TotalPrice = itemTotal
                        });
                    }

                    storeOrder.Subtotal = storeSubtotal;
                    storeOrder.TotalAmount = storeSubtotal + flatDeliveryPerStore;

                    parentOrder.StoreOrders.Add(storeOrder);
                    parentSubtotal += storeSubtotal;
                    parentDeliveryFee += flatDeliveryPerStore;
                }

                parentOrder.Subtotal = parentSubtotal;
                parentOrder.DeliveryFee = parentDeliveryFee;
                parentOrder.TotalAmount = parentSubtotal + parentDeliveryFee;

                _dbContext.Orders.Add(parentOrder);

                // Clear cart items
                _dbContext.CartItems.RemoveRange(cart.Items);

                // Create seller notifications for each suborder
                foreach (var so in parentOrder.StoreOrders)
                {
                    var store = await _dbContext.Stores.FindAsync(so.StoreId);
                    if (store != null)
                    {
                        var notification = new Notification
                        {
                            UserId = store.SellerId,
                            Title = "طلب شراء جديد",
                            Message = $"وصلك طلب شراء جديد بقيمة {so.TotalAmount:N2} ج.م",
                            LinkUrl = "/Seller/Orders",
                            Type = "order",
                            CreatedAt = DateTime.UtcNow
                        };
                        _dbContext.Notifications.Add(notification);
                        notifications.Add(notification);
                    }
                }

                if (!string.IsNullOrEmpty(userId))
                {
                    var buyerNotification = new Notification
                    {
                        UserId = userId,
                        Title = "تم استلام طلبك",
                        Message = $"تم تسجيل طلبك رقم {parentOrder.OrderNumber} بنجاح.",
                        LinkUrl = "/Order/MyOrders",
                        Type = "order",
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.Notifications.Add(buyerNotification);
                    notifications.Add(buyerNotification);
                }

                await _dbContext.SaveChangesAsync();
                return (parentOrder, notifications);
            }

            (Order Order, List<Notification> Notifications) result;
            if (_dbContext.Database.IsRelational())
            {
                var strategy = _dbContext.Database.CreateExecutionStrategy();
                result = await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    var orderResult = await ProcessOrderAsync();
                    await transaction.CommitAsync();
                    return orderResult;
                });
            }
            else
            {
                // In-memory or non-relational test provider
                result = await ProcessOrderAsync();
            }

            foreach (var notification in result.Notifications)
                await _notificationService.PublishAsync(notification);

            return result.Order;
        }

        public async Task<bool> UpdateStoreOrderStatusAsync(int storeOrderId, int storeId, OrderStatus newStatus, string? trackingNumber)
        {
            var storeOrder = await _dbContext.StoreOrders
                .Include(so => so.ParentOrder)
                .FirstOrDefaultAsync(so => so.Id == storeOrderId && so.StoreId == storeId);

            if (storeOrder == null) return false;

            // Allowed status transitions check
            if (storeOrder.Status == OrderStatus.Delivered || storeOrder.Status == OrderStatus.Canceled)
            {
                return false; // Terminal states
            }

            storeOrder.Status = newStatus;
            if (!string.IsNullOrEmpty(trackingNumber))
            {
                storeOrder.TrackingNumber = trackingNumber;
            }
            storeOrder.UpdatedAt = DateTime.UtcNow;

            // Check if all suborders in parent order are in same status, update parent order
            var parentOrder = storeOrder.ParentOrder;
            if (parentOrder != null)
            {
                var allSubOrders = await _dbContext.StoreOrders
                    .Where(so => so.ParentOrderId == parentOrder.Id)
                    .ToListAsync();

                if (allSubOrders.All(so => so.Status == OrderStatus.Delivered))
                {
                    parentOrder.Status = OrderStatus.Delivered;
                }
                else if (allSubOrders.All(so => so.Status == OrderStatus.Canceled))
                {
                    parentOrder.Status = OrderStatus.Canceled;
                }
                else if (allSubOrders.Any(so => so.Status == OrderStatus.Shipped))
                {
                    parentOrder.Status = OrderStatus.Shipped;
                }
                else if (allSubOrders.Any(so => so.Status == OrderStatus.Processing))
                {
                    parentOrder.Status = OrderStatus.Processing;
                }
            }

            await _dbContext.SaveChangesAsync();
            if (parentOrder != null &&
                !string.IsNullOrEmpty(parentOrder.UserId))
            {
                await _notificationService.CreateNotificationAsync(
                    parentOrder.UserId,
                    "تم تحديث حالة طلبك",
                    $"تم تحديث حالة طلبك رقم {parentOrder.OrderNumber} إلى: {newStatus}.",
                    "order",
                    "/Order/MyOrders");
            }

            return true;
        }
    }
}
