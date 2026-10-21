
using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;

namespace Souqna.Web.Areas.Seller.Controllers
{
    [Area("Seller")]
    [Authorize(Roles = "Seller,Admin")]
    public class SubscriptionsController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionsController(
            SouqnaDbContext dbContext,
            ISubscriptionService subscriptionService)
        {
            _dbContext = dbContext;
            _subscriptionService = subscriptionService;
        }

        private async Task<Store?> GetCurrentStoreAsync()
        {
            string? userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return null;

            return await _dbContext.Stores
                .FirstOrDefaultAsync(s => s.SellerId == userId);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var store = await GetCurrentStoreAsync();

            if (store == null)
                return NotFound();

            var currentSub =
                await _subscriptionService.GetActiveSubscriptionAsync(
                    store.Id);

            var usage =
                await _subscriptionService.GetStoreUsageAsync(
                    store.Id);

            var allPlans = await _dbContext.SubscriptionPlans
                .Where(p => p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            ViewBag.CurrentSub = currentSub;
            ViewBag.Usage = usage;

            return View(allPlans);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(
            int planId,
            BillingInterval interval = BillingInterval.Monthly)
        {
            var store = await GetCurrentStoreAsync();

            if (store == null)
            {
                TempData["ErrorMessage"] =
                    "يجب أن تمتلك متجرًا للاشتراك.";

                return RedirectToAction(nameof(Index));
            }

            var plan = await _dbContext.SubscriptionPlans
                .FirstOrDefaultAsync(
                    p => p.Id == planId && p.IsActive);

            if (plan == null)
                return NotFound();

            if (interval != BillingInterval.Monthly &&
                interval != BillingInterval.Yearly)
            {
                return BadRequest();
            }

            decimal amount = interval == BillingInterval.Yearly
                ? plan.PriceYearly
                : plan.PriceMonthly;

            // الباقة المجانية لا تحتاج إلى طلب دفع.
            // لا نغيّر الاشتراك الحالي من هنا.
            if (amount <= 0)
            {
                TempData["InfoMessage"] =
                    "الباقة المجانية لا تحتاج إلى دفع، ولم يتم تغيير اشتراكك الحالي.";

                return RedirectToAction(nameof(Index));
            }

            // إنشاء طلب اشتراك Pending فقط.
            // لا تستدعِ خدمة الدفع التجريبية هنا.
            var subscription =
                await _subscriptionService.SubscribeToPlanAsync(
                    store.Id,
                    planId,
                    interval);

            string intervalText =
                interval == BillingInterval.Yearly
                    ? "سنوي"
                    : "شهري";

            // استخدام السعر المحفوظ في الطلب لتطابق الرسالة
            // مع قيمة الطلب إذا كان الطلب المعلّق موجودًا مسبقًا.
            decimal requestAmount = subscription.PricePaid;

            string whatsappMessage =
                $"أهلًا، أريد الاشتراك في باقة {plan.NameAr} لمتجر {store.StoreName} على منصة سوقنا.\n" +
                $"رقم طلب الاشتراك: {subscription.Id}\n" +
                $"السعر: {requestAmount.ToString("0.00", CultureInfo.InvariantCulture)} جنيه مصري\n" +
                $"مدة الاشتراك: {intervalText}\n" +
                "يرجى إرسال تفاصيل الدفع.";

            string whatsappUrl =
                "https://wa.me/201553480299?text=" +
                Uri.EscapeDataString(whatsappMessage);

            return Redirect(whatsappUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel()
        {
            var store = await GetCurrentStoreAsync();

            if (store == null)
                return NotFound();

            bool canceled =
                await _subscriptionService.CancelSubscriptionAsync(
                    store.Id);

            if (canceled)
            {
                TempData["SuccessMessage"] =
                    "تم إلغاء التجديد التلقائي للباقة بنجاح.";
            }
            else
            {
                TempData["ErrorMessage"] =
                    "لا يوجد اشتراك نشط للإلغاء.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
