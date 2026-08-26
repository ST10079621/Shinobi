using System.ComponentModel.DataAnnotations.Schema;

namespace ShinobiClothing.Models
{
    public class OrderItem
    {
        public int OrderItemId { get; set; }

        public int OrderId { get; set; }

        public int ProductVariantId { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        // Navigation properties
        public Order? Order { get; set; }

        public ProductVariant? ProductVariant { get; set; }
    }
}