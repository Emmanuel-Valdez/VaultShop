using DotNetEnv;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minio;
using System.Threading.RateLimiting;
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
using VaultShop.Web.Services.Pagination;
using VaultShop.Web.Services.Pricing;
using VaultShopRateLimiterOptions = VaultShop.Web.Services.RateLimiting.RateLimiterOptions;
using VaultShop.Web.Services.RichText;
using VaultShop.Web.Services;
using VaultShop.Web.Services.Billing;


DotNetEnv.Env.Load();

// Configure QuestPDF community license before any document generation.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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
builder.Services.Configure<PaginationOptions>(builder.Configuration.GetSection("Pagination"));
builder.Services.Configure<VaultShopRateLimiterOptions>(builder.Configuration.GetSection("RateLimiting"));
builder.Services.AddOptions<PaymentReconciliationOptions>()
    .Bind(builder.Configuration.GetSection(PaymentReconciliationOptions.SectionName))
    .Validate(o => o.BatchSize >= 1, "Payments:Reconciliation:BatchSize must be >= 1.")
    .Validate(o => o.Interval >= TimeSpan.FromMinutes(1), "Payments:Reconciliation:Interval must be >= 00:01:00.")
    .Validate(o => o.StaleAfter < o.MaxAge, "Payments:Reconciliation:StaleAfter must be < MaxAge.")
    .ValidateOnStart();

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
	builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddRazorPages(options =>
{
	options.Conventions.AuthorizeAreaFolder("Identity", "/Account/Manage");
});
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Lockout.AllowedForNewUsers = builder.Configuration.GetValue("Identity:Lockout:AllowedForNewUsers", true);
    options.Lockout.MaxFailedAccessAttempts = builder.Configuration.GetValue<int>("Identity:Lockout:MaxFailedAccessAttempts", 5);
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue<double>("Identity:Lockout:DefaultLockoutTimeSpanMinutes", 5));
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Google OAuth (optional: keys Google:ClientId/Google:ClientSecret, i.e. env vars Google__ClientId/Google__ClientSecret; only registered when both are set).
var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
	builder.Services.AddAuthentication()
		.AddGoogle(options =>
		{
			options.ClientId = googleClientId;
			options.ClientSecret = googleClientSecret;
			options.ClaimActions.MapJsonKey("email_verified", "email_verified");
			options.ClaimActions.MapJsonKey("email_verified", "verified_email");
		});
}
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

// Behind a reverse proxy (Nginx): trust forwarded headers so OAuth builds https:// redirect URIs.
// Without this, Google receives an http://...:8080 redirect_uri and rejects it with redirect_uri_mismatch.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
	options.KnownNetworks.Clear();
	options.KnownProxies.Clear();
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
builder.Services.AddHealthChecks()
	.AddDbContextCheck<ApplicationDbContext>(name: "database")
	.AddCheck<StorageHealthCheck>("storage");
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddHttpClient("GitHub", client =>
{
	client.BaseAddress = new Uri("https://api.github.com/");
	client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
});
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<VaultShop.Web.Services.System.DeploymentVersionService>();
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
builder.Services.AddKeyedScoped<IPaymentRefundService, MercadoPagoPaymentRefundService>(SD.PaymentMethodMercadoPago);
builder.Services.AddScoped<IPaymentStatusService, PaymentStatusService>();
builder.Services.AddScoped<IPricingCalculatorService, PricingCalculatorService>();
builder.Services.AddScoped<IRichTextSanitizer, RichTextSanitizer>();
builder.Services.AddScoped<ITransactionalEmailService, TransactionalEmailService>();
builder.Services.AddScoped<IOrderSummaryService, OrderSummaryService>();
builder.Services.AddScoped<IOrderSummaryPdfGenerator, OrderSummaryPdfGenerator>();
builder.Services.AddScoped<OrderAccessPolicy>();
builder.Services.AddHostedService<PaymentReconciliationBackgroundService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimiter");
        var endpoint = context.HttpContext.GetEndpoint();
        var policy = endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "Global";
        var path = context.HttpContext.Request.Path;
        var key = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        logger.LogWarning("Rate limit rejected: policy={Policy} path={Path} client={Client}", policy, path, key);
        return ValueTask.CompletedTask;
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var opts = httpContext.RequestServices.GetRequiredService<IOptions<VaultShopRateLimiterOptions>>().Value;
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = opts.GlobalPermitLimit,
            Window = opts.GlobalWindow,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = opts.GlobalQueueLimit
        });
    });
    options.AddPolicy("Login", httpContext =>
    {
        var opts = httpContext.RequestServices.GetRequiredService<IOptions<VaultShopRateLimiterOptions>>().Value;
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = opts.LoginPermitLimit,
            Window = opts.LoginWindow,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = opts.LoginQueueLimit
        });
    });
});

var app = builder.Build();

// Log reconciliation config without secrets — validates options on startup.
var reconciliationOptions = app.Services.GetRequiredService<IOptions<PaymentReconciliationOptions>>().Value;
app.Logger.LogInformation(
    "Payment reconciliation config: Enabled={Enabled} Interval={Interval} StaleAfter={StaleAfter} MaxAge={MaxAge} BatchSize={BatchSize}",
    reconciliationOptions.Enabled,
    reconciliationOptions.Interval,
    reconciliationOptions.StaleAfter,
    reconciliationOptions.MaxAge,
    reconciliationOptions.BatchSize);

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

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
StripeConfiguration.ApiKey=builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();
app.UseRouting();

app.UseRateLimiter();
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

// Health endpoints: /health/live reports process liveness only; /health/ready reflects real dependency health.
// No connection strings, keys, or credentials are ever written to the response body.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
	Predicate = _ => false,
	ResponseWriter = WriteHealthResponseAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
	Predicate = _ => true,
	ResponseWriter = WriteHealthResponseAsync
});

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
// bare /privacy y /terms existen solo para Google OAuth; redirigen a URL con cultura.
app.MapGet("/privacy", (HttpContext ctx) => {
	var culture = CultureInfo.CurrentCulture.Name is "en-US" or "es-AR" ? CultureInfo.CurrentCulture.Name : "es-AR";
	return Results.Redirect($"/{culture}/Customer/Home/Privacy{ctx.Request.QueryString}", false);
});
app.MapGet("/terms", (HttpContext ctx) => {
	var culture = CultureInfo.CurrentCulture.Name is "en-US" or "es-AR" ? CultureInfo.CurrentCulture.Name : "es-AR";
	return Results.Redirect($"/{culture}/Customer/Home/Terms{ctx.Request.QueryString}", false);
});
app.MapControllerRoute(
	name: "default",
	pattern: "{culture=es-AR}/{area=Customer}/{controller=Home}/{action=Index}/{id?}",
	defaults: new { culture = "es-AR", area = "Customer", controller = "Home", action = "Index" },
	constraints: new { culture = new RegexRouteConstraint("^(es-AR|en-US)$") });
app.Run();

async Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
	context.Response.ContentType = "application/json; charset=utf-8";
	var checks = report.Entries
		.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
		.ToList();
	await context.Response.WriteAsJsonAsync(new
	{
		status = report.Status.ToString(),
		checks
	});
}

void SeedDatabase()
{
	using (var scope = app.Services.CreateScope())
	{
		var dbInitializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
		dbInitializer.Initialize();
	}
}
public partial class Program { }

