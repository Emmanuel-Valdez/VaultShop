using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using NuGet.DependencyResolver;
using SkiaSharp;
using System.Drawing;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.DTO;
using UkiyoDesigns.Models.ViewModels;
using UkiyoDesigns.Utility;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class ProductController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IWebHostEnvironment _webHostEnvironment;
		private readonly IStringLocalizer<ProductController> _localizer;

		public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, IStringLocalizer<ProductController> localizer)
		{
			_unitOfWork = unitOfWork;
			_webHostEnvironment = webHostEnvironment;
			_localizer = localizer;
		}
		public IActionResult Index()
		{
			return View();
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
				Product = new Product()
			};
			if (id == 0 || id == null)
			{
				//create
				return View(productVM);
			}
			else
			{
				//update
				productVM.Product = _unitOfWork.Product.Get(u => u.Id == id && u.IsDeleted == false, includeProperties: "ProductImages");
				return View(productVM);
			}
		}
		[HttpPost]
		public IActionResult Upsert(ProductVM productVM, List<IFormFile> files)
		{
			if (productVM.Product.FinalWholesalePrice > productVM.Product.FinalRetailPrice && productVM.Product.Id != 0 && productVM.Product.FinalRetailPrice > 0)
			{
				ModelState.AddModelError("", _localizer["MinorLowerMajor"].Value);
			}
			if (!ModelState.IsValid)
			{
				productVM.CategoryList = _unitOfWork.Category.GetAll(u => u.IsDeleted == false).Select(u => new SelectListItem
				{
					Text = u.Name,
					Value = u.Id.ToString()
				});
				return View(productVM);
			}
			try
			{
				if (productVM.Product.Id == 0)
				{
					productVM.Product.GarmentHardwareByProduct = new();
					productVM.Product.FabricByProduct = new();
					_unitOfWork.Product.Add(productVM.Product);

					TempData["success"] = _localizer["ProductCreatedSuccesfully"].Value;
				}
				else
				{
					_unitOfWork.Product.Update(productVM.Product);
					TempData["success"] = _localizer["ProductUpdatedSuccessfully"].Value;
				}
				_unitOfWork.Save();

				string wwwRoothPath = _webHostEnvironment.WebRootPath;


				if (files != null)
				{
					foreach (IFormFile file in files)
					{
						string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
						string productPath = @"images\products\product-" + productVM.Product.Id;
						string finalPath = Path.Combine(wwwRoothPath, productPath);
						if (!Directory.Exists(finalPath))
							Directory.CreateDirectory(finalPath);

						using (var memoryStream = new MemoryStream())
						{
							// Copia el archivo original en un MemoryStream
							file.CopyTo(memoryStream);
							memoryStream.Seek(0, SeekOrigin.Begin);

							// Optimiza la imagen
							string outputFilePath = Path.Combine(finalPath, fileName);
							int outputWidth = 1000;
							int outputHeight = 1200;

							using (var original = SKBitmap.Decode(memoryStream))
							{
								// Calcula el nuevo tamaño manteniendo la relación de aspecto
								int newWidth, newHeight;
								float aspectRatio = (float)original.Width / original.Height;
								if (original.Width > original.Height)
								{
									newWidth = outputWidth;
									newHeight = (int)(outputWidth / aspectRatio);
								}
								else
								{
									newWidth = (int)(outputHeight * aspectRatio);
									newHeight = outputHeight;
								}

								using (var resizedBitmap = new SKBitmap(newWidth, newHeight))
								{
									using (var canvas = new SKCanvas(resizedBitmap))
									{
										// Dibuja la imagen redimensionada en el lienzo
										canvas.DrawBitmap(original, new SKRect(0, 0, newWidth, newHeight));
									}

									// Crear un bitmap con el tamaño de salida y fondo sólido
									using (var finalBitmap = new SKBitmap(outputWidth, outputHeight))
									{
										using (var canvas = new SKCanvas(finalBitmap))
										{
											// Dibuja el fondo sólido
											canvas.Clear(SKColors.White); // Cambia SKColors.White por el color deseado

											// Calcula la posición para centrar la imagen redimensionada
											int offsetX = (outputWidth - newWidth) / 2;
											int offsetY = (outputHeight - newHeight) / 2;

											// Dibuja la imagen redimensionada en el centro del fondo sólido
											canvas.DrawBitmap(resizedBitmap, offsetX, offsetY);
										}

										using (var image = SKImage.FromBitmap(finalBitmap))
										{
											using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 75)) // Calidad 75%
											{
												// Guarda la imagen final en el archivo de salida
												using (var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
												{
													data.SaveTo(outputStream);
												}
											}
										}
									}
								}
							}
						}

						ProductImage productImage = new()
						{
							ImageUrl = @"\" + productPath + @"\" + fileName,
							ProductId = productVM.Product.Id,
						};
						if (productVM.Product.ProductImages == null)
							productVM.Product.ProductImages = new List<ProductImage>();

						productVM.Product.ProductImages.Add(productImage);
					}
					_unitOfWork.Product.Update(productVM.Product);
					_unitOfWork.Save();
				}
				return RedirectToAction("Index");

			}
			catch (Exception ex)
			{
				TempData["error"] = _localizer["UnexpectedError"].Value;
				Console.WriteLine(ex.ToString());
				return View(productVM);
			}
		}
		public IActionResult DeleteImage(int imageId)
		{
			var imageToBeDeleted = _unitOfWork.ProductImage.Get(u => u.Id == imageId);
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
			Product productToBeDeleted = _unitOfWork.Product.Get(u => u.Id == id && u.IsDeleted == false);
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

