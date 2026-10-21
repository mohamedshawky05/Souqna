using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Entities;
using Souqna.Infrastructure.Data;

namespace Souqna.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PlansController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public PlansController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var plans = await _dbContext.SubscriptionPlans
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            return View(plans);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var plan = await _dbContext.SubscriptionPlans.FindAsync(id);
            if (plan == null) return NotFound();

            return View(plan);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SubscriptionPlan model)
        {
            var plan = await _dbContext.SubscriptionPlans.FindAsync(model.Id);
            if (plan == null) return NotFound();

            plan.NameAr = model.NameAr;
            plan.NameEn = model.NameEn;
            plan.DescriptionAr = model.DescriptionAr;
            plan.PriceMonthly = model.PriceMonthly;
            plan.PriceYearly = model.PriceYearly;
            plan.MaxActiveProducts = model.MaxActiveProducts;
            plan.MaxPublishedVideos = model.MaxPublishedVideos;
            plan.MaxVideoDurationSeconds = model.MaxVideoDurationSeconds;
            plan.StorageLimitMb = model.StorageLimitMb;
            plan.AllowsCoupons = model.AllowsCoupons;
            plan.AllowsCsvExportImport = model.AllowsCsvExportImport;
            plan.HasAdvancedAnalytics = model.HasAdvancedAnalytics;
            plan.IsActive = model.IsActive;

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = $"تم تحديث باقة \"{plan.NameAr}\" والحدود والأسعار بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}
