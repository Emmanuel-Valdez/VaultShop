namespace UkiyoDesignsWeb.Services.Branding;

public sealed class BrandingOptions
{
	public string PublicName { get; set; } = "VaultShop";

	public string LogoPath { get; set; } = "/images/brand/vaultshop-logo.svg";

	public string LogoDarkPath { get; set; } = "/images/brand/vaultshop-logo-dark.svg";

	public string MarkPath { get; set; } = "/images/brand/vaultshop-mark.svg";

	public string AppleTouchIconPath { get; set; } = "/images/brand/vaultshop-mark.svg";

	public string SocialPreviewImagePath { get; set; } = "/images/brand/vaultshop-og.svg";

	public string TwitterSite { get; set; } = "@VaultShop";
}
