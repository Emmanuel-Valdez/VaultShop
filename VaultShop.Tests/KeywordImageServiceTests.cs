using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using SkiaSharp;
using VaultShop.Web.Services.ImageStorage;
using VaultShop.Web.Services.KeywordImages;

namespace VaultShop.Web.Tests;

public class KeywordImageServiceTests
{
	[Fact]
	public async Task SaveChipAsync_EmptyFile_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([], "chip.jpg", "image/jpeg");

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(() => service.SaveChipAsync(1, file));

		Assert.Equal("UploadFileEmpty", ex.Message);
	}

	[Fact]
	public async Task SaveChipAsync_OversizedFile_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = new FormFile(new MemoryStream([1]), 0, 10 * 1024 * 1024 + 1, "file", "chip.jpg")
		{
			Headers = new HeaderDictionary(),
			ContentType = "image/jpeg"
		};

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(() => service.SaveChipAsync(1, file));

		Assert.Equal("UploadFileTooLarge", ex.Message);
	}

	[Fact]
	public async Task SaveChipAsync_UnsupportedExtension_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([1, 2, 3], "chip.exe", "image/jpeg");

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(() => service.SaveChipAsync(1, file));

		Assert.Equal("UploadFileInvalidExtension", ex.Message);
	}

	[Fact]
	public async Task SaveChipAsync_UnsupportedContentType_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([1, 2, 3], "chip.jpg", "application/pdf");

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(() => service.SaveChipAsync(1, file));

		Assert.Equal("UploadFileInvalidContentType", ex.Message);
	}

	[Fact]
	public async Task SaveChipAsync_UndecodableImage_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([1, 2, 3], "chip.jpg", "image/jpeg");

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(() => service.SaveChipAsync(1, file));

		Assert.Equal("UploadFileInvalidImage", ex.Message);
	}

	[Fact]
	public async Task SaveChipAsync_ValidImage_StoresBytesThroughStorageAbstraction()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			var file = CreateFormFile(CreateValidPngBytes(), "chip.png", "image/png");

			var stored = await service.SaveChipAsync(42, file);

			Assert.StartsWith("images/keywords/keyword-42/", stored.ObjectKey);
			Assert.EndsWith(".jpg", stored.ObjectKey);
			Assert.Equal("image/jpeg", stored.ContentType);
			Assert.Equal(LocalImageStorageService.ProviderName, stored.StorageProvider);
			Assert.True(stored.SizeBytes > 0);

			var savedPath = Path.Combine(webRootPath, stored.ObjectKey.Replace('/', Path.DirectorySeparatorChar));
			Assert.True(File.Exists(savedPath));
			using var savedChip = SKBitmap.Decode(savedPath);
			Assert.Equal(400, savedChip.Width);
			Assert.Equal(400, savedChip.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task SaveCoverAsync_EmptyFile_RejectsWithLocalizedError()
	{
		var service = CreateService();
		var file = CreateFormFile([], "cover.jpg", "image/jpeg");

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(() => service.SaveCoverAsync(1, file));

		Assert.Equal("UploadFileEmpty", ex.Message);
	}

	[Fact]
	public async Task SaveCoverAsync_OneMaster_ProducesThreeCropsWithDistinctKeys()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			var png = CreateSolidPngBytes(2100, 900, SKColors.Red);

			var variants = await service.SaveCoverAsync(7, CreateFormFile(png, "cover.png", "image/png"));

			Assert.NotNull(variants.Large);
			Assert.NotNull(variants.Medium);
			Assert.StartsWith("images/keywords/keyword-7/", variants.Large.ObjectKey);
			Assert.StartsWith("images/keywords/keyword-7/", variants.Medium.ObjectKey);
			Assert.StartsWith("images/keywords/keyword-7/", variants.Small.ObjectKey);
			Assert.NotEqual(variants.Large.ObjectKey, variants.Medium.ObjectKey);
			Assert.NotEqual(variants.Large.ObjectKey, variants.Small.ObjectKey);
			Assert.NotEqual(variants.Medium.ObjectKey, variants.Small.ObjectKey);

			using var savedLarge = SKBitmap.Decode(Path.Combine(webRootPath, variants.Large.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			using var savedMedium = SKBitmap.Decode(Path.Combine(webRootPath, variants.Medium.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			using var savedSmall = SKBitmap.Decode(Path.Combine(webRootPath, variants.Small.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			Assert.Equal(1905, savedLarge.Width);
			Assert.Equal(714, savedLarge.Height);
			Assert.Equal(1280, savedMedium.Width);
			Assert.Equal(480, savedMedium.Height);
			Assert.Equal(768, savedSmall.Width);
			Assert.Equal(288, savedSmall.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task SaveCoverAsync_SquareMaster_CenterCropsWithoutLetterbox()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			var square = CreateSolidPngBytes(2200, 2200, SKColors.Red);

			var variants = await service.SaveCoverAsync(9, CreateFormFile(square, "cover.png", "image/png"));

			Assert.NotNull(variants.Large);
			Assert.NotNull(variants.Medium);
			foreach (var stored in new[] { variants.Large, variants.Medium, variants.Small })
			{
				using var saved = SKBitmap.Decode(Path.Combine(webRootPath, stored.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
				// ponytail: solid-red master — a letterboxed contain would leave white bars at edges/corners
				foreach (var (x, y) in new[] { (10, 10), (saved.Width - 10, 10), (10, saved.Height - 10), (saved.Width - 10, saved.Height - 10), (saved.Width / 2, saved.Height / 2) })
				{
					var pixel = saved.GetPixel(x, y);
					Assert.True(pixel.Red > 200 && pixel.Green < 80 && pixel.Blue < 80,
						$"Expected full-bleed red crop, got ({pixel.Red},{pixel.Green},{pixel.Blue}) at ({x},{y}).");
				}
			}
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task ChipCoverAndReplace_PersistIndependentlyUnderSamePrefix()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			var png = CreateValidPngBytes();

			var chip = await service.SaveChipAsync(7, CreateFormFile(png, "chip.png", "image/png"));
			var cover = await service.SaveCoverAsync(7, CreateFormFile(CreateSolidPngBytes(2100, 900, SKColors.Red), "cover.png", "image/png"));
			var replacedChip = await service.SaveChipAsync(7, CreateFormFile(png, "chip2.png", "image/png"));

			Assert.StartsWith("images/keywords/keyword-7/", chip.ObjectKey);
			Assert.NotNull(cover.Large);
			Assert.StartsWith("images/keywords/keyword-7/", cover.Large.ObjectKey);
			Assert.StartsWith("images/keywords/keyword-7/", replacedChip.ObjectKey);

			Assert.NotEqual(chip.ObjectKey, cover.Large.ObjectKey);
			Assert.NotEqual(chip.ObjectKey, replacedChip.ObjectKey);

			Assert.True(File.Exists(Path.Combine(webRootPath, replacedChip.ObjectKey.Replace('/', Path.DirectorySeparatorChar))));

			using var savedCover = SKBitmap.Decode(Path.Combine(webRootPath, cover.Large.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			Assert.Equal(1905, savedCover.Width);
			Assert.Equal(714, savedCover.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task SaveCoverAsync_ExactRatioMaster_PreservesFullContent()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			// ponytail: blue edge bars die under any center crop; surviving bars prove the zero-crop path.
			// Bars are 40px wide (sampled at center) so JPEG chroma bleed can't fake a failure.
			var master = CreatePaintedPngBytes(1905, 714, bitmap =>
			{
				bitmap.Erase(SKColors.Red);
				using var canvas = new SKCanvas(bitmap);
				using var blue = new SKPaint { Color = SKColors.Blue };
				canvas.DrawRect(0, 0, 40, 714, blue);
				canvas.DrawRect(1905 - 40, 0, 40, 714, blue);
			});

			var variants = await service.SaveCoverAsync(11, CreateFormFile(master, "cover.png", "image/png"));

			Assert.NotNull(variants.Large);
			Assert.NotNull(variants.Medium);
			foreach (var stored in new[] { variants.Large, variants.Medium, variants.Small })
			{
				using var saved = SKBitmap.Decode(Path.Combine(webRootPath, stored.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
				// ponytail: bar-center in output coords — the bar narrows with each downscale.
				var edge = saved.Width * 20 / 1905;
				AssertDominantBlue(saved.GetPixel(edge, saved.Height / 2), "left edge");
				AssertDominantBlue(saved.GetPixel(saved.Width - 1 - edge, saved.Height / 2), "right edge");
				var center = saved.GetPixel(saved.Width / 2, saved.Height / 2);
				Assert.True(center.Red > 200 && center.Blue < 80, "center must stay red.");
			}
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task SaveCoverAsync_SmallMaster_RefusesUpscale()
	{
		var service = CreateService();
		// ponytail: 600x400 center-crops to 600x225 — below the 768x288 floor, so no variant fits.
		var small = CreateSolidPngBytes(600, 400, SKColors.Red);

		var ex = await Assert.ThrowsAsync<KeywordImageValidationException>(
			() => service.SaveCoverAsync(1, CreateFormFile(small, "cover.png", "image/png")));

		Assert.Equal("UploadCoverTooSmall", ex.Message);
	}

	[Fact]
	public async Task SaveCoverAsync_MediumMaster_SkipsLargeOnly()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			// ponytail: 1400x600 center-crops to 1400x524 — covers 1280x480 but not 1905x714.
			var master = CreateSolidPngBytes(1400, 600, SKColors.Red);

			var variants = await service.SaveCoverAsync(12, CreateFormFile(master, "cover.png", "image/png"));

			Assert.Null(variants.Large);
			Assert.NotNull(variants.Medium);
			using var savedMedium = SKBitmap.Decode(Path.Combine(webRootPath, variants.Medium.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			Assert.Equal(1280, savedMedium.Width);
			Assert.Equal(480, savedMedium.Height);
			using var savedSmall = SKBitmap.Decode(Path.Combine(webRootPath, variants.Small.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			Assert.Equal(768, savedSmall.Width);
			Assert.Equal(288, savedSmall.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task SaveCoverAsync_NarrowMaster_YieldsSmallOnlyWithoutUpscale()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			// ponytail: exact-ratio 800x300 — the user case: only the 768 variant fits, nothing upscales.
			var master = CreateSolidPngBytes(800, 300, SKColors.Red);

			var variants = await service.SaveCoverAsync(14, CreateFormFile(master, "cover.png", "image/png"));

			Assert.Null(variants.Large);
			Assert.Null(variants.Medium);
			using var savedSmall = SKBitmap.Decode(Path.Combine(webRootPath, variants.Small.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			Assert.Equal(768, savedSmall.Width);
			Assert.Equal(288, savedSmall.Height);
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task SaveCoverAsync_TransparentMaster_FlattensWithoutHalo()
	{
		var webRootPath = Directory.CreateTempSubdirectory("vaultshop-keyword-image-tests-").FullName;
		try
		{
			var service = CreateService(webRootPath);
			var master = CreatePaintedPngBytes(2100, 900, bitmap =>
			{
				bitmap.Erase(SKColors.Transparent);
				using var canvas = new SKCanvas(bitmap);
				using var red = new SKPaint { Color = SKColors.Red };
				canvas.DrawRect(1050, 0, 1050, 900, red);
			});

			var variants = await service.SaveCoverAsync(13, CreateFormFile(master, "cover.png", "image/png"));

			Assert.NotNull(variants.Large);
			using var saved = SKBitmap.Decode(Path.Combine(webRootPath, variants.Large.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
			var flat = saved.GetPixel(30, saved.Height / 2);
			Assert.True(flat.Red > 200 && flat.Green > 200 && flat.Blue > 200,
				$"Transparent area must flatten to white, got ({flat.Red},{flat.Green},{flat.Blue}).");
			var red = saved.GetPixel(saved.Width - 30, saved.Height / 2);
			Assert.True(red.Red > 200 && red.Green < 80 && red.Blue < 80,
				$"Opaque area must stay red, got ({red.Red},{red.Green},{red.Blue}).");
		}
		finally
		{
			Directory.Delete(webRootPath, recursive: true);
		}
	}

	private static void AssertDominantBlue(SKColor pixel, string where)
		=> Assert.True(pixel.Blue > 200 && pixel.Red < 80, $"{where} bar must survive, got ({pixel.Red},{pixel.Green},{pixel.Blue}).");

	private static KeywordImageService CreateService(string? webRootPath = null)
	{
		var environment = new Mock<IWebHostEnvironment>();
		environment.Setup(x => x.WebRootPath).Returns(webRootPath ?? Path.GetTempPath());
		var imageStorageService = new LocalImageStorageService(environment.Object, Mock.Of<ILogger<LocalImageStorageService>>());

		var localizer = new Mock<IStringLocalizer<KeywordImageService>>();
		localizer.Setup(x => x[It.IsAny<string>()])
			.Returns((string key) => new LocalizedString(key, key));

		return new KeywordImageService(imageStorageService, Mock.Of<ILogger<KeywordImageService>>(), localizer.Object);
	}

	private static FormFile CreateFormFile(byte[] content, string fileName, string contentType)
	{
		return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
		{
			Headers = new HeaderDictionary(),
			ContentType = contentType
		};
	}

	private static byte[] CreateValidPngBytes()
	{
		return CreateSolidPngBytes(600, 400, SKColors.Red);
	}

	private static byte[] CreateSolidPngBytes(int width, int height, SKColor color)
	{
		using var bitmap = new SKBitmap(width, height);
		bitmap.Erase(color);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}

	private static byte[] CreatePaintedPngBytes(int width, int height, Action<SKBitmap> paint)
	{
		using var bitmap = new SKBitmap(width, height);
		paint(bitmap);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}
}
