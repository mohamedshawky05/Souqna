using System;
using System.Linq;
using System.Security.Claims;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;
using Souqna.Web.ViewModels;

namespace Souqna.Web.Areas.Seller.Controllers
{
    [Area("Seller")]
    [Authorize(Roles = "Seller,Admin")]
    public class ReelsController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IVideoProcessingService _videoProcessingService;
        private readonly IStorageService _storageService;

        public ReelsController(
            SouqnaDbContext dbContext,
            ISubscriptionService subscriptionService,
            IVideoProcessingService videoProcessingService,
            IStorageService storageService)
        {
            _dbContext = dbContext;
            _subscriptionService = subscriptionService;
            _videoProcessingService = videoProcessingService;
            _storageService = storageService;
        }

        private async Task<Store?> GetCurrentStoreAsync()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await _dbContext.Stores.FirstOrDefaultAsync(s => s.SellerId == userId);
        }

        public async Task<IActionResult> Index()
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var reels = await _dbContext.Videos
                .Include(v => v.Product)
                .Where(v => v.StoreId == store.Id)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            ViewBag.Usage = await _subscriptionService.GetStoreUsageAsync(store.Id);
            return View(reels);
        }

        [HttpGet]
        public async Task<IActionResult> Upload()
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var usage = await _subscriptionService.GetStoreUsageAsync(store.Id);
            if (usage.PublishedVideosCount >= usage.MaxVideos)
            {
                TempData["ErrorMessage"] = $"لقد وصلت إلى الحد الأقصى المسموح به من الفيديوهات ({usage.MaxVideos}) في باقتك الحالية. يرجى ترقية الباقة لرفع المزيد.";
                return RedirectToAction("Index", "Subscriptions", new { area = "Seller" });
            }

            var products = await _dbContext.Products
                .Where(p => p.StoreId == store.Id && p.IsPublished && !p.IsArchived)
                .ToListAsync();

            ViewBag.Products = new SelectList(products, "Id", "Name");
            ViewBag.MaxDurationSeconds = usage.MaxVideoDurationSeconds;
            return View(new VideoUploadViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(VideoUploadViewModel model)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var usage = await _subscriptionService.GetStoreUsageAsync(store.Id);
            if (usage.PublishedVideosCount >= usage.MaxVideos)
            {
                TempData["ErrorMessage"] = $"لقد وصلت إلى الحد الأقصى المسموح به من الفيديوهات ({usage.MaxVideos}). يرجى ترقية باقتك.";
                return RedirectToAction("Index", "Subscriptions", new { area = "Seller" });
            }

            if (model.VideoFile == null || model.VideoFile.Length == 0)
            {
                ModelState.AddModelError(nameof(model.VideoFile), "يرجى اختيار ملف فيديو صالح.");
            }

            if (!ModelState.IsValid)
            {
                var products = await _dbContext.Products
                    .Where(p => p.StoreId == store.Id && p.IsPublished && !p.IsArchived)
                    .ToListAsync();
                ViewBag.Products = new SelectList(products, "Id", "Name", model.ProductId);
                ViewBag.MaxDurationSeconds = usage.MaxVideoDurationSeconds;
                return View(model);
            }

            if (model.ProductId.HasValue &&
                !await _dbContext.Products.AnyAsync(p =>
                    p.Id == model.ProductId.Value &&
                    p.StoreId == store.Id &&
                    !p.IsArchived))
            {
                ModelState.AddModelError(
                    nameof(model.ProductId),
                    "المنتج المحدد غير متاح في متجرك.");
                var products = await _dbContext.Products
                    .Where(p => p.StoreId == store.Id && !p.IsArchived)
                    .ToListAsync();
                ViewBag.Products = new SelectList(products, "Id", "Name", model.ProductId);
                ViewBag.MaxDurationSeconds = usage.MaxVideoDurationSeconds;
                return View(model);
            }

            using var stream = model.VideoFile!.OpenReadStream();
            var validation = await _videoProcessingService.ValidateAndProcessVideoAsync(
                stream, 
                model.VideoFile.FileName, 
                usage.MaxVideoDurationSeconds);

            if (!validation.IsValid)
            {
                ModelState.AddModelError(string.Empty, validation.ErrorMessage ?? "فشل التحقق من صحة ملف الفيديو.");
                var products = await _dbContext.Products
                    .Where(p => p.StoreId == store.Id && p.IsPublished && !p.IsArchived)
                    .ToListAsync();
                ViewBag.Products = new SelectList(products, "Id", "Name", model.ProductId);
                ViewBag.MaxDurationSeconds = usage.MaxVideoDurationSeconds;
                return View(model);
            }

            var video = new Video
            {
                StoreId = store.Id,
                ProductId = model.ProductId,
                Title = model.Title,
                Description = model.Description,
                VideoUrl = validation.RelativeVideoUrl!,
                ThumbnailUrl = validation.RelativeThumbnailUrl,
                DurationSeconds = validation.DurationSeconds,
                FileSizeBytes = validation.FileSizeBytes,
                Status = VideoStatus.Published,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Videos.Add(video);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم نشر فيديو الريلز بنجاح! يمكن للمشترين الآن مشاهدته وطلب المنتج مباشرة.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var video = await _dbContext.Videos
                .FirstOrDefaultAsync(v =>
                    v.Id == id &&
                    v.StoreId == store.Id &&
                    v.Status != VideoStatus.Archived);
            if (video == null) return NotFound();

            video.Status = VideoStatus.Archived;
            await _dbContext.SaveChangesAsync();

            if (IsLocalUpload(video.VideoUrl) &&
                !await TryDeleteVideoFileAsync(video.VideoUrl))
            {
                TempData["WarningMessage"] =
                    "تمت أرشفة الفيديو، لكن تعذر حذف ملفه من التخزين.";
            }
            else
            {
                TempData["SuccessMessage"] = "تمت أرشفة الفيديو بنجاح.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var video = await _dbContext.Videos
                .FirstOrDefaultAsync(v =>
                    v.Id == id &&
                    v.StoreId == store.Id &&
                    v.Status != VideoStatus.Archived);

            if (video == null) return NotFound();

            await PopulateProductsAsync(store.Id, video.ProductId);
            ViewBag.MaxDurationSeconds =
                (await _subscriptionService.GetStoreUsageAsync(store.Id))
                .MaxVideoDurationSeconds;

            return View(new VideoEditViewModel
            {
                Id = video.Id,
                Title = video.Title,
                Description = video.Description,
                ProductId = video.ProductId,
                VideoUrl = video.VideoUrl
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(VideoEditViewModel model)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var video = await _dbContext.Videos
                .FirstOrDefaultAsync(v =>
                    v.Id == model.Id &&
                    v.StoreId == store.Id &&
                    v.Status != VideoStatus.Archived);

            if (video == null) return NotFound();

            if (model.VideoFile is { Length: > 0 } file &&
                (file.Length > 100 * 1024 * 1024 ||
                 !IsSupportedVideoFile(file.FileName)))
            {
                ModelState.AddModelError(
                    nameof(model.VideoFile),
                    "يجب أن يكون الفيديو بصيغة MP4 أو WebM وألا يتجاوز حجمه 100 ميجابايت.");
            }

            if (model.ProductId.HasValue &&
                !await _dbContext.Products.AnyAsync(p =>
                    p.Id == model.ProductId.Value &&
                    p.StoreId == store.Id &&
                    !p.IsArchived))
            {
                ModelState.AddModelError(nameof(model.ProductId), "المنتج المحدد غير متاح في متجرك.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateProductsAsync(store.Id, model.ProductId);
                ViewBag.MaxDurationSeconds =
                    (await _subscriptionService.GetStoreUsageAsync(store.Id))
                    .MaxVideoDurationSeconds;
                return View(model);
            }

            string? newVideoUrl = null;
            if (model.VideoFile is { Length: > 0 } videoFile)
            {
                var usage = await _subscriptionService.GetStoreUsageAsync(store.Id);
                using var stream = videoFile.OpenReadStream();
                var validation =
                    await _videoProcessingService.ValidateAndProcessVideoAsync(
                        stream,
                        videoFile.FileName,
                        usage.MaxVideoDurationSeconds);

                if (!validation.IsValid)
                {
                    ModelState.AddModelError(
                        nameof(model.VideoFile),
                        validation.ErrorMessage ?? "تعذر التحقق من ملف الفيديو.");
                    await PopulateProductsAsync(store.Id, model.ProductId);
                    ViewBag.MaxDurationSeconds = usage.MaxVideoDurationSeconds;
                    return View(model);
                }

                newVideoUrl = validation.RelativeVideoUrl;
                video.DurationSeconds = validation.DurationSeconds;
                video.FileSizeBytes = validation.FileSizeBytes;
                video.ThumbnailUrl = validation.RelativeThumbnailUrl;
            }

            string previousVideoUrl = video.VideoUrl;
            video.Title = model.Title.Trim();
            video.Description = model.Description?.Trim();
            video.ProductId = model.ProductId;

            if (newVideoUrl != null)
                video.VideoUrl = newVideoUrl;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch
            {
                if (newVideoUrl != null)
                    await TryDeleteVideoFileAsync(newVideoUrl);

                throw;
            }

            if (newVideoUrl != null &&
                IsLocalUpload(previousVideoUrl) &&
                !await TryDeleteVideoFileAsync(previousVideoUrl))
            {
                TempData["WarningMessage"] =
                    "تم حفظ التعديلات، لكن تعذر حذف ملف الفيديو السابق من التخزين.";
            }
            else
            {
                TempData["SuccessMessage"] = "تم تحديث بيانات الفيديو بنجاح.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateProductsAsync(int storeId, int? selectedProductId)
        {
            var products = await _dbContext.Products
                .Where(p => p.StoreId == storeId && !p.IsArchived)
                .ToListAsync();

            ViewBag.Products = new SelectList(
                products,
                "Id",
                "Name",
                selectedProductId);
        }

        private static bool IsSupportedVideoFile(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension is ".mp4" or ".webm";
        }

        private static bool IsLocalUpload(string fileUrl) =>
            fileUrl.StartsWith("/uploads/reels/", StringComparison.Ordinal);

        private async Task<bool> TryDeleteVideoFileAsync(string fileUrl)
        {
            return await _storageService.DeleteFileAsync(fileUrl);
        }
    }
}
