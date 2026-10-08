using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using VaultShop.Web.Services.Pricing;
using VaultShop.Web.Services.ProductVariants;
using static VaultShop.Web.Services.Checkout.ICheckoutService;

namespace VaultShop.Web.Services.Checkout
{
	public class CheckoutService : ICheckoutService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<CheckoutService> _logger;
		private readonly IProductVariantService _variantService;
		private readonly IDiscountEvaluator _discountEvaluator;

		public CheckoutService(IUnitOfWork unitOfWork, ILogger<CheckoutService> logger, IProductVariantService variantService, IDiscountEvaluator discountEvaluator)
		{
			_unitOfWork = unitOfWork;
			_logger = logger;
			_variantService = variantService;
			_discountEvaluator = discountEvaluator;
		}

		public DiscountEvaluation EvaluateCart(IEnumerable<ShoppingCart> carts, bool useWholesalePrice, string? couponCode, string? paymentMethod)
		{
			var list = carts.ToList();
			// ponytail: coupon/promotion tables stay tiny; load once and let the
			// evaluator apply windows/validity so display, summary, and creation agree.
			var coupons = _unitOfWork.Coupon.GetAll().ToList();
			var promotions = _unitOfWork.Promotion.GetAll(p => p.IsActive).ToList();
			var evaluation = _discountEvaluator.Evaluate(new DiscountEvaluationRequest
			{
				Lines = list.Select(c => new DiscountLineRequest
				{
					LineKey = c.Id,
					ProductId = c.ProductId,
					CategoryId = c.Product.CategoryId,
					KeywordIds = c.Product.Keywords?.Select(k => k.KeywordId).ToList() ?? new List<int>(),
					Count = c.Count,
					RetailPrice = c.Product.FinalRetailPrice,
					WholesalePrice = c.Product.FinalWholesalePrice,
					SaleRetailPrice = c.Product.SaleRetailPrice,
					SaleWholesalePrice = c.Product.SaleWholesalePrice,
					SaleFromUtc = c.Product.SaleFromUtc,
					SaleToUtc = c.Product.SaleToUtc
				}).ToList(),
				UseWholesalePrice = useWholesalePrice,
				CouponCode = couponCode,
				Coupons = coupons,
				Promotions = promotions,
				PaymentMethod = paymentMethod,
				UtcNow = DateTime.UtcNow
			});
			// ponytail: keyed by cart id (unique per row); first-wins tolerates
			// keyless in-memory doubles sharing the default id.
			var byKey = evaluation.Lines.GroupBy(l => l.LineKey)
				.ToDictionary(g => g.Key, g => g.First());
			// ponytail: whole-cent units; remainder absorbed by last line of same product.
			foreach (var group in list
				.Where(c => byKey.ContainsKey(c.Id))
				.OrderBy(c => c.Id)
				.GroupBy(c => c.ProductId))
			{
				var ordered = group.ToList();
				var groupEffective = ordered.Sum(c => byKey[c.Id].EffectiveTotal);
				var assigned = 0m;
				for (var gi = 0; gi < ordered.Count; gi++)
				{
					var cart = ordered[gi];
					var evaluated = byKey[cart.Id];
					cart.OriginalPrice = evaluated.BaseUnitPrice;
					cart.DiscountAmount = evaluated.DiscountAmount;
					cart.DiscountMotive = evaluated.DiscountMotive;
					if (cart.Count <= 0)
					{
						cart.Price = 0m;
						continue;
					}
					if (gi < ordered.Count - 1)
					{
						cart.Price = Math.Round(evaluated.EffectiveTotal / cart.Count, 2, MidpointRounding.AwayFromZero);
						assigned += cart.Price * cart.Count;
					}
					else
					{
						var remaining = groupEffective - assigned;
						cart.Price = Math.Round(remaining / cart.Count, 2, MidpointRounding.AwayFromZero);
					}
				}
			}
			return evaluation;
		}

		public CheckoutSummaryResult BuildSummary(string userId, bool useWholesalePrice, string? couponCode = null, string? paymentMethod = null)
		{
			if (string.IsNullOrEmpty(userId))
			{
				return new CheckoutSummaryResult
				{
					IsAuthorized = false,
				};
			}

		var shoppingCartVM = new ShoppingCartVM()
		{
			ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product,Product.Keywords,Variant.Values.Value.VariantOptionType"),
			OrderHeader = new()
		};

			shoppingCartVM.ShoppingCartList = RemoveShoppingCartsOutdated(userId,
												shoppingCartVM.ShoppingCartList);

			if (!shoppingCartVM.ShoppingCartList.Any())
			{
				return new CheckoutSummaryResult
				{
					IsCartEmpty = true,
				};
			}

			var applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId, tracked: true);

			if (applicationUser == null)
			{
				return new CheckoutSummaryResult
				{
					IsAuthorized = false,
				};
			}

			if (HasDeletedCompany(applicationUser))
			{
				return new CheckoutSummaryResult
				{
					ShouldBlockUser = true,
					ApplicationUser = applicationUser,
				};
			}

			shoppingCartVM.OrderHeader.ApplicationUser = applicationUser;
			shoppingCartVM.OrderHeader.ApplicationUserId = userId;
			shoppingCartVM.OrderHeader.PhoneNumber = applicationUser.PhoneNumber ?? string.Empty;
			shoppingCartVM.OrderHeader.StreetAddress = applicationUser.StreetAddress ?? string.Empty;
			shoppingCartVM.OrderHeader.City = applicationUser.City ?? string.Empty;
			shoppingCartVM.OrderHeader.State = applicationUser.State ?? string.Empty;
			shoppingCartVM.OrderHeader.PostalCode = applicationUser.PostalCode ?? string.Empty;
			shoppingCartVM.OrderHeader.Name = shoppingCartVM.OrderHeader.ApplicationUser.Name;

			var evaluation = EvaluateCart(shoppingCartVM.ShoppingCartList, useWholesalePrice, couponCode, paymentMethod);
			StampDiscountHeader(shoppingCartVM.OrderHeader, evaluation);

			return new CheckoutSummaryResult
			{
				ApplicationUser = applicationUser,
				ShoppingCartVM = shoppingCartVM,
				Discounts = evaluation
			};
		}

		public CheckoutCreateOrderResult CreateOrder(string userId,
			OrderHeader postedOrderHeader, bool useWholesalePrice, string? couponCode = null)
		{
			if (string.IsNullOrEmpty(userId))
			{
				return new CheckoutCreateOrderResult
				{
					IsAuthorized = false,
				};
			}

			var shoppingCartVM = new ShoppingCartVM()
			{
				ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u =>
					u.ApplicationUserId == userId &&
					u.Product.IsDeleted == false &&
					u.Product.IsAvailableInStore == true,
					includeProperties: "Product,Product.Keywords"),
				OrderHeader = postedOrderHeader
			};

			if (!shoppingCartVM.ShoppingCartList.Any())
			{
				return new CheckoutCreateOrderResult
				{
					IsCartEmpty = true,
					ShoppingCartVM = shoppingCartVM,
				};
			}

			ApplicationUser? applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userId, tracked: true);
			if (applicationUser == null)
			{
				return new CheckoutCreateOrderResult
				{
					IsAuthorized = false,
				};
			}

			if (HasDeletedCompany(applicationUser))
			{
				return new CheckoutCreateOrderResult
				{
					ShouldBlockUser = true,
					ApplicationUser = applicationUser,
				};
			}

			shoppingCartVM.OrderHeader.ApplicationUserId = userId;
			shoppingCartVM.OrderHeader.OrderDate = DateTime.UtcNow;

			var evaluation = EvaluateCart(shoppingCartVM.ShoppingCartList, useWholesalePrice,
				couponCode, shoppingCartVM.OrderHeader.PaymentMethod);
			StampDiscountHeader(shoppingCartVM.OrderHeader, evaluation);

			if (shoppingCartVM.OrderHeader.OrderTotal <= 0)
			{
				return new CheckoutCreateOrderResult
				{
					OrderTotalInvalid = true,
					ShoppingCartVM = shoppingCartVM,
					ApplicationUser = applicationUser,
				};
			}

			bool isCompanyOrder = applicationUser.CompanyId.GetValueOrDefault() > 0;
			bool requiresOnlinePayment = !isCompanyOrder && shoppingCartVM.OrderHeader.PaymentMethod is
				SD.PaymentMethodStripe or SD.PaymentMethodMercadoPago;

			if (!isCompanyOrder)
			{
				shoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
				shoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
				shoppingCartVM.OrderHeader.RazonSocialSnapshot = null;
				shoppingCartVM.OrderHeader.DomicilioFiscalSnapshot = null;
				shoppingCartVM.OrderHeader.CuitSnapshot = null;
				// PaymentMethod is already validated by the caller against enabled methods; keep it as posted.
			}
			else
			{
				shoppingCartVM.OrderHeader.PaymentMethod = null;
				shoppingCartVM.OrderHeader.CompanyId = applicationUser.CompanyId;

				var company = _unitOfWork.Company.Get(c => c.Id == applicationUser.CompanyId);
				if (company != null)
				{
					shoppingCartVM.OrderHeader.RazonSocialSnapshot = company.RazonSocial;
					shoppingCartVM.OrderHeader.DomicilioFiscalSnapshot = company.DomicilioFiscal;
					shoppingCartVM.OrderHeader.CuitSnapshot = company.Cuit;
				}

				shoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
				shoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
				shoppingCartVM.OrderHeader.PaymentDueDate = DateOnly.FromDateTime(shoppingCartVM.OrderHeader.OrderDate.AddDays(SD.CompanyPaymentDueDays));
			}

		bool insufficientStock = false;
		bool variantUnavailable = false;
		bool couponDropped = false;
		try
		{
		_unitOfWork.ExecuteInTransaction(() =>
		{
			// descuentos-promociones: coupon re-validated at creation with the
			// tracked row; a lost race proceeds undiscounted, never blocked.
			if (evaluation.AppliedCouponCode is not null)
			{
				var trackedCoupon = _unitOfWork.Coupon.Get(c => c.Code == evaluation.AppliedCouponCode);
				if (trackedCoupon is null || _unitOfWork.Coupon.TryIncrementUses(trackedCoupon.Id) == 0)
				{
					couponDropped = true;
					evaluation = EvaluateCart(shoppingCartVM.ShoppingCartList, useWholesalePrice, null, shoppingCartVM.OrderHeader.PaymentMethod);
					StampDiscountHeader(shoppingCartVM.OrderHeader, evaluation);
				}
			}
			// ponytail: pre-read fast path only — the conditional write below is the real guard.
			var groups = shoppingCartVM.ShoppingCartList.GroupBy(c => c.ProductId).ToList();
			var totals = groups.ToDictionary(g => g.Key, g => g.Sum(c => c.Count));
			foreach (var group in groups)
			{
				var product = _unitOfWork.Product.Get(p => p.Id == group.Key, tracked: true);
				if (product == null || totals[group.Key] > product.StockQuantity)
				{
					insufficientStock = true;
					return;
				}
				foreach (var cart in group)
				{
					cart.Product = product;
					// A variant disabled after being added must fail checkout (design.md:60);
					// the line stays intact for the shopper to fix, deletes are blocked at the source.
					// Variant-less lines skip: legacy products have nothing to re-resolve.
					if (cart.VariantId.HasValue &&
						!_variantService.ValidateVariantForProduct(group.Key, cart.VariantId).IsValid)
					{
						variantUnavailable = true;
						return;
					}
				}
			}

			// product-variants-hardening 1.1: conditional relative decrement per product.
			// An absolute write of a pre-read value loses a last-unit race (READ COMMITTED);
			// UPDATE ... WHERE StockQuantity >= total makes the loser fail here instead of overselling.
			foreach (var group in groups)
			{
				if (_unitOfWork.Product.DecrementStockIfSufficient(group.Key, totals[group.Key]) == 0)
				{
					insufficientStock = true;
					return;
				}
			}

			_unitOfWork.OrderHeader.Add(shoppingCartVM.OrderHeader);
			_unitOfWork.Save();
			_logger.LogInformation("Created order {OrderId} during checkout. UserId: {UserId}, CartItemCount: {CartItemCount}, OrderTotal: {OrderTotal}, PaymentStatus: {PaymentStatus}", shoppingCartVM.OrderHeader.Id, userId, shoppingCartVM.ShoppingCartList.Count(), shoppingCartVM.OrderHeader.OrderTotal, shoppingCartVM.OrderHeader.PaymentStatus);

			foreach (var cart in shoppingCartVM.ShoppingCartList)
			{
				OrderDetail orderDetail = new()
				{
					ProductId = cart.ProductId,
					OrderHeaderId = shoppingCartVM.OrderHeader.Id,
					Price = cart.Price,
					Count = cart.Count,
					// descuentos-promociones: frozen breakdown, survives later promotion edits.
					OriginalPrice = cart.OriginalPrice,
					DiscountAmount = cart.DiscountAmount,
					DiscountMotive = cart.DiscountMotive,
					// product-variants 5.3: frozen label captured like Price; renames never propagate.
					VariantId = cart.VariantId,
					VariantLabel = cart.VariantId.HasValue ? _variantService.BuildVariantLabel(cart.VariantId.Value) : null,
				};
				_unitOfWork.OrderDetail.Add(orderDetail);
			}
			_unitOfWork.Save();
		});
		}
		catch (DbUpdateException ex)
		{
			// ponytail: only constraint in this transaction is CK_Products_StockQuantity_NonNegative — map to InsufficientStock
			_logger.LogWarning(ex, "Checkout stock constraint violation for user {UserId} - concurrent stock exhausted.", userId);
			insufficientStock = true;
		}

			if (insufficientStock)
			{
				return new CheckoutCreateOrderResult
				{
					InsufficientStock = true,
					ShoppingCartVM = shoppingCartVM,
					ApplicationUser = applicationUser,
				};
			}

			if (variantUnavailable)
			{
				return new CheckoutCreateOrderResult
				{
					VariantUnavailable = true,
					ShoppingCartVM = shoppingCartVM,
					ApplicationUser = applicationUser,
				};
			}

			return new CheckoutCreateOrderResult
			{
				OrderId = shoppingCartVM.OrderHeader.Id,
				RequiresOnlinePayment = requiresOnlinePayment,
				// Stale session coupon (expired/exhausted since applied) also surfaces the notice.
				CouponDroppedAtCreation = couponDropped ||
					(!string.IsNullOrWhiteSpace(couponCode) && evaluation.CouponState == CouponEvaluationState.Rejected),
				ShoppingCartVM = shoppingCartVM,
				ApplicationUser = applicationUser,
			};
		}

		private static void StampDiscountHeader(OrderHeader header, DiscountEvaluation evaluation)
		{
			header.OrderTotal = evaluation.Total;
			header.CouponCode = evaluation.AppliedCouponCode;
			header.DiscountTotal = evaluation.SpecificDiscountTotal;
			header.PaymentDiscountTotal = evaluation.PaymentDiscountTotal;
			header.PaymentDiscountMotive = evaluation.PaymentDiscountMotive;
			header.AppliedPromotionIds = evaluation.AppliedPromotionIds.Count > 0
				? string.Join(",", evaluation.AppliedPromotionIds)
				: null;
		}

		private IEnumerable<ShoppingCart> RemoveShoppingCartsOutdated(string userId, IEnumerable<ShoppingCart> shoppingCarts)
		{
			var cartsToRemove = shoppingCarts.Where(cart => cart.Product.IsAvailableInStore == false || cart.Product.IsDeleted == true).ToList();
			if (cartsToRemove.Any())
			{
				_unitOfWork.ShoppingCart.RemoveRange(cartsToRemove);
				_unitOfWork.Save();
			}
			return _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product,Product.Keywords,Variant.Values.Value.VariantOptionType");
		}

		private bool HasDeletedCompany(ApplicationUser applicationUser)
		{
			int companyId = applicationUser.CompanyId.GetValueOrDefault();
			return companyId > 0 && _unitOfWork.Company.Get(u => u.Id == companyId && u.IsDeleted == false) == null;
		}
	}
}
