using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Enums;
using Souqna.Infrastructure.Data;
using Souqna.Web.Models;
using Souqna.Web.ViewModels;

namespace Souqna.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly SouqnaDbContext _dbContext;

        public HomeController(SouqnaDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _dbContext.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Take(8)
                .ToListAsync();

            var featuredProducts = await _dbContext.Products
                .Include(p => p.Store)
                .Include(p => p.Images)
                .Where(p => p.IsPublished && !p.IsArchived && p.IsFeatured)
                .OrderByDescending(p => p.CreatedAt)
                .Take(8)
                .ToListAsync();

            var latestProducts = await _dbContext.Products
                .Include(p => p.Store)
                .Include(p => p.Images)
                .Where(p => p.IsPublished && !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .Take(8)
                .ToListAsync();

            var topStores = await _dbContext.Stores
                .Include(s => s.Products)
                .Where(s => s.IsActive && s.IsVerified)
                .Take(4)
                .ToListAsync();

            var latestReels = await _dbContext.Videos
                .Include(v => v.Store)
                .Include(v => v.Product)
                .Where(v => v.Status == VideoStatus.Published)
                .OrderByDescending(v => v.CreatedAt)
                .Take(6)
                .ToListAsync();

            var vm = new HomeViewModel
            {
                Categories = categories,
                FeaturedProducts = featuredProducts,
                LatestProducts = latestProducts,
                TopStores = topStores,
                LatestReels = latestReels
            };

            return View(vm);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
