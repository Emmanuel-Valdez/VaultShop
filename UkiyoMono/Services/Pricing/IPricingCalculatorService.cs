using UkiyoDesigns.Models.CalculatorModels;
using UkiyoDesigns.Models.CalculatorModels.SQLViews;

namespace UkiyoDesignsWeb.Services.Pricing;

public interface IPricingCalculatorService
{
	FixedCostMonthly GetFixedCostMonthly();
	TotalPercentageCost GetTotalPercentageCost();
	PercentageProfit? GetPercentageProfit();
	void UpdatePercentageProfit(decimal retail, decimal wholesale);
	IReadOnlyDictionary<int, decimal> GetFabricTotalsByProduct();
	IReadOnlyDictionary<int, decimal> GetGarmentHardwareTotalsByProduct();
	IReadOnlyDictionary<int, decimal> GetPackagingTotalsByCategory();
	IReadOnlyList<CostByProductView> GetCostByProducts();
	IReadOnlyList<FinalPriceView> GetFinalPrices();
	int GetOutdatedProductCount(decimal tolerance = 0.02m);
	int PublishSuggestedPrices(decimal tolerance = 0.02m);
}


