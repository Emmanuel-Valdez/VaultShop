using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VaultShop.Models;

namespace VaultShop.Web.Services.Account;

/// <summary>
/// Blocks sign-in for anonymized accounts on every path (password, external, 2FA).
/// </summary>
public sealed class ApplicationSignInManager : SignInManager<ApplicationUser>
{
	public ApplicationSignInManager(
		UserManager<ApplicationUser> userManager,
		IHttpContextAccessor contextAccessor,
		IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
		IOptions<IdentityOptions> optionsAccessor,
		ILogger<SignInManager<ApplicationUser>> logger,
		IAuthenticationSchemeProvider schemes,
		IUserConfirmation<ApplicationUser> confirmation)
		: base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
	{
	}

	public override async Task<bool> CanSignInAsync(ApplicationUser user)
	{
		return !user.IsDeleted && await base.CanSignInAsync(user);
	}
}
