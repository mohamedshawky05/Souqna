using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Souqna.Core.Entities;
using Souqna.Core.Enums;

namespace Souqna.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(
            SouqnaDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            ILogger logger)
        {
            try
            {
                // Ensure Database Created & Migrations Applied
                await context.Database.MigrateAsync();
                logger.LogInformation("Database migrated successfully.");

                // 1. Seed Roles
                string[] roles = [nameof(UserRole.Admin), nameof(UserRole.Seller), nameof(UserRole.Customer)];
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                    }
                }

                // 2. Seed Subscription Plans
                if (!await context.SubscriptionPlans.AnyAsync())
                {
                    var plans = new List<SubscriptionPlan>
                    {
                        new SubscriptionPlan
                        {
                            NameAr = "الباقة المجانية",
                            NameEn = "Free",
                            Slug = "free",
                            DescriptionAr = "مناسبة للبداية وتجربة البيع على المنصة بسهولة وبدون أي رسوم.",
                            PriceMonthly = 0m,
                            PriceYearly = 0m,
                            MaxActiveProducts = 3,
                            MaxPublishedVideos = 3,
                            MaxVideoDurationSeconds = 60,
                            StorageLimitMb = 100,
                            AllowsCoupons = false,
                            AllowsCsvExportImport = false,
                            HasAdvancedAnalytics = false,
                            FeaturesJson = "[\"حتى 3 منتجات نشطة\",\"3 فيديوهات ريلز شهرياً\",\"مدة الفيديو حتى 60 ثانية\",\"صفحة متجر أساسية\",\"دعم فني عبر البريد\"]",
                            DisplayOrder = 1,
                            IsActive = true
                        },
                        new SubscriptionPlan
                        {
                            NameAr = "باقة البداية (Starter)",
                            NameEn = "Starter",
                            Slug = "starter",
                            DescriptionAr = "مثالية للبائعين الناشئين لزيادة حجم المبيعات والمنتجات.",
                            PriceMonthly = 99m,
                            PriceYearly = 990m,
                            MaxActiveProducts = 20,
                            MaxPublishedVideos = 20,
                            MaxVideoDurationSeconds = 180,
                            StorageLimitMb = 500,
                            AllowsCoupons = false,
                            AllowsCsvExportImport = false,
                            HasAdvancedAnalytics = false,
                            FeaturesJson = "[\"حتى 20 منتج نشط\",\"20 فيديو ريلز شهرياً\",\"مدة الفيديو حتى 180 ثانية\",\"إحصائيات أساسية للمتجر\",\"أولوية في الظهور\"]",
                            DisplayOrder = 2,
                            IsActive = true
                        },
                        new SubscriptionPlan
                        {
                            NameAr = "الباقة الاحترافية (Pro)",
                            NameEn = "Pro",
                            Slug = "pro",
                            DescriptionAr = "الخيار الأمثل للمتاجر النامية التي تحتاج أدوات تسويق وتحليلات متقدمة.",
                            PriceMonthly = 199m,
                            PriceYearly = 1990m,
                            MaxActiveProducts = 100,
                            MaxPublishedVideos = 100,
                            MaxVideoDurationSeconds = 180,
                            StorageLimitMb = 2000,
                            AllowsCoupons = true,
                            AllowsCsvExportImport = false,
                            HasAdvancedAnalytics = true,
                            FeaturesJson = "[\"حتى 100 منتج نشط\",\"100 فيديو ريلز شهرياً\",\"مدة الفيديو حتى 180 ثانية\",\"كوبونات وتخفيضات\",\"تحليلات متقدمة وسلوك المشترين\",\"شارة متجر متميز\"]",
                            DisplayOrder = 3,
                            IsActive = true
                        },
                        new SubscriptionPlan
                        {
                            NameAr = "باقة الأعمال (Business)",
                            NameEn = "Business",
                            Slug = "business",
                            DescriptionAr = "حل متكامل وشامل للشركات الكبرى والمصانع والموزعين.",
                            PriceMonthly = 399m,
                            PriceYearly = 3990m,
                            MaxActiveProducts = 500,
                            MaxPublishedVideos = 300,
                            MaxVideoDurationSeconds = 600,
                            StorageLimitMb = 10000,
                            AllowsCoupons = true,
                            AllowsCsvExportImport = true,
                            HasAdvancedAnalytics = true,
                            FeaturesJson = "[\"حتى 500 منتج نشط\",\"300 فيديو ريلز شهرياً\",\"مدة الفيديو حتى 10 دقائق (600 ثانية)\",\"استيراد وتصدير المنتجات عبر CSV\",\"مدير حساب مخصص\",\"ظهور حصري في صدارة البحث\"]",
                            DisplayOrder = 4,
                            IsActive = true
                        }
                    };

                    await context.SubscriptionPlans.AddRangeAsync(plans);
                    await context.SaveChangesAsync();
                }

                // 3. Seed Categories
                if (!await context.Categories.AnyAsync())
                {
                    var categories = new List<Category>
                    {
                        new Category
                        {
                            NameAr = "أزياء وملابس",
                            NameEn = "Fashion & Clothing",
                            Slug = "fashion",
                            Icon = "bi-bag-check",
                            Description = "أحدث صيحات الموضة المصرية والملابس الرجالية والنسائية والأطفال",
                            DisplayOrder = 1
                        },
                        new Category
                        {
                            NameAr = "إلكترونيات وأجهزة",
                            NameEn = "Electronics",
                            Slug = "electronics",
                            Icon = "bi-laptop",
                            Description = "هواتف ذكية، حواسيب، إلكترونيات منزلية وملحقات تقنية",
                            DisplayOrder = 2
                        },
                        new Category
                        {
                            NameAr = "المنزل والمطبخ",
                            NameEn = "Home & Kitchen",
                            Slug = "home-kitchen",
                            Icon = "bi-house-door",
                            Description = "مستلزمات البيت العصري، أدوات الطهي والديكورات الحديثة",
                            DisplayOrder = 3
                        },
                        new Category
                        {
                            NameAr = "الصحة والجمال والعطور",
                            NameEn = "Beauty & Fragrances",
                            Slug = "beauty",
                            Icon = "bi-flower1",
                            Description = "منتجات العناية الطبيعية، العطور الشرقية ومستحضرات التجميل",
                            DisplayOrder = 4
                        },
                        new Category
                        {
                            NameAr = "الحرف اليدوية والتراث",
                            NameEn = "Handmade & Crafts",
                            Slug = "handmade",
                            Icon = "bi-gem",
                            Description = "منتجات خان الخليلي، الخزف، السجاد اليدوي والمصنوعات الجلدية",
                            DisplayOrder = 5
                        },
                        new Category
                        {
                            NameAr = "سوبرماركت وأغذية",
                            NameEn = "Supermarket",
                            Slug = "supermarket",
                            Icon = "bi-basket",
                            Description = "أجود المنتجات الغذائية والمشروبات والمحاصيل الطبيعية",
                            DisplayOrder = 6
                        }
                    };

                    await context.Categories.AddRangeAsync(categories);
                    await context.SaveChangesAsync();
                }

                // 4. Seed Administrator (Read config / secrets with fallback)
                string adminEmail = configuration["AdminUser:Email"] ?? "admin@souqna.eg";
                string adminPassword = configuration["AdminUser:Password"] ?? "Admin@Souqna2026!";
                
                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true,
                        FullName = "مدير المنصة العام",
                        PhoneNumber = "+201000000001",
                        PhoneNumberConfirmed = true,
                        UserType = UserRole.Admin,
                        CreatedAt = DateTime.UtcNow
                    };

                    var result = await userManager.CreateAsync(adminUser, adminPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, nameof(UserRole.Admin));
                        logger.LogInformation("Administrator account seeded: {Email}", adminEmail);
                    }
                    else
                    {
                        logger.LogError("Failed to seed admin: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }

                // 5. Seed Demo Seller & Store
                string sellerEmail = "seller@souqna.eg";
                string sellerPassword = "Seller@Souqna2026!";
                var sellerUser = await userManager.FindByEmailAsync(sellerEmail);
                if (sellerUser == null)
                {
                    sellerUser = new ApplicationUser
                    {
                        UserName = sellerEmail,
                        Email = sellerEmail,
                        EmailConfirmed = true,
                        FullName = "أحمد رضوان (تاجر معتمد)",
                        PhoneNumber = "+201112223334",
                        PhoneNumberConfirmed = true,
                        UserType = UserRole.Seller,
                        CreatedAt = DateTime.UtcNow
                    };

                    var createResult = await userManager.CreateAsync(sellerUser, sellerPassword);
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(sellerUser, nameof(UserRole.Seller));

                        // Create Verified Store
                        var store = new Store
                        {
                            SellerId = sellerUser.Id,
                            StoreName = "متجر الأهرام للموضة والأزياء",
                            Slug = "ahram-fashion",
                            Description = "أرقى الملابس والمنسوجات المصرية المصنوعة من القطن المصري 100% بأعلى جودة وتصميمات عصرية.",
                            LogoUrl = "https://images.unsplash.com/photo-1441986300917-64674bd600d8?w=300",
                            CoverUrl = "https://images.unsplash.com/photo-1441984904996-e0b6ba687e04?w=1200",
                            City = "القاهرة",
                            Address = "شارع طلعت حرب، وسط البلد",
                            ContactPhone = "+201112223334",
                            ContactEmail = sellerEmail,
                            IsVerified = true,
                            VerificationBadge = "Store Verified",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        context.Stores.Add(store);
                        await context.SaveChangesAsync();

                        // Keep the demo store on the free tier until payment is confirmed.
                        var freePlan = await context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Slug == "free");
                        if (freePlan != null)
                        {
                            context.Subscriptions.Add(new Subscription
                            {
                                StoreId = store.Id,
                                SubscriptionPlanId = freePlan.Id,
                                BillingInterval = BillingInterval.Monthly,
                                StartDate = DateTime.UtcNow,
                                EndDate = DateTime.UtcNow.AddYears(10),
                                Status = SubscriptionStatus.Active,
                                PricePaid = 0m,
                                CreatedAt = DateTime.UtcNow
                            });
                            await context.SaveChangesAsync();
                        }

                        // Seed Initial Products for Store
                        var fashionCategory = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "fashion");
                        if (fashionCategory != null)
                        {
                            var prod1 = new Product
                            {
                                StoreId = store.Id,
                                CategoryId = fashionCategory.Id,
                                Name = "قميص قطن مصري فاخر - أبيض ناصع",
                                Slug = "luxury-egyptian-cotton-shirt-white",
                                Description = "مصنوع من أجود خيوط القطن المصري طويل التيلة. مريح، أنيق ومناسب للمناسبات الرسمية والعملية.",
                                Price = 450.00m,
                                SalePrice = 380.00m,
                                StockQuantity = 25,
                                SKU = "SHIRT-EGY-01",
                                IsPublished = true,
                                IsFeatured = true,
                                ViewCount = 142,
                                CreatedAt = DateTime.UtcNow
                            };
                            prod1.Images.Add(new ProductImage { ImageUrl = "https://images.unsplash.com/photo-1602810318383-e386cc2a3ccf?w=600", IsMain = true, DisplayOrder = 1 });

                            var prod2 = new Product
                            {
                                StoreId = store.Id,
                                CategoryId = fashionCategory.Id,
                                Name = "عباءة شرقية مطرزة يدوياً بتطريز سيناوي",
                                Slug = "handmade-embroidered-abaya",
                                Description = "تصميم تراثي أنيق يجمع بين الأصالة والعصرية. قماش عالي الجودة وتطريز متقن على الأكمام والصدر.",
                                Price = 890.00m,
                                SalePrice = null,
                                StockQuantity = 12,
                                SKU = "ABY-SINAI-02",
                                IsPublished = true,
                                IsFeatured = true,
                                ViewCount = 285,
                                CreatedAt = DateTime.UtcNow
                            };
                            prod2.Images.Add(new ProductImage { ImageUrl = "https://images.unsplash.com/photo-1583391733956-3750e0ff4e8b?w=600", IsMain = true, DisplayOrder = 1 });

                            var prod3 = new Product
                            {
                                StoreId = store.Id,
                                CategoryId = fashionCategory.Id,
                                Name = "حذاء جلدي طبيعي كلاسيكي",
                                Slug = "classic-genuine-leather-shoes",
                                Description = "جلد طبيعي 100% مع نعل مريح يدوم طويلاً، صناعة مصرية متقنة ومناسب لساعات العمل الطويلة.",
                                Price = 620.00m,
                                SalePrice = 550.00m,
                                StockQuantity = 18,
                                SKU = "SHOE-LTHR-03",
                                IsPublished = true,
                                IsFeatured = false,
                                ViewCount = 98,
                                CreatedAt = DateTime.UtcNow
                            };
                            prod3.Images.Add(new ProductImage { ImageUrl = "https://images.unsplash.com/photo-1549298916-b41d501d3772?w=600", IsMain = true, DisplayOrder = 1 });

                            context.Products.AddRange(prod1, prod2, prod3);
                            await context.SaveChangesAsync();

                            // Seed Sample Reels for Store
                            var reel1 = new Video
                            {
                                StoreId = store.Id,
                                ProductId = prod1.Id,
                                Title = "استعراض تفاصيل القميص القطني المصري الفاخر",
                                Description = "شاهد ملمس القماش الفاخر وجودة الخياطة المتقنة! متوفر بجميع المقاسات.",
                                VideoUrl = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4",
                                ThumbnailUrl = "https://images.unsplash.com/photo-1602810318383-e386cc2a3ccf?w=600",
                                DurationSeconds = 15,
                                FileSizeBytes = 2400000,
                                Status = VideoStatus.Published,
                                ViewsCount = 1240,
                                LikesCount = 89,
                                IsSponsored = true,
                                CreatedAt = DateTime.UtcNow
                            };

                            var reel2 = new Video
                            {
                                StoreId = store.Id,
                                ProductId = prod2.Id,
                                Title = "تفاصيل التطريز السيناوي على العباءة اليدوية",
                                Description = "إتقان فني بأيدي أمهر الحرفيات في سيناء. توصيل لجميع محافظات مصر.",
                                VideoUrl = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerEscapes.mp4",
                                ThumbnailUrl = "https://images.unsplash.com/photo-1583391733956-3750e0ff4e8b?w=600",
                                DurationSeconds = 15,
                                FileSizeBytes = 2900000,
                                Status = VideoStatus.Published,
                                ViewsCount = 850,
                                LikesCount = 54,
                                IsSponsored = false,
                                CreatedAt = DateTime.UtcNow
                            };

                            context.Videos.AddRange(reel1, reel2);
                            await context.SaveChangesAsync();
                        }
                    }
                }

                // 6. Seed Demo Customer
                string customerEmail = "customer@souqna.eg";
                string customerPassword = "Customer@Souqna2026!";
                var customerUser = await userManager.FindByEmailAsync(customerEmail);
                if (customerUser == null)
                {
                    customerUser = new ApplicationUser
                    {
                        UserName = customerEmail,
                        Email = customerEmail,
                        EmailConfirmed = true,
                        FullName = "محمود عادل (عميل تجريبي)",
                        PhoneNumber = "+201099887766",
                        PhoneNumberConfirmed = true,
                        UserType = UserRole.Customer,
                        CreatedAt = DateTime.UtcNow
                    };

                    var res = await userManager.CreateAsync(customerUser, customerPassword);
                    if (res.Succeeded)
                    {
                        await userManager.AddToRoleAsync(customerUser, nameof(UserRole.Customer));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }
    }
}
