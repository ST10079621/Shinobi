using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShinobiClothing.Data;
using ShinobiClothing.Models;
using ShinobiClothing.Models.ViewModels;

namespace ShinobiClothing.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CheckoutController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Checkout
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var user = await _userManager.GetUserAsync(User);

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["CartMessage"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            ViewBag.Cart = cart;

            var model = new CheckoutViewModel
            {
                DeliveryAddress = user?.Address ?? string.Empty
            };

            return View(model);
        }

        // POST: Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CheckoutViewModel model)
        {
            var userId = _userManager.GetUserId(User);

            if (!ModelState.IsValid)
            {
                var invalidCart = await _context.Carts
                    .Include(c => c.CartItems)
                        .ThenInclude(ci => ci.ProductVariant)
                            .ThenInclude(pv => pv.Product)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                ViewBag.Cart = invalidCart;

                return View(model);
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null || !cart.CartItems.Any())
            {
                TempData["CartMessage"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            // Check stock before creating order
            foreach (var item in cart.CartItems)
            {
                if (item.ProductVariant == null ||
                    item.Quantity > item.ProductVariant.StockQuantity)
                {
                    TempData["CartMessage"] =
                        "One or more items no longer have enough stock.";

                    return RedirectToAction("Index", "Cart");
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                decimal total = 0;

                var order = new Order
                {
                    UserId = userId!,
                    OrderDate = DateTime.Now,
                    DeliveryAddress = model.DeliveryAddress,
                    OrderStatus = "Pending"
                };

                foreach (var item in cart.CartItems)
                {
                    var variant = item.ProductVariant!;
                    var product = variant.Product!;

                    var orderItem = new OrderItem
                    {
                        ProductVariantId = variant.ProductVariantId,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price
                    };

                    order.OrderItems.Add(orderItem);

                    total += product.Price * item.Quantity;

                    // Reduce stock
                    variant.StockQuantity -= item.Quantity;
                }

                order.TotalAmount = total;

                _context.Orders.Add(order);

                await _context.SaveChangesAsync();

                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    Amount = total,
                    PaymentStatus = "Pending"
                };

                _context.Payments.Add(payment);

                _context.CartItems.RemoveRange(cart.CartItems);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return RedirectToAction(
                    nameof(Confirmation),
                    new { id = order.OrderId });
            }
            catch
            {
                await transaction.RollbackAsync();

                TempData["CartMessage"] =
                    "Something went wrong while creating your order.";

                return RedirectToAction("Index", "Cart");
            }
        }

        // GET: Confirmation
        public async Task<IActionResult> Confirmation(int id)
        {
            var userId = _userManager.GetUserId(User);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o =>
                    o.OrderId == id &&
                    o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }
    }
}