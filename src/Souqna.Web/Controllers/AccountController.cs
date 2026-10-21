using System;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Entities;
using Souqna.Core.Enums;
using Souqna.Core.Interfaces;
using Souqna.Infrastructure.Data;
using Souqna.Web.ViewModels;

namespace Souqna.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly SouqnaDbContext _dbContext;
        private readonly IPhoneVerificationService _phoneVerificationService;
        private readonly ISubscriptionService _subscriptionService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            SouqnaDbContext dbContext,
            IPhoneVerificationService phoneVerificationService,
            ISubscriptionService subscriptionService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _dbContext = dbContext;
            _phoneVerificationService = phoneVerificationService;
            _subscriptionService = subscriptionService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
                return View(model);
            }

            if (user.IsSuspended)
            {
                ModelState.AddModelError(string.Empty, "تم إيقاف هذا الحساب لمخالفة شروط وسياسات المنصة.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                if (await _userManager.IsInRoleAsync(user, nameof(UserRole.Admin)))
                {
                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                }
                if (await _userManager.IsInRoleAsync(user, nameof(UserRole.Seller)))
                {
                    return RedirectToAction("Index", "Dashboard", new { area = "Seller" });
                }

                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "تم قفل الحساب مؤقتاً بسبب تكرار المحاولات الخاطئة. يرجى المحاولة بعد 10 دقائق.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError(nameof(model.Email), "هذا البريد الإلكتروني مسجل بالفعل.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                UserType = model.IsSeller ? UserRole.Seller : UserRole.Customer,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                return View(model);
            }

            string role = model.IsSeller ? nameof(UserRole.Seller) : nameof(UserRole.Customer);
            await _userManager.AddToRoleAsync(user, role);

            // If seller, create the store and assign default Free plan
            if (model.IsSeller)
            {
                string storeName = !string.IsNullOrWhiteSpace(model.StoreName) ? model.StoreName.Trim() : $"متجر {model.FullName}";
                string slug = Regex.Replace(storeName.ToLowerInvariant(), @"[^a-z0-9\u0600-\u06FF]+", "-").Trim('-');
                if (string.IsNullOrEmpty(slug)) slug = "store-" + Guid.NewGuid().ToString("N")[..6];

                // Ensure unique slug
                if (await _dbContext.Stores.AnyAsync(s => s.Slug == slug))
                {
                    slug += "-" + Random.Shared.Next(100, 999);
                }

                var store = new Store
                {
                    SellerId = user.Id,
                    StoreName = storeName,
                    Slug = slug,
                    Description = "أهلاً بكم في متجرنا الرسمي على منصة سوقنا.",
                    City = model.City ?? "القاهرة",
                    ContactPhone = model.PhoneNumber,
                    ContactEmail = model.Email,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Stores.Add(store);
                await _dbContext.SaveChangesAsync();

                // Assign Free Subscription
                await _subscriptionService.GetActiveSubscriptionAsync(store.Id);
            }

            await _signInManager.SignInAsync(user, isPersistent: true);
            TempData["SuccessMessage"] = "تم إنشاء الحساب بنجاح! أهلاً بك في منصة سوقنا.";

            if (model.IsSeller)
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Seller" });
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var addresses = await _dbContext.DeliveryAddresses
                .Where(a => a.UserId == user.Id)
                .ToListAsync();

            ViewBag.Addresses = addresses;
            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string fullName, string phoneNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FullName = fullName;
            if (user.PhoneNumber != phoneNumber)
            {
                user.PhoneNumber = phoneNumber;
                user.PhoneNumberConfirmed = false; // requires re-verification
            }

            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "تم تحديث البيانات الشخصية بنجاح.";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> RequestPhoneOtp(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return Json(new { success = false, message = "رقم الهاتف غير صالح" });
            }

            bool sent = await _phoneVerificationService.RequestOtpAsync(phoneNumber);
            if (sent)
            {
                return Json(new { success = true, message = "تم إرسال رمز التحقق (OTP) بنجاح عبر الرسائل النصية." });
            }
            return Json(new { success = false, message = "تعذر إرسال الرمز. يرجى التأكد من صحة الرقم والانتظار 60 ثانية قبل إعادة المحاولة." });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> VerifyPhoneOtp(string phoneNumber, string otpCode)
        {
            bool verified = await _phoneVerificationService.VerifyOtpAsync(phoneNumber, otpCode);
            if (verified)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    user.PhoneNumber = phoneNumber;
                    user.PhoneNumberConfirmed = true;
                    await _userManager.UpdateAsync(user);
                }
                return Json(new { success = true, message = "تم تأكيد وتوثيق رقم هاتفك المحمول بنجاح!" });
            }
            return Json(new { success = false, message = "رمز التحقق غير صحيح أو منتهي الصلاحية." });
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
