namespace ShinobiClothing.Models
{
    public class CartItem
    {
        public int CartItemId { get; set; }

        public int CartId { get; set; }

        public int ProductVariantId { get; set; }

        public int Quantity { get; set; }

        // Navigation properties
        public Cart? Cart { get; set; }

        public ProductVariant? ProductVariant { get; set; }
    }
}