using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Infrastructure.Data;
using Souqna.Web.ViewModels;

namespace Souqna.Web.Controllers
{
    public class ProductController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public ProductController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Details(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return NotFound();

            var product = await _dbContext.Products
                .Include(p => p.Store)
                    .ThenInclude(s => s!.Seller)
                .Include(p => p.Category)
                .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                .Include(p => p.Variants)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished && !p.IsArchived);

            if (product == null) return NotFound();

            // Increment view count
            product.ViewCount++;
            await _dbContext.SaveChangesAsync();

            // Related products in same category
            var relatedProducts = await _dbContext.Products
                .Include(p => p.Images)
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id && p.IsPublished && !p.IsArchived)
                .Take(4)
                .ToListAsync();

            double avgRating = product.Reviews.Any() ? product.Reviews.Average(r => r.Rating) : 5.0;

            // Check if user is eligible to review (has purchased item in a delivered order)
            bool canReview = false;
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                canReview = await _dbContext.StoreOrders
                    .Include(so => so.ParentOrder)
                    .Include(so => so.Items)
                    .AnyAsync(so => so.ParentOrder!.UserId == userId &&
                                    so.Status == OrderStatus.Delivered &&
                                    so.Items.Any(i => i.ProductId == product.Id));
            }

            var vm = new ProductDetailsViewModel
            {
                Product = product,
                RelatedProducts = relatedProducts,
                AverageRating = avgRating,
                TotalReviews = product.Reviews.Count,
                CanReview = canReview
            };

            return View(vm);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int productId, int rating, string comment)
        {
            var product = await _dbContext.Products.FindAsync(productId);
            if (product == null) return NotFound();

            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            // Prevent duplicate reviews from same user for same product
            bool existing = await _dbContext.Reviews
                .AnyAsync(r => r.ProductId == productId && r.UserId == userId);

            if (existing)
            {
                TempData["ErrorMessage"] = "لقد قمت بتقييم هذا المنتج مسبقاً.";
                return RedirectToAction(nameof(Details), new { slug = product.Slug });
            }

            var review = new Review
            {
                ProductId = productId,
                StoreId = product.StoreId,
                UserId = userId,
                Rating = Math.Clamp(rating, 1, 5),
                Comment = comment ?? string.Empty,
                IsApproved = true
            };

            _dbContext.Reviews.Add(review);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "شكراً لك! تم إضافة تقييمك بنجاح.";
            return RedirectToAction(nameof(Details), new { slug = product.Slug });
        }
    }
}
