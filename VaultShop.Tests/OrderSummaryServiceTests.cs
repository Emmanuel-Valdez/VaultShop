using System.Linq.Expressions;
using System.Security.Claims;
using Moq;
using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;
using VaultShop.Utility;
using VaultShop.Web.Services;
using VaultShop.Web.Services.Billing;

namespace VaultShop.Web.Tests
{
	public class OrderSummaryServiceTests
	{
		[Fact]
		public void GetSummary_CustomerOrder_ReturnsViewModelWithPersistedValues()
		{
			var order = CreateCustomerOrder();
			var details = CreateCustomerOrderDetails();
			var unitOfWork = CreateUnitOfWork(order, details, [CreateUser("user-1")]);
			var service = new OrderSummaryService(unitOfWork.Object, new OrderAccessPolicy(unitOfWork.Object));
			var principal = CreatePrincipal("user-1");

			var result = service.GetSummary(order.Id, principal);

			Assert.NotNull(result);
			Assert.Equal(42, result.OrderId);
			Assert.Equal(order.OrderDate, result.OrderDate);
			Assert.Equal(SD.PaymentStatusPending, result.PaymentStatus);
			Assert.Equal(SD.StatusPending, result.OrderStatus);
			Assert.Equal(SD.PaymentMethodStripe, result.PaymentMethod);
			Assert.Equal("Juan Perez", result.CustomerName);
			Assert.Null(result.CompanyName);
			Assert.Null(result.RazonSocial);
			Assert.Null(result.DomicilioFiscal);
			Assert.Null(result.Cuit);
			Assert.Equal("Juan Perez", result.ShippingName);
			Assert.Equal("Calle Falsa 123", result.ShippingStreetAddress);
			Assert.Equal("Buenos Aires", result.ShippingCity);
			Assert.Equal(2, result.Items.Count);
			Assert.Equal("Remera Básica", result.Items[0].ProductName);
			Assert.Equal(1500m, result.Items[0].UnitPrice);
			Assert.Equal(2, result.Items[0].Quantity);
			Assert.Equal(3000m, result.Items[0].LineTotal);
			Assert.Equal("Gorra Classic", result.Items[1].ProductName);
			Assert.Equal(800m, result.Items[1].UnitPrice);
			Assert.Equal(1, result.Items[1].Quantity);
			Assert.Equal(3800m, result.OrderTotal);
		}

		[Fact]
		public void GetSummary_CompanyOrder_IncludesFiscalSnapshot()
		{
			var order = CreateCompanyOrder();
			var details = CreateCompanyOrderDetails();
			var unitOfWork = CreateUnitOfWork(order, details, [CreateUser("user-1", companyId: 7)], [CreateCompany(7)]);
			var service = new OrderSummaryService(unitOfWork.Object, new OrderAccessPolicy(unitOfWork.Object));
			var principal = CreatePrincipal("user-1");

			var result = service.GetSummary(order.Id, principal);

			Assert.NotNull(result);
			Assert.Equal("Textiles SA", result.CompanyName);
			Assert.Equal("Textiles SA SRL", result.RazonSocial);
			Assert.Equal("Av. Corrientes 1234, CABA", result.DomicilioFiscal);
			Assert.Equal("30-71234567-9", result.Cuit);
		}

		[Fact]
		public void GetSummary_PickupOrder_MapsAgencySnapshot()
		{
			var order = CreateCustomerOrder();
			order.DeliveryType = SD.DeliveryTypePickup;
			order.PickupAgencyCode = "TEST01";
			order.PickupAgencyName = "Test Branch";
			order.PickupAgencyAddress = "San Martin 123, Godoy Cruz, Mendoza M5501";
			order.PickupAgencyHours = "LUN A VIE 9 A 18";
			var details = CreateCustomerOrderDetails();
			var unitOfWork = CreateUnitOfWork(order, details, [CreateUser("user-1")]);
			var service = new OrderSummaryService(unitOfWork.Object, new OrderAccessPolicy(unitOfWork.Object));
			var principal = CreatePrincipal("user-1");

			var result = service.GetSummary(order.Id, principal);

			Assert.NotNull(result);
			Assert.Equal(SD.DeliveryTypePickup, result.DeliveryType);
			Assert.Equal("TEST01", result.PickupAgencyCode);
			Assert.Equal("Test Branch", result.PickupAgencyName);
			Assert.Equal("San Martin 123, Godoy Cruz, Mendoza M5501", result.PickupAgencyAddress);
			Assert.Equal("LUN A VIE 9 A 18", result.PickupAgencyHours);
		}

		[Fact]
		public void GetSummary_PreChangeOrder_AgencyFieldsAreNull()
		{
			var order = CreateCustomerOrder();
			var details = CreateCustomerOrderDetails();
			var unitOfWork = CreateUnitOfWork(order, details, [CreateUser("user-1")]);
			var service = new OrderSummaryService(unitOfWork.Object, new OrderAccessPolicy(unitOfWork.Object));
			var principal = CreatePrincipal("user-1");

			var result = service.GetSummary(order.Id, principal);

			Assert.NotNull(result);
			Assert.Null(result.DeliveryType);
			Assert.Null(result.PickupAgencyCode);
			Assert.Null(result.PickupAgencyName);
			Assert.Null(result.PickupAgencyAddress);
		}

		[Fact]
		public void GetSummary_ForeignOrder_ReturnsNull()
		{
			var order = CreateCustomerOrder();
			var details = CreateCustomerOrderDetails();
			var unitOfWork = CreateUnitOfWork(order, details, [CreateUser("user-1")]);
			var service = new OrderSummaryService(unitOfWork.Object, new OrderAccessPolicy(unitOfWork.Object));
			var principal = CreatePrincipal("other-user");

			var result = service.GetSummary(order.Id, principal);

			Assert.Null(result);
		}

		[Fact]
		public void GetSummary_AdminUser_CanAccessAnyOrder()
		{
			var order = CreateCustomerOrder();
			var details = CreateCustomerOrderDetails();
			var unitOfWork = CreateUnitOfWork(order, details, [CreateUser("user-1")]);
			var service = new OrderSummaryService(unitOfWork.Object, new OrderAccessPolicy(unitOfWork.Object));
			var principal = CreateAdminPrincipal();

			var result = service.GetSummary(order.Id, principal);

			Assert.NotNull(result);
		}

		[Fact]
		public void GetSummary_TotalPreserved_DoesNotRecomputeFromItems()
		{
			var order = CreateCustomerOrder();
			order.OrderTotal = 9999m; // header total intentionally differs from sum of item LineTotals
			var details = CreateCustomerOrderDetails();
			var unitOfWork = CreateUnitOfWork(order, details, [CreateUser("user-1")]);
			var service = new OrderSummaryService(unitOfWork.Object, new OrderAccessPolicy(unitOfWork.Object));
			var principal = CreatePrincipal("user-1");

			var result = service.GetSummary(order.Id, principal);

			Assert.NotNull(result);
			Assert.Equal(9999m, result.OrderTotal);
		}

		private static OrderHeader CreateCustomerOrder()
		{
			return new OrderHeader
			{
				Id = 42,
				ApplicationUserId = "user-1",
				ApplicationUser = new ApplicationUser { Id = "user-1", Name = "Juan Perez" },
				OrderDate = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc),
				OrderStatus = SD.StatusPending,
				PaymentStatus = SD.PaymentStatusPending,
				PaymentMethod = SD.PaymentMethodStripe,
				Name = "Juan Perez",
				StreetAddress = "Calle Falsa 123",
				City = "Buenos Aires",
				State = "Buenos Aires",
				PostalCode = "1234",
				PhoneNumber = "+54 11 1234-5678",
				OrderTotal = 3800m,
			};
		}

		private static List<OrderDetail> CreateCustomerOrderDetails()
		{
			return
			[
				new() { OrderHeaderId = 42, ProductId = 10, Product = new Product { Id = 10, Name = "Remera Básica" }, Price = 1500m, Count = 2 },
				new() { OrderHeaderId = 42, ProductId = 11, Product = new Product { Id = 11, Name = "Gorra Classic" }, Price = 800m, Count = 1 },
			];
		}

		private static OrderHeader CreateCompanyOrder()
		{
			return new OrderHeader
			{
				Id = 99,
				ApplicationUserId = "user-1",
				ApplicationUser = new ApplicationUser { Id = "user-1", Name = "Maria Lopez", CompanyId = 7 },
				CompanyId = 7,
				Company = new Company { Id = 7, Name = "Textiles SA" },
				RazonSocialSnapshot = "Textiles SA SRL",
				DomicilioFiscalSnapshot = "Av. Corrientes 1234, CABA",
				CuitSnapshot = "30-71234567-9",
				OrderDate = new DateTime(2026, 8, 21, 14, 0, 0, DateTimeKind.Utc),
				OrderStatus = SD.StatusApproved,
				PaymentStatus = SD.PaymentStatusDelayedPayment,
				Name = "Maria Lopez",
				StreetAddress = "Av. Corrientes 1234",
				City = "CABA",
				State = "CABA",
				PostalCode = "1043",
				PhoneNumber = "+54 11 8765-4321",
				OrderTotal = 5000m,
			};
		}

		private static List<OrderDetail> CreateCompanyOrderDetails()
		{
			return
			[
				new() { OrderHeaderId = 99, ProductId = 20, Product = new Product { Id = 20, Name = "Tela Algodón" }, Price = 2500m, Count = 2 },
			];
		}

		private static ClaimsPrincipal CreatePrincipal(string userId)
		{
			var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
			return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
		}

		private static ClaimsPrincipal CreateAdminPrincipal()
		{
			var claims = new List<Claim>
			{
				new(ClaimTypes.NameIdentifier, "admin-1"),
				new(ClaimTypes.Role, SD.Role_Admin)
			};
			return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
		}

		private static ApplicationUser CreateUser(string userId, int? companyId = null)
		{
			return new ApplicationUser { Id = userId, Name = "Test User", CompanyId = companyId };
		}

		private static Company CreateCompany(int companyId)
		{
			return new Company { Id = companyId, Name = "Test Company", IsDeleted = false };
		}

		private static Mock<IUnitOfWork> CreateUnitOfWork(
			OrderHeader order,
			List<OrderDetail> details,
			IEnumerable<ApplicationUser> users,
			IEnumerable<Company>? companies = null)
		{
			var mock = new Mock<IUnitOfWork>();
			var userList = users.ToList();
			var companyList = (companies ?? []).ToList();
			var detailList = details;

			mock.Setup(x => x.OrderHeader.Get(
				It.IsAny<Expression<Func<OrderHeader, bool>>>(),
				It.IsAny<string?>(),
				It.IsAny<bool>()))
				.Returns((Expression<Func<OrderHeader, bool>> filter, string? _, bool _) =>
					new[] { order }.SingleOrDefault(filter.Compile()));

			mock.Setup(x => x.OrderDetail.GetAll(
				It.IsAny<Expression<Func<OrderDetail, bool>>>(),
				It.IsAny<string?>(),
				It.IsAny<bool>()))
				.Returns((Expression<Func<OrderDetail, bool>> filter, string? _, bool _) =>
					detailList.Where(filter.Compile()).ToList());

			mock.Setup(x => x.ApplicationUser.Get(
				It.IsAny<Expression<Func<ApplicationUser, bool>>>(),
				It.IsAny<string?>(),
				It.IsAny<bool>()))
				.Returns((Expression<Func<ApplicationUser, bool>> filter, string? _, bool _) =>
					userList.SingleOrDefault(filter.Compile()));

			mock.Setup(x => x.Company.Get(
				It.IsAny<Expression<Func<Company, bool>>>(),
				It.IsAny<string?>(),
				It.IsAny<bool>()))
				.Returns((Expression<Func<Company, bool>> filter, string? _, bool _) =>
					companyList.SingleOrDefault(filter.Compile()));

			return mock;
		}
	}
}
