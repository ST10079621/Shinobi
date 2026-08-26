using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShinobiClothing.Data;
using ShinobiClothing.Models;

namespace ShinobiClothing.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CartController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Cart
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId!
                };

                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return View(cart);
        }

        // POST: /Cart/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(
            int productVariantId,
            int quantity = 1)
        {
            if (quantity < 1)
            {
                quantity = 1;
            }

            var userId = _userManager.GetUserId(User);

            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v =>
                    v.ProductVariantId == productVariantId);

            if (variant == null)
            {
                return NotFound();
            }

            if (variant.StockQuantity <= 0)
            {
                TempData["CartMessage"] = "This item is currently out of stock.";
                return RedirectToAction(
                    "Details",
                    "Product",
                    new { id = variant.ProductId });
            }

            if (quantity > variant.StockQuantity)
            {
                TempData["CartMessage"] =
                    $"Only {variant.StockQuantity} item(s) are available.";

                return RedirectToAction(
                    "Details",
                    "Product",
                    new { id = variant.ProductId });
            }

            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId!
                };

                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            var existingItem = cart.CartItems
                .FirstOrDefault(ci =>
                    ci.ProductVariantId == productVariantId);

            if (existingItem != null)
            {
                if (existingItem.Quantity + quantity >
                    variant.StockQuantity)
                {
                    TempData["CartMessage"] =
                        $"You cannot add more than {variant.StockQuantity} of this item.";

                    return RedirectToAction(
                        "Details",
                        "Product",
                        new { id = variant.ProductId });
                }

                existingItem.Quantity += quantity;
            }
            else
            {
                cart.CartItems.Add(new CartItem
                {
                    ProductVariantId = productVariantId,
                    Quantity = quantity
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            int cartItemId,
            int quantity)
        {
            var userId = _userManager.GetUserId(User);

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .Include(ci => ci.ProductVariant)
                .FirstOrDefaultAsync(ci =>
                    ci.CartItemId == cartItemId &&
                    ci.Cart!.UserId == userId);

            if (cartItem == null)
            {
                return NotFound();
            }

            if (quantity <= 0)
            {
                _context.CartItems.Remove(cartItem);
            }
            else
            {
                if (quantity > cartItem.ProductVariant!.StockQuantity)
                {
                    TempData["CartMessage"] =
                        $"Only {cartItem.ProductVariant.StockQuantity} item(s) are available.";

                    return RedirectToAction(nameof(Index));
                }

                cartItem.Quantity = quantity;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int cartItemId)
        {
            var userId = _userManager.GetUserId(User);

            var cartItem = await _context.CartItems
                .Include(ci => ci.Cart)
                .FirstOrDefaultAsync(ci =>
                    ci.CartItemId == cartItemId &&
                    ci.Cart!.UserId == userId);

            if (cartItem == null)
            {
                return NotFound();
            }

            _context.CartItems.Remove(cartItem);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}