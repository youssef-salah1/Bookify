using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Bookify.Web.Helpers
{
    public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        public ApplicationUserClaimsPrincipalFactory(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IOptions<IdentityOptions> options) : base(userManager, roleManager, options)
        {

        }
        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var Identity = await base.GenerateClaimsAsync(user);
            Identity.AddClaim(new Claim(ClaimTypes.GivenName, user.FullName));
            return Identity;
        }
    }
}