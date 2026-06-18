using Microsoft.EntityFrameworkCore;
using UkiyoDesigns.DataAccess.Data;
using UkiyoDesigns.Models;
using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Models.CalculatorModels.SQLViews;

namespace UkiyoDesignsWeb.Services.Pricing;

public class PricingCalculatorService : IPricingCalculatorService
{
	private readonly ApplicationDbContext _db;

	public PricingCalculatorService(ApplicationDbContext db)
	{
		_db = db;
	}

	public FixedCostMonthly GetFixedCostMonthly()
	{
		return new FixedCostMonthly
		{
			TotalFixedCostMonthly = _db.FixedCosts
				.AsNoTracking()
				.Sum(fixedCost => fixedCost.Cost)
		};
	}
	public TotalPercentageCost GetTotalPercentageCost()
	{
		return new TotalPercentageCost
		{
			TotalPercentage = _db.PercentageCosts
				.AsNoTracking()
				.Sum(percentageCost => percentageCost.Percentage)
		};
	}

	public PercentageProfit? GetPercentageProfit()
	{
		return _db.PercentageProfits
			.AsNoTracking()
			.FirstOrDefault(percentageProfit => percentageProfit.Id == 1);
	}

	public void UpdatePercentageProfit(decimal retail, decimal wholesale)
	{
		var percentageProfit = _db.PercentageProfits
			.FirstOrDefault(percentageProfit => percentageProfit.Id == 1);

		if (percentageProfit == null)
		{
			throw new InvalidOperationException("Price calculator data is not initialized.");
		}

		percentageProfit.Retail = retail;
		percentageProfit.Wholesale = wholesale;
		_db.SaveChanges();
	}

	public IReadOnlyList<CostByProductView> GetCostByProducts()
	{
		var fixedCost = _db.FixedCosts
			.AsNoTracking()
			.Sum(fixedCost => fixedCost.Cost);

		var fabricTotals = GetFabricTotalsByProduct();
		var garmentHardwareTotals = GetGarmentHardwareTotalsByProduct();
		var packagingTotals = GetPackagingTotalsByCategory();

		var products = _db.Products
			.AsNoTracking()
			.Include(product => product.Category)
			.Where(product => !product.IsDeleted)
			.ToList();

		return products
			.Select(product =>
			{
				fabricTotals.TryGetValue(product.Id, out var fabricTotal);
				garmentHardwareTotals.TryGetValue(product.Id, out var garmentHardwareTotal);
				packagingTotals.TryGetValue(product.CategoryId, out var packagingTotal);

				var fixedCostAddedByCategory = fixedCost / product.Category.MaxExpectation;
				var totalCostByProduct = garmentHardwareTotal + fabricTotal + packagingTotal + fixedCostAddedByCategory;

				return new CostByProductView
				{
					ProductId = product.Id,
					Product = product,
					CategoryName = product.Category.Name,
					MaxExpectationMonthly = product.Category.MaxExpectation,
					FixedCostAddedByCategory = fixedCostAddedByCategory,
					GarmentHardware = garmentHardwareTotal,
					Fabric = fabricTotal,
					Packaging = packagingTotal,
					TotalCostByProduct = totalCostByProduct
				};
			})
			.ToList();
	}

	public IReadOnlyList<FinalPriceView> GetFinalPrices()
	{
		var percentageProfit = GetPercentageProfit();
		if (percentageProfit == null)
		{
			return Array.Empty<FinalPriceView>();
		}

		var totalPercentage = GetTotalPercentageCost().TotalPercentage;
		var costsByProduct = GetCostByProducts();

		return costsByProduct
			.Select(costByProduct =>
			{
				var product = costByProduct.Product;
				var retailWithProfit = costByProduct.TotalCostByProduct / (1 - percentageProfit.Retail / 100);
				var retailWithShipping = retailWithProfit + product.Category.AvgShippingCost;
				var finalRetail = retailWithShipping / (1 - totalPercentage / 100);
				var wholesaleWithProfit = costByProduct.TotalCostByProduct / (1 - percentageProfit.Wholesale / 100);

				return new FinalPriceView
				{
					Id = product.Id,
					Product = product,
					CategoryName = product.Category.Name,
					AvgShippingCostByCategory = product.Category.AvgShippingCost,
					TotalCost = costByProduct.TotalCostByProduct,
					RetailWithProfit = retailWithProfit,
					RetailWithShipping = retailWithShipping,
					FinalRetail = finalRetail,
					ActualListPrice = product.ListPrice,
					ActualRetailPrice = product.FinalRetailPrice,
					ActualWholesalePrice = product.FinalWholesalePrice,
					WholesaleWithProfit = wholesaleWithProfit
				};
			})
			.ToList();
	}

	public int GetOutdatedProductCount(decimal tolerance = 0.02m)
	{
		return GetFinalPrices()
			.Count(suggestedPrice =>
				!ArePricesEqual(suggestedPrice.FinalRetail, suggestedPrice.ActualListPrice, tolerance) ||
				!ArePricesEqual(suggestedPrice.WholesaleWithProfit, suggestedPrice.ActualWholesalePrice, tolerance));
	}

	public int PublishSuggestedPrices(decimal tolerance = 0.02m)
	{
		var suggestedPricesByProductId = GetFinalPrices()
			.ToDictionary(suggestedPrice => suggestedPrice.Id);

		var products = _db.Products
			.Where(product => !product.IsDeleted)
			.ToList();

		var updatedCount = 0;

		foreach (var product in products)
		{
			if (!suggestedPricesByProductId.TryGetValue(product.Id, out var suggestedPrice))
			{
				continue;
			}

			if (ArePricesEqual(suggestedPrice.FinalRetail, product.ListPrice, tolerance) &&
				ArePricesEqual(suggestedPrice.WholesaleWithProfit, product.FinalWholesalePrice, tolerance))
			{
				continue;
			}

			product.ListPrice = suggestedPrice.FinalRetail;
			product.FinalWholesalePrice = suggestedPrice.WholesaleWithProfit;
			updatedCount++;
		}

		if (updatedCount > 0)
		{
			_db.SaveChanges();
		}

		return updatedCount;
	}

	public IReadOnlyDictionary<int, decimal> GetFabricTotalsByProduct()
	{
		return _db.UnitsFabricByProduct
			.AsNoTracking()
			.Include(unit => unit.Fabric)
			.GroupBy(unit => unit.ProductId)
			.ToDictionary(
				group => group.Key,
				group => group.Sum(unit => (unit.Fabric.Price / unit.Fabric.Quantity) / unit.Quantity));
	}

	public IReadOnlyDictionary<int, decimal> GetGarmentHardwareTotalsByProduct()
	{
		return _db.UnitsGarmentHardwareByProduct
			.AsNoTracking()
			.Include(unit => unit.GarmentHardware)
			.GroupBy(unit => unit.ProductId)
			.ToDictionary(
				group => group.Key,
				group => group.Sum(unit => (unit.GarmentHardware.Price / unit.GarmentHardware.Quantity) * unit.Quantity));
	}

	public IReadOnlyDictionary<int, decimal> GetPackagingTotalsByCategory()
	{
		return _db.UnitsPackagingByCategory
			.AsNoTracking()
			.Include(unit => unit.Packaging)
			.GroupBy(unit => unit.CategoryId)
			.ToDictionary(
				group => group.Key,
				group => group.Sum(unit => (unit.Packaging.Price / unit.Packaging.Quantity) * unit.Quantity));
	}

	private static bool ArePricesEqual(decimal price1, decimal price2, decimal tolerance)
	{
		return Math.Abs(price1 - price2) <= tolerance;
	}
}



