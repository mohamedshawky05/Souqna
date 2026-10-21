using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Enums;
using Souqna.Infrastructure.Data;
using Souqna.Web.ViewModels;

namespace Souqna.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public DashboardController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            int totalUsers = await _dbContext.Users.CountAsync();
            int totalStores = await _dbContext.Stores.CountAsync();
            int verifiedStores = await _dbContext.Stores.CountAsync(s => s.IsVerified);
            int totalProducts = await _dbContext.Products.CountAsync(p => !p.IsArchived);
            int totalVideos = await _dbContext.Videos.CountAsync(v => v.Status == VideoStatus.Published);

            decimal totalGmv = await _dbContext.Orders
                .Where(o => o.Status != OrderStatus.Canceled)
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

            var pendingVerifications = await _dbContext.IdentityVerifications
                .Include(iv => iv.Store)
                .Where(iv => iv.Status == VerificationStatus.Pending || iv.Status == VerificationStatus.UnderReview)
                .OrderBy(iv => iv.CreatedAt)
                .ToListAsync();

            var recentOrders = await _dbContext.Orders
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Store)
                .OrderByDescending(o => o.CreatedAt)
                .Take(6)
                .ToListAsync();

            var vm = new AdminDashboardViewModel
            {
                TotalUsers = totalUsers,
                TotalStores = totalStores,
                VerifiedStores = verifiedStores,
                TotalProducts = totalProducts,
                TotalVideos = totalVideos,
                TotalGmv = totalGmv,
                PendingVerificationsCount = pendingVerifications.Count,
                PendingVerifications = pendingVerifications,
                RecentOrders = recentOrders
            };

            return View(vm);
        }
    }
}
