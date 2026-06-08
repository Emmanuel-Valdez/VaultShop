using UkiyoDesigns.DataAccess.Repository.IRepository;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.ViewModels;
using static UkiyoDesignsWeb.Services.Checkout.ICheckoutService;

namespace UkiyoDesignsWeb.Services.Checkout
{
	public class CheckoutService : ICheckoutService
	{
		private readonly IUnitOfWork _unitOfWork;

		public CheckoutService(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
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
			return new CheckoutCreateOrderResult
			{

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
