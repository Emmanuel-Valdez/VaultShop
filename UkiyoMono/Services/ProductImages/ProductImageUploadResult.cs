namespace UkiyoDesignsWeb.Services.ProductImages;

public sealed class ProductImageUploadResult
{
	private readonly List<string> _savedImageUrls = new();
	private readonly List<string> _errors = new();

	public IReadOnlyCollection<string> SavedImageUrls => _savedImageUrls;

	public IReadOnlyCollection<string> Errors => _errors;

	public bool HasErrors => _errors.Count > 0;

	public void AddSavedImageUrl(string imageUrl)
	{
		_savedImageUrls.Add(imageUrl);
	}

	public void AddError(string error)
	{
		_errors.Add(error);
	}
}
