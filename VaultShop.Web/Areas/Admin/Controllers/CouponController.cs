using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services.Pricing;

namespace VaultShop.Web.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
	public class CouponController : Controller
	{
		public readonly IUnitOfWork _unitOfWork;
		private readonly IStringLocalizer<CouponController> _localizer;

		public CouponController(IUnitOfWork unitOfWork, IStringLocalizer<CouponController> localizer)
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
			if (id == 0 || id == null)
			{
				return View(new Coupon());
			}
			var couponFromDb = _unitOfWork.Coupon.Get(u => u.Id == id);
			if (couponFromDb == null)
				return NotFound();
			couponFromDb.ValidFromUtc = OfferTimeZone.ToLocal(couponFromDb.ValidFromUtc);
			couponFromDb.ValidToUtc = OfferTimeZone.ToLocal(couponFromDb.ValidToUtc);
			return View(couponFromDb);
		}

		[HttpPost]
		public IActionResult Upsert(Coupon obj)
		{
			obj.Code = obj.Code?.Trim().ToUpperInvariant() ?? string.Empty;
			ValidateCoupon(obj);
			if (!ModelState.IsValid)
				return View(obj);

			obj.ValidFromUtc = OfferTimeZone.ToUtc(obj.ValidFromUtc);
			obj.ValidToUtc = OfferTimeZone.ToUtc(obj.ValidToUtc);

			if (obj.Id == 0)
			{
				_unitOfWork.Coupon.Add(obj);
				TempData["success"] = _localizer["CouponCreatedSuccesfully"].Value;
			}
			else
			{
				var couponFromDb = _unitOfWork.Coupon.Get(u => u.Id == obj.Id);
				if (couponFromDb == null)
					return NotFound();
				couponFromDb.Code = obj.Code;
				couponFromDb.DiscountType = obj.DiscountType;
				couponFromDb.Value = obj.Value;
				couponFromDb.MinSubtotal = obj.MinSubtotal;
				couponFromDb.ValidFromUtc = obj.ValidFromUtc;
				couponFromDb.ValidToUtc = obj.ValidToUtc;
				couponFromDb.MaxUses = obj.MaxUses;
				couponFromDb.IsActive = obj.IsActive;
				_unitOfWork.Coupon.Update(couponFromDb);
				TempData["success"] = _localizer["CouponEditedSuccesfully"].Value;
			}
			_unitOfWork.Save();
			return RedirectToAction("Index");
		}

		private void ValidateCoupon(Coupon coupon)
		{
			if (string.IsNullOrWhiteSpace(coupon.Code))
				return;
			var duplicate = _unitOfWork.Coupon
				.Get(c => c.Code.ToUpper() == coupon.Code && c.Id != coupon.Id);
			if (duplicate != null)
				ModelState.AddModelError(nameof(Coupon.Code), _localizer["CouponCodeExists"].Value);
			if (coupon.DiscountType == CouponDiscountType.Percent
				&& (coupon.Value < 1 || coupon.Value > 100))
				ModelState.AddModelError(nameof(Coupon.Value), _localizer["PercentValueRange"].Value);
			if (coupon.DiscountType == CouponDiscountType.FixedAmount && coupon.Value <= 0)
				ModelState.AddModelError(nameof(Coupon.Value), _localizer["FixedValuePositive"].Value);
			if (coupon.MinSubtotal.HasValue && coupon.MinSubtotal.Value < 0)
				ModelState.AddModelError(nameof(Coupon.MinSubtotal), _localizer["MinSubtotalNegative"].Value);
			if (coupon.MaxUses.HasValue && coupon.MaxUses.Value < 1)
				ModelState.AddModelError(nameof(Coupon.MaxUses), _localizer["MaxUsesRange"].Value);
			if (coupon.ValidFromUtc.HasValue && coupon.ValidToUtc.HasValue
				&& coupon.ValidFromUtc.Value > coupon.ValidToUtc.Value)
				ModelState.AddModelError(nameof(Coupon.ValidToUtc), _localizer["EndBeforeStart"].Value);
		}

		// oferta-huso-horario-ar: admin wall time is Argentina; see OfferTimeZone.
		#region API CALLS
		[HttpGet]
		public IActionResult GetAll()
		{
			var objCouponList = _unitOfWork.Coupon.GetAll()
				.Select(c => new
				{
					c.Id,
					c.Code,
					Type = c.DiscountType.ToString(),
					c.Value,
					c.MinSubtotal,
					c.MaxUses,
					c.UsesCount,
					c.IsActive,
					c.ValidFromUtc,
					c.ValidToUtc
				}).ToList();
			return Json(new { data = objCouponList });
		}

		[HttpDelete]
		public IActionResult Delete(int? id)
		{
			var couponToBeDeleted = _unitOfWork.Coupon.Get(u => u.Id == id);
			if (couponToBeDeleted == null)
				return Json(new { success = false, message = _localizer["ErrorWhileDeleting"].Value });
			_unitOfWork.Coupon.Remove(couponToBeDeleted);
			_unitOfWork.Save();
			return Json(new { success = true, message = _localizer["DeleteSuccesfully"].Value });
		}
		#endregion
	}
}
