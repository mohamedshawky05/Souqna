using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;

namespace Souqna.Infrastructure.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly SouqnaDbContext _dbContext;

        public SubscriptionService(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Subscription> GetActiveSubscriptionAsync(int storeId)
        {
            var sub = await _dbContext.Subscriptions
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.StoreId == storeId && s.Status == SubscriptionStatus.Active && s.EndDate >= DateTime.UtcNow)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            if (sub != null) return sub;

            // Check if there is an existing expired subscription
            var expiredSub = await _dbContext.Subscriptions
                .Include(s => s.SubscriptionPlan)
.Where(s =>
    s.StoreId == storeId &&
    s.Status == SubscriptionStatus.Active)
.OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (expiredSub != null && expiredSub.EndDate < DateTime.UtcNow)
            {
                expiredSub.Status = SubscriptionStatus.Expired;
                await _dbContext.SaveChangesAsync();
            }

            // Fallback to Free Plan default
            var freePlan = await _dbContext.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.Slug == "free" || p.PriceMonthly == 0);

            if (freePlan == null)
            {
                freePlan = new SubscriptionPlan
                {
                    NameAr = "الباقة المجانية",
                    NameEn = "Free Plan",
                    Slug = "free",
                    PriceMonthly = 0m,
                    PriceYearly = 0m,
                    MaxActiveProducts = 3,
                    MaxPublishedVideos = 3,
                    MaxVideoDurationSeconds = 60,
                    IsActive = true
                };
                _dbContext.SubscriptionPlans.Add(freePlan);
                await _dbContext.SaveChangesAsync();
            }

            // Assign default free subscription
            var defaultSub = new Subscription
            {
                StoreId = storeId,
                SubscriptionPlanId = freePlan.Id,
                SubscriptionPlan = freePlan,
                BillingInterval = BillingInterval.Monthly,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(10),
                Status = SubscriptionStatus.Active,
                PricePaid = 0m
            };

            _dbContext.Subscriptions.Add(defaultSub);
            await _dbContext.SaveChangesAsync();

            return defaultSub;
        }

        public async Task<SubscriptionPlanCheckResult> CanAddProductAsync(int storeId)
        {
            var sub = await GetActiveSubscriptionAsync(storeId);
            var plan = sub.SubscriptionPlan!;

            int activeCount = await _dbContext.Products
                .CountAsync(p => p.StoreId == storeId && !p.IsArchived && p.IsPublished);

            if (activeCount >= plan.MaxActiveProducts)
            {
                return new SubscriptionPlanCheckResult
                {
                    IsAllowed = false,
                    CurrentCount = activeCount,
                    MaxAllowed = plan.MaxActiveProducts,
                    Reason = $"لقد وصلت إلى الحد الأقصى للمنتجات المسموح بها ({plan.MaxActiveProducts} منتجات) في باقة \"{plan.NameAr}\". يرجى ترقية باقتك لإضافة المزيد."
                };
            }

            return new SubscriptionPlanCheckResult
            {
                IsAllowed = true,
                CurrentCount = activeCount,
                MaxAllowed = plan.MaxActiveProducts
            };
        }

        public async Task<SubscriptionPlanCheckResult> CanUploadVideoAsync(int storeId, int requestedDurationSeconds)
        {
            var sub = await GetActiveSubscriptionAsync(storeId);
            var plan = sub.SubscriptionPlan!;

            // Check published videos count limit
            int publishedVideos = await _dbContext.Videos
                .CountAsync(v => v.StoreId == storeId && v.Status == VideoStatus.Published);

            if (publishedVideos >= plan.MaxPublishedVideos)
            {
                return new SubscriptionPlanCheckResult
                {
                    IsAllowed = false,
                    CurrentCount = publishedVideos,
                    MaxAllowed = plan.MaxPublishedVideos,
                    Reason = $"لقد وصلت إلى الحد الأقصى للفيديوهات المنشورة ({plan.MaxPublishedVideos} فيديو) في باقة \"{plan.NameAr}\". يرجى ترقية باقتك لنشر المزيد."
                };
            }

            // Check duration limit
            if (requestedDurationSeconds > plan.MaxVideoDurationSeconds)
            {
                return new SubscriptionPlanCheckResult
                {
                    IsAllowed = false,
                    CurrentCount = requestedDurationSeconds,
                    MaxAllowed = plan.MaxVideoDurationSeconds,
                    Reason = $"مدة الفيديو ({requestedDurationSeconds} ثانية) تتجاوز الحد الأقصى المسموح به في باقتك الحالية ({plan.MaxVideoDurationSeconds} ثانية)."
                };
            }

            return new SubscriptionPlanCheckResult
            {
                IsAllowed = true,
                CurrentCount = publishedVideos,
                MaxAllowed = plan.MaxPublishedVideos
            };
        }

        public async Task<SubscriptionUsageDto> GetStoreUsageAsync(int storeId)
        {
            var sub = await GetActiveSubscriptionAsync(storeId);
            var plan = sub.SubscriptionPlan!;

            int activeProducts = await _dbContext.Products
                .CountAsync(p => p.StoreId == storeId && !p.IsArchived && p.IsPublished);

            int publishedVideos = await _dbContext.Videos
                .CountAsync(v => v.StoreId == storeId && v.Status == VideoStatus.Published);

            return new SubscriptionUsageDto
            {
                PlanNameAr = plan.NameAr,
                PlanNameEn = plan.NameEn,
                Status = sub.Status,
                EndDate = sub.EndDate,
                ActiveProductsCount = activeProducts,
                MaxProducts = plan.MaxActiveProducts,
                PublishedVideosCount = publishedVideos,
                MaxVideos = plan.MaxPublishedVideos,
                MaxVideoDurationSeconds = plan.MaxVideoDurationSeconds,
                AllowsCoupons = plan.AllowsCoupons,
                AllowsCsvExportImport = plan.AllowsCsvExportImport,
                HasAdvancedAnalytics = plan.HasAdvancedAnalytics
            };
        }

        public async Task<Subscription> SubscribeToPlanAsync(
            int storeId,
            int planId,
            BillingInterval interval)
        {
            if (interval != BillingInterval.Monthly &&
                interval != BillingInterval.Yearly)
            {
                throw new ArgumentOutOfRangeException(nameof(interval));
            }

            var plan = await _dbContext.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.Id == planId && p.IsActive);

            if (plan == null)
                throw new InvalidOperationException("باقة الاشتراك غير موجودة أو غير متاحة.");

            decimal price = interval == BillingInterval.Yearly
                ? plan.PriceYearly
                : plan.PriceMonthly;

            if (price <= 0)
                throw new InvalidOperationException(
                    "هذه الدالة مخصصة لطلبات الاشتراك المدفوعة فقط.");

            var pendingSub = await _dbContext.Subscriptions
                .Where(s =>
                    s.StoreId == storeId &&
                    s.SubscriptionPlanId == planId &&
                    s.BillingInterval == interval &&
                    s.Status == SubscriptionStatus.Pending)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (pendingSub != null)
                return pendingSub;

            var now = DateTime.UtcNow;
            var newSub = new Subscription
            {
                StoreId = storeId,
                SubscriptionPlanId = plan.Id,
                BillingInterval = interval,
                StartDate = now,
                EndDate = interval == BillingInterval.Yearly
                    ? now.AddYears(1)
                    : now.AddMonths(1),
                Status = SubscriptionStatus.Pending,
                PricePaid = price,
                CreatedAt = now,
                AutoRenew = false
            };

            _dbContext.Subscriptions.Add(newSub);
            await _dbContext.SaveChangesAsync();

            return newSub;
        }
        public async Task<bool> CancelSubscriptionAsync(int storeId)
        {
            var sub = await _dbContext.Subscriptions
                .Where(s => s.StoreId == storeId && s.Status == SubscriptionStatus.Active)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            if (sub == null) return false;

            sub.Status = SubscriptionStatus.Canceled;
            sub.AutoRenew = false;
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
