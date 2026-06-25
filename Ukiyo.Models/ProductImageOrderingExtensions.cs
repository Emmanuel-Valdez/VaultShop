namespace UkiyoDesigns.Models;

public static class ProductImageOrderingExtensions
{
	public static IEnumerable<ProductImage> OrderedForDisplay(this IEnumerable<ProductImage> images)
	{
		return images
			.OrderByDescending(image => image.IsPrimary)
			.ThenBy(image => image.SortOrder)
			.ThenBy(image => image.Id);
	}

	public static ProductImage? PrimaryOrFirstForDisplay(this IEnumerable<ProductImage>? images)
	{
		return images?.OrderedForDisplay().FirstOrDefault();
	}
}
