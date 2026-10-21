using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Infrastructure.Data;
using Souqna.Infrastructure.Services;
using Xunit;

namespace Souqna.UnitTests
{
    public class SubscriptionTests
    {
        private SouqnaDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<SouqnaDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new SouqnaDbContext(options);

            // Seed sample plans
            context.SubscriptionPlans.AddRange(
                new SubscriptionPlan
                {
                    Id = 1,
                    NameAr = "الباقة المجانية",
                    NameEn = "Free",
                    Slug = "free",
                    PriceMonthly = 0m,
                    PriceYearly = 0m,
                    MaxActiveProducts = 3,
                    MaxPublishedVideos = 3,
                    MaxVideoDurationSeconds = 60,
                    IsActive = true
                },
                new SubscriptionPlan
                {
                    Id = 2,
                    NameAr = "باقة البداية (Starter)",
                    NameEn = "Starter",
                    Slug = "starter",
                    PriceMonthly = 99m,
                    PriceYearly = 990m,
                    MaxActiveProducts = 20,
                    MaxPublishedVideos = 20,
                    MaxVideoDurationSeconds = 180,
                    IsActive = true
                }
            );

            context.SaveChanges();
            return context;
        }

        [Fact]
        public async Task FreeUser_CannotPublishFourthProduct_WhenLimitIsThree()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new SubscriptionService(db);

            var store = new Store
            {
                Id = 10,
                SellerId = "seller-1",
                StoreName = "متجر التجربة",
                Slug = "test-store"
            };
            db.Stores.Add(store);

            // Active free subscription
            db.Subscriptions.Add(new Subscription
            {
                StoreId = 10,
                SubscriptionPlanId = 1,
                Status = SubscriptionStatus.Active,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(1)
            });

            // 3 active products
            db.Products.AddRange(
                new Product { StoreId = 10, Name = "P1", Slug = "p1", Price = 100, StockQuantity = 5, IsPublished = true },
                new Product { StoreId = 10, Name = "P2", Slug = "p2", Price = 150, StockQuantity = 3, IsPublished = true },
                new Product { StoreId = 10, Name = "P3", Slug = "p3", Price = 200, StockQuantity = 8, IsPublished = true }
            );
            await db.SaveChangesAsync();

            // Act
            var checkResult = await service.CanAddProductAsync(10);

            // Assert
            Assert.False(checkResult.IsAllowed);
            Assert.Equal(3, checkResult.CurrentCount);
            Assert.Equal(3, checkResult.MaxAllowed);
            Assert.Contains("لقد وصلت إلى الحد الأقصى للمنتجات", checkResult.Reason);
        }

        [Fact]
        public async Task FreeUser_CannotPublishVideo_LongerThanAllowedDuration()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new SubscriptionService(db);

            var store = new Store
            {
                Id = 20,
                SellerId = "seller-2",
                StoreName = "متجر الفيديوهات",
                Slug = "video-store"
            };
            db.Stores.Add(store);
            db.Subscriptions.Add(new Subscription
            {
                StoreId = 20,
                SubscriptionPlanId = 1, // Free allows 60s
                Status = SubscriptionStatus.Active,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(1)
            });
            await db.SaveChangesAsync();

            // Act: attempting 61 seconds on a 60-second limit plan
            var checkResult = await service.CanUploadVideoAsync(20, requestedDurationSeconds: 61);

            // Assert
            Assert.False(checkResult.IsAllowed);
            Assert.Equal(60, checkResult.MaxAllowed);
            Assert.Contains("تتجاوز الحد الأقصى", checkResult.Reason);
        }

        [Fact]
        public async Task PaidSubscriptionRequest_RemainsPendingAndKeepsCurrentSubscription()
        {
            using var db = CreateInMemoryDbContext();
            var service = new SubscriptionService(db);
            var store = new Store
            {
                Id = 30,
                SellerId = "seller-3",
                StoreName = "متجر الدفع",
                Slug = "pay-store"
            };
            db.Stores.Add(store);
            var currentSubscription = new Subscription
            {
                StoreId = store.Id,
                SubscriptionPlanId = 1,
                Status = SubscriptionStatus.Active,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(1),
                PricePaid = 0m
            };
            db.Subscriptions.Add(currentSubscription);
            await db.SaveChangesAsync();

            var request = await service.SubscribeToPlanAsync(
                store.Id,
                planId: 2,
                BillingInterval.Monthly);

            var plan = await db.SubscriptionPlans.FindAsync(2);
            Assert.NotNull(plan);
            plan.PriceMonthly = 125m;
            await db.SaveChangesAsync();

            var repeatedRequest = await service.SubscribeToPlanAsync(
                store.Id,
                planId: 2,
                BillingInterval.Monthly);

            Assert.Equal(request.Id, repeatedRequest.Id);
            Assert.Equal(99m, request.PricePaid);
            Assert.Equal(SubscriptionStatus.Pending, request.Status);
            Assert.Equal(SubscriptionStatus.Active, currentSubscription.Status);
            Assert.Empty(await db.SubscriptionPayments.ToListAsync());
        }

        [Fact]
        public async Task MockPaymentService_DoesNotActivateOrRecordPayment()
        {
            using var db = CreateInMemoryDbContext();
            var loggerMock = new Mock<ILogger<PaymentService>>();
            var paymentService = new PaymentService(db, loggerMock.Object);

            var sub = new Subscription
            {
                Id = 55,
                StoreId = 30,
                SubscriptionPlanId = 2,
                Status = SubscriptionStatus.Pending,
                BillingInterval = BillingInterval.Monthly,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow
            };
            db.Subscriptions.Add(sub);
            await db.SaveChangesAsync();

            var result = await paymentService.ProcessSubscriptionPaymentAsync(55, 99m, "EGP", PaymentMethod.MockCreditCard);

            Assert.False(result.Success);

            var updatedSub = await db.Subscriptions.FindAsync(55);
            Assert.NotNull(updatedSub);
            Assert.Equal(SubscriptionStatus.Pending, updatedSub.Status);

            var recordedPayment = await db.SubscriptionPayments.FirstOrDefaultAsync(p => p.SubscriptionId == 55);
            Assert.Null(recordedPayment);
        }

        [Fact]
        public async Task ExpiredSubscription_FallsBackToFreeEntitlements_WithoutDeletingProducts()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new SubscriptionService(db);

            var store = new Store { Id = 40, SellerId = "seller-4", StoreName = "متجر منتهي", Slug = "expired-store" };
            db.Stores.Add(store);

            // Expired Pro subscription
            db.Subscriptions.Add(new Subscription
            {
                StoreId = 40,
                SubscriptionPlanId = 2,
                Status = SubscriptionStatus.Active,
                StartDate = DateTime.UtcNow.AddMonths(-2),
                EndDate = DateTime.UtcNow.AddDays(-5) // Expired 5 days ago
            });

            // 5 products currently exist in store
            for (int i = 1; i <= 5; i++)
            {
                db.Products.Add(new Product { StoreId = 40, Name = $"Product {i}", Slug = $"prod-{i}", Price = 100, StockQuantity = 10, IsPublished = true });
            }
            await db.SaveChangesAsync();

            // Act
            var activeSub = await service.GetActiveSubscriptionAsync(40);
            var checkResult = await service.CanAddProductAsync(40);

            // Assert
            // Fallback to Free Plan (max 3)
            Assert.Equal(1, activeSub.SubscriptionPlanId);
            Assert.False(checkResult.IsAllowed); // Cannot add more because current (5) >= free plan limit (3)
            
            // Critical rule: Content is preserved, NOT deleted!
            int productCount = await db.Products.CountAsync(p => p.StoreId == 40);
            Assert.Equal(5, productCount);
        }
    }
}
