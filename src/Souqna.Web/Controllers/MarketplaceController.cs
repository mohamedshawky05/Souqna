using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Infrastructure.Data;
using Souqna.Web.ViewModels;

namespace Souqna.Web.Controllers
{
    public class MarketplaceController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private const int PageSize = 12;

        public MarketplaceController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(
            string? q, 
            int? categoryId, 
            decimal? minPrice, 
            decimal? maxPrice, 
            string? city, 
            bool inStockOnly = false, 
            bool verifiedOnly = false, 
            string sortBy = "newest", 
            int page = 1)
        {
            if (page < 1) page = 1;

            var query = _dbContext.Products
                .Include(p => p.Store)
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Where(p => p.IsPublished && !p.IsArchived);

            // Search by keyword
            if (!string.IsNullOrWhiteSpace(q))
            {
                string cleanQuery = q.Trim();
                query = query.Where(p => p.Name.Contains(cleanQuery) || 
                                         p.Description.Contains(cleanQuery) || 
                                         (p.Store != null && p.Store.StoreName.Contains(cleanQuery)));
            }

            // Filter by Category
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            // Filter by Price range
            if (minPrice.HasValue && minPrice.Value > 0)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }
            if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            // Filter by City
            if (!string.IsNullOrWhiteSpace(city))
            {
                query = query.Where(p => p.Store != null && p.Store.City == city);
            }

            // In Stock only
            if (inStockOnly)
            {
                query = query.Where(p => p.StockQuantity > 0);
            }

            // Verified Store only
            if (verifiedOnly)
            {
                query = query.Where(p => p.Store != null && p.Store.IsVerified);
            }

            // Sorting
            query = sortBy switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "popular" => query.OrderByDescending(p => p.ViewCount),
                _ => query.OrderByDescending(p => p.CreatedAt) // "newest"
            };

            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalCount / PageSize);
            if (totalPages < 1) totalPages = 1;

            var products = await query
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var categories = await _dbContext.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            var vm = new MarketplaceViewModel
            {
                Products = products,
                Categories = categories,
                Query = q,
                CategoryId = categoryId,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                City = city,
                InStockOnly = inStockOnly,
                VerifiedOnly = verifiedOnly,
                SortBy = sortBy,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount
            };

            return View(vm);
        }
    }
}
