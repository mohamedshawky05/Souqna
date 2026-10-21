using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;
using Souqna.Web.ViewModels;

namespace Souqna.Web.Areas.Seller.Controllers
{
    [Area("Seller")]
    [Authorize(Roles = "Seller,Admin")]
    public class DashboardController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly ISubscriptionService _subscriptionService;

        public DashboardController(SouqnaDbContext dbContext, ISubscriptionService subscriptionService)
        {
            _dbContext = dbContext;
            _subscriptionService = subscriptionService;
        }

        public async Task<IActionResult> Index()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var store = await _dbContext.Stores
                .Include(s => s.Subscriptions)
                    .ThenInclude(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s => s.SellerId == userId);

            if (store == null)
            {
                TempData["ErrorMessage"] = "لم يتم العثور على متجر مسجل لحسابك.";
                return RedirectToAction("Index", "Home", new { area = "" });
            }

            var usage = await _subscriptionService.GetStoreUsageAsync(store.Id);

            var storeOrders = await _dbContext.StoreOrders
                .Include(so => so.ParentOrder)
                .Include(so => so.Items)
                .Where(so => so.StoreId == store.Id)
                .OrderByDescending(so => so.UpdatedAt)
                .ToListAsync();

            decimal totalSales = storeOrders
                .Where(so => so.Status != OrderStatus.Canceled)
                .Sum(so => so.TotalAmount);

            int pendingOrders = storeOrders.Count(so => so.Status == OrderStatus.Pending || so.Status == OrderStatus.Confirmed);

            var vm = new SellerDashboardViewModel
            {
                Store = store,
                Usage = usage,
                TotalSales = totalSales,
                PendingOrdersCount = pendingOrders,
                TotalOrdersCount = storeOrders.Count,
                RecentOrders = storeOrders.Take(5).ToList()
            };

            return View(vm);
        }
    }
}
