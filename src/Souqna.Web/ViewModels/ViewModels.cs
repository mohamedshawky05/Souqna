using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Souqna.Core.DTOs;
using Souqna.Core.Entities;
using Souqna.Core.Enums;

namespace Souqna.Web.ViewModels
{
    public class HomeViewModel
    {
        public List<Category> Categories { get; set; } = new();
        public List<Product> FeaturedProducts { get; set; } = new();
        public List<Product> LatestProducts { get; set; } = new();
        public List<Store> TopStores { get; set; } = new();
        public List<Video> LatestReels { get; set; } = new();
    }

    public class MarketplaceViewModel
    {
        public List<Product> Products { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
        
        public string? Query { get; set; }
        public int? CategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? City { get; set; }
        public bool InStockOnly { get; set; }
        public bool VerifiedOnly { get; set; }
        public string SortBy { get; set; } = "newest"; // newest, price_asc, price_desc, popular

        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
    }

    public class ProductDetailsViewModel
    {
        public Product Product { get; set; } = null!;
        public List<Product> RelatedProducts { get; set; } = new();
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public bool CanReview { get; set; }
    }

    public class StoreProfileViewModel
    {
        public Store Store { get; set; } = null!;
        public List<Product> Products { get; set; } = new();
        public List<Video> Reels { get; set; } = new();
        public double RatingAverage { get; set; }
        public int TotalProducts { get; set; }
    }

    public class ReelsFeedViewModel
    {
        public List<Video> Videos { get; set; } = new();
        public int InitialVideoId { get; set; } = 0;
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = true;
        public string? ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف المحمول مطلوب")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "يجب ألا تقل كلمة المرور عن 6 أحرف")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "كلمتا المرور غير متطابقتين")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public bool IsSeller { get; set; } = false;

        // If seller:
        public string? StoreName { get; set; }
        public string? City { get; set; } = "القاهرة";
    }

    public class PhoneVerificationViewModel
    {
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز التحقق OTP مطلوب")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "الرمز مكون من 6 أرقام")]
        public string OtpCode { get; set; } = string.Empty;
    }

    public class SellerDashboardViewModel
    {
        public Store Store { get; set; } = null!;
        public SubscriptionUsageDto Usage { get; set; } = null!;
        public decimal TotalSales { get; set; }
        public int PendingOrdersCount { get; set; }
        public int TotalOrdersCount { get; set; }
        public List<StoreOrder> RecentOrders { get; set; } = new();
    }

    public class ProductFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المنتج مطلوب")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "وصف المنتج مطلوب")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "سعر المنتج مطلوب")]
        [Range(1, 1000000, ErrorMessage = "السعر يجب أن يكون 1 ج.م على الأقل")]
        public decimal Price { get; set; }

        [Range(typeof(decimal), "0", "1000000", ErrorMessage = "سعر الخصم غير صالح")]
        public decimal? SalePrice { get; set; }

        [Required(ErrorMessage = "كمية المخزون مطلوبة")]
        [Range(0, 100000, ErrorMessage = "الكمية يجب أن تكون 0 أو أكثر")]
        public int StockQuantity { get; set; }

        public string? SKU { get; set; }

        [Required(ErrorMessage = "يرجى اختيار القسم")]
        public int CategoryId { get; set; }

        public bool IsPublished { get; set; } = true;

        public IFormFile? MainImage { get; set; }
        public List<IFormFile> AdditionalImages { get; set; } = new();
        public string? ExistingImageUrl { get; set; }
        public List<string> AdditionalImageUrls { get; set; } = new();
    }

    public class VideoUploadViewModel
    {
        [Required(ErrorMessage = "عنوان الفيديو مطلوب")]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int? ProductId { get; set; }

        [Required(ErrorMessage = "يرجى اختيار ملف الفيديو")]
        public IFormFile VideoFile { get; set; } = null!;
    }

    public class VideoEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "عنوان الفيديو مطلوب")]
        [StringLength(200, ErrorMessage = "عنوان الفيديو يجب ألا يتجاوز 200 حرف")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "وصف الفيديو يجب ألا يتجاوز 2000 حرف")]
        public string? Description { get; set; }

        public int? ProductId { get; set; }

        public IFormFile? VideoFile { get; set; }

        public string VideoUrl { get; set; } = string.Empty;
    }

    public class IdentityVerificationFormViewModel
    {
        [Required(ErrorMessage = "الرقم القومي مطلوب")]
        [RegularExpression(@"^[23]\d{13}$", ErrorMessage = "الرقم القومي المصري يجب أن يتكون من 14 رقماً ويبدأ بـ 2 أو 3")]
        public string NationalIdNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم القانوني الرباعي مطلوب")]
        public string FullLegalName { get; set; } = string.Empty;

        [Required(ErrorMessage = "يجب إرفاق صورة وجه البطاقة القومية")]
        public IFormFile FrontIdImage { get; set; } = null!;

        [Required(ErrorMessage = "يجب إرفاق صورة ظهر البطاقة القومية")]
        public IFormFile BackIdImage { get; set; } = null!;

        [Range(typeof(bool), "true", "true", ErrorMessage = "يجب الموافقة على إشعار الخصوصية وشروط معالجة البيانات")]
        public bool PrivacyNoticeAcknowledged { get; set; }
    }

    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalStores { get; set; }
        public int VerifiedStores { get; set; }
        public int TotalProducts { get; set; }
        public int TotalVideos { get; set; }
        public decimal TotalGmv { get; set; }
        public int PendingVerificationsCount { get; set; }
        public List<Order> RecentOrders { get; set; } = new();
        public List<IdentityVerification> PendingVerifications { get; set; } = new();
    }
}
