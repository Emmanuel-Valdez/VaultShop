using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;
using VaultShop.Models;

namespace VaultShop.Web.Services.Account;

/// <summary>
/// Erases the customer account's personal data while keeping an anonymized account tombstone
/// and the immutable order records (commercial/accounting retention). Runs in one transaction.
/// </summary>
public sealed class AccountDeletionService
{
	private readonly ApplicationDbContext _db;
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly ILogger<AccountDeletionService> _logger;

	public AccountDeletionService(
		ApplicationDbContext db,
		UserManager<ApplicationUser> userManager,
		ILogger<AccountDeletionService> logger)
	{
		_db = db;
		_userManager = userManager;
		_logger = logger;
	}

	public async Task<bool> DeletePersonalDataAsync(string userId)
	{
		var user = await _userManager.FindByIdAsync(userId);
		if (user is null || user.IsDeleted)
		{
			return false;
		}

		await using var transaction = await _db.Database.BeginTransactionAsync();
		try
		{
			foreach (var login in await _userManager.GetLoginsAsync(user))
			{
				await _userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
			}

			var roles = await _userManager.GetRolesAsync(user);
			if (roles.Count > 0)
			{
				await _userManager.RemoveFromRolesAsync(user, roles);
			}

			var placeholder = $"deleted+{user.Id}@invalid.local";
			user.Name = "Deleted user";
			user.UserName = placeholder;
			user.NormalizedUserName = placeholder.ToUpperInvariant();
			user.Email = placeholder;
			user.NormalizedEmail = placeholder.ToUpperInvariant();
			user.EmailConfirmed = false;
			user.PhoneNumber = null;
			user.PhoneNumberConfirmed = false;
			user.StreetAddress = null;
			user.City = null;
			user.State = null;
			user.PostalCode = null;
			user.PasswordHash = null;
			user.TwoFactorEnabled = false;
			user.IsDeleted = true;
			user.DeletedAtUtc = DateTime.UtcNow;
			user.LockoutEnabled = true;
			user.LockoutEnd = DateTimeOffset.MaxValue;

			// Persists the anonymized fields and rotates the stamp, invalidating live cookies.
			var updateResult = await _userManager.UpdateSecurityStampAsync(user);
			if (!updateResult.Succeeded)
			{
				var errors = string.Join("; ", updateResult.Errors.Select(error => error.Description));
				throw new InvalidOperationException($"Failed to anonymize account {userId}: {errors}");
			}

			// Order records stay untouched: they are the commercial/accounting record and must
			// remain immutable even after the account is anonymized.
			await _db.ShoppingCarts.Where(cart => cart.ApplicationUserId == userId).ExecuteDeleteAsync();
			await _db.FavoriteProducts.Where(favorite => favorite.ApplicationUserId == userId).ExecuteDeleteAsync();
			await _db.UserNotifications.Where(notification => notification.ApplicationUserId == userId).ExecuteDeleteAsync();
			// Claims/tokens can carry personal data or credentials (e.g. authenticator seeds).
			await _db.UserClaims.Where(claim => claim.UserId == userId).ExecuteDeleteAsync();
			await _db.UserTokens.Where(token => token.UserId == userId).ExecuteDeleteAsync();

			await _db.SaveChangesAsync();
			await transaction.CommitAsync();

			_logger.LogInformation("Anonymized personal data for user {UserId}; account disabled and order history retained.", userId);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete personal data for user {UserId}; transaction rolled back.", userId);
			throw;
		}
	}
}
