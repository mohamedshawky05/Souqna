using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Enums;
using Souqna.Infrastructure.Data;
using Souqna.Web.ViewModels;

namespace Souqna.Web.Controllers
{
    public class StoreController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public StoreController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(string? q, string? city)
        {
            var query = _dbContext.Stores
                .Include(s => s.Products)
                .Where(s => s.IsActive);

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(s => s.StoreName.Contains(q.Trim()) || s.Description.Contains(q.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                query = query.Where(s => s.City == city);
            }

            var stores = await query
                .OrderByDescending(s => s.IsVerified)
                .ThenByDescending(s => s.CreatedAt)
                .ToListAsync();

            ViewBag.Query = q;
            ViewBag.City = city;
            return View(stores);
        }

        public async Task<IActionResult> Details(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return NotFound();

            var store = await _dbContext.Stores
                .Include(s => s.StoreAddress)
                .Include(s => s.Seller)
                .Include(s => s.Reviews)
                .FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);

            if (store == null) return NotFound();

            var products = await _dbContext.Products
                .Include(p => p.Images)
                .Where(p => p.StoreId == store.Id && p.IsPublished && !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var reels = await _dbContext.Videos
                .Include(v => v.Product)
                .Where(v => v.StoreId == store.Id && v.Status == VideoStatus.Published)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            double rating = store.Reviews.Any() ? store.Reviews.Average(r => r.Rating) : 5.0;

            var vm = new StoreProfileViewModel
            {
                Store = store,
                Products = products,
                Reels = reels,
                RatingAverage = rating,
                TotalProducts = products.Count
            };

            return View(vm);
        }
    }
}
