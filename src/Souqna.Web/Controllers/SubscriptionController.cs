using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;

namespace Souqna.Web.Controllers
{
    public class SubscriptionController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionController(
            SouqnaDbContext dbContext,
            ISubscriptionService subscriptionService)
        {
            _dbContext = dbContext;
            _subscriptionService = subscriptionService;
        }

        public async Task<IActionResult> Index()
        {
            var plans = await _dbContext.SubscriptionPlans
                .Where(p => p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            return View(plans);
        }

        [Authorize(Roles = "Seller,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Subscribe(int planId, BillingInterval interval = BillingInterval.Monthly)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var store = await _dbContext.Stores.FirstOrDefaultAsync(s => s.SellerId == userId);

            if (store == null)
            {
                TempData["ErrorMessage"] = "يجب أن تمتلك متجراً للاشتراك في هذه الباقة.";
                return RedirectToAction(nameof(Index));
            }

            var plan = await _dbContext.SubscriptionPlans.FindAsync(planId);
            if (plan == null || !plan.IsActive) return NotFound();

            if (interval != BillingInterval.Monthly &&
                interval != BillingInterval.Yearly)
            {
                return BadRequest();
            }

            decimal amount = interval == BillingInterval.Yearly
                ? plan.PriceYearly
                : plan.PriceMonthly;

            if (amount <= 0)
            {
                TempData["InfoMessage"] =
                    "الباقة المجانية لا تحتاج إلى دفع، ولم يتم تغيير اشتراكك الحالي.";

                return RedirectToAction("Index", "Subscriptions", new { area = "Seller" });
            }

            var subscription = await _subscriptionService.SubscribeToPlanAsync(
                store.Id,
                planId,
                interval);

            string intervalText = interval == BillingInterval.Yearly
                ? "سنوي"
                : "شهري";
            string whatsappMessage =
                $"أهلًا، أريد الاشتراك في باقة {plan.NameAr} لمتجر {store.StoreName} على منصة سوقنا.\n" +
                $"رقم طلب الاشتراك: {subscription.Id}\n" +
                $"السعر: {subscription.PricePaid.ToString("0.00", CultureInfo.InvariantCulture)} جنيه مصري\n" +
                $"مدة الاشتراك: {intervalText}\n" +
                "يرجى إرسال تفاصيل الدفع.";

            string whatsappUrl =
                "https://wa.me/201553480299?text=" +
                Uri.EscapeDataString(whatsappMessage);

            return Redirect(whatsappUrl);
        }
    }
}
