using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShinobiClothing.Data;
using ShinobiClothing.Models;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Net;

namespace ShinobiClothing.Controllers
{
    [Authorize]
    public class CustomerOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly PayFastSettings _payFastSettings;

        public CustomerOrderController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IOptions<PayFastSettings> payFastSettings)
        {
            _context = context;
            _userManager = userManager;
            _payFastSettings = payFastSettings.Value;
        }

        // GET: /CustomerOrder
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var orders = await _context.Orders
                .Include(o => o.Payment)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        // GET: /CustomerOrder/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);

            var order = await _context.Orders
                .Include(o => o.Payment)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .FirstOrDefaultAsync(o =>
                    o.OrderId == id &&
                    o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: /CustomerOrder/Pay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int orderId)
        {
            var userId = _userManager.GetUserId(User);

            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o =>
                    o.OrderId == orderId &&
                    o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            // Prevent paying for an already paid order
            if (order.Payment != null &&
                order.Payment.PaymentStatus == "Paid")
            {
                TempData["PaymentMessage"] =
                    "This order has already been paid.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = orderId });
            }

            // Create payment record if one does not already exist
            if (order.Payment == null)
            {
                order.Payment = new Payment
                {
                    OrderId = order.OrderId,
                    Amount = order.TotalAmount,
                    PaymentStatus = "Pending"
                };

                _context.Payments.Add(order.Payment);

                await _context.SaveChangesAsync();
            }

            var user = await _userManager.GetUserAsync(User);

            var email = user?.Email ?? string.Empty;

            
            var data = new Dictionary<string, string>
            {
                ["merchant_id"] = _payFastSettings.MerchantId,

                ["merchant_key"] = _payFastSettings.MerchantKey,

                ["return_url"] =
    $"https://ultra-decade-sprinkled.ngrok-free.dev/CustomerOrder/PaymentSuccess?orderId={order.OrderId}",

                ["cancel_url"] =
    $"https://ultra-decade-sprinkled.ngrok-free.dev/CustomerOrder/PaymentCancelled?orderId={order.OrderId}",

                ["notify_url"] =
    "https://ultra-decade-sprinkled.ngrok-free.dev/CustomerOrder/PaymentNotify",

                ["name_first"] =
                    user?.FirstName ?? "Customer",

                ["email_address"] =
                    email,

                ["m_payment_id"] =
                    order.Payment.PaymentId.ToString(),

                ["amount"] =
                    order.TotalAmount.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture),

                ["item_name"] =
                    $"Thee Shinobi Order #{order.OrderId}",

                ["item_description"] =
                    $"Payment for Thee Shinobi Order #{order.OrderId}"
            };

            var signature = GenerateSignature(
                data,
                _payFastSettings.Passphrase);

            data["signature"] = signature;

            return View("PayFastRedirect", data);
        }

        // GET: /CustomerOrder/PaymentSuccess
        [AllowAnonymous]
        public async Task<IActionResult> PaymentSuccess(int orderId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction(
                    "Login",
                    "Account",
                    new { returnUrl = $"/CustomerOrder/Details/{orderId}" });
            }

            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o =>
                    o.OrderId == orderId &&
                    o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            TempData["PaymentMessage"] =
                "You have returned from PayFast. Your payment is being confirmed.";

            return RedirectToAction(
                nameof(Details),
                new { id = orderId });
        }

        // GET: /CustomerOrder/PaymentCancelled
        public IActionResult PaymentCancelled(int orderId)
        {
            TempData["PaymentMessage"] =
                "Your payment was cancelled.";

            return RedirectToAction(
                nameof(Details),
                new { id = orderId });
        }

        // POST: /CustomerOrder/PaymentNotify
        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> PaymentNotify()
        {
            var form = await Request.ReadFormAsync();

            var data = new Dictionary<string, string>();

            foreach (var item in form)
            {
                data[item.Key] = item.Value.ToString();
            }

            // PayFast requires a successful HTTP response
            // when the notification has been received.
            Response.StatusCode = StatusCodes.Status200OK;

            // -------------------------------------------------
            // Required values
            // -------------------------------------------------

            if (!data.TryGetValue(
                    "m_payment_id",
                    out var paymentIdString) ||
                !int.TryParse(
                    paymentIdString,
                    out var paymentId))
            {
                return Ok();
            }

            if (!data.TryGetValue(
                    "merchant_id",
                    out var merchantId))
            {
                return Ok();
            }

            if (!data.TryGetValue(
                    "signature",
                    out var receivedSignature))
            {
                return Ok();
            }

            // -------------------------------------------------
            // 1. Verify Merchant ID
            // -------------------------------------------------

            if (merchantId != _payFastSettings.MerchantId)
            {
                return Ok();
            }

            // -------------------------------------------------
            // 2. Verify Signature
            // -------------------------------------------------

            var signatureData =
                new Dictionary<string, string>();

            foreach (var item in data)
            {
                if (!item.Key.Equals(
                        "signature",
                        StringComparison.OrdinalIgnoreCase))
                {
                    signatureData[item.Key] = item.Value;
                }
            }

            var calculatedSignature =
                GenerateSignature(
                    signatureData,
                    _payFastSettings.Passphrase);

            if (!calculatedSignature.Equals(
                    receivedSignature,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Ok();
            }

            // -------------------------------------------------
            // Find payment record
            // -------------------------------------------------

            var payment = await _context.Payments
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p =>
                    p.PaymentId == paymentId);

            if (payment == null)
            {
                return Ok();
            }

            // -------------------------------------------------
            // 3. Verify payment amount
            // -------------------------------------------------

            if (!data.TryGetValue(
                    "amount_gross",
                    out var amountString) ||
                !decimal.TryParse(
                    amountString,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var paidAmount))
            {
                return Ok();
            }

            if (Math.Abs(
                    payment.Amount - paidAmount) > 0.01m)
            {
                return Ok();
            }

            // -------------------------------------------------
            // 4. Verify payment status
            // -------------------------------------------------

            if (!data.TryGetValue(
                    "payment_status",
                    out var paymentStatus))
            {
                return Ok();
            }

            if (!paymentStatus.Equals(
                    "COMPLETE",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Ok();
            }

            // -------------------------------------------------
            // 5. Server-side validation with PayFast
            // -------------------------------------------------

            var validationData =
                new Dictionary<string, string>();

            foreach (var item in data)
            {
                if (!item.Key.Equals(
                        "signature",
                        StringComparison.OrdinalIgnoreCase))
                {
                    validationData[item.Key] = item.Value;
                }
            }

            var validationString =
                BuildParameterString(
                    validationData,
                    _payFastSettings.Passphrase);

            using var httpClient = new HttpClient();

            var content = new StringContent(
                validationString,
                Encoding.UTF8,
                "application/x-www-form-urlencoded");

            var validationResponse =
                await httpClient.PostAsync(
                    _payFastSettings.ValidateUrl,
                    content);

            var validationResult =
                await validationResponse.Content
                    .ReadAsStringAsync();

            if (!validationResult.Trim().Equals(
                    "VALID",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Ok();
            }

            // -------------------------------------------------
            // Payment has passed all checks
            // -------------------------------------------------

            payment.PaymentStatus = "Paid";

            payment.PaymentDate = DateTime.UtcNow;

            if (data.TryGetValue(
                    "pf_payment_id",
                    out var payFastPaymentId))
            {
                payment.TransactionReference =
                    payFastPaymentId;
            }

            if (payment.Order != null)
            {
                payment.Order.OrderStatus = "Processing";
            }

            await _context.SaveChangesAsync();

            return Ok();
        }

        // -----------------------------------------------------
        // Generate PayFast Signature
        // -----------------------------------------------------

        private static string GenerateSignature(
    Dictionary<string, string> data,
    string? passphrase)
        {
            var parameterString = new StringBuilder();

            foreach (var item in data)
            {
                if (!string.IsNullOrWhiteSpace(item.Value))
                {
                    parameterString.Append(item.Key);
                    parameterString.Append('=');
                    parameterString.Append(
                        WebUtility.UrlEncode(item.Value.Trim()));
                    parameterString.Append('&');
                }
            }

            // Remove the final &
            if (parameterString.Length > 0)
            {
                parameterString.Length--;
            }

            // Add the PayFast passphrase
            if (!string.IsNullOrWhiteSpace(passphrase))
            {
                parameterString.Append("&passphrase=");
                parameterString.Append(
                    WebUtility.UrlEncode(passphrase.Trim()));
            }

            using var md5 = MD5.Create();

            var inputBytes = Encoding.UTF8.GetBytes(
                parameterString.ToString());

            var hashBytes = md5.ComputeHash(inputBytes);

            return Convert.ToHexString(hashBytes)
                .ToLowerInvariant();
        }

        // -----------------------------------------------------
        // Build PayFast Parameter String
        // -----------------------------------------------------

        private static string BuildParameterString(
            Dictionary<string, string> data,
            string? passphrase)
        {
            var parameterString =
                new StringBuilder();

            foreach (var item in data)
            {
                if (!string.IsNullOrWhiteSpace(
                        item.Value))
                {
                    parameterString.Append(
                        item.Key);

                    parameterString.Append('=');

                    parameterString.Append(
                        Uri.EscapeDataString(
                            item.Value.Trim()));

                    parameterString.Append('&');
                }
            }

            if (parameterString.Length > 0)
            {
                parameterString.Length--;
            }

            if (!string.IsNullOrWhiteSpace(
                    passphrase))
            {
                parameterString.Append(
                    "&passphrase=");

                parameterString.Append(
                    Uri.EscapeDataString(
                        passphrase.Trim()));
            }

            return parameterString.ToString();
        }
    }
}