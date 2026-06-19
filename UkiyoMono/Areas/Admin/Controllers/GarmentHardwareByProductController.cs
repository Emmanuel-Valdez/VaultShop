using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Models.DTO;
using UkiyoDesigns.Models.ViewModels;
using UkiyoDesigns.Utility;
using UkiyoDesignsWeb.Services.Pricing;
using UkiyoDesignsWeb.Services.RichText;

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class GarmentHardwareByProductController : Controller
	{

		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<GarmentHardwareByProductController> _localizer;
		private readonly ILogger<GarmentHardwareByProductController> _logger;
		private readonly IPricingCalculatorService _pricingCalculatorService;
		private readonly IRichTextSanitizer _richTextSanitizer;
		[BindProperty]
		public GarmentHardwareByProductVM GarmentHardwareByProductVM { get; set; } = null!;

		public GarmentHardwareByProductController(IUnitOfWork unitOfWork, IStringLocalizer<GarmentHardwareByProductController> localizer, ILogger<GarmentHardwareByProductController> logger, IPricingCalculatorService pricingCalculatorService, IRichTextSanitizer richTextSanitizer)
		{
			_unitOfWork = unitOfWork;
			_localizer= localizer;
			_logger = logger;
			_pricingCalculatorService = pricingCalculatorService;
			_richTextSanitizer = richTextSanitizer;
		}

		public IActionResult Index()
		{
			return View();
		}

		public IActionResult Upsert(int id, int? unitId)
		{
			var unitFromDb = _unitOfWork.UnitGarmentHardwareByProduct
					.Get(u => u.Id == unitId && u.ProductId == id);
			// It is revised to avoid error when deleting while in the editing view
			if (unitFromDb == null)
			{
				unitId = null;
			}
			var garmentHardwareByProduct = _unitOfWork.GarmentHardwareByProduct
				.Get(u => u.ProductId == id, includeProperties: "Product,UnitGarmentHardwareByProductList");
			if (garmentHardwareByProduct == null)
			{
				return NotFound();
			}

			GarmentHardwareByProductVM = new()
			{
				GarmentHardwareList = _unitOfWork.GarmentHardware.GetAll().Select(u => new SelectListItem
				{
					Text = u.Name,
					Value = u.Id.ToString()
				}),
				GarmentHardwareByProduct = garmentHardwareByProduct,
				CalculatedTotalByProduct = _pricingCalculatorService.GetGarmentHardwareTotalsByProduct().GetValueOrDefault(id),
			};
			if (unitId == 0 || unitId == null)
			{
				//create
				GarmentHardwareByProductVM.UnitGarmentHardwareByProduct = new();
				GarmentHardwareByProductVM.UnitGarmentHardwareByProduct.ProductId = GarmentHardwareByProductVM.GarmentHardwareByProduct.ProductId;
			}
			else
			{
				//update
				if (unitFromDb == null)
				{
					return NotFound();
				}

				GarmentHardwareByProductVM.UnitGarmentHardwareByProduct = unitFromDb;
			}
			return View(GarmentHardwareByProductVM);
		}

		[HttpPost]
		public IActionResult Upsert()
		{
			GarmentHardwareByProductVM.UnitGarmentHardwareByProduct.Description = _richTextSanitizer.Sanitize(GarmentHardwareByProductVM.UnitGarmentHardwareByProduct.Description);
			ModelState.Remove("UnitGarmentHardwareByProduct.Description");

			if (!ModelState.IsValid)
			{
				return View(GarmentHardwareByProductVM);
			}
			try
			{
				var garmenthardwareFromDb = _unitOfWork.GarmentHardware
				.Get(u => u.Id == GarmentHardwareByProductVM.UnitGarmentHardwareByProduct.GarmentHardwareId);
				if (garmenthardwareFromDb == null)
				{
					return NotFound();
				}

				var existingUnitGarmentHardware = _unitOfWork.UnitGarmentHardwareByProduct
					.GetAll(u => u.ProductId == GarmentHardwareByProductVM.GarmentHardwareByProduct.ProductId)
					.ToList();

				GarmentHardwareByProductVM.GarmentHardwareByProduct.UnitGarmentHardwareByProductList = existingUnitGarmentHardware;

				var unitGarmentHardwareToUpdate = GarmentHardwareByProductVM.GarmentHardwareByProduct.UnitGarmentHardwareByProductList
						.FirstOrDefault(u => u.GarmentHardwareId == garmenthardwareFromDb.Id);

				if (unitGarmentHardwareToUpdate == null)
				{
					GarmentHardwareByProductVM.GarmentHardwareByProduct.UnitGarmentHardwareByProductList.Add(GarmentHardwareByProductVM.UnitGarmentHardwareByProduct);
					TempData["success"] = _localizer["UnitGarmentHardwareAddedSuccessfully"].Value;
			    }
				else
				{
					if (unitGarmentHardwareToUpdate.Id != GarmentHardwareByProductVM.UnitGarmentHardwareByProduct.Id)
					{
						//change id 0 to existing Id
						GarmentHardwareByProductVM.UnitGarmentHardwareByProduct.Id = unitGarmentHardwareToUpdate.Id;
					}
					_unitOfWork.UpdateEntityValues(unitGarmentHardwareToUpdate, GarmentHardwareByProductVM.UnitGarmentHardwareByProduct);
					TempData["success"] = _localizer["UnitGarmentHardwareUpdatedSuccessfully"].Value; 
				}

				
				_unitOfWork.GarmentHardwareByProduct.Update(GarmentHardwareByProductVM.GarmentHardwareByProduct);
				_unitOfWork.Save();


				return RedirectToAction(nameof(Upsert), new { id = GarmentHardwareByProductVM.GarmentHardwareByProduct.ProductId });
			}
			catch (Exception ex)
			{
				TempData["error"] = _localizer["UnexpectedError"].Value;
				_logger.LogError(ex, "Unexpected error while saving garment hardware assignment for product {ProductId} and garment hardware {GarmentHardwareId}.", GarmentHardwareByProductVM.GarmentHardwareByProduct.ProductId, GarmentHardwareByProductVM.UnitGarmentHardwareByProduct.GarmentHardwareId);
				return View(GarmentHardwareByProductVM);
			}
		}

		#region API CALLS

		[HttpGet]
		public IActionResult GetAllProducts()
		{
			List<Product> objProductList = _unitOfWork.Product.GetAll(u => u.IsDeleted == false).ToList();
			var garmentHardwareTotalsByProduct = _pricingCalculatorService.GetGarmentHardwareTotalsByProduct();
			var productListDTOs = objProductList.Select(c => new ProductDTO
			{
				Id = c.Id,
				Name = c.Name,
				TotalByProduct = garmentHardwareTotalsByProduct.GetValueOrDefault(c.Id)
			}).ToList();
			return Json(new { data = productListDTOs });
		}
		[HttpGet]
		public IActionResult GetAllUnitGarmentHardwares(int productId)
		{
			List<UnitGarmentHardwareByProduct> objUnitGarmentHardwareByProducts = _unitOfWork.UnitGarmentHardwareByProduct
				.GetAll(u => u.ProductId == productId, includeProperties: "GarmentHardware").ToList();

			var unitGarmentHardwareDTOs = objUnitGarmentHardwareByProducts.Select(unit => new
			{
				unit.Id,
				unit.Quantity,
				unit.Description,
				unit.ProductId,
				unit.GarmentHardwareId,
				unit.GarmentHardware,
				UnitTotal = unit.GarmentHardware.Price / unit.GarmentHardware.Quantity * unit.Quantity
			}).ToList();

			return Json(new { data = unitGarmentHardwareDTOs });
		}
		[HttpDelete]
		public IActionResult Delete(int? id)
		{
			UnitGarmentHardwareByProduct? unitToBeDeleted = _unitOfWork.UnitGarmentHardwareByProduct.Get(u => u.Id == id);
			if (unitToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			}
			_unitOfWork.UnitGarmentHardwareByProduct.Remove(unitToBeDeleted);
			_unitOfWork.Save();
			return Json(new { success = true, message = _localizer["DeleteSuccesfully"].Value });
		}

		[HttpGet]
		public IActionResult EditUnit(int unitId, int productId)
		{
			return RedirectToAction(nameof(Upsert), new { unitId = unitId, id = productId });
		}
		#endregion
	}
}



