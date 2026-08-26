namespace ShinobiClothing.Models
{
    public class PayFastSettings
    {
        public string MerchantId { get; set; } = string.Empty;

        public string MerchantKey { get; set; } = string.Empty;

        public string Passphrase { get; set; } = string.Empty;

        public string ProcessUrl { get; set; } = string.Empty;

        public string ValidateUrl { get; set; } = string.Empty;
    }
}