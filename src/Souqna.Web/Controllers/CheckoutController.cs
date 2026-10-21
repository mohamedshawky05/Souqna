using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;

namespace Souqna.Web.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly IOrderService _orderService;

        public CheckoutController(SouqnaDbContext dbContext, IOrderService orderService)
        {
            _dbContext = dbContext;
            _orderService = orderService;
        }

        public async Task<IActionResult> Index()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? anonId = Request.Cookies["souqna_cart_anon"];

            var cart = await _dbContext.Carts
                .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p!.Store)
                .FirstOrDefaultAsync(c => 
                    (!string.IsNullOrEmpty(userId) && c.UserId == userId) || 
                    (!string.IsNullOrEmpty(anonId) && c.AnonymousId == anonId));

            if (cart == null || !cart.Items.Any())
            {
                TempData["ErrorMessage"] = "سلة المشتريات فارغة، يرجى إضافة منتجات أولاً.";
                return RedirectToAction("Index", "Cart");
            }

            var items = cart.Items.Where(i => i.Product != null && !i.Product.IsArchived && i.Product.IsPublished).ToList();
            decimal subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
            int storesCount = items.Select(i => i.Product!.StoreId).Distinct().Count();
            decimal deliveryFee = storesCount * 35.00m;

            ViewBag.Subtotal = subtotal;
            ViewBag.DeliveryFee = deliveryFee;
            ViewBag.Total = subtotal + deliveryFee;
            ViewBag.StoresCount = storesCount;
            ViewBag.CartItems = items;

            var dto = new CheckoutDto();
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user != null)
                {
                    dto.FullName = user.FullName;
                    dto.Phone = user.PhoneNumber ?? string.Empty;
                }
            }

            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(CheckoutDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Phone) || string.IsNullOrWhiteSpace(dto.StreetAddress))
            {
                TempData["ErrorMessage"] = "يرجى استكمال جميع بيانات العنوان ورقم الهاتف.";
                return RedirectToAction(nameof(Index));
            }

            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? anonId = Request.Cookies["souqna_cart_anon"];

            try
            {
                var order = await _orderService.CreateOrderFromCartAsync(userId, anonId, dto);
                TempData["SuccessMessage"] = $"تم تأكيد طلبك بنجاح برقم {order.OrderNumber}!";
                return RedirectToAction("Confirmation", new { id = order.Id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Confirmation(int id)
        {
            var order = await _dbContext.Orders
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Store)
                .Include(o => o.StoreOrders)
                    .ThenInclude(so => so.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }
    }
}
