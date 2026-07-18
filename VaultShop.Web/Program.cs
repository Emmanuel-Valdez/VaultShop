using DotNetEnv;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Stripe;
using System.Globalization;
using System.Net.Http.Headers;
using VaultShop.DataAccess.Data;
using VaultShop.DataAccess.DbInitializer;
using VaultShop.DataAccess.Repository;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Checkout;
using VaultShop.Web.Services.Branding;
using VaultShop.Web.Services.Email;
using VaultShop.Web.Services.ImageStorage;
using VaultShop.Web.Services.ProductImages;
using VaultShop.Web.Services.Payments;
using VaultShop.Web.Services.Pricing;
using VaultShop.Web.Services.RichText;


DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


var supportedCultures = new[]
{
	new CultureInfo("en-US"),
	new CultureInfo("es-AR"),
};
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
	options.DefaultRequestCulture = new RequestCulture("es-AR");
	options.SupportedUICultures = supportedCultures;
	options.SupportedCultures = supportedCultures;
	options.FallBackToParentCultures = true;
	options.FallBackToParentUICultures = true;
	var requestProvider = new RouteDataRequestCultureProvider(); 
	options.RequestCultureProviders.Insert(0, requestProvider);
});
builder.Services.AddControllersWithViews(options =>
{
	options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
}).AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
	.AddDataAnnotationsLocalization(); 
builder.Services.AddDbContext<ApplicationDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.Configure<BrandingOptions>(builder.Configuration.GetSection("Branding"));
builder.Services.Configure<ThemeOptions>(builder.Configuration.GetSection("Theme"));

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
	builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddRazorPages(options =>
{
	options.Conventions.AuthorizeAreaFolder("Identity", "/Account/Manage");
});
builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
	options.ValidationInterval = TimeSpan.Zero;
});
//Aotorization allways go after identity user!!
builder.Services.ConfigureApplicationCookie(options =>
{
	options.LoginPath = $"/Identity/Account/Login";
	options.LogoutPath = $"/Identity/Account/Logout";
	options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
	options.Events.OnValidatePrincipal = async context =>
	{
		if (context.Principal == null)
		{
			return;
		}

		var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
		var user = await userManager.GetUserAsync(context.Principal);
		if (user != null)
		{
			return;
		}

		context.RejectPrincipal();
		context.HttpContext.Items["SessionExpired"] = true;
		await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
	};
	options.Events.OnRedirectToLogin = context =>
	{
		if (context.HttpContext.Items.ContainsKey("SessionExpired"))
		{
			var returnUrl = Uri.EscapeDataString(context.Request.PathBase + context.Request.Path + context.Request.QueryString);
			context.RedirectUri = $"{options.LoginPath}?sessionExpired=true&returnUrl={returnUrl}";
		}

		context.Response.Redirect(context.RedirectUri);
		return Task.CompletedTask;
	};
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromMinutes(100);
	options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
});
builder.Services.AddScoped<IDbInitializer, DbInitializer>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
var useFakeEmailSender = builder.Configuration.GetValue("Email:UseFakeEmailSender", true);
var emailProvider = builder.Configuration["Email:Provider"];
builder.Services.AddScoped<IEmailSender>(serviceProvider =>
{
	var selectedProvider = string.IsNullOrWhiteSpace(emailProvider)
		? useFakeEmailSender ? "Fake" : "Unconfigured"
		: emailProvider;

	return selectedProvider.Trim().ToUpperInvariant() switch
	{
		"FAKE" => serviceProvider.GetRequiredService<FakeEmailSender>(),
		"RESEND" => serviceProvider.GetRequiredService<ResendEmailSender>(),
		"UNCONFIGURED" => serviceProvider.GetRequiredService<UnconfiguredEmailSender>(),
		_ => throw new InvalidOperationException($"Unsupported Email:Provider value '{selectedProvider}'. Use Fake, Resend, or Unconfigured.")
	};
});
builder.Services.AddScoped<FakeEmailSender>();
builder.Services.AddScoped<ResendEmailSender>();
builder.Services.AddScoped<UnconfiguredEmailSender>();
builder.Services.Configure<ImageStorageOptions>(builder.Configuration.GetSection("ImageStorage"));
builder.Services.Configure<MinioStorageOptions>(builder.Configuration.GetSection("ImageStorage:Minio"));

var imageStorageProvider = builder.Configuration["ImageStorage:Provider"] ?? "Local";
switch (imageStorageProvider.Trim().ToUpperInvariant())
{
	case "LOCAL":
		builder.Services.AddScoped<IImageStorageService, LocalImageStorageService>();
		break;
	case "MINIO":
		builder.Services.AddSingleton<IMinioClient>(serviceProvider =>
		{
			var options = serviceProvider.GetRequiredService<IOptions<MinioStorageOptions>>().Value;
			return new MinioClient()
				.WithEndpoint(options.Endpoint)
				.WithCredentials(options.AccessKey, options.SecretKey)
				.WithSSL(options.UseSsl)
				.Build();
		});
		builder.Services.AddScoped<IImageStorageService, MinioImageStorageService>();
		break;
	default:
		throw new InvalidOperationException($"Unsupported ImageStorage:Provider value '{imageStorageProvider}'. Use Local or Minio.");
}

builder.Services.AddScoped<IProductImageService, ProductImageService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddHttpClient("MercadoPago", client =>
{
	client.BaseAddress = new Uri("https://api.mercadopago.com");
	client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
	var mercadoPagoAccessToken = builder.Configuration["Payments:MercadoPagoAccessToken"];
	if (!string.IsNullOrWhiteSpace(mercadoPagoAccessToken))
	{
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mercadoPagoAccessToken);
	}
});
builder.Services.AddScoped<IStripeCheckoutSessionClient, StripeCheckoutSessionClient>();
builder.Services.AddScoped<IPaymentRefundService, StripePaymentRefundService>();
builder.Services.AddScoped<IPaymentSessionService, StripePaymentSessionService>();
builder.Services.AddKeyedScoped<IPaymentSessionService, StripePaymentSessionService>(SD.PaymentMethodStripe);
builder.Services.AddKeyedScoped<IPaymentSessionService, MercadoPagoPaymentSessionService>(SD.PaymentMethodMercadoPago);
builder.Services.AddScoped<IPaymentStatusService, PaymentStatusService>();
builder.Services.AddScoped<IPricingCalculatorService, PricingCalculatorService>();
builder.Services.AddScoped<IRichTextSanitizer, RichTextSanitizer>();
builder.Services.AddScoped<ITransactionalEmailService, TransactionalEmailService>();

var facebookAppId = builder.Configuration["Facebook:AppId"];
var facebookAppSecret = builder.Configuration["Facebook:AppSecret"];
if (!string.IsNullOrWhiteSpace(facebookAppId) && !string.IsNullOrWhiteSpace(facebookAppSecret))
{
	builder.Services.AddAuthentication().AddFacebook(option =>
	{
		option.AppId = facebookAppId;
		option.AppSecret = facebookAppSecret;
	});
}
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseDeveloperExceptionPage();
}
else
{
	app.UseExceptionHandler("/Home/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnet-hsts.
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
StripeConfiguration.ApiKey=builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();
app.UseRouting();

app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

var runMigrationsOnStartup = builder.Configuration.GetValue("Database:RunMigrationsOnStartup", true);
if (runMigrationsOnStartup)
{
	SeedDatabase();
}
else
{
	app.Logger.LogInformation("Skipping startup database initialization because Database:RunMigrationsOnStartup is false.");
}

app.MapRazorPages();

app.MapGet("/site.webmanifest", (IOptions<BrandingOptions> brandingOptions) =>
{
	var branding = brandingOptions.Value;

	return Results.Json(new
	{
		name = branding.PublicName,
		short_name = branding.PublicName,
		icons = new[]
		{
			new { src = branding.MarkPath, sizes = "any", purpose = "any" },
			new { src = branding.AppleTouchIconPath, sizes = "180x180", purpose = "any" }
		},
		theme_color = "#ffffff",
		background_color = "#ffffff",
		display = "standalone"
	}, contentType: "application/manifest+json");
});
app.MapControllerRoute(
	name: "default",
	pattern: "{culture=es-AR}/{area=Customer}/{controller=Home}/{action=Index}/{id?}");
app.Run();

void SeedDatabase()
{
	using (var scope = app.Services.CreateScope())
	{
		var dbInitializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
		dbInitializer.Initialize();
	}
}
public partial class Program { }

