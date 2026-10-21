using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Infrastructure.Data;

namespace Souqna.Web.Controllers
{
    public class CartController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public CartController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private async Task<Cart> GetOrCreateCartAsync()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? anonId = Request.Cookies["souqna_cart_anon"];

            if (string.IsNullOrEmpty(anonId))
            {
                anonId = Guid.NewGuid().ToString("N");
                Response.Cookies.Append("souqna_cart_anon", anonId, new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    HttpOnly = true,
                    IsEssential = true
                });
            }

            Cart? cart = null;
            if (!string.IsNullOrEmpty(userId))
            {
                cart = await _dbContext.Carts
                    .Include(c => c.Items)
                        .ThenInclude(i => i.Product)
                            .ThenInclude(p => p!.Store)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                // If user logged in and has an anon cart, merge it
                if (!string.IsNullOrEmpty(anonId))
                {
                    var anonCart = await _dbContext.Carts
                        .Include(c => c.Items)
                        .FirstOrDefaultAsync(c => c.AnonymousId == anonId);

                    if (anonCart != null && anonCart.Items.Any())
                    {
                        if (cart == null)
                        {
                            anonCart.UserId = userId;
                            anonCart.AnonymousId = null;
                            cart = anonCart;
                        }
                        else
                        {
                            foreach (var aItem in anonCart.Items)
                            {
                                var existing = cart.Items.FirstOrDefault(i => i.ProductId == aItem.ProductId);
                                if (existing != null)
                                {
                                    existing.Quantity += aItem.Quantity;
                                }
                                else
                                {
                                    cart.Items.Add(new CartItem
                                    {
                                        ProductId = aItem.ProductId,
                                        Quantity = aItem.Quantity,
                                        UnitPrice = aItem.UnitPrice
                                    });
                                }
                            }
                            _dbContext.Carts.Remove(anonCart);
                        }
                        await _dbContext.SaveChangesAsync();
                    }
                }
            }
            else
            {
                cart = await _dbContext.Carts
                    .Include(c => c.Items)
                        .ThenInclude(i => i.Product)
                            .ThenInclude(p => p!.Store)
                    .FirstOrDefaultAsync(c => c.AnonymousId == anonId);
            }

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    AnonymousId = string.IsNullOrEmpty(userId) ? anonId : null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.Carts.Add(cart);
                await _dbContext.SaveChangesAsync();
            }

            return cart;
        }

        public async Task<IActionResult> Index()
        {
            var cart = await GetOrCreateCartAsync();
            var items = cart.Items
                .Where(i => i.Product != null && !i.Product.IsArchived && i.Product.IsPublished)
                .Select(i => new CartItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product!.Name,
                    ProductImage = i.Product.Images.FirstOrDefault()?.ImageUrl,
                    StoreId = i.Product.StoreId,
                    StoreName = i.Product.Store?.StoreName ?? "متجر",
                    UnitPrice = i.Product.Price,
                    Quantity = i.Quantity,
                    StockQuantity = i.Product.StockQuantity
                }).ToList();

            decimal subtotal = items.Sum(i => i.TotalPrice);
            // Multi-vendor flat delivery 35 EGP per distinct store
            int distinctStores = items.Select(i => i.StoreId).Distinct().Count();
            decimal deliveryFee = distinctStores * 35.00m;

            var dto = new CartDto
            {
                Id = cart.Id,
                Items = items,
                Subtotal = subtotal,
                DeliveryFee = deliveryFee,
                Total = subtotal + deliveryFee,
                TotalItems = items.Sum(i => i.Quantity)
            };

            return View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CartAddRequest request)
        {
            if (request == null || request.ProductId <= 0 || request.Quantity <= 0)
            {
                return Json(new { success = false, message = "بيانات الطلب غير صحيحة" });
            }

            var product = await _dbContext.Products
                .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsPublished && !p.IsArchived);

            if (product == null)
            {
                return Json(new { success = false, message = "المنتج غير متوفر حالياً" });
            }

            if (product.StockQuantity < request.Quantity)
            {
                return Json(new { success = false, message = $"الكمية المتاحة في المخزن هي {product.StockQuantity} فقط" });
            }

            var cart = await GetOrCreateCartAsync();
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);

            if (existingItem != null)
            {
                int newQty = existingItem.Quantity + request.Quantity;
                if (newQty > product.StockQuantity)
                {
                    return Json(new { success = false, message = $"لا يمكنك إضافة أكثر من {product.StockQuantity} قطع من هذا المنتج" });
                }
                existingItem.Quantity = newQty;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    CartId = cart.Id,
                    ProductId = product.Id,
                    UnitPrice = product.Price,
                    Quantity = request.Quantity
                });
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            int totalItems = cart.Items.Sum(i => i.Quantity);
            return Json(new { success = true, message = "تمت إضافة المنتج إلى السلة بنجاح", totalItems });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity([FromBody] CartUpdateRequest request)
        {
            if (request == null || request.ProductId <= 0)
            {
                return Json(new { success = false, message = "طلب غير صالح" });
            }

            var cart = await GetOrCreateCartAsync();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
            if (item == null)
            {
                return Json(new { success = false, message = "المنتج غير موجود في السلة" });
            }

            var product = await _dbContext.Products.FindAsync(request.ProductId);
            if (product == null)
            {
                return Json(new { success = false, message = "المنتج غير موجود" });
            }

            if (request.Quantity <= 0)
            {
                _dbContext.CartItems.Remove(item);
            }
            else
            {
                if (request.Quantity > product.StockQuantity)
                {
                    return Json(new { success = false, message = $"المخزون المتاح هو {product.StockQuantity} فقط" });
                }
                item.Quantity = request.Quantity;
            }

            await _dbContext.SaveChangesAsync();

            // Recalculate
            var items = cart.Items.ToList();
            decimal subtotal = items.Sum(i => i.UnitPrice * i.Quantity);
            int storesCount = items.Select(i => i.Product?.StoreId ?? 0).Distinct().Count();
            decimal deliveryFee = storesCount * 35.00m;

            return Json(new
            {
                success = true,
                totalItems = items.Sum(i => i.Quantity),
                subtotal,
                deliveryFee,
                total = subtotal + deliveryFee
            });
        }

        [HttpPost]
        public async Task<IActionResult> Remove(int productId)
        {
            var cart = await GetOrCreateCartAsync();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                _dbContext.CartItems.Remove(item);
                await _dbContext.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "تم حذف المنتج من السلة";
            return RedirectToAction(nameof(Index));
        }
    }

    public class CartAddRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class CartUpdateRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
