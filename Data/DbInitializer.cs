using Microsoft.AspNetCore.Identity;
using ShinobiClothing.Models;

namespace ShinobiClothing.Data
{
    public static class DbInitializer
    {
        public static async Task SeedRolesAsync(
            RoleManager<IdentityRole> roleManager)
        {
            string[] roles = { "Admin", "Customer" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(
                        new IdentityRole(role));
                }
            }
        }

        public static async Task SeedAdminAsync(
            UserManager<ApplicationUser> userManager)
        {
            string adminEmail = "admin@shinobi.co.za";
            string adminPassword = "ShinobiAdmin123!";

            var existingAdmin =
                await userManager.FindByEmailAsync(adminEmail);

            if (existingAdmin == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "Shinobi",
                    LastName = "Administrator",
                    PhoneNumber = "0000000000",
                    Address = "Thee Shinobi"
                };

                var result = await userManager.CreateAsync(
                    admin,
                    adminPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(
                        admin,
                        "Admin");
                }
            }
        }
    }
}