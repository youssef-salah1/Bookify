using Microsoft.AspNetCore.Identity;

namespace Bookify.Web.Seeds
{
    public static class DefaultUsers
    {
        public static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager)
        {
            ApplicationUser user = new()
            {
                UserName = "Admin",
                Email = "Admin@gmail.com",
                FullName="Admin",
                EmailConfirmed = true
            };
            var valid = await userManager.FindByEmailAsync(user.Email);
            if(valid is null)
            {
                await userManager.CreateAsync(user, "Admin@1234");
                await userManager.AddToRoleAsync(user, AppRoles.Admin);
            }
        }
    }
}
