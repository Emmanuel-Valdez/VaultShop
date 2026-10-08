using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;

namespace VaultShop.Web.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class PromotionController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<PromotionController> _localizer;

		public PromotionController(IUnitOfWork unitOfWork, IStringLocalizer<PromotionController> localizer)
		{
			_unitOfWork = unitOfWork;
			_localizer = localizer;
		}

		public IActionResult Index()
		{
			return View();
		}

		public IActionResult Upsert(int? id)
		{
			PromotionVM promotionVM = new();
			PopulatePromotionFormData(promotionVM);
			if (id == 0 || id == null)
				return View(promotionVM);

			var promotion = _unitOfWork.Promotion.Get(u => u.Id == id);
			if (promotion == null)
				return NotFound();
			promotion.ValidFromUtc = OfferToLocal(promotion.ValidFromUtc);
			promotion.ValidToUtc = OfferToLocal(promotion.ValidToUtc);
			promotionVM.Promotion = promotion;
			return View(promotionVM);
		}

		[HttpPost]
		public IActionResult Upsert(PromotionVM promotionVM)
		{
			ValidatePromotion(promotionVM.Promotion);
			if (!ModelState.IsValid)
			{
				PopulatePromotionFormData(promotionVM);
				return View(promotionVM);
			}

			var promotion = promotionVM.Promotion;
			promotion.ValidFromUtc = OfferToUtc(promotion.ValidFromUtc);
			promotion.ValidToUtc = OfferToUtc(promotion.ValidToUtc);
			if (promotion.Scope == PromotionScope.Store)
			{
				promotion.ProductId = null;
				promotion.CategoryId = null;
				promotion.KeywordId = null;
			}
			if (promotion.Kind != PromotionKind.PaymentMethodDiscount)
				promotion.PaymentMethod = null;

			if (promotion.Id == 0)
			{
				_unitOfWork.Promotion.Add(promotion);
				TempData["success"] = _localizer["PromotionCreatedSuccesfully"].Value;
			}
			else
			{
				var promotionFromDb = _unitOfWork.Promotion.Get(u => u.Id == promotion.Id);
				if (promotionFromDb == null)
					return NotFound();
				promotionFromDb.Name = promotion.Name;
				promotionFromDb.Kind = promotion.Kind;
				promotionFromDb.Scope = promotion.Scope;
				promotionFromDb.ProductId = promotion.ProductId;
				promotionFromDb.CategoryId = promotion.CategoryId;
				promotionFromDb.KeywordId = promotion.KeywordId;
				promotionFromDb.PaymentMethod = promotion.PaymentMethod;
				promotionFromDb.BuyQty = promotion.BuyQty;
				promotionFromDb.GetQty = promotion.GetQty;
				promotionFromDb.GetDiscountPercent = promotion.GetDiscountPercent;
				promotionFromDb.DiscountPercent = promotion.DiscountPercent;
				promotionFromDb.SameSkuOnly = promotion.SameSkuOnly;
				promotionFromDb.IncludeWholesale = promotion.IncludeWholesale;
				promotionFromDb.ValidFromUtc = promotion.ValidFromUtc;
				promotionFromDb.ValidToUtc = promotion.ValidToUtc;
				promotionFromDb.IsActive = promotion.IsActive;
				_unitOfWork.Promotion.Update(promotionFromDb);
				TempData["success"] = _localizer["PromotionEditedSuccesfully"].Value;
			}
			_unitOfWork.Save();
			return RedirectToAction("Index");
		}

		private void ValidatePromotion(Promotion promotion)
		{
			if (promotion.Kind == PromotionKind.BxGy)
			{
				if (promotion.BuyQty < 1)
					ModelState.AddModelError("Promotion.BuyQty", _localizer["BuyQtyRange"].Value);
				if (promotion.GetQty < 1)
					ModelState.AddModelError("Promotion.GetQty", _localizer["GetQtyRange"].Value);
				if (promotion.GetDiscountPercent < 1 || promotion.GetDiscountPercent > 100)
					ModelState.AddModelError("Promotion.GetDiscountPercent", _localizer["PercentRange"].Value);
			}
			if ((promotion.Kind == PromotionKind.PercentOff || promotion.Kind == PromotionKind.PaymentMethodDiscount)
				&& (promotion.DiscountPercent < 1 || promotion.DiscountPercent > 100))
				ModelState.AddModelError("Promotion.DiscountPercent", _localizer["PercentRange"].Value);
			if (promotion.Kind == PromotionKind.PaymentMethodDiscount
				&& !IsKnownPaymentMethod(promotion.PaymentMethod))
				ModelState.AddModelError("Promotion.PaymentMethod", _localizer["PaymentMethodRequired"].Value);
			if (promotion.Scope == PromotionScope.Product)
			{
				if (!promotion.ProductId.HasValue || _unitOfWork.Product.Get(u => u.Id == promotion.ProductId && !u.IsDeleted) == null)
					ModelState.AddModelError("Promotion.ProductId", _localizer["ScopeTargetRequired"].Value);
			}
			if (promotion.Scope == PromotionScope.Category)
			{
				if (!promotion.CategoryId.HasValue || _unitOfWork.Category.Get(u => u.Id == promotion.CategoryId && !u.IsDeleted) == null)
					ModelState.AddModelError("Promotion.CategoryId", _localizer["ScopeTargetRequired"].Value);
			}
			if (promotion.Scope == PromotionScope.Keyword)
			{
				if (!promotion.KeywordId.HasValue || _unitOfWork.Keyword.Get(u => u.Id == promotion.KeywordId && !u.IsDeleted) == null)
					ModelState.AddModelError("Promotion.KeywordId", _localizer["ScopeTargetRequired"].Value);
			}
			if (promotion.ValidFromUtc.HasValue && promotion.ValidToUtc.HasValue
				&& promotion.ValidFromUtc.Value > promotion.ValidToUtc.Value)
				ModelState.AddModelError("Promotion.ValidToUtc", _localizer["EndBeforeStart"].Value);
		}

		private static bool IsKnownPaymentMethod(string? paymentMethod)
			=> paymentMethod == SD.PaymentMethodStripe
				|| paymentMethod == SD.PaymentMethodBankTransfer
				|| paymentMethod == SD.PaymentMethodMercadoPago;

		private void PopulatePromotionFormData(PromotionVM promotionVM)
		{
			promotionVM.ProductList = _unitOfWork.Product.GetAll(u => !u.IsDeleted)
				.Select(u => new SelectListItem { Text = u.Name, Value = u.Id.ToString() });
			promotionVM.CategoryList = _unitOfWork.Category.GetAll(u => !u.IsDeleted)
				.Select(u => new SelectListItem { Text = u.Name, Value = u.Id.ToString() });
			promotionVM.KeywordList = _unitOfWork.Keyword.GetAll(u => !u.IsDeleted)
				.Select(u => new SelectListItem { Text = u.Name, Value = u.Id.ToString() });
			promotionVM.PaymentMethodList = new List<SelectListItem>
			{
				new() { Text = SD.PaymentMethodStripe, Value = SD.PaymentMethodStripe },
				new() { Text = SD.PaymentMethodBankTransfer, Value = SD.PaymentMethodBankTransfer },
				new() { Text = SD.PaymentMethodMercadoPago, Value = SD.PaymentMethodMercadoPago }
			};
		}

		// ponytail: datetime-local posts server-local wall time; evaluator compares UTC.
		private static DateTime? OfferToUtc(DateTime? value)
			=> value.HasValue
				? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified), TimeZoneInfo.Local)
				: null;

		private static DateTime? OfferToLocal(DateTime? value)
			=> value.HasValue
				? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc), TimeZoneInfo.Local)
				: null;

		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			var promotions = _unitOfWork.Promotion
				.GetAll(includeProperties: "Product,Category,Keyword")
				.Select(p => new
				{
					p.Id,
					p.Name,
					Kind = p.Kind.ToString(),
					Scope = p.Scope.ToString(),
					Target = p.Scope == PromotionScope.Product ? p.Product!.Name
						: p.Scope == PromotionScope.Category ? p.Category!.Name
						: p.Scope == PromotionScope.Keyword ? p.Keyword!.Name
						: "*",
					p.IsActive,
					p.ValidFromUtc,
					p.ValidToUtc
				}).ToList();
			return Json(new { data = promotions });
		}

		[HttpDelete]
		public IActionResult Delete(int? id)
		{
			var promotionToBeDeleted = _unitOfWork.Promotion.Get(u => u.Id == id);
			if (promotionToBeDeleted == null)
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			_unitOfWork.Promotion.Remove(promotionToBeDeleted);
			_unitOfWork.Save();
			return Json(new { success = true, message = _localizer["DeleteSuccesfully"].Value });
		}
		#endregion
	}
}
