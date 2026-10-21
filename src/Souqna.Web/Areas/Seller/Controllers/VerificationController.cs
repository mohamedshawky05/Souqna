using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;
using Souqna.Web.ViewModels;

namespace Souqna.Web.Areas.Seller.Controllers
{
    [Area("Seller")]
    [Authorize(Roles = "Seller,Admin")]
    public class VerificationController : Controller
    {
        private readonly SouqnaDbContext _dbContext;
        private readonly IIdentityVerificationService _verificationService;

        public VerificationController(
            SouqnaDbContext dbContext,
            IIdentityVerificationService verificationService)
        {
            _dbContext = dbContext;
            _verificationService = verificationService;
        }

        private async Task<Store?> GetCurrentStoreAsync()
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return await _dbContext.Stores
                .Include(s => s.Seller)
                .Include(s => s.VerificationRequests)
                    .ThenInclude(v => v.Documents)
                .FirstOrDefaultAsync(s => s.SellerId == userId);
        }

        public async Task<IActionResult> Index()
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            var latestRequest = store.VerificationRequests
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefault();

            ViewBag.Store = store;
            ViewBag.LatestRequest = latestRequest;
            ViewBag.IsPhoneVerified = store.Seller?.PhoneNumberConfirmed ?? false;

            return View(new IdentityVerificationFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(IdentityVerificationFormViewModel model)
        {
            var store = await GetCurrentStoreAsync();
            if (store == null) return NotFound();

            // Validate front ID file
            if (model.FrontIdImage == null || model.FrontIdImage.Length == 0)
            {
                ModelState.AddModelError(nameof(model.FrontIdImage), "يرجى إرفاق صورة واضحة لوجه بطاقة الرقم القومي.");
            }
            else
            {
                string ext = Path.GetExtension(model.FrontIdImage.FileName).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".pdf")
                {
                    ModelState.AddModelError(nameof(model.FrontIdImage), "صيغة الملف غير مدعومة. المقبول JPG أو PNG أو PDF.");
                }
                if (model.FrontIdImage.Length > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(model.FrontIdImage), "الحد الأقصى لحجم الملف هو 10 ميجابايت.");
                }
            }

            // Validate back ID file
            if (model.BackIdImage == null || model.BackIdImage.Length == 0)
            {
                ModelState.AddModelError(nameof(model.BackIdImage), "يرجى إرفاق صورة واضحة لظهر بطاقة الرقم القومي.");
            }
            else
            {
                string ext = Path.GetExtension(model.BackIdImage.FileName).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".pdf")
                {
                    ModelState.AddModelError(nameof(model.BackIdImage), "صيغة الملف غير مدعومة. المقبول JPG أو PNG أو PDF.");
                }
                if (model.BackIdImage.Length > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(model.BackIdImage), "الحد الأقصى لحجم الملف هو 10 ميجابايت.");
                }
            }

            if (!model.PrivacyNoticeAcknowledged)
            {
                ModelState.AddModelError(nameof(model.PrivacyNoticeAcknowledged), "يجب الموافقة الصريحة على إشعار الخصوصية وشروط المعالجة.");
            }

            if (!ModelState.IsValid)
            {
                var latest = store.VerificationRequests.OrderByDescending(v => v.CreatedAt).FirstOrDefault();
                ViewBag.Store = store;
                ViewBag.LatestRequest = latest;
                ViewBag.IsPhoneVerified = store.Seller?.PhoneNumberConfirmed ?? false;
                return View(nameof(Index), model);
            }

            using var frontStream = model.FrontIdImage!.OpenReadStream();
            using var backStream = model.BackIdImage!.OpenReadStream();

            var submission = new IdentityVerificationSubmissionDto
            {
                NationalIdNumber = model.NationalIdNumber,
                FullLegalName = model.FullLegalName,
                PrivacyNoticeAcknowledged = true,
                FrontIdStream = frontStream,
                FrontIdFileName = model.FrontIdImage.FileName,
                FrontIdContentType = model.FrontIdImage.ContentType,
                BackIdStream = backStream,
                BackIdFileName = model.BackIdImage.FileName,
                BackIdContentType = model.BackIdImage.ContentType
            };

            await _verificationService.SubmitVerificationAsync(store.Id, submission);

            TempData["SuccessMessage"] = "تم استلام مستندات توثيق الهوية بنجاح وبأمان تام. طلبك قيد المراجعة اليدوية من قبل إدارة المنصة.";
            return RedirectToAction(nameof(Index));
        }
    }
}
