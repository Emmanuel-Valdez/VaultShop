using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using VaultShop.Web.Services.Payments;
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
			ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product,Product.Category,Product.Keywords,Variant.Values.Value.VariantOptionType"),
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

			// envio-gratis-o-cargo 3.1/3.2: the charge is added after discounts so no coupon,
			// promotion or payment-method discount can touch it (design decision 3).
			var shipping = GetShippingQuote(shoppingCartVM.ShoppingCartList, useWholesalePrice);
			shoppingCartVM.OrderHeader.ShippingCharge = shipping.Charge;
			shoppingCartVM.OrderHeader.OrderTotal += shipping.Charge;

			return new CheckoutSummaryResult
			{
				ApplicationUser = applicationUser,
				ShoppingCartVM = shoppingCartVM,
				Discounts = evaluation,
				Shipping = shipping
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
					includeProperties: "Product,Product.Category,Product.Keywords"),
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

			// envio-gratis-o-cargo 4.4: resolve and validate the modality before persisting.
			// Null stays S for legacy callers and Company checkout.
			var shipping = GetShippingQuote(shoppingCartVM.ShoppingCartList, useWholesalePrice);
			var deliveryType = string.IsNullOrWhiteSpace(shoppingCartVM.OrderHeader.DeliveryType)
				? SD.DeliveryTypePickup
				: shoppingCartVM.OrderHeader.DeliveryType;
			if (!GetAllowedDeliveryTypes(shoppingCartVM.OrderHeader.City, useWholesalePrice, shipping).Contains(deliveryType))
			{
				return new CheckoutCreateOrderResult
				{
					InvalidDeliveryModality = true,
					ShoppingCartVM = shoppingCartVM,
					ApplicationUser = applicationUser,
				};
			}

			shoppingCartVM.OrderHeader.DeliveryType = deliveryType;
			if (deliveryType != SD.DeliveryTypePickup)
			{
				// Motorcycle / vendor pickup carry no branch snapshot.
				shoppingCartVM.OrderHeader.PickupAgencyCode = null;
				shoppingCartVM.OrderHeader.PickupAgencyName = null;
				shoppingCartVM.OrderHeader.PickupAgencyAddress = null;
				shoppingCartVM.OrderHeader.PickupAgencyHours = PostalAgency.HoursUnknown;
			}
			shoppingCartVM.OrderHeader.DeliveryNote = deliveryType == SD.DeliveryTypeVendorPickup
				? SD.VendorPickupDeliveryNote
				: null;

			// envio-gratis-o-cargo 3.2: freeze the charge onto the order and fold it into the
			// charged total before the OrderTotal guard and before persistence. Vendor pickup is free.
			var shippingCharge = deliveryType == SD.DeliveryTypeVendorPickup ? 0m : shipping.Charge;
			shoppingCartVM.OrderHeader.ShippingCharge = shippingCharge;
			shoppingCartVM.OrderHeader.OrderTotal += shippingCharge;

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
					// envio-gratis-o-cargo 3.2: re-stamp drops the charge; add the frozen amount back.
					shoppingCartVM.OrderHeader.OrderTotal += shoppingCartVM.OrderHeader.ShippingCharge;
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

		// envio-gratis-o-cargo 3.1: one shipping quote per cart. The charge is the largest category
		// average among the non-free lines, grossed up by the aggregate percentage costs and counted
		// once (never per unit/line); any free line makes the whole order ship free. Wholesale
		// (Company) orders and wholesale preview never carry a charge.
		public ShippingQuote GetShippingQuote(IEnumerable<ShoppingCart> carts, bool useWholesalePrice)
		{
			var lines = carts.ToList();
			if (useWholesalePrice || lines.Count == 0)
			{
				return new ShippingQuote(false, 0m, 0m);
			}

			var categoryAverages = lines.Select(cart => cart.Product.Category?.AvgShippingCost ?? 0m).ToList();
			var largestAll = categoryAverages.Max();
			var standardAmount = largestAll <= 0m ? 0m : GrossUpShipping(largestAll);
			var anyFree = lines.Any(cart => cart.Product.FreeShipping);

			if (anyFree || largestAll <= 0m)
			{
				return new ShippingQuote(anyFree, 0m, standardAmount);
			}

			var largestNonFree = lines
				.Where(cart => !cart.Product.FreeShipping)
				.Select(cart => cart.Product.Category?.AvgShippingCost ?? 0m)
				.Max();
			return new ShippingQuote(false, GrossUpShipping(largestNonFree), standardAmount);
		}

		// envio-gratis-o-cargo 4.2: Gran Mendoza is the only zone with modal delivery; a free
		// cart hides vendor pickup so nothing free can be "arranged" unnecessarily.
		public IReadOnlyList<string> GetAllowedDeliveryTypes(string? locality, bool useWholesalePrice, ShippingQuote quote)
		{
			if (useWholesalePrice)
			{
				return [SD.DeliveryTypePickup];
			}

			if (string.IsNullOrWhiteSpace(locality) || !SD.IsGranMendoza(locality))
			{
				return [SD.DeliveryTypePickup];
			}

			return quote.QualifiesForFreeShipping
				? [SD.DeliveryTypeMotorcycle, SD.DeliveryTypePickup]
				: [SD.DeliveryTypeMotorcycle, SD.DeliveryTypeVendorPickup, SD.DeliveryTypePickup];
		}

		private decimal GrossUpShipping(decimal amount)
		{
			// Same aggregate the price calculator uses; the charge must survive the percentage costs.
			var percentage = _unitOfWork.PercentageCost.GetAll().Sum(percentageCost => percentageCost.Percentage);
			if (percentage >= 100m)
			{
				throw new InvalidOperationException("Cannot calculate shipping charge: percentage must be less than 100%.");
			}

			return Round(amount / (1m - percentage / 100m));
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

		// descuento-medio-de-pago §1: session-only proration of the header-level
		// payment discount across lines (whole cents, remainder on the largest
		// line so the request sums exactly to OrderTotal). Persisted
		// OrderDetail rows and the header stay untouched.
		public IReadOnlyList<PaymentSessionLineItem> BuildPaymentSessionLineItems(ShoppingCartVM vm)
		{
			var lines = vm.ShoppingCartList.ToList();
			var shippingCharge = vm.OrderHeader.ShippingCharge;
			// envio-gratis-o-cargo 3.3: the charge is a separate, non-discounted line, so the
			// product lines only need to reconcile against the product portion of the total.
			var productTotal = vm.OrderHeader.OrderTotal - shippingCharge;
			var discount = vm.OrderHeader.PaymentDiscountTotal;
			var payables = lines.Select(c => c.Price * c.Count).ToList();
			List<PaymentSessionLineItem> items;
			if (discount <= 0 || payables.Sum() <= 0)
			{
				items = lines.Select(c => new PaymentSessionLineItem(c.Product.Name, c.Price, c.Count)).ToList();
			}
			else
			{
				// ponytail: nominal shares sum exactly (remainder on largest), mirroring ProrateCoupon.
				var payBase = payables.Sum();
				var largest = 0;
				for (var i = 1; i < lines.Count; i++)
					if (payables[i] > payables[largest]) largest = i;
				var shares = new decimal[lines.Count];
				var assigned = 0m;
				for (var i = 0; i < lines.Count; i++)
				{
					if (i == largest) continue;
					shares[i] = Round(payables[i] * discount / payBase);
					assigned += shares[i];
				}
				shares[largest] = discount - assigned;
				var units = new decimal[lines.Count];
				for (var i = 0; i < lines.Count; i++)
					units[i] = lines[i].Count <= 0 ? 0m : Round((payables[i] - shares[i]) / lines[i].Count);
				// ponytail: per-unit rounding can drift a cent; absorb the residual on the largest line.
				var residual = productTotal - units.Select((u, i) => u * lines[i].Count).Sum();
				units[largest] += residual;
				items = lines.Select((c, i) => new PaymentSessionLineItem(c.Product.Name, units[i], c.Count)).ToList();
			}

			if (shippingCharge > 0m)
			{
				// ponytail: inline es/en label, matching the email-template convention; a resx
				// resource is only warranted if this line ever needs more than two strings.
				var label = CultureInfo.CurrentCulture.Name.StartsWith("es", StringComparison.OrdinalIgnoreCase)
					? "Envío"
					: "Shipping";
				items.Add(new PaymentSessionLineItem(label, shippingCharge, 1));
			}

			return items;
		}

		private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

		private IEnumerable<ShoppingCart> RemoveShoppingCartsOutdated(string userId, IEnumerable<ShoppingCart> shoppingCarts)
		{
			var cartsToRemove = shoppingCarts.Where(cart => cart.Product.IsAvailableInStore == false || cart.Product.IsDeleted == true
				|| !_variantService.ValidateVariantForProduct(cart.ProductId, cart.VariantId).IsValid).ToList();
			if (cartsToRemove.Any())
			{
				_unitOfWork.ShoppingCart.RemoveRange(cartsToRemove);
				_unitOfWork.Save();
			}
			return _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product,Product.Category,Product.Keywords,Variant.Values.Value.VariantOptionType");
		}

		private bool HasDeletedCompany(ApplicationUser applicationUser)
		{
			int companyId = applicationUser.CompanyId.GetValueOrDefault();
			return companyId > 0 && _unitOfWork.Company.Get(u => u.Id == companyId && u.IsDeleted == false) == null;
		}
	}
}
