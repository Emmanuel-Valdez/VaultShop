using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using UkiyoDesigns.DataAccess.DbInitializer;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.ViewModels;
using UkiyoDesigns.Utility;
using UkiyoDesignsWeb.Services.ProductImages;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class ProductController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly IStringLocalizer<ProductController> _localizer;
		private readonly IDemoDataSeeder _demoDataSeeder;
		private readonly IProductImageService _productImageService;
		private readonly ILogger<ProductController> _logger;

		public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, IStringLocalizer<ProductController> localizer, IDemoDataSeeder demoDataSeeder, IProductImageService productImageService, ILogger<ProductController> logger)
		{
			_unitOfWork = unitOfWork;
			_webHostEnvironment = webHostEnvironment;
			_localizer = localizer;
			_demoDataSeeder = demoDataSeeder;
			_productImageService = productImageService;
			_logger = logger;
		}
		public IActionResult Index()
		{
			ViewBag.ShowSeedDemoCatalogButton = User.IsInRole(SD.Role_Admin) && !_demoDataSeeder.HasProducts();
			ViewBag.ShowSeedDemoActivityButton = User.IsInRole(SD.Role_Admin) && _demoDataSeeder.HasProducts() && !_demoDataSeeder.HasOrders();
			return View();
		}

		[HttpPost]
		[Authorize(Roles = SD.Role_Admin)]
		[ValidateAntiForgeryToken]
		public IActionResult SeedDemoCatalog()
		{
			if (_demoDataSeeder.HasProducts())
			{
				TempData["error"] = "Demo products were not created because products already exist.";
				return RedirectToAction(nameof(Index));
			}

			try
			{
				_demoDataSeeder.SeedDemoCatalog();
				TempData["success"] = "Demo products were created successfully.";
			}
			catch (Exception ex)
			{
				TempData["error"] = "Demo products could not be created.";
				Console.WriteLine(ex.ToString());
			}

			return RedirectToAction(nameof(Index));
		}

		[HttpPost]
		[Authorize(Roles = SD.Role_Admin)]
		[ValidateAntiForgeryToken]
		public IActionResult SeedDemoActivity()
		{
			if (!_demoDataSeeder.HasProducts())
			{
				TempData["error"] = "Create demo products before creating demo activity.";
				return RedirectToAction(nameof(Index));
			}

			if (_demoDataSeeder.HasOrders())
			{
				TempData["error"] = "Demo activity was not created because orders already exist.";
				return RedirectToAction(nameof(Index));
			}

			try
			{
				_demoDataSeeder.SeedDemoShoppingActivity();
				_demoDataSeeder.SeedDemoOrders();
				TempData["success"] = "Demo users, carts, favorites, and orders were created successfully.";
			}
			catch (Exception ex)
			{
				TempData["error"] = "Demo activity could not be created.";
				Console.WriteLine(ex.ToString());
			}

			return RedirectToAction(nameof(Index));
		}
		public IActionResult Upsert(int? id)
		{

			//ViewBag.CategoryList = CategoryList;
			//ViewData["CategoryList"] = CategoryList; <@(ViewData["CategoryList"] as IEnumerable<SelectListItem>)>

			ProductVM productVM = new()
			{
				CategoryList = _unitOfWork.Category.GetAll(u => u.IsDeleted == false).Select(u => new SelectListItem
				{
					Text = u.Name,
					Value = u.Id.ToString()
				}),
				Product = new Product
				{
					IsAvailableInStore = true
				}
			};
			if (id == 0 || id == null)
			{
				//create
				return View(productVM);
			}
			else
			{
				//update
				var product = _unitOfWork.Product.Get(u => u.Id == id && u.IsDeleted == false, includeProperties: "ProductImages");
				if (product == null)
				{
					return NotFound();
				}

				productVM.Product = product;
				return View(productVM);
			}
		}
		[HttpPost]
		public async Task<IActionResult> Upsert(ProductVM productVM, List<IFormFile> files)
		{
			if (productVM.Product.FinalWholesalePrice > productVM.Product.FinalRetailPrice && productVM.Product.Id != 0 && productVM.Product.FinalRetailPrice > 0)
			{
				ModelState.AddModelError("", _localizer["MinorLowerMajor"].Value);
			}
			if (!ModelState.IsValid)
			{
				PopulateProductFormData(productVM);
				return View(productVM);
			}
			try
			{
				var isNewProduct = productVM.Product.Id == 0;

				if (productVM.Product.Id == 0)
				{
					productVM.Product.GarmentHardwareByProduct = new();
					productVM.Product.FabricByProduct = new();
					_unitOfWork.Product.Add(productVM.Product);
				}
				else
				{
					_unitOfWork.Product.Update(productVM.Product);
				}
				_unitOfWork.Save();

				var imageUploadResult = await _productImageService.SaveProductImagesAsync(productVM.Product.Id, files);
				if (imageUploadResult.HasErrors)
				{
					ModelState.Clear();
					foreach (var error in imageUploadResult.Errors)
					{
						ModelState.AddModelError("files", error);
					}

					PopulateProductFormData(productVM);
					return View(productVM);
				}

				if (imageUploadResult.SavedImageUrls.Count > 0)
				{
					productVM.Product.ProductImages ??= new List<ProductImage>();
					foreach (var imageUrl in imageUploadResult.SavedImageUrls)
					{
						productVM.Product.ProductImages.Add(new ProductImage
						{
							ImageUrl = imageUrl,
							ProductId = productVM.Product.Id,
						});
					}

					_unitOfWork.Product.Update(productVM.Product);
					_unitOfWork.Save();
				}

				TempData["success"] = isNewProduct
					? _localizer["ProductCreatedSuccesfully"].Value
					: _localizer["ProductUpdatedSuccessfully"].Value;

				return RedirectToAction("Index");

			}
			catch (Exception ex)
			{
				TempData["error"] = _localizer["UnexpectedError"].Value;
				_logger.LogError(ex, "Unexpected error while saving product {ProductId} and processing product image uploads.", productVM.Product.Id);
				PopulateProductFormData(productVM);
				return View(productVM);
			}
		}

		private void PopulateProductFormData(ProductVM productVM)
		{
			productVM.CategoryList = _unitOfWork.Category.GetAll(u => u.IsDeleted == false).Select(u => new SelectListItem
			{
				Text = u.Name,
				Value = u.Id.ToString()
			});

			if (productVM.Product.Id == 0)
			{
				return;
			}

			var productWithImages = _unitOfWork.Product.Get(u => u.Id == productVM.Product.Id && u.IsDeleted == false, includeProperties: "ProductImages");
			productVM.Product.ProductImages = productWithImages?.ProductImages ?? new List<ProductImage>();
		}

		public IActionResult DeleteImage(int imageId)
		{
			var imageToBeDeleted = _unitOfWork.ProductImage.Get(u => u.Id == imageId);
			if (imageToBeDeleted == null)
			{
				return NotFound();
			}

			int productId = imageToBeDeleted.ProductId;
			if (imageToBeDeleted != null)
			{
				if (!string.IsNullOrEmpty(imageToBeDeleted.ImageUrl))
				{
					var oldImagePath = Path.Combine(_webHostEnvironment.WebRootPath, imageToBeDeleted.ImageUrl.TrimStart('\\'));
					if (System.IO.File.Exists(oldImagePath))
						System.IO.File.Delete(oldImagePath);
				}
				_unitOfWork.ProductImage.Remove(imageToBeDeleted);
				_unitOfWork.Save();
				TempData["success"] = _localizer["DeletedSuccessfully"].Value;
			}
			return RedirectToAction(nameof(Upsert), new { id = productId });
		}

		#region API CALLS

		[HttpGet]
		public IActionResult GetAll()
		{
			List<Product> objProductList = _unitOfWork.Product.GetAll(u => u.IsDeleted == false, includeProperties: "Category").ToList();
			return Json(new { data = objProductList });

		}
		[HttpPost]
		public IActionResult Delete(int? id)
		{
			if (id == null)
			{
				return BadRequest(_localizer["IdCantBeNull"].Value);
			}
			Product? productToBeDeleted = _unitOfWork.Product.Get(u => u.Id == id && u.IsDeleted == false);
			if (productToBeDeleted == null)
			{
				return StatusCode(500, new { success = false, message = _localizer["DeletingError"].Value });
			}

			string productPath = @"images\products\product-" + id;
			string finalPath = Path.Combine(_webHostEnvironment.WebRootPath, productPath);

			if (Directory.Exists(finalPath))
			{
				string[] filePaths = Directory.GetFiles(finalPath);
				foreach (string filePath in filePaths)
				{
					System.IO.File.Delete(filePath);
				}

				Directory.Delete(finalPath);
			}

			productToBeDeleted.IsDeleted = true;
			_unitOfWork.Product.Update(productToBeDeleted);
			_unitOfWork.Save();
			return Ok(new { success = true, message = _localizer["DeletedSuccessfully"].Value });

		}
		[HttpPost]
		public IActionResult UpdateProductAvailability([FromBody] int id)
		{
			var productFromDB = _unitOfWork.Product.Get(u => u.Id == id && u.IsDeleted == false);
			if (productFromDB == null)
			{
				return Json(new { success = true, message = _localizer["ErrorWhileUpdating"].Value });
			}
			productFromDB.IsAvailableInStore = !productFromDB.IsAvailableInStore;
			_unitOfWork.Product.Update(productFromDB);
			_unitOfWork.Save();
			return Ok(new { success = true, message = _localizer["AvailabilityUpdated"].Value });
		}
		#endregion
	}
}

