using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Souqna.Core.Entities;

namespace Souqna.Infrastructure.Data
{
    public class SouqnaDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public SouqnaDbContext(DbContextOptions<SouqnaDbContext> options) : base(options)
        {
        }

        public DbSet<Store> Stores => Set<Store>();
        public DbSet<StoreAddress> StoreAddresses => Set<StoreAddress>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
        public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
        public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();
        public DbSet<IdentityVerification> IdentityVerifications => Set<IdentityVerification>();
        public DbSet<VerificationDocument> VerificationDocuments => Set<VerificationDocument>();
        public DbSet<PhoneVerification> PhoneVerifications => Set<PhoneVerification>();
        public DbSet<Cart> Carts => Set<Cart>();
        public DbSet<CartItem> CartItems => Set<CartItem>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<StoreOrder> StoreOrders => Set<StoreOrder>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<DeliveryAddress> DeliveryAddresses => Set<DeliveryAddress>();
        public DbSet<Coupon> Coupons => Set<Coupon>();
        public DbSet<ProductFavorite> ProductFavorites => Set<ProductFavorite>();
        public DbSet<Video> Videos => Set<Video>();
        public DbSet<VideoLike> VideoLikes => Set<VideoLike>();
        public DbSet<VideoView> VideoViews => Set<VideoView>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<Report> Reports => Set<Report>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Decimal precision for currency columns
            builder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
            builder.Entity<Product>().Property(p => p.SalePrice).HasPrecision(18, 2);
            builder.Entity<ProductVariant>().Property(p => p.AdditionalPrice).HasPrecision(18, 2);

            builder.Entity<SubscriptionPlan>().Property(p => p.PriceMonthly).HasPrecision(18, 2);
            builder.Entity<SubscriptionPlan>().Property(p => p.PriceYearly).HasPrecision(18, 2);
            builder.Entity<Subscription>().Property(p => p.PricePaid).HasPrecision(18, 2);
            builder.Entity<SubscriptionPayment>().Property(p => p.Amount).HasPrecision(18, 2);

            builder.Entity<CartItem>().Property(p => p.UnitPrice).HasPrecision(18, 2);
            builder.Entity<Order>().Property(p => p.Subtotal).HasPrecision(18, 2);
            builder.Entity<Order>().Property(p => p.DeliveryFee).HasPrecision(18, 2);
            builder.Entity<Order>().Property(p => p.DiscountAmount).HasPrecision(18, 2);
            builder.Entity<Order>().Property(p => p.TotalAmount).HasPrecision(18, 2);

            builder.Entity<StoreOrder>().Property(p => p.Subtotal).HasPrecision(18, 2);
            builder.Entity<StoreOrder>().Property(p => p.DeliveryFee).HasPrecision(18, 2);
            builder.Entity<StoreOrder>().Property(p => p.TotalAmount).HasPrecision(18, 2);

            builder.Entity<OrderItem>().Property(p => p.UnitPrice).HasPrecision(18, 2);
            builder.Entity<OrderItem>().Property(p => p.TotalPrice).HasPrecision(18, 2);

            builder.Entity<Coupon>().Property(p => p.DiscountPercentage).HasPrecision(18, 2);
            builder.Entity<Coupon>().Property(p => p.MaxDiscountAmount).HasPrecision(18, 2);
            builder.Entity<Coupon>().Property(p => p.MinOrderAmount).HasPrecision(18, 2);

            // Indexes & Unique constraints
            builder.Entity<Store>().HasIndex(s => s.Slug).IsUnique();
            builder.Entity<Category>().HasIndex(c => c.Slug).IsUnique();
            builder.Entity<Product>().HasIndex(p => p.Slug);
            builder.Entity<ProductFavorite>().HasIndex(f => new { f.UserId, f.ProductId }).IsUnique();
            builder.Entity<VideoLike>().HasIndex(l => new { l.UserId, l.VideoId }).IsUnique();
            builder.Entity<PhoneVerification>().HasIndex(p => p.PhoneNumber);

            // Relationships and Delete Behaviors (Prevent cycles in SQL Server)
            builder.Entity<Store>()
                .HasOne(s => s.Seller)
                .WithOne(u => u.Store)
                .HasForeignKey<Store>(s => s.SellerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Product>()
                .HasOne(p => p.Store)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StoreOrder>()
                .HasOne(so => so.ParentOrder)
                .WithMany(o => o.StoreOrders)
                .HasForeignKey(so => so.ParentOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StoreOrder>()
                .HasOne(so => so.Store)
                .WithMany(s => s.StoreOrders)
                .HasForeignKey(so => so.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.StoreOrder)
                .WithMany(so => so.Items)
                .HasForeignKey(oi => oi.StoreOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany(p => p.CartItems)
                .HasForeignKey(ci => ci.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Video>()
                .HasOne(v => v.Store)
                .WithMany(s => s.Videos)
                .HasForeignKey(v => v.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Video>()
                .HasOne(v => v.Product)
                .WithMany(p => p.Videos)
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Review>()
                .HasOne(r => r.Product)
                .WithMany(p => p.Reviews)
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Review>()
                .HasOne(r => r.Store)
                .WithMany(s => s.Reviews)
                .HasForeignKey(r => r.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<IdentityVerification>()
                .HasOne(iv => iv.Store)
                .WithMany(s => s.VerificationRequests)
                .HasForeignKey(iv => iv.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Subscription>()
                .HasOne(s => s.Store)
                .WithMany(st => st.Subscriptions)
                .HasForeignKey(s => s.StoreId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
