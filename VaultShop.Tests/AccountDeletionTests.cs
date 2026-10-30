using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VaultShop.DataAccess.Data;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Account;
using VaultShop.Web.Services.Billing;

namespace VaultShop.Web.Tests;

public class AccountDeletionTests
{
	[Fact]
	public async Task DeletePersonalData_AnonymizesUser_KeepsOrders_AndRemovesPersonalActivity()
	{
		using var factory = new CustomWebApplicationFactory();
		string userId;
		int orderId;

		using (var scope = factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			var db = services.GetRequiredService<ApplicationDbContext>();
			var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

			var user = new ApplicationUser
			{
				UserName = "delete.me@vaultshop.local",
				Email = "delete.me@vaultshop.local",
				Name = "Delete Me",
				PhoneNumber = "555-0100",
				StreetAddress = "123 Test St",
				City = "Buenos Aires",
				State = "Buenos Aires",
				PostalCode = "1000",
				EmailConfirmed = true,
			};
			Assert.True((await userManager.CreateAsync(user, "Test123!")).Succeeded);
			Assert.True((await userManager.AddToRoleAsync(user, SD.Role_Customer)).Succeeded);
			userId = user.Id;

			var category = new Category { Name = "Deletion Category", AvgShippingCost = 100m };
			var product = new Product
			{
				Name = "Deletion Product",
				Description = "Product used by the account deletion test.",
				Category = category,
				MaxExpectation = 10,
				ListPrice = 1000m,
				FinalRetailPrice = 1000m,
				FinalWholesalePrice = 900m,
			};
			db.Categories.Add(category);
			db.Products.Add(product);
			await db.SaveChangesAsync();

			var order = new OrderHeader
			{
				ApplicationUserId = userId,
				OrderDate = DateTime.UtcNow,
				ShippingDate = DateTime.UtcNow,
				PaymentDate = DateTime.UtcNow,
				PaymentDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
				OrderTotal = 100m,
				PaymentMethod = SD.PaymentMethodBankTransfer,
				PaymentStatus = SD.PaymentStatusPending,
				OrderStatus = SD.StatusPending,
				Name = "Delete Me",
				StreetAddress = "123 Test St",
				City = "Buenos Aires",
				State = "Buenos Aires",
				PostalCode = "1000",
				PhoneNumber = "555-0100",
				RazonSocialSnapshot = "Delete Me S.A.",
				DomicilioFiscalSnapshot = "123 Test St",
				CuitSnapshot = "20-12345678-9",
			};
			db.OrderHeaders.Add(order);
			await db.SaveChangesAsync();
			orderId = order.Id;

			db.OrderDetails.Add(new OrderDetail
			{
				OrderHeaderId = order.Id,
				ProductId = product.Id,
				Count = 1,
				Price = 100m,
			});
			db.ShoppingCarts.Add(new ShoppingCart { ApplicationUserId = userId, ProductId = product.Id, Count = 1 });
			db.FavoriteProducts.Add(new FavoriteProduct { ApplicationUserId = userId, ProductId = product.Id });
			db.UserNotifications.Add(new UserNotification
			{
				ApplicationUserId = userId,
				OrderId = order.Id,
				Type = OrderNotificationType.PaymentApproved,
			});
			await db.SaveChangesAsync();

			var deletion = services.GetRequiredService<AccountDeletionService>();
			Assert.True(await deletion.DeletePersonalDataAsync(userId));
		}

		using var verifyScope = factory.Services.CreateScope();
		var verifyServices = verifyScope.ServiceProvider;
		var verifyDb = verifyServices.GetRequiredService<ApplicationDbContext>();

		var deletedUser = await verifyDb.ApplicationUsers.AsNoTracking().SingleAsync(user => user.Id == userId);
		Assert.True(deletedUser.IsDeleted);
		Assert.NotNull(deletedUser.DeletedAtUtc);
		Assert.Equal("Deleted user", deletedUser.Name);
		Assert.Null(deletedUser.PasswordHash);
		Assert.Null(deletedUser.PhoneNumber);
		Assert.Null(deletedUser.StreetAddress);
		Assert.Null(deletedUser.City);
		Assert.Null(deletedUser.State);
		Assert.Null(deletedUser.PostalCode);
		Assert.StartsWith("deleted+", deletedUser.Email);
		Assert.True(deletedUser.LockoutEnabled);
		Assert.True(deletedUser.LockoutEnd > DateTimeOffset.UtcNow.AddYears(100));
		Assert.Empty(await verifyDb.UserRoles.Where(role => role.UserId == userId).ToListAsync());

		var keptOrder = await verifyDb.OrderHeaders.AsNoTracking().SingleAsync(order => order.Id == orderId);
		Assert.Equal(userId, keptOrder.ApplicationUserId);
		Assert.Equal("Delete Me", keptOrder.Name);
		Assert.Equal("123 Test St", keptOrder.StreetAddress);
		Assert.Equal("Buenos Aires", keptOrder.City);
		Assert.Equal("Buenos Aires", keptOrder.State);
		Assert.Equal("1000", keptOrder.PostalCode);
		Assert.Equal("555-0100", keptOrder.PhoneNumber);
		Assert.Equal("Delete Me S.A.", keptOrder.RazonSocialSnapshot);
		Assert.Equal("123 Test St", keptOrder.DomicilioFiscalSnapshot);
		Assert.Equal("20-12345678-9", keptOrder.CuitSnapshot);
		Assert.Equal(100m, keptOrder.OrderTotal);
		Assert.Equal(1, await verifyDb.OrderDetails.CountAsync(detail => detail.OrderHeaderId == orderId));

		var summary = verifyServices.GetRequiredService<IOrderSummaryService>().GetSummary(
			orderId,
			new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, SD.Role_Admin)], "TestAuth")));
		Assert.NotNull(summary);
		Assert.Equal("Delete Me", summary.CustomerName);
		Assert.Equal("Delete Me", summary.ShippingName);
		Assert.Equal("123 Test St", summary.ShippingStreetAddress);
		Assert.Equal("20-12345678-9", summary.Cuit);
		Assert.Equal("Delete Me S.A.", summary.RazonSocial);

		Assert.False(await verifyDb.ShoppingCarts.AnyAsync(cart => cart.ApplicationUserId == userId));
		Assert.False(await verifyDb.FavoriteProducts.AnyAsync(favorite => favorite.ApplicationUserId == userId));
		Assert.False(await verifyDb.UserNotifications.AnyAsync(notification => notification.ApplicationUserId == userId));
	}

	[Fact]
	public async Task SoftDeletedUser_CannotSignIn()
	{
		using var factory = new CustomWebApplicationFactory();
		const string email = "soft.deleted@vaultshop.local";

		using (var scope = factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			Assert.IsType<ApplicationSignInManager>(services.GetRequiredService<SignInManager<ApplicationUser>>());
			var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
			var user = new ApplicationUser
			{
				UserName = email,
				Email = email,
				Name = "Soft Deleted",
				EmailConfirmed = true,
				IsDeleted = true,
			};
			Assert.True((await userManager.CreateAsync(user, "Test123!")).Succeeded);
			Assert.True((await userManager.AddToRoleAsync(user, SD.Role_Customer)).Succeeded);
		}

		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		TestAuthHelper.SetRequestCulture(client, "en-US");

		var token = await TestAuthHelper.GetAntiforgeryTokenAsync(client, "/Identity/Account/Login");
		var response = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(
			new Dictionary<string, string>
			{
				["Input.Email"] = email,
				["Input.Password"] = "Test123!",
				["__RequestVerificationToken"] = token,
			}));

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		var signedIn = response.Headers.TryGetValues("Set-Cookie", out var cookies)
			&& cookies.Any(cookie => cookie.Contains("Identity.Application", StringComparison.Ordinal));
		Assert.False(signedIn, "A soft-deleted user must not receive an authentication cookie.");
	}

	[Fact]
	public async Task SoftDeletedUser_ExistingCookie_IsRejectedOnNextRequest()
	{
		using var factory = new CustomWebApplicationFactory();
		const string email = "cookie.deleted@vaultshop.local";

		using (var scope = factory.Services.CreateScope())
		{
			var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
			var user = new ApplicationUser
			{
				UserName = email,
				Email = email,
				Name = "Cookie Deleted",
				EmailConfirmed = true,
			};
			Assert.True((await userManager.CreateAsync(user, "Test123!")).Succeeded);
			Assert.True((await userManager.AddToRoleAsync(user, SD.Role_Customer)).Succeeded);
		}

		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		await TestAuthHelper.LoginAsync(client, email, "Test123!");

		using (var scope = factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var user = await db.ApplicationUsers.SingleAsync(item => item.Email == email);
			user.IsDeleted = true;
			await db.SaveChangesAsync();
		}

		var response = await client.GetAsync("/en-US/Customer/Cart/Index");

		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.StartsWith("/Identity/Account/Login?sessionExpired=true", response.Headers.Location!.OriginalString);
	}

	[Fact]
	public async Task AdminUserList_ExcludesSoftDeletedUsers()
	{
		using var factory = new CustomWebApplicationFactory();
		const string email = "hidden.by.delete@vaultshop.local";

		using (var scope = factory.Services.CreateScope())
		{
			var services = scope.ServiceProvider;
			var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
			var user = new ApplicationUser
			{
				UserName = email,
				Email = email,
				Name = "Hidden User",
				EmailConfirmed = true,
			};
			Assert.True((await userManager.CreateAsync(user, "Test123!")).Succeeded);
			Assert.True((await userManager.AddToRoleAsync(user, SD.Role_Customer)).Succeeded);
			user.IsDeleted = true;
			Assert.True((await userManager.UpdateAsync(user)).Succeeded);
		}

		using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
		TestAuthHelper.SetRequestCulture(client, "en-US");
		await TestAuthHelper.LoginAsync(client, factory.AdminEmail, factory.TestPassword);

		var json = await client.GetStringAsync("/en-US/Admin/User/GetAll");

		Assert.DoesNotContain(email, json);
		Assert.Contains(factory.CustomerEmail, json);
	}
}
