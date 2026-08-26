using System.ComponentModel.DataAnnotations;

namespace ShinobiClothing.Models.ViewModels
{
    public class CheckoutViewModel
    {
        [Required]
        [Display(Name = "Delivery Address")]
        public string DeliveryAddress { get; set; } = string.Empty;
    }
}