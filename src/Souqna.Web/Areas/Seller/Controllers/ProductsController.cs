using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Entities;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;
using Souqna.Web.ViewModels;

namespace Souqna.Web.Areas.Seller.Controllers
{
    [Area("Seller")]
    [Authorize(Roles = "Seller,Admin")]
    public class ProductsController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IStorageService _storageService;

        public ProductsController(
            SouqnaDbContext dbContext,
            ISubscriptionService subscriptionService,
            IStorageService storageService)
        {
            _dbContext = dbContext;
            _subscriptionService = subscriptionService;
            _storageService = storageService;
        }

        private async Task<Store?> GetCurrentStoreAsync()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await _dbContext.Stores.FirstOrDefaultAsync(s => s.SellerId == userId);
        }

        public async Task<IActionResult> Index(string? q)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var query = _dbContext.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Where(p => p.StoreId == store.Id && !p.IsArchived);

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(p => p.Name.Contains(q.Trim()) || p.SKU!.Contains(q.Trim()));
            }

            var products = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            ViewBag.Usage = await _subscriptionService.GetStoreUsageAsync(store.Id);
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            // Check subscription product limit
            var check = await _subscriptionService.CanAddProductAsync(store.Id);
            if (!check.IsAllowed)
            {
                TempData["ErrorMessage"] = check.Reason;
                return RedirectToAction("Index", "Subscriptions", new { area = "Seller" });
            }

            ViewBag.Categories = new SelectList(await _dbContext.Categories.Where(c => c.IsActive).ToListAsync(), "Id", "NameAr");
            return View(new ProductFormViewModel { StockQuantity = 10, Price = 100 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductFormViewModel model)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            // Server-side subscription quota verification
            var check = await _subscriptionService.CanAddProductAsync(store.Id);
            if (!check.IsAllowed)
            {
                TempData["ErrorMessage"] = check.Reason;
                return RedirectToAction("Index", "Subscriptions", new { area = "Seller" });
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = new SelectList(await _dbContext.Categories.Where(c => c.IsActive).ToListAsync(), "Id", "NameAr", model.CategoryId);
                return View(model);
            }

            string slug = Regex.Replace(model.Name.ToLowerInvariant(), @"[^a-z0-9\u0600-\u06FF]+", "-").Trim('-');
            if (string.IsNullOrEmpty(slug)) slug = "product-" + Guid.NewGuid().ToString("N")[..6];

            if (await _dbContext.Products.AnyAsync(p => p.Slug == slug))
            {
                slug += "-" + Random.Shared.Next(100, 999);
            }

            var product = new Product
            {
                StoreId = store.Id,
                CategoryId = model.CategoryId,
                Name = model.Name,
                Slug = slug,
                Description = model.Description,
                Price = model.Price,
                SalePrice = model.SalePrice,
                StockQuantity = model.StockQuantity,
                SKU = model.SKU,
                IsPublished = model.IsPublished,
                CreatedAt = DateTime.UtcNow
            };

            // Process image upload
            if (model.MainImage != null && model.MainImage.Length > 0)
            {
                using var stream = model.MainImage.OpenReadStream();
                string imgUrl = await _storageService.SavePublicFileAsync(stream, model.MainImage.FileName, "products");
                product.Images.Add(new ProductImage
                {
                    ImageUrl = imgUrl,
                    IsMain = true,
                    DisplayOrder = 1
                });
            }
            else
            {
                // Default product placeholder image
                product.Images.Add(new ProductImage
                {
                    ImageUrl = "https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=600",
                    IsMain = true,
                    DisplayOrder = 1
                });
            }

            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إضافة المنتج بنجاح!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var product = await _dbContext.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p =>
                    p.Id == id &&
                    p.StoreId == store.Id &&
                    !p.IsArchived);

            if (product == null) return NotFound();

            var vm = new ProductFormViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                SalePrice = product.SalePrice,
                StockQuantity = product.StockQuantity,
                SKU = product.SKU,
                CategoryId = product.CategoryId,
                IsPublished = product.IsPublished,
                ExistingImageUrl = product.Images.FirstOrDefault(image => image.IsMain)?.ImageUrl,
                AdditionalImageUrls = product.Images
                    .Where(image => !image.IsMain)
                    .OrderBy(image => image.DisplayOrder)
                    .Select(image => image.ImageUrl)
                    .ToList()
            };

            ViewBag.Categories = new SelectList(await _dbContext.Categories.Where(c => c.IsActive).ToListAsync(), "Id", "NameAr", product.CategoryId);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductFormViewModel model)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var product = await _dbContext.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p =>
                    p.Id == model.Id &&
                    p.StoreId == store.Id &&
                    !p.IsArchived);

            if (product == null) return NotFound();

            bool validCategory = await _dbContext.Categories.AnyAsync(c =>
                c.Id == model.CategoryId && c.IsActive);
            bool validMainImage = await IsValidImageAsync(model.MainImage);
            bool validAdditionalImages = true;
            foreach (var image in model.AdditionalImages)
            {
                if (!await IsValidImageAsync(image))
                {
                    validAdditionalImages = false;
                    break;
                }
            }
            bool validImages = validMainImage && validAdditionalImages;

            if (!ModelState.IsValid ||
                !validCategory ||
                (model.SalePrice.HasValue && model.SalePrice.Value > model.Price) ||
                !validImages)
            {
                if (!ModelState.IsValid)
                {
                    await PopulateCategoriesAsync(model.CategoryId);
                    return View(model);
                }

                if (!validCategory)
                {
                    ModelState.AddModelError(nameof(model.CategoryId), "القسم المحدد غير متاح.");
                }
                if (model.SalePrice.HasValue && model.SalePrice.Value > model.Price)
                {
                    ModelState.AddModelError(nameof(model.SalePrice), "سعر الخصم يجب ألا يتجاوز السعر الأساسي.");
                }
                if (!validImages)
                {
                    ModelState.AddModelError(nameof(model.MainImage), "الصور يجب أن تكون JPG أو PNG أو WebP وألا يتجاوز حجم كل صورة 5 ميجابايت.");
                }

                await PopulateCategoriesAsync(model.CategoryId);
                return View(model);
            }

            if (model.IsPublished && !product.IsPublished)
            {
                var quota = await _subscriptionService.CanAddProductAsync(store.Id);
                if (!quota.IsAllowed)
                {
                    TempData["ErrorMessage"] = quota.Reason;
                    return RedirectToAction("Index", "Subscriptions", new { area = "Seller" });
                }
            }

            product.Name = model.Name;
            product.Description = model.Description;
            product.Price = model.Price;
            product.SalePrice = model.SalePrice;
            product.StockQuantity = model.StockQuantity;
            product.SKU = model.SKU;
            product.CategoryId = model.CategoryId;
            product.IsPublished = model.IsPublished;
            product.UpdatedAt = DateTime.UtcNow;

            var previousImages = new List<string>();
            var newImages = new List<string>();
            if (model.MainImage is { Length: > 0 })
            {
                using var stream = model.MainImage.OpenReadStream();
                string imgUrl = await _storageService.SavePublicFileAsync(stream, model.MainImage.FileName, "products");
                newImages.Add(imgUrl);
                
                var mainImg = product.Images.FirstOrDefault(i => i.IsMain);
                if (mainImg != null)
                {
                    previousImages.Add(mainImg.ImageUrl);
                    mainImg.ImageUrl = imgUrl;
                }
                else
                {
                    product.Images.Add(new ProductImage { ImageUrl = imgUrl, IsMain = true });
                }
            }

            foreach (var image in model.AdditionalImages.Where(image => image.Length > 0))
            {
                using var stream = image.OpenReadStream();
                string imageUrl = await _storageService.SavePublicFileAsync(
                    stream,
                    image.FileName,
                    "products");
                newImages.Add(imageUrl);
                product.Images.Add(new ProductImage
                {
                    ImageUrl = imageUrl,
                    IsMain = false,
                    DisplayOrder = product.Images.Count
                });
            }

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch
            {
                foreach (string newImage in newImages)
                    await _storageService.DeleteFileAsync(newImage);

                throw;
            }

            foreach (string previousImage in previousImages)
            {
                if (previousImage.StartsWith("/uploads/products/", StringComparison.Ordinal) &&
                    !await _storageService.DeleteFileAsync(previousImage))
                {
                    TempData["WarningMessage"] =
                        "تم حفظ التعديلات، لكن تعذر حذف صورة سابقة من التخزين.";
                }
            }

            TempData["SuccessMessage"] = "تم تحديث بيانات المنتج بنجاح!";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateCategoriesAsync(int selectedCategoryId)
        {
            ViewBag.Categories = new SelectList(
                await _dbContext.Categories
                    .Where(category => category.IsActive)
                    .ToListAsync(),
                "Id",
                "NameAr",
                selectedCategoryId);
        }

        private static bool IsValidImage(Microsoft.AspNetCore.Http.IFormFile? image)
        {
            if (image == null || image.Length == 0)
                return true;

            string extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            return image.Length <= 5 * 1024 * 1024 &&
                   extension is ".jpg" or ".jpeg" or ".png" or ".webp";
        }

        private static async Task<bool> IsValidImageAsync(
            Microsoft.AspNetCore.Http.IFormFile? image)
        {
            if (!IsValidImage(image))
                return false;
            if (image == null || image.Length == 0)
                return true;

            string extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            byte[] header = new byte[12];
            await using var stream = image.OpenReadStream();
            int bytesRead = await stream.ReadAsync(header.AsMemory());

            return extension switch
            {
                ".jpg" or ".jpeg" =>
                    bytesRead >= 3 &&
                    header[0] == 0xFF &&
                    header[1] == 0xD8 &&
                    header[2] == 0xFF,
                ".png" =>
                    bytesRead >= 8 &&
                    header.AsSpan(0, 8).SequenceEqual(
                        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                ".webp" =>
                    bytesRead >= 12 &&
                    header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                    header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
                _ => false
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublish(int id)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var product = await _dbContext.Products.FirstOrDefaultAsync(p =>
                p.Id == id && p.StoreId == store.Id && !p.IsArchived);
            if (product == null) return NotFound();

            if (!product.IsPublished)
            {
                var check = await _subscriptionService.CanAddProductAsync(store.Id);
                if (!check.IsAllowed)
                {
                    TempData["ErrorMessage"] = check.Reason;
                    return RedirectToAction(nameof(Index));
                }
            }

            product.IsPublished = !product.IsPublished;
            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = product.IsPublished ? "تم نشر المنتج بالسوق." : "تم إخفاء المنتج من السوق.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var product = await _dbContext.Products.FirstOrDefaultAsync(p =>
                p.Id == id && p.StoreId == store.Id && !p.IsArchived);
            if (product == null) return NotFound();

            product.IsArchived = true;
            product.IsPublished = false;
            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم أرشفة وحذف المنتج بنجاح.";

            return RedirectToAction(nameof(Index));
        }
    }
}
