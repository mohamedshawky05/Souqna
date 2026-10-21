using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;

namespace Souqna.Web.Areas.Seller.Controllers
{
    [Area("Seller")]
    [Authorize(Roles = "Seller,Admin")]
    public class StoreSettingsController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly IStorageService _storageService;

        public StoreSettingsController(SouqnaDbContext dbContext, IStorageService storageService)
        {
            _dbContext = dbContext;
            _storageService = storageService;
        }

        private async Task<Core.Entities.Store?> GetCurrentStoreAsync()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await _dbContext.Stores.FirstOrDefaultAsync(s => s.SellerId == userId);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            return View(store);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(
            string storeName, 
            string description, 
            string city, 
            string address, 
            string contactPhone, 
            string contactEmail,
            IFormFile? logoFile,
            IFormFile? coverFile)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            store.StoreName = storeName;
            store.Description = description;
            store.City = city;
            store.Address = address;
            store.ContactPhone = contactPhone;
            store.ContactEmail = contactEmail;

            if (logoFile != null && logoFile.Length > 0)
            {
                using var stream = logoFile.OpenReadStream();
                store.LogoUrl = await _storageService.SavePublicFileAsync(stream, logoFile.FileName, "stores");
            }

            if (coverFile != null && coverFile.Length > 0)
            {
                using var stream = coverFile.OpenReadStream();
                store.CoverUrl = await _storageService.SavePublicFileAsync(stream, coverFile.FileName, "stores");
            }

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حفظ إعدادات المتجر بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}
