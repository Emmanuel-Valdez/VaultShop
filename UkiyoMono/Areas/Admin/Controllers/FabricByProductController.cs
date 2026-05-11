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

namespace UkiyoDesignsWeb.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class FabricByProductController : Controller
	{

		public readonly IUnitOfWork _unitOfWork;
		[BindProperty]
		public FabricByProductVM FabricByProductVM { get; set; }
		private readonly IStringLocalizer<FabricByProductController> _localizer;


		public FabricByProductController(IUnitOfWork unitOfWork, IStringLocalizer<FabricByProductController> localizer)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
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
			FabricByProductVM = new()
			{
				FabricList = _unitOfWork.Fabric.GetAll().Select(u => new SelectListItem
				{
					Text = u.Name,
					Value = u.Id.ToString()
				}),
				FabricByProduct = _unitOfWork.FabricByProduct
				.Get(u => u.ProductId == id, includeProperties: "Product,UnitFabricByProductList"),
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
				FabricByProductVM.UnitFabricByProduct = unitFromDb;
			}
			return View(FabricByProductVM);
		}

		[HttpPost]
		public IActionResult Upsert()
		{
			if (!ModelState.IsValid)
			{
				return View(FabricByProductVM);
			}
			try
			{
				var fabricFromDb = _unitOfWork.Fabric.Get(u => u.Id == FabricByProductVM.UnitFabricByProduct.FabricId);

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
				Console.WriteLine(ex.ToString());
				return View(FabricByProductVM);
			}
		}
		
		#region API CALLS

		[HttpGet]
		public IActionResult GetAllProducts()
		{
			List<Product> objProductList = _unitOfWork.Product.GetAll(u => u.IsDeleted == false, includeProperties: "FabricByProduct").ToList();
			var productListDTOs = objProductList.Select(c => new ProductDTO
			{
				Id = c.Id,
				Name = c.Name,
				TotalByProduct = c.FabricByProduct.TotalFabricByProduct
			}).ToList();
			return Json(new { data = productListDTOs });
		}
		[HttpGet]
		public IActionResult GetAllUnitFabrics(int productId)
		{

			List<UnitFabricByProduct> objUnitFabricByProducts = _unitOfWork.UnitFabricByProduct
				.GetAll(u => u.ProductId == productId, includeProperties: "Fabric").ToList();

			return Json(new { data = objUnitFabricByProducts });

		}
		[HttpDelete]
		public IActionResult Delete(int? id)
		{
			UnitFabricByProduct unitToBeDeleted = _unitOfWork.UnitFabricByProduct.Get(u => u.Id == id);
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
