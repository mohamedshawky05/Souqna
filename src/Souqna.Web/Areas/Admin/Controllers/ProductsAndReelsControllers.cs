using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Enums;
using Souqna.Infrastructure.Data;

namespace Souqna.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public ProductsController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(string? q)
        {
            var query = _dbContext.Products
                .Include(p => p.Store)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(p => p.Name.Contains(q.Trim()) || p.Store!.StoreName.Contains(q.Trim()));
            }

            var products = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            return View(products);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFeatured(int id)
        {
            var product = await _dbContext.Products.FindAsync(id);
            if (product != null)
            {
                product.IsFeatured = !product.IsFeatured;
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = product.IsFeatured ? "تم تمييز المنتج بالصفحة الرئيسية." : "تم إلغاء التمييز.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublish(int id)
        {
            var product = await _dbContext.Products.FindAsync(id);
            if (product != null)
            {
                product.IsPublished = !product.IsPublished;
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = product.IsPublished ? "تم نشر المنتج." : "تم إخفاء المنتج لمراجعة الإدارة.";
            }
            return RedirectToAction(nameof(Index));
        }
    }

    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReelsController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public ReelsController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var videos = await _dbContext.Videos
                .Include(v => v.Store)
                .Include(v => v.Product)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            return View(videos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSponsored(int id)
        {
            var video = await _dbContext.Videos.FindAsync(id);
            if (video != null)
            {
                video.IsSponsored = !video.IsSponsored;
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = video.IsSponsored ? "تم تحديد الفيديو كفيديو ممول (Sponsored)." : "تم إلغاء التمويل.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var video = await _dbContext.Videos.FindAsync(id);
            if (video != null)
            {
                video.Status = VideoStatus.Archived;
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف الفيديو بنجاح.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
