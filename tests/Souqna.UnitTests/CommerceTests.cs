using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;
using Souqna.Infrastructure.Services;
using Moq;
using Xunit;

namespace Souqna.UnitTests
{
    public class CommerceTests
    {
        private SouqnaDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<SouqnaDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new SouqnaDbContext(options);
        }

        [Fact]
        public async Task MultiVendorCart_SplitsIntoSeparateStoreOrders_WithCorrectOwnership()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var notificationMock = new Mock<INotificationService>();
            notificationMock
                .Setup(service => service.PublishAsync(It.IsAny<Notification>()))
                .Returns(Task.CompletedTask);
            notificationMock
                .Setup(service => service.CreateNotificationAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>()))
                .ReturnsAsync(new Notification());
            var orderService = new OrderService(db, notificationMock.Object);

            // Two stores
            var storeA = new Store { Id = 1, SellerId = "seller-a", StoreName = "متجر النيل", Slug = "nile" };
            var storeB = new Store { Id = 2, SellerId = "seller-b", StoreName = "متجر الهرم", Slug = "pyramid" };
            db.Stores.AddRange(storeA, storeB);

            // Products
            var prodA = new Product { Id = 101, StoreId = 1, Store = storeA, Name = "قميص", Price = 300.00m, StockQuantity = 10, IsPublished = true, Slug = "shirt" };
            var prodB = new Product { Id = 102, StoreId = 2, Store = storeB, Name = "ساعة", Price = 750.50m, StockQuantity = 5, IsPublished = true, Slug = "watch" };
            db.Products.AddRange(prodA, prodB);

            // Cart with items from both stores
            var cart = new Cart { Id = 5, UserId = "buyer-1" };
            cart.Items.Add(new CartItem { CartId = 5, ProductId = 101, Product = prodA, Quantity = 2, UnitPrice = 300.00m }); // 600 EGP
            cart.Items.Add(new CartItem { CartId = 5, ProductId = 102, Product = prodB, Quantity = 1, UnitPrice = 750.50m }); // 750.50 EGP
            db.Carts.Add(cart);
            await db.SaveChangesAsync();

            var checkoutDto = new CheckoutDto
            {
                FullName = "أحمد المصري",
                Phone = "+201012345678",
                City = "القاهرة",
                Area = "المعادي",
                StreetAddress = "شارع 9",
                PaymentMethod = PaymentMethod.CashOnDelivery
            };

            // Act
            var order = await orderService.CreateOrderFromCartAsync("buyer-1", null, checkoutDto);

            // Assert
            Assert.NotNull(order);
            Assert.Equal("buyer-1", order.UserId);
            Assert.Equal(2, order.StoreOrders.Count);

            var storeOrderA = order.StoreOrders.FirstOrDefault(so => so.StoreId == 1);
            var storeOrderB = order.StoreOrders.FirstOrDefault(so => so.StoreId == 2);

            Assert.NotNull(storeOrderA);
            Assert.NotNull(storeOrderB);

            // Check store A line items & totals
            Assert.Single(storeOrderA.Items);
            Assert.Equal(600.00m, storeOrderA.Subtotal);
            Assert.Equal(35.00m, storeOrderA.DeliveryFee);
            Assert.Equal(635.00m, storeOrderA.TotalAmount);

            // Check store B line items & totals
            Assert.Single(storeOrderB.Items);
            Assert.Equal(750.50m, storeOrderB.Subtotal);
            Assert.Equal(35.00m, storeOrderB.DeliveryFee);
            Assert.Equal(785.50m, storeOrderB.TotalAmount);

            // Parent order grand totals (using decimal arithmetic)
            Assert.Equal(1350.50m, order.Subtotal);
            Assert.Equal(70.00m, order.DeliveryFee); // 35 * 2
            Assert.Equal(1420.50m, order.TotalAmount);

            // Stock decremented accurately
            var updatedProdA = await db.Products.FindAsync(101);
            var updatedProdB = await db.Products.FindAsync(102);
            Assert.Equal(8, updatedProdA!.StockQuantity); // 10 - 2
            Assert.Equal(4, updatedProdB!.StockQuantity); // 5 - 1
            Assert.Equal(3, await db.Notifications.CountAsync());
            notificationMock.Verify(
                service => service.PublishAsync(It.IsAny<Notification>()),
                Times.Exactly(3));
        }

        [Fact]
        public async Task InsufficientStock_ThrowsException_AndPreventsOrderCreation()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var notificationMock = new Mock<INotificationService>();
            var orderService = new OrderService(db, notificationMock.Object);

            var store = new Store { Id = 3, SellerId = "seller-c", StoreName = "متجر الإلكترونيات", Slug = "electro" };
            db.Stores.Add(store);

            var prod = new Product { Id = 201, StoreId = 3, Store = store, Name = "لابتوب", Price = 15000m, StockQuantity = 1, IsPublished = true, Slug = "laptop" };
            db.Products.Add(prod);

            var cart = new Cart { Id = 6, UserId = "buyer-2" };
            cart.Items.Add(new CartItem { CartId = 6, ProductId = 201, Product = prod, Quantity = 2, UnitPrice = 15000m }); // Wants 2, stock is only 1
            db.Carts.Add(cart);
            await db.SaveChangesAsync();

            var checkoutDto = new CheckoutDto
            {
                FullName = "محمود حسن",
                Phone = "+201098765432",
                City = "الجيزة",
                PaymentMethod = PaymentMethod.CashOnDelivery
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                orderService.CreateOrderFromCartAsync("buyer-2", null, checkoutDto));

            Assert.Contains("غير متوفرة حالياً", ex.Message);

            // Assert stock remained untouched
            var unmodifiedProd = await db.Products.FindAsync(201);
            Assert.Equal(1, unmodifiedProd!.StockQuantity);
        }

        [Fact]
        public async Task Seller_CannotUpdateOtherSellerSuborder()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var notificationMock = new Mock<INotificationService>();
            var orderService = new OrderService(db, notificationMock.Object);

            var storeOrder = new StoreOrder
            {
                Id = 99,
                StoreId = 1, // Store 1
                Status = OrderStatus.Pending
            };
            db.StoreOrders.Add(storeOrder);
            await db.SaveChangesAsync();

            // Act: Store 2 tries to update Store 1's suborder
            bool success = await orderService.UpdateStoreOrderStatusAsync(99, storeId: 2, OrderStatus.Shipped, "TRACK-123");

            // Assert
            Assert.False(success);
            var reloaded = await db.StoreOrders.FindAsync(99);
            Assert.Equal(OrderStatus.Pending, reloaded!.Status);
        }
    }
}
