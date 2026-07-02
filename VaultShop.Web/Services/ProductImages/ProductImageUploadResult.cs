using VaultShop.Web.Services.ImageStorage;

namespace VaultShop.Web.Services.ProductImages;

public sealed class ProductImageUploadResult
{
	private readonly List<StoredImage> _savedImages = new();
	private readonly List<string> _errors = new();

	public IReadOnlyCollection<StoredImage> SavedImages => _savedImages;

	public IReadOnlyCollection<string> SavedImageUrls => _savedImages.Select(image => image.ImageUrl).ToArray();

	public IReadOnlyCollection<string> Errors => _errors;

	public bool HasErrors => _errors.Count > 0;

	public void AddSavedImage(StoredImage image)
	{
		_savedImages.Add(image);
	}

	public void AddError(string error)
	{
		_errors.Add(error);
	}
}
