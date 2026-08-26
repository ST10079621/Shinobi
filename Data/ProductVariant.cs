using System.ComponentModel.DataAnnotations;

namespace ShinobiClothing.Models
{
    public class ProductVariant
    {
        public int ProductVariantId { get; set; }

        [Required]
        [StringLength(20)]
        public string Size { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Colour { get; set; } = string.Empty;

        public int StockQuantity { get; set; }

        public int ProductId { get; set; }

        // Navigation property
        public Product? Product { get; set; }

        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}