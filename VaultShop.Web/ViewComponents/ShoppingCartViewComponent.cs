using Microsoft.AspNetCore.Mvc;
using System.Numerics;
using System.Security.Claims;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Utility;

namespace VaultShop.Web.ViewComponents
{
	public class ShoppingCartViewComponent: ViewComponent
	{
		private readonly IUnitOfWork _unitOfWork;
        public ShoppingCartViewComponent(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
    public IViewComponentResult Invoke()
        {
			if (User.Identity is not ClaimsIdentity claimsIdentity)
			{
				HttpContext.Session.Clear();
				return View(0);
			}

			var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

			if (claim != null)
			{
				if (HttpContext.Session.GetInt32(SD.SessionCart) == null)
				{
					HttpContext.Session.SetInt32(SD.SessionCart,
						_unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value && u.Product.IsDeleted == false && u.Product.IsAvailableInStore == true).Count());
				}
				
				return View(HttpContext.Session.GetInt32(SD.SessionCart));
			}
			else
			{
				HttpContext.Session.Clear();
				return View(0);
			}
		}
    
    }
}
