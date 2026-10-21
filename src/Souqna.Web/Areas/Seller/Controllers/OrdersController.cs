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
    public class OrdersController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly IOrderService _orderService;

        public OrdersController(SouqnaDbContext dbContext, IOrderService orderService)
        {
            _dbContext = dbContext;
            _orderService = orderService;
        }

        private async Task<Store?> GetCurrentStoreAsync()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await _dbContext.Stores.FirstOrDefaultAsync(s => s.SellerId == userId);
        }

        public async Task<IActionResult> Index(OrderStatus? status)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var query = _dbContext.StoreOrders
                .Include(so => so.ParentOrder)
                .Include(so => so.Items)
                    .ThenInclude(i => i.Product)
                .Where(so => so.StoreId == store.Id);

            if (status.HasValue)
            {
                query = query.Where(so => so.Status == status.Value);
            }

            var orders = await query.OrderByDescending(so => so.UpdatedAt).ToListAsync();
            ViewBag.CurrentStatus = status;
            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var storeOrder = await _dbContext.StoreOrders
                .Include(so => so.ParentOrder)
                .Include(so => so.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(so => so.Id == id && so.StoreId == store.Id);

            if (storeOrder == null) return NotFound();

            return View(storeOrder);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatus newStatus, string? trackingNumber)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            bool updated = await _orderService.UpdateStoreOrderStatusAsync(id, store.Id, newStatus, trackingNumber);
            if (updated)
            {
                TempData["SuccessMessage"] = $"تم تحديث حالة الطلب إلى: {newStatus}";
            }
            else
            {
                TempData["ErrorMessage"] = "تعذر تحديث حالة الطلب.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
