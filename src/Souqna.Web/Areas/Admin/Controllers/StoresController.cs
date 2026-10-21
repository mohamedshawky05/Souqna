using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Infrastructure.Data;

namespace Souqna.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class StoresController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public StoresController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(string? q)
        {
            var query = _dbContext.Stores
                .Include(s => s.Seller)
                .Include(s => s.Products)
                .Include(s => s.Subscriptions)
                    .ThenInclude(sub => sub.SubscriptionPlan)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(s => s.StoreName.Contains(q.Trim()) || s.Seller!.Email!.Contains(q.Trim()));
            }

            var stores = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
            return View(stores);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var store = await _dbContext.Stores.FindAsync(id);
            if (store != null)
            {
                store.IsActive = !store.IsActive;
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = store.IsActive ? "تم تفعيل المتجر بنجاح." : "تم إيقاف المتجر.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBadge(int id)
        {
            var store = await _dbContext.Stores.FindAsync(id);
            if (store != null)
            {
                store.IsVerified = !store.IsVerified;
                store.VerificationBadge = store.IsVerified ? "Store Verified" : null;
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = store.IsVerified ? "تم منح شارة التوثيق للمتجر." : "تم إلغاء شارة التوثيق.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
