using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;

namespace Souqna.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class VerificationController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly IIdentityVerificationService _verificationService;
        private readonly IStorageService _storageService;

        public VerificationController(
            SouqnaDbContext dbContext,
            IIdentityVerificationService verificationService,
            IStorageService storageService)
        {
            _dbContext = dbContext;
            _verificationService = verificationService;
            _storageService = storageService;
        }

        public async Task<IActionResult> Index(VerificationStatus? status)
        {
            var query = _dbContext.IdentityVerifications
                .Include(iv => iv.Store)
                    .ThenInclude(s => s!.Seller)
                .Include(iv => iv.Documents)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(iv => iv.Status == status.Value);
            }

            var requests = await query.OrderByDescending(iv => iv.CreatedAt).ToListAsync();
            ViewBag.CurrentStatus = status;
            return View(requests);
        }

        public async Task<IActionResult> Review(int id)
        {
            var request = await _dbContext.IdentityVerifications
                .Include(iv => iv.Store)
                    .ThenInclude(s => s!.Seller)
                .Include(iv => iv.Documents)
                .FirstOrDefaultAsync(iv => iv.Id == id);

            if (request == null) return NotFound();

            if (request.Status == VerificationStatus.Pending)
            {
                request.Status = VerificationStatus.UnderReview;
                await _dbContext.SaveChangesAsync();
            }

            return View(request);
        }

        [HttpGet]
        public async Task<IActionResult> Document(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return NotFound();

            var docData = await _storageService.GetPrivateDocumentAsync(key);
            if (docData == null) return NotFound();

            return File(docData.Value.Stream, docData.Value.ContentType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitDecision(int id, VerificationStatus decision, string? notes)
        {
            string reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            bool success = await _verificationService.ReviewVerificationAsync(id, reviewerId, decision, notes);

            if (success)
            {
                TempData["SuccessMessage"] = $"تم حفظ القرار بنجاح: {decision}";
            }
            else
            {
                TempData["ErrorMessage"] = "فشلت عملية حفظ القرار.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
