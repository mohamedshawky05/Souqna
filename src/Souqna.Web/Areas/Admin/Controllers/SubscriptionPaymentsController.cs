using System;
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

namespace Souqna.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SubscriptionPaymentsController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly INotificationService _notificationService;

        public SubscriptionPaymentsController(
            SouqnaDbContext dbContext,
            INotificationService notificationService)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var pending = await _dbContext.Subscriptions
                .Include(s => s.Store)
                .Include(s => s.SubscriptionPlan)
                .Where(s => s.Status == SubscriptionStatus.Pending)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();

            return View(pending);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync();

            var subscription = await _dbContext.Subscriptions
                .Include(s => s.Store)
                .Include(s => s.SubscriptionPlan)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);

            if (subscription == null)
                return NotFound();

            if (subscription.Status != SubscriptionStatus.Pending)
            {
                TempData["ErrorMessage"] =
                    "هذا الطلب ليس معلقًا؛ لم يتم إجراء أي تغيير.";

                return RedirectToAction(nameof(Index));
            }

            if (subscription.PricePaid <= 0)
            {
                TempData["ErrorMessage"] =
                    "تعذر تأكيد الدفع لأن قيمة الطلب غير صالحة.";
                return RedirectToAction(nameof(Index));
            }

            bool alreadyPaid = await _dbContext.SubscriptionPayments
                .AnyAsync(p =>
                    p.SubscriptionId == subscription.Id &&
                    p.Status == PaymentStatus.Completed);

            if (alreadyPaid)
            {
                TempData["ErrorMessage"] =
                    "توجد دفعة مكتملة لهذا الطلب بالفعل.";

                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.UtcNow;
            var endDate =
                subscription.BillingInterval == BillingInterval.Yearly
                    ? now.AddYears(1)
                    : now.AddMonths(1);

            int activatedCount = await _dbContext.Subscriptions
                .Where(s =>
                    s.Id == subscription.Id &&
                    s.Status == SubscriptionStatus.Pending)
                .ExecuteUpdateAsync(updates => updates
                    .SetProperty(s => s.Status, SubscriptionStatus.Active)
                    .SetProperty(s => s.StartDate, now)
                    .SetProperty(s => s.EndDate, endDate)
                    .SetProperty(s => s.AutoRenew, false));

            if (activatedCount != 1)
            {
                TempData["ErrorMessage"] =
                    "تمت معالجة هذا الطلب بالفعل؛ لم يتم تسجيل دفعة مكررة.";
                return RedirectToAction(nameof(Index));
            }

            var currentSubscriptions = await _dbContext.Subscriptions
                .Where(s =>
                    s.StoreId == subscription.StoreId &&
                    s.Id != subscription.Id &&
                    s.Status == SubscriptionStatus.Active)
                .ToListAsync();

            foreach (var current in currentSubscriptions)
            {
                current.Status = SubscriptionStatus.Canceled;
                current.AutoRenew = false;
            }

            var payment = new SubscriptionPayment
            {
                SubscriptionId = subscription.Id,
                Amount = subscription.PricePaid,
                Currency = "EGP",
                PaymentMethod = PaymentMethod.ManualWhatsApp,
                TransactionReference =
                    $"MANUAL-SUB-{subscription.Id}-{Guid.NewGuid():N}",
                Status = PaymentStatus.Completed,
                PaymentDate = now,
                ProviderPayload =
                    $"Manual payment confirmed by admin user " +
                    $"{User.FindFirstValue(ClaimTypes.NameIdentifier)}"
            };

            _dbContext.SubscriptionPayments.Add(payment);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            await _notificationService.CreateNotificationAsync(
                subscription.Store!.SellerId,
                "تم تفعيل اشتراكك",
                $"تم تأكيد استلام الدفع وتفعيل اشتراكك في باقة {subscription.SubscriptionPlan?.NameAr}.",
                "subscription",
                "/Seller/Subscriptions");

            TempData["SuccessMessage"] =
                $"تم تأكيد الدفع وتفعيل الاشتراك رقم {subscription.Id}.";

            return RedirectToAction(nameof(Index));
        }
    }
}