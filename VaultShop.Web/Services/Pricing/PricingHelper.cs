using System.Security.Claims;
using VaultShop.Models;
using VaultShop.Utility;

namespace VaultShop.Web.Services.Pricing;

public static class PricingHelper
{
    public static bool IsWholesalePreview(HttpContext? httpContext)
    {
        if (httpContext?.User is null) return false;
        var user = httpContext.User;
        if (!user.IsInRole(SD.Role_Admin) && !user.IsInRole(SD.Role_Employee)) return false;
        return string.Equals(httpContext.Session.GetString(SD.AdminPreviewMode), "wholesale", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldUseWholesale(ClaimsPrincipal user, HttpContext? httpContext)
    {
        if (user.IsInRole(SD.Role_Company)) return true;
        return IsWholesalePreview(httpContext);
    }

    public static decimal GetPrice(Product product, ClaimsPrincipal user, HttpContext? httpContext)
        => ShouldUseWholesale(user, httpContext) ? product.FinalWholesalePrice : product.FinalRetailPrice;
}
