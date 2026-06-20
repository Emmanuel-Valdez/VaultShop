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
				.Select(fixedCost => fixedCost.Cost)
				.AsEnumerable()
				.Sum()
		};
	}
	public TotalPercentageCost GetTotalPercentageCost()
	{
		return new TotalPercentageCost
		{
			TotalPercentage = _db.PercentageCosts
				.AsNoTracking()
				.Select(percentageCost => percentageCost.Percentage)
				.AsEnumerable()
				.Sum()
		};
	}

	public TotalPercentageCostWholesale GetTotalPercentageCostWholesale()
	{
		return new TotalPercentageCostWholesale
		{
			TotalPercentage = _db.PercentageCostsWholesale
				.AsNoTracking()
				.Select(percentageCost => percentageCost.Percentage)
				.AsEnumerable()
				.Sum()
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
			.Select(fixedCost => fixedCost.Cost)
			.AsEnumerable()
			.Sum();

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
		var totalPercentageCostWholesale = GetTotalPercentageCostWholesale().TotalPercentage;
		var costsByProduct = GetCostByProducts();

		return costsByProduct
			.Select(costByProduct =>
			{
				var product = costByProduct.Product;
				var retailWithProfit = CalculatePriceWithPercentage(costByProduct.TotalCostByProduct, percentageProfit.Retail, "retail profit");
				var retailWithShipping = retailWithProfit + product.Category.AvgShippingCost;
				var finalRetail = CalculatePriceWithPercentage(retailWithShipping, totalPercentage, "retail percentage cost");
				var wholesaleWithProfit = CalculatePriceWithPercentage(costByProduct.TotalCostByProduct, percentageProfit.Wholesale, "wholesale profit");
				var combinedWholesalePercentage = percentageProfit.Wholesale + totalPercentageCostWholesale;
				var finalWholesale = CalculatePriceWithPercentage(costByProduct.TotalCostByProduct, combinedWholesalePercentage, "combined wholesale percentage");

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
					WholesaleWithProfit = wholesaleWithProfit,
					FinalWholesale = finalWholesale
				};
			})
			.ToList();
	}

	public int GetOutdatedProductCount(decimal tolerance = 0.02m)
	{
		return GetFinalPrices()
			.Count(suggestedPrice =>
				!ArePricesEqual(suggestedPrice.FinalRetail, suggestedPrice.ActualListPrice, tolerance) ||
				!ArePricesEqual(suggestedPrice.FinalWholesale, suggestedPrice.ActualWholesalePrice, tolerance));
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
				ArePricesEqual(suggestedPrice.FinalWholesale, product.FinalWholesalePrice, tolerance))
			{
				continue;
			}

			product.ListPrice = suggestedPrice.FinalRetail;
			product.FinalWholesalePrice = suggestedPrice.FinalWholesale;
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

	private static decimal CalculatePriceWithPercentage(decimal basePrice, decimal percentage, string calculationName)
	{
		if (percentage >= 100m)
		{
			throw new InvalidOperationException($"Cannot calculate {calculationName}: percentage must be less than 100%.");
		}

		return basePrice / (1m - percentage / 100m);
	}

	private static bool ArePricesEqual(decimal price1, decimal price2, decimal tolerance)
	{
		return Math.Abs(price1 - price2) <= tolerance;
	}
}



