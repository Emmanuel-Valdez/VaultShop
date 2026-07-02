using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.CalculatorModels;
using VaultShop.Models.DTO;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using VaultShop.Web.Services.Pricing;
using VaultShop.Web.Services.RichText;

namespace VaultShop.Web.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class FabricByProductController : Controller
	{

		public readonly IUnitOfWork _unitOfWork;
		[BindProperty]
		public FabricByProductVM FabricByProductVM { get; set; } = null!;
		private readonly IStringLocalizer<FabricByProductController> _localizer;
		private readonly ILogger<FabricByProductController> _logger;
		private readonly IPricingCalculatorService _pricingCalculatorService;
		private readonly IRichTextSanitizer _richTextSanitizer;


		public FabricByProductController(IUnitOfWork unitOfWork, IStringLocalizer<FabricByProductController> localizer, ILogger<FabricByProductController> logger, IPricingCalculatorService pricingCalculatorService, IRichTextSanitizer richTextSanitizer)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
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
			var unitFromDb = _unitOfWork.UnitFabricByProduct
					.Get(u => u.Id == unitId && u.ProductId == id);
			// It is revised to avoid error when deleting while in the editing view
			if (unitFromDb == null)
			{
				unitId = null;
			}
			var fabricByProduct = _unitOfWork.FabricByProduct
				.Get(u => u.ProductId == id, includeProperties: "Product,UnitFabricByProductList");
			if (fabricByProduct == null)
			{
				return NotFound();
			}

			FabricByProductVM = new()
			{
				FabricList = _unitOfWork.Fabric.GetAll().Select(u => new SelectListItem
				{
					Text = u.Name,
					Value = u.Id.ToString()
				}),
				FabricByProduct = fabricByProduct,
				CalculatedTotalByProduct = _pricingCalculatorService.GetFabricTotalsByProduct().GetValueOrDefault(id),
			};
		
			if (unitId == 0 || unitId == null)
			{
				//create
				FabricByProductVM.UnitFabricByProduct = new();
				FabricByProductVM.UnitFabricByProduct.ProductId = FabricByProductVM.FabricByProduct.ProductId;
			}
			else
			{
				//update
				if (unitFromDb == null)
				{
					return NotFound();
				}

				FabricByProductVM.UnitFabricByProduct = unitFromDb;
			}
			return View(FabricByProductVM);
		}

		[HttpPost]
		public IActionResult Upsert()
		{
			FabricByProductVM.UnitFabricByProduct.Description = _richTextSanitizer.Sanitize(FabricByProductVM.UnitFabricByProduct.Description);
			ModelState.Remove("UnitFabricByProduct.Description");

			if (!ModelState.IsValid)
			{
				return View(FabricByProductVM);
			}
			try
			{
				var fabricFromDb = _unitOfWork.Fabric.Get(u => u.Id == FabricByProductVM.UnitFabricByProduct.FabricId);
				if (fabricFromDb == null)
				{
					return NotFound();
				}

				// Calcular TotalPartial from fabric
				
				var existingUnitFabric = _unitOfWork.UnitFabricByProduct
					.GetAll(u => u.ProductId == FabricByProductVM.FabricByProduct.ProductId)
					.ToList();

				FabricByProductVM.FabricByProduct.UnitFabricByProductList = existingUnitFabric;

				var unitFabricToUpdate = FabricByProductVM.FabricByProduct.UnitFabricByProductList
						.FirstOrDefault(u => u.FabricId == fabricFromDb.Id);

				if (unitFabricToUpdate == null)
				{
					FabricByProductVM.FabricByProduct.UnitFabricByProductList.Add(FabricByProductVM.UnitFabricByProduct);
					TempData["success"] = _localizer["UnitFabricAddSuccesfully"].Value;
			    }
				else
				{
					if (unitFabricToUpdate.Id != FabricByProductVM.UnitFabricByProduct.Id)
					{
						//change id 0 to existing Id
						FabricByProductVM.UnitFabricByProduct.Id = unitFabricToUpdate.Id;
					}
					_unitOfWork.UpdateEntityValues(unitFabricToUpdate, FabricByProductVM.UnitFabricByProduct);
					TempData["success"] = _localizer["UnitFabricUpdatedSuccessfully"].Value;
				}

				
				_unitOfWork.FabricByProduct.Update(FabricByProductVM.FabricByProduct);
				_unitOfWork.Save();


				return RedirectToAction(nameof(Upsert), new { id = FabricByProductVM.FabricByProduct.ProductId });
			}
			catch (Exception ex)
			{
				TempData["error"] = _localizer["UnexpectedError"].Value;
				_logger.LogError(ex, "Unexpected error while saving fabric assignment for product {ProductId} and fabric {FabricId}.", FabricByProductVM.FabricByProduct.ProductId, FabricByProductVM.UnitFabricByProduct.FabricId);
				return View(FabricByProductVM);
			}
		}
		
		#region API CALLS

		[HttpGet]
		public IActionResult GetAllProducts()
		{
			List<Product> objProductList = _unitOfWork.Product.GetAll(u => u.IsDeleted == false).ToList();
			var fabricTotalsByProduct = _pricingCalculatorService.GetFabricTotalsByProduct();
			var productListDTOs = objProductList.Select(c => new ProductDTO
			{
				Id = c.Id,
				Name = c.Name,
				TotalByProduct = fabricTotalsByProduct.GetValueOrDefault(c.Id)
			}).ToList();
			return Json(new { data = productListDTOs });
		}
		[HttpGet]
		public IActionResult GetAllUnitFabrics(int productId)
		{
			List<UnitFabricByProduct> objUnitFabricByProducts = _unitOfWork.UnitFabricByProduct
				.GetAll(u => u.ProductId == productId, includeProperties: "Fabric").ToList();

			var unitFabricDTOs = objUnitFabricByProducts.Select(unit => new
			{
				unit.Id,
				unit.Quantity,
				unit.Description,
				unit.ProductId,
				unit.FabricId,
				unit.Fabric,
				UnitTotal = unit.Fabric.Price / unit.Fabric.Quantity / unit.Quantity
			}).ToList();

			return Json(new { data = unitFabricDTOs });
		}
		[HttpDelete]
		public IActionResult Delete(int? id)
		{
			UnitFabricByProduct? unitToBeDeleted = _unitOfWork.UnitFabricByProduct.Get(u => u.Id == id);
			if (unitToBeDeleted == null)
			{
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value});
			}
			_unitOfWork.UnitFabricByProduct.Remove(unitToBeDeleted);
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



