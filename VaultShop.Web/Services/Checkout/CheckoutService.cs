using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Models.ViewModels;
using VaultShop.Utility;
using static VaultShop.Web.Services.Checkout.ICheckoutService;

namespace VaultShop.Web.Services.Checkout
{
	public class CheckoutService : ICheckoutService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<CheckoutService> _logger;

		public CheckoutService(IUnitOfWork unitOfWork, ILogger<CheckoutService> logger)
		{
			_unitOfWork = unitOfWork;
			_logger = logger;
		}

		public CheckoutSummaryResult BuildSummary(string userId, bool useWholesalePrice)
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
				ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product"),
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

			foreach (var cart in shoppingCartVM.ShoppingCartList)
			{
				cart.Price = GetPrice(cart, useWholesalePrice);
				shoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
			}

			return new CheckoutSummaryResult
			{
				ApplicationUser = applicationUser,
				ShoppingCartVM = shoppingCartVM,
			};
		}

		public CheckoutCreateOrderResult CreateOrder(string userId,
			OrderHeader postedOrderHeader, bool useWholesalePrice)
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
					includeProperties: "Product"),
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
			shoppingCartVM.OrderHeader.OrderTotal = 0;

			foreach (var cart in shoppingCartVM.ShoppingCartList)
			{
				cart.Price = GetPrice(cart, useWholesalePrice);
				shoppingCartVM.OrderHeader.OrderTotal += cart.Price * cart.Count;
			}

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
		try
		{
			_unitOfWork.ExecuteInTransaction(() =>
			{
				// Validate every line against a fresh tracked read BEFORE writing anything,
				// so a single failing line leaves no order/stock side-effects.
				foreach (var cart in shoppingCartVM.ShoppingCartList)
				{
					var product = _unitOfWork.Product.Get(p => p.Id == cart.ProductId, tracked: true);
					if (product == null || cart.Count > product.StockQuantity)
					{
						insufficientStock = true;
						return;
					}
					cart.Product = product;
				}

				// ponytail: single-transaction re-read + decrement, no row lock — under high
				// contention upgrade to SELECT FOR UPDATE or a RowVersion on Product.
				foreach (var cart in shoppingCartVM.ShoppingCartList)
				{
					cart.Product.StockQuantity -= cart.Count;
					_unitOfWork.Product.Update(cart.Product);
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
						Count = cart.Count
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

			return new CheckoutCreateOrderResult
			{
				OrderId = shoppingCartVM.OrderHeader.Id,
				RequiresOnlinePayment = requiresOnlinePayment,
				ShoppingCartVM = shoppingCartVM,
				ApplicationUser = applicationUser,
			};
		}

		private decimal GetPrice(ShoppingCart shoppingCart, bool useWholesalePrice)
		{
			return useWholesalePrice
			? shoppingCart.Product.FinalWholesalePrice
			: shoppingCart.Product.FinalRetailPrice;
		}

		private IEnumerable<ShoppingCart> RemoveShoppingCartsOutdated(string userId, IEnumerable<ShoppingCart> shoppingCarts)
		{
			var cartsToRemove = shoppingCarts.Where(cart => cart.Product.IsAvailableInStore == false || cart.Product.IsDeleted == true).ToList();
			if (cartsToRemove.Any())
			{
				_unitOfWork.ShoppingCart.RemoveRange(cartsToRemove);
				_unitOfWork.Save();
			}
			return _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId, includeProperties: "Product");
		}

		private bool HasDeletedCompany(ApplicationUser applicationUser)
		{
			int companyId = applicationUser.CompanyId.GetValueOrDefault();
			return companyId > 0 && _unitOfWork.Company.Get(u => u.Id == companyId && u.IsDeleted == false) == null;
		}
	}
}
