using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;
using UkiyoDesigns.Models.CalculatorModels;

namespace UkiyoDesigns.DataAccess.DbInitializer
{
	public static class SqlScripts
	{
		public const string View_UpdateFixedCost = @"
			IF NOT EXISTS 
			(SELECT * FROM sys.views 
			WHERE object_id = OBJECT_ID(N'[dbo].[FixedCostMonthlyView]'))
			   BEGIN
			         EXEC('
						CREATE VIEW FixedCostMonthlyView AS 
						SELECT ISNULL(SUM(Cost), 0) AS TotalFixedCostMonthly
						FROM dbo.FixedCosts');
			 END";
		
		public const string View_UpdatePercentageCost = @"
			IF NOT EXISTS 
			(SELECT * FROM sys.views 
			WHERE object_id = OBJECT_ID(N'[dbo].[TotalPercentageCostView]'))
			   BEGIN
			    EXEC('CREATE VIEW TotalPercentageCostView AS 
				SELECT ISNULL(SUM(Percentage), 0) AS TotalPercentage
                FROM dbo.PercentageCosts');
			 END";
		public const string View_CostByProduct = @"
			IF NOT EXISTS 
			(SELECT * FROM sys.views 
			WHERE object_id = OBJECT_ID(N'[dbo].[CostByProductView]'))
			   BEGIN
					 EXEC('CREATE VIEW CostByProductView AS 
					SELECT 
					p.Id as ProductId,
					c.Name as CategoryName,
					c.MaxExpectation as MaxExpectationMonthly,
					(fc.FixedCost /c.MaxExpectation) as FixedCostAddedByCategory,
					ghp.TotalGarmentHardwareByProduct as GarmentHardware,
					fp.TotalFabricByProduct as Fabric,
					pc.TotalPackagingByCategory as Packaging,
					(ghp.TotalGarmentHardwareByProduct 
					+ fp.TotalFabricByProduct + pc.TotalPackagingByCategory +
					(fc.FixedCost / c.MaxExpectation))
					AS TotalCostByProduct
					
					FROM
									[dbo].[Products] p
					JOIN
									[dbo].[Categories] c on c.Id = p.CategoryId
					JOIN 
									[dbo].[GarmentHardwaresByProduct] ghp on ghp.ProductId = p.Id
					JOIN
									[dbo].[FabricsByProduct] fp on fp.ProductId = p.Id
					JOIN
									[dbo].[PackagingsByCategory] pc on  pc.CategoryId=c.Id
					CROSS JOIN
							(SELECT SUM(TotalFixedCostMonthly) AS FixedCost FROM [dbo].[FixedCostMonthlyView]) fc
					where p.IsDeleted=0
				');
				END";

		public const string View_FinalPrice = @"
			IF NOT EXISTS 
			(SELECT * FROM sys.views 
			WHERE object_id = OBJECT_ID(N'[dbo].[FinalPriceView]'))
			   BEGIN
					 EXEC('CREATE VIEW [dbo].[FinalPriceView] AS 
					SELECT 
						p.Id as Id,
						c.Name as CategoryName,
						c.AvgShippingCost as AvgShippingCostByCategory,
						cbp.TotalCostByProduct as TotalCost,
						(cbp.TotalCostByProduct / (1 - pp.Retail / 100)) as RetailWithProfit,
						(cbp.TotalCostByProduct / (1 - pp.Retail / 100)) + c.AvgShippingCost as RetailWithShipping,
						((cbp.TotalCostByProduct / (1 - pp.Retail / 100)) + c.AvgShippingCost) / (1 - tpc.TotalPercentage / 100) as FinalRetail,
						p.ListPrice as ActualListPrice,
						p.FinalRetailPrice as ActualRetailPrice,
						p.FinalWholesalePrice as ActualWholesalePrice, 
						(cbp.TotalCostByProduct / (1 - pp.Wholesale / 100)) as WholesaleWithProfit
						FROM
										[dbo].[Products] p
						JOIN
										[dbo].[Categories] c on c.Id = p.CategoryId
						JOIN
										[dbo].[CostByProductView] cbp on cbp.ProductId = p.Id
						JOIN
										[dbo].[PercentageProfits] pp on pp.Id = 1
						CROSS JOIN
										(SELECT SUM(TotalPercentage) AS TotalPercentage FROM [dbo].[TotalPercentageCostView]) tpc
						WHERE 
						p.IsDeleted=0
				');
				END";

		public const string TR_UpdateUnitsTotalFabric = @"
		IF NOT EXISTS 
		(SELECT * FROM sys.triggers 
		WHERE name = 'TR_UpdateUnitsTotalFabric') 
		BEGIN
		EXEC('
			CREATE TRIGGER TR_UpdateUnitsTotalFabric
			ON Fabrics
			AFTER INSERT, UPDATE
			AS
			BEGIN
				SET NOCOUNT ON;

				UPDATE ufc
				SET ufc.UnitTotal = f.PriceMeter / ufc.Quantity
				FROM [dbo].[UnitsFabricByProduct] ufc
				INNER JOIN [dbo].[Fabrics] f ON ufc.fabricId = f.Id;
			END;')
		END;
		";
		public const string TR_UpdateUnitsTotalGarmentHardware = @"
		IF NOT EXISTS 
		(SELECT * FROM sys.triggers 
		WHERE name = 'TR_UpdateUnitsTotalGarmentHardware') 
		BEGIN
		EXEC('
			CREATE TRIGGER TR_UpdateUnitsTotalGarmentHardware
			ON GarmentHardwares
			AFTER INSERT, UPDATE
			AS
			BEGIN
				SET NOCOUNT ON;

				UPDATE ugh
				SET ugh.UnitTotal = g.UnitPrice * ugh.Quantity
				FROM [dbo].[UnitsGarmentHardwareByProduct] ugh
				INNER JOIN [dbo].[GarmentHardwares] g ON ugh.GarmentHardwareId = g.Id;
			END;')
		END;
		";
		public const string TR_UpdateUnitsTotalPackaging = @"
		IF NOT EXISTS 
		(SELECT * FROM sys.triggers 
		WHERE name = 'TR_UpdateUnitsTotalPackaging') 
		BEGIN
		EXEC('
			CREATE TRIGGER TR_UpdateUnitsTotalPackaging
			ON Packagings
			AFTER INSERT, UPDATE
			AS
			BEGIN
				SET NOCOUNT ON;

				UPDATE upc
				SET upc.UnitTotal = p.UnitPrice * upc.Quantity
				FROM [dbo].[UnitsPackagingByCategory] upc
				INNER JOIN [dbo].[Packagings] p ON upc.PackagingId = p.Id;
			END;')
		END;
		";

		public const string TR_UpdateUnitPackaging_UnitsTable = @"
		IF NOT EXISTS 
		(SELECT * FROM sys.triggers 
		WHERE name = 'TR_UpdateUnitPackaging_UnitsTable') 
		BEGIN
		EXEC('
			CREATE TRIGGER TR_UpdateUnitPackaging_UnitsTable
			ON UnitsPackagingByCategory
			AFTER INSERT, UPDATE
			AS
			BEGIN
				SET NOCOUNT ON;

				UPDATE upc
				SET upc.UnitTotal = p.UnitPrice * upc.Quantity
				FROM [dbo].[UnitsPackagingByCategory] upc
				INNER JOIN [dbo].[Packagings] p ON upc.PackagingId = p.Id;
			END;')
		END;
		";
		public const string TR_UpdateUnitsTotalFabric_UnitsTable = @"
		IF NOT EXISTS 
		(SELECT * FROM sys.triggers 
		WHERE name = 'TR_UpdateUnitsTotalFabric_UnitsTable') 
		BEGIN
		EXEC('
			CREATE TRIGGER TR_UpdateUnitsTotalFabric_UnitsTable
			ON UnitsFabricByProduct
			AFTER INSERT, UPDATE
			AS
			BEGIN
				SET NOCOUNT ON;

				UPDATE ufc
				SET ufc.UnitTotal = f.PriceMeter / ufc.Quantity
				FROM [dbo].[UnitsFabricByProduct] ufc
				INNER JOIN [dbo].[Fabrics] f ON ufc.fabricId = f.Id;
			END;')
		END;
		";
		public const string TR_UpdateUnitsTotalGarmentHardware_UnitsTable = @"
		IF NOT EXISTS 
		(SELECT * FROM sys.triggers 
		WHERE name = 'TR_UpdateUnitsTotalGarmentHardware_UnitsTable') 
		BEGIN
		EXEC('
			CREATE TRIGGER TR_UpdateUnitsTotalGarmentHardware_UnitsTable
			ON UnitsGarmentHardwareByProduct
			AFTER INSERT, UPDATE
			AS
			BEGIN
				SET NOCOUNT ON;

				UPDATE ugh
				SET ugh.UnitTotal = g.UnitPrice * ugh.Quantity
				FROM [dbo].[UnitsGarmentHardwareByProduct] ugh
				INNER JOIN [dbo].[GarmentHardwares] g ON ugh.GarmentHardwareId = g.Id;
			END;')
		END;
		";

		public const string TR_UpdateTotalPackagingByCategory = @"
			IF NOT EXISTS 
			(SELECT * FROM sys.triggers 
			WHERE object_id = OBJECT_ID(N'[dbo].[TR_UpdateTotalPackagingByCategory]'))
			BEGIN
				EXEC('CREATE TRIGGER TR_UpdateTotalPackagingByCategory
					ON UnitsPackagingByCategory
					AFTER INSERT, UPDATE, DELETE
					AS
					BEGIN
						-- Actualizar los totales para las categorías afectadas
						UPDATE pc
						SET pc.TotalPackagingByCategory = (
							SELECT COALESCE(SUM(upc.UnitTotal), 0)
							FROM UnitsPackagingByCategory upc
							WHERE upc.CategoryId = pc.CategoryId
						)
						FROM PackagingsByCategory pc
						INNER JOIN (
							SELECT DISTINCT CategoryId
							FROM INSERTED
							UNION
							SELECT DISTINCT CategoryId
							FROM DELETED
						) affectedCategories
						ON pc.CategoryId = affectedCategories.CategoryId;
					END;');
			END;";
		public const string TR_UpdateTotalFabricByProduct = @"
			IF NOT EXISTS 
			(SELECT * FROM sys.triggers 
			WHERE object_id = OBJECT_ID(N'[dbo].[TR_UpdateTotalFabricByProduct]'))
			BEGIN
				EXEC('CREATE TRIGGER TR_UpdateTotalFabricByProduct
					ON UnitsFabricByProduct
					AFTER INSERT, UPDATE, DELETE
					AS
					BEGIN
						-- Actualizar los totales para los Products afectados
						UPDATE pc
						SET pc.TotalFabricByProduct = (
							SELECT COALESCE(SUM(upc.UnitTotal), 0)
							FROM UnitsFabricByProduct upc
							WHERE upc.ProductId = pc.ProductId
						)
						FROM FabricsByProduct pc
						INNER JOIN (
							SELECT DISTINCT ProductId
							FROM INSERTED
							UNION
							SELECT DISTINCT ProductId
							FROM DELETED
						) affectedProducts
						ON pc.ProductId = affectedProducts.ProductId;
					END;');
			END;";
		public const string TR_UpdateTotalGarmentHardwareByProduct = @"
			IF NOT EXISTS 
			(SELECT * FROM sys.triggers 
			WHERE object_id = OBJECT_ID(N'[dbo].[TR_UpdateTotalGarmentHardwareByProduct]'))
			BEGIN
				EXEC('CREATE TRIGGER TR_UpdateTotalGarmentHardwareByProduct
					ON UnitsGarmentHardwareByProduct
					AFTER INSERT, UPDATE, DELETE
					AS
					BEGIN
						UPDATE ghp
						SET ghp.TotalGarmentHardwareByProduct = (
							SELECT COALESCE(SUM(ughp.UnitTotal), 0)
							FROM UnitsGarmentHardwareByProduct ughp
							WHERE ughp.ProductId = ghp.ProductId
						)
						FROM GarmentHardwaresByProduct ghp
						INNER JOIN (
							SELECT DISTINCT ProductId
							FROM INSERTED
							UNION
							SELECT DISTINCT ProductId
							FROM DELETED
						) affectedProducts
						ON ghp.ProductId = affectedProducts.ProductId;
					END;');
			END;";

		//Drops for Migration

		public const string Drop_View_UpdateFixedCost = @"DROP VIEW IF EXISTS FixedCostMonthlyView;";
		public const string Drop_View_CostByProduct = @"DROP VIEW IF EXISTS TotalPercentageCostView;";

		public const string Drop_View_FinalPrice = @"DROP VIEW IF EXISTS FinalPriceView;";

		public const string Drop_TR_UpdateUnitsTotalFabric = @"DROP TRIGGER IF EXISTS TR_UpdateUnitsTotalFabric;";
		public const string Drop_TR_UpdateUnitsTotalGarmentHardware = @"DROP TRIGGER IF EXISTS TR_UpdateUnitsTotalGarmentHardware;";
		public const string Drop_TR_UpdateUnitsTotalPackaging = @"DROP TRIGGER IF EXISTS TR_UpdateUnitsTotalPackaging;";

		public const string Drop_TR_UpdateUnitPackaging_UnitsTable = @"DROP TRIGGER IF EXISTS TR_UpdateUnitPackaging_UnitsTable;";
		public const string Drop_TR_UpdateUnitsTotalFabric_UnitsTable = @"DROP TRIGGER IF EXISTS TR_UpdateUnitsTotalFabric_UnitsTable;";
		public const string Drop_TR_UpdateUnitsTotalGarmentHardware_UnitsTable = @"DROP TRIGGER IF EXISTS TR_UpdateUnitsTotalGarmentHardware_UnitsTable;";

		public const string Drop_TR_UpdateTotalPackagingByCategory = @"DROP TRIGGER IF EXISTS TR_UpdateTotalPackagingByCategory;";
		public const string Drop_TR_UpdateTotalFabricByProduct = @"DROP TRIGGER IF EXISTS TR_UpdateTotalFabricByProduct;";
		public const string Drop_TR_UpdateTotalGarmentHardwareByProduct = @"DROP TRIGGER IF EXISTS TR_UpdateTotalGarmentHardwareByProduct;";



		
	}
}
