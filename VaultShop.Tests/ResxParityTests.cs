using System.Xml.Linq;

namespace VaultShop.Web.Tests;

// envio-gratis-o-cargo 7.2: every key defined in a touched .en.resx must exist in its .es.resx,
// so no es-AR page silently falls back to the English default at runtime.
public class ResxParityTests
{
	private static readonly string[] ResourceBases =
	[
		"Areas/Customer/Views/Cart/Index",
		"Areas/Customer/Views/Cart/OrderConfirmation",
		"Areas/Customer/Views/Cart/Summary",
		"Areas/Customer/Controllers/CartController",
		"Areas/Admin/Views/Order/Details",
		"Areas/Admin/Views/Order/Summary",
		"Areas/Admin/Views/Product/Upsert",
		"Areas/Admin/Views/ProductPrice/Index",
		"Services/Billing/OrderSummaryPdfGenerator",
		"Views/Shared/_DiscountPrice",
		"Views/Shared/_BranchCascadePicker",
	];

	public static IEnumerable<object[]> ResourcePairs() => ResourceBases.Select(b => new object[] { b });

	[Theory]
	[MemberData(nameof(ResourcePairs))]
	public void SpanishResource_DefinesEveryEnglishKey(string relativeBase)
	{
		var en = LoadKeys(relativeBase + ".en.resx");
		var es = LoadKeys(relativeBase + ".es.resx");

		var missing = en.Except(es).OrderBy(k => k, StringComparer.Ordinal).ToList();

		Assert.True(missing.Count == 0,
			$"{relativeBase}.es.resx is missing {missing.Count} key(s): {string.Join(", ", missing)}");
	}

	private static HashSet<string> LoadKeys(string relativePath)
	{
		var path = Path.GetFullPath(Path.Combine(
			AppContext.BaseDirectory, "..", "..", "..", "..", "VaultShop.Web", "Resources",
			relativePath.Replace('/', Path.DirectorySeparatorChar)));

		return XDocument.Load(path).Root!
			.Elements("data")
			.Select(e => (string?)e.Attribute("name"))
			.Where(name => !string.IsNullOrEmpty(name))
			.Select(name => name!)
			.ToHashSet(StringComparer.Ordinal);
	}
}
