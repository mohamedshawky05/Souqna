using System;
using System.Collections.Generic;

namespace Souqna.Core.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public virtual Store? Store { get; set; }

        public int CategoryId { get; set; }
        public virtual Category? Category { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public decimal Price { get; set; }
        public decimal? SalePrice { get; set; }
        public int StockQuantity { get; set; } = 0;
        public string? SKU { get; set; }

        public bool IsArchived { get; set; } = false;
        public bool IsPublished { get; set; } = true;
        public bool IsFeatured { get; set; } = false;
        public int ViewCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public virtual ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
        public virtual ICollection<Video> Videos { get; set; } = new List<Video>();
        public virtual ICollection<ProductFavorite> Favorites { get; set; } = new List<ProductFavorite>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    public class ProductImage
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public string ImageUrl { get; set; } = string.Empty;
        public bool IsMain { get; set; } = false;
        public int DisplayOrder { get; set; } = 0;
    }

    public class ProductVariant
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public string Name { get; set; } = string.Empty; // e.g. "المقاس" or "اللون"
        public string Value { get; set; } = string.Empty; // e.g. "XL" or "أسود"
        public decimal AdditionalPrice { get; set; } = 0m;
        public int StockQuantity { get; set; } = 0;
    }
}
