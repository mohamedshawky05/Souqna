using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Enums;
using Souqna.Infrastructure.Data;

namespace Souqna.Web.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public OrderController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> MyOrders()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var orders = await _dbContext.Orders
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Store)
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Items)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var order = await _dbContext.Orders
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Store)
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Items)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var order = await _dbContext.Orders
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Items)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null) return NotFound();

            if (order.Status != OrderStatus.Pending)
            {
                TempData["ErrorMessage"] = "لا يمكن إلغاء الطلب لأنه قيد التنفيذ أو تم شحنه بالفعل.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = OrderStatus.Canceled;
            foreach (var so in order.StoreOrders)
            {
                so.Status = OrderStatus.Canceled;
                // Restore inventory stock
                foreach (var item in so.Items)
                {
                    var product = await _dbContext.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                    }
                }
            }

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إلغاء الطلب واسترجاع المخزون بنجاح.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
