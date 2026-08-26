using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShinobiClothing.Data;
using ShinobiClothing.Models;

namespace ShinobiClothing.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ProductVariantController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductVariantController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /ProductVariant/Index/5
        public async Task<IActionResult> Index(int productId)
        {
            var product = await _context.Products
                .Include(p => p.ProductVariants)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.Product = product;

            return View(product.ProductVariants);
        }

        // GET: /ProductVariant/Create/5
        public async Task<IActionResult> Create(int productId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.Product = product;

            var variant = new ProductVariant
            {
                ProductId = productId
            };

            return View(variant);
        }

        // POST: /ProductVariant/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductVariant variant)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Product = await _context.Products
                    .FirstOrDefaultAsync(p =>
                        p.ProductId == variant.ProductId);

                return View(variant);
            }

            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Index),
                new { productId = variant.ProductId });
        }

        // GET: /ProductVariant/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v =>
                    v.ProductVariantId == id);

            if (variant == null)
            {
                return NotFound();
            }

            return View(variant);
        }

        // POST: /ProductVariant/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            ProductVariant variant)
        {
            if (id != variant.ProductVariantId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                variant.Product = await _context.Products
                    .FirstOrDefaultAsync(p =>
                        p.ProductId == variant.ProductId);

                return View(variant);
            }

            try
            {
                _context.Update(variant);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VariantExists(variant.ProductVariantId))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(
                nameof(Index),
                new { productId = variant.ProductId });
        }

        // GET: /ProductVariant/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v =>
                    v.ProductVariantId == id);

            if (variant == null)
            {
                return NotFound();
            }

            return View(variant);
        }

        // POST: /ProductVariant/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var variant = await _context.ProductVariants
                .FindAsync(id);

            if (variant != null)
            {
                var productId = variant.ProductId;

                _context.ProductVariants.Remove(variant);
                await _context.SaveChangesAsync();

                return RedirectToAction(
                    nameof(Index),
                    new { productId });
            }

            return RedirectToAction("Index", "Product");
        }

        private bool VariantExists(int id)
        {
            return _context.ProductVariants
                .Any(v => v.ProductVariantId == id);
        }
    }
}