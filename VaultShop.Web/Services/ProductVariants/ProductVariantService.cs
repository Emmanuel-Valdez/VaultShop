using VaultShop.DataAccess.Repository.IRepository;
using VaultShop.Models;

namespace VaultShop.Web.Services.ProductVariants
{
	public class ProductVariantService : IProductVariantService
	{
		private readonly IUnitOfWork _unitOfWork;

		public ProductVariantService(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
		}

		public IProductVariantService.VariantAdminData? GetAdminData(int productId)
		{
			var product = _unitOfWork.Product.Get(p => p.Id == productId && !p.IsDeleted);
			if (product == null)
			{
				return null;
			}

			var values = _unitOfWork.VariantOptionValue
				.GetAll(v => v.ProductId == productId, includeProperties: "VariantOptionType")
				.OrderBy(v => v.VariantOptionTypeId)
				.ThenBy(v => v.SortOrder)
				.ThenBy(v => v.Id)
				.ToList();

			var variants = _unitOfWork.ProductVariant
				.GetAll(v => v.ProductId == productId, includeProperties: "Values,Values.Value,Values.Value.VariantOptionType")
				.OrderBy(v => v.Id)
				.ToList();

			return new IProductVariantService.VariantAdminData
			{
				Product = product,
				AllTypeNames = _unitOfWork.VariantOptionType.GetAll().Select(t => t.Name).OrderBy(n => n).ToList(),
				Types = values
					.GroupBy(v => v.VariantOptionTypeId)
					.Select(g => new IProductVariantService.VariantTypeGroup
					{
						TypeId = g.Key,
						TypeName = g.First().VariantOptionType.Name,
						Values = g.ToList(),
					})
					.ToList(),
				Variants = variants
					.Select(v => new IProductVariantService.VariantRow
					{
						Variant = v,
						Label = FormatLabel(v.Values.OrderBy(vv => vv.Value.VariantOptionTypeId).ToList()),
					})
					.ToList(),
			};
		}

		public IProductVariantService.VariantResult AddValue(int productId, string typeName, string value, int sortOrder)
		{
			var product = _unitOfWork.Product.Get(p => p.Id == productId && !p.IsDeleted);
			if (product == null)
			{
				return IProductVariantService.VariantResult.Fail("ProductNotFound");
			}

			typeName = (typeName ?? string.Empty).Trim();
			value = (value ?? string.Empty).Trim();
			if (typeName.Length == 0 || typeName.Length > 100)
			{
				return IProductVariantService.VariantResult.Fail("VariantTypeRequired");
			}
			if (value.Length == 0 || value.Length > 100)
			{
				return IProductVariantService.VariantResult.Fail("VariantValueRequired");
			}

			var type = _unitOfWork.VariantOptionType.GetAll().FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
			if (type == null)
			{
				type = new VariantOptionType { Name = typeName };
				_unitOfWork.VariantOptionType.Add(type);
				_unitOfWork.Save();
			}

			var duplicate = _unitOfWork.VariantOptionValue
				.GetAll(v => v.ProductId == productId && v.VariantOptionTypeId == type.Id)
				.Any(v => v.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
			if (duplicate)
			{
				return IProductVariantService.VariantResult.Fail("VariantValueAlreadyExists");
			}

			_unitOfWork.VariantOptionValue.Add(new VariantOptionValue
			{
				ProductId = productId,
				VariantOptionTypeId = type.Id,
				Value = value,
				SortOrder = sortOrder,
			});
			_unitOfWork.Save();
			return IProductVariantService.VariantResult.Ok();
		}

		public IProductVariantService.VariantResult DeleteValue(int productId, int valueId)
		{
			// ponytail: tracked fetch — the same request context may already track this row (e.g. right after AddValue).
			var value = _unitOfWork.VariantOptionValue.Get(v => v.Id == valueId, tracked: true);
			if (value == null)
			{
				return IProductVariantService.VariantResult.Fail("VariantValueNotFound");
			}
			if (value.ProductId != productId)
			{
				return IProductVariantService.VariantResult.Fail("VariantInvalid");
			}

			var referenced = _unitOfWork.ProductVariantValue.GetAll(vv => vv.ValueId == valueId).Any();
			if (referenced)
			{
				return IProductVariantService.VariantResult.Fail("VariantValueReferenced");
			}

			_unitOfWork.VariantOptionValue.Remove(value);
			_unitOfWork.Save();
			return IProductVariantService.VariantResult.Ok();
		}

		public IProductVariantService.VariantResult GenerateCombinations(int productId)
		{
			var product = _unitOfWork.Product.Get(p => p.Id == productId && !p.IsDeleted);
			if (product == null)
			{
				return IProductVariantService.VariantResult.Fail("ProductNotFound");
			}

			var valuesByType = _unitOfWork.VariantOptionValue
				.GetAll(v => v.ProductId == productId)
				.OrderBy(v => v.VariantOptionTypeId)
				.ThenBy(v => v.SortOrder)
				.ThenBy(v => v.Id)
				.GroupBy(v => v.VariantOptionTypeId)
				.Select(g => g.ToList())
				.ToList();
			if (valuesByType.Count == 0 || valuesByType.Any(g => g.Count == 0))
			{
				return IProductVariantService.VariantResult.Fail("NoVariantValues");
			}

			var existing = _unitOfWork.ProductVariant
				.GetAll(v => v.ProductId == productId, includeProperties: "Values")
				.Select(v => string.Join(",", v.Values.Select(vv => vv.ValueId).OrderBy(id => id)))
				.ToHashSet();

			var created = 0;
			// hardening 3.3: one SaveChanges for all rows — EF resolves the join FKs from the
			// Variant nav on insert, so no per-row round-trips are needed.
			foreach (var combo in Cartesian(valuesByType))
			{
				var key = string.Join(",", combo.Select(v => v.Id).OrderBy(id => id));
				if (existing.Contains(key))
				{
					continue;
				}

				var variant = new ProductVariant { ProductId = productId, IsAvailable = true };
				foreach (var value in combo)
				{
					variant.Values.Add(new ProductVariantValue { Variant = variant, ValueId = value.Id });
				}
				_unitOfWork.ProductVariant.Add(variant);
				existing.Add(key);
				created++;
			}
			_unitOfWork.Save();

			return IProductVariantService.VariantResult.Ok(created);
		}

		public IProductVariantService.VariantResult SetAvailability(int productId, int variantId, bool isAvailable)
		{
			// ponytail: tracked fetch + Save, no detached Update — same context may track the row from GenerateCombinations.
			var variant = _unitOfWork.ProductVariant.Get(v => v.Id == variantId, tracked: true);
			if (variant == null)
			{
				return IProductVariantService.VariantResult.Fail("VariantNotFound");
			}
			if (variant.ProductId != productId)
			{
				return IProductVariantService.VariantResult.Fail("VariantInvalid");
			}

			variant.IsAvailable = isAvailable;
			_unitOfWork.Save();
			return IProductVariantService.VariantResult.Ok();
		}

		public IProductVariantService.VariantResult DeleteVariant(int productId, int variantId)
		{
			// ponytail: tracked fetch, same reason as above.
			var variant = _unitOfWork.ProductVariant.Get(v => v.Id == variantId, includeProperties: "Values", tracked: true);
			if (variant == null)
			{
				return IProductVariantService.VariantResult.Fail("VariantNotFound");
			}
			if (variant.ProductId != productId)
			{
				return IProductVariantService.VariantResult.Fail("VariantInvalid");
			}
			// hardening 2.1: a deleted variant must not silently degrade a live cart line into a
			// base-product purchase (FK is SetNull). OrderDetails keep their frozen label regardless.
			if (_unitOfWork.ShoppingCart.GetAll(c => c.VariantId == variantId).Any())
			{
				return IProductVariantService.VariantResult.Fail("VariantReferencedByCart");
			}

			// ponytail: FK SetNull keeps carts/orders intact; join rows go with the variant.
			_unitOfWork.ProductVariantValue.RemoveRange(variant.Values.ToList());
			_unitOfWork.ProductVariant.Remove(variant);
			_unitOfWork.Save();
			return IProductVariantService.VariantResult.Ok();
		}

		public IProductVariantService.VariantValidationResult ValidateVariantForProduct(int productId, int? variantId)
		{
			var product = _unitOfWork.Product.Get(p => p.Id == productId && !p.IsDeleted);
			if (product == null)
			{
				return IProductVariantService.VariantValidationResult.Invalid("ProductNotFound");
			}

			var productTypeIds = _unitOfWork.VariantOptionValue
				.GetAll(v => v.ProductId == productId)
				.Select(v => v.VariantOptionTypeId)
				.Distinct()
				.ToList();

			if (productTypeIds.Count == 0)
			{
				return variantId == null
					? IProductVariantService.VariantValidationResult.Valid()
					: IProductVariantService.VariantValidationResult.Invalid("VariantInvalid");
			}

			if (variantId == null)
			{
				return IProductVariantService.VariantValidationResult.Invalid("VariantRequired");
			}

			var variant = _unitOfWork.ProductVariant.Get(v => v.Id == variantId, includeProperties: "Values,Values.Value");
			if (variant == null || variant.ProductId != productId)
			{
				return IProductVariantService.VariantValidationResult.Invalid("VariantInvalid");
			}
			if (!variant.IsAvailable)
			{
				return IProductVariantService.VariantValidationResult.Invalid("VariantUnavailable");
			}

			var variantTypeIds = variant.Values.Select(vv => vv.Value.VariantOptionTypeId).ToList();
			if (variantTypeIds.Count != productTypeIds.Count
				|| variantTypeIds.Distinct().Count() != variantTypeIds.Count
				|| !variantTypeIds.OrderBy(id => id).SequenceEqual(productTypeIds.OrderBy(id => id)))
			{
				return IProductVariantService.VariantValidationResult.Invalid("VariantInvalid");
			}

			return IProductVariantService.VariantValidationResult.Valid();
		}

		public IProductVariantService.VariantSelectionData? GetSelectionData(int productId)
		{
			var values = _unitOfWork.VariantOptionValue
				.GetAll(v => v.ProductId == productId, includeProperties: "VariantOptionType")
				.OrderBy(v => v.VariantOptionTypeId)
				.ThenBy(v => v.SortOrder)
				.ThenBy(v => v.Id)
				.ToList();
			if (values.Count == 0)
			{
				return null;
			}

			var variants = _unitOfWork.ProductVariant
				.GetAll(v => v.ProductId == productId, includeProperties: "Values")
				.OrderBy(v => v.Id)
				.ToList();

			return new IProductVariantService.VariantSelectionData
			{
				Types = values
					.GroupBy(v => v.VariantOptionTypeId)
					.Select(g => new IProductVariantService.VariantTypeGroup
					{
						TypeId = g.Key,
						TypeName = g.First().VariantOptionType.Name,
						Values = g.ToList(),
					})
					.ToList(),
				Variants = variants
					.Select(v => new IProductVariantService.VariantRow
					{
						Variant = v,
						Label = string.Empty,
					})
					.ToList(),
			};
		}

		public string BuildVariantLabel(int variantId)
		{
			var variant = _unitOfWork.ProductVariant.Get(v => v.Id == variantId, includeProperties: "Values,Values.Value,Values.Value.VariantOptionType");
			if (variant == null)
			{
				return string.Empty;
			}

			return FormatLabel(variant.Values.OrderBy(vv => vv.Value.VariantOptionTypeId).ToList());
		}

		private static string FormatLabel(List<ProductVariantValue> orderedValues)
		{
			return string.Join(", ", orderedValues.Select(vv => $"{vv.Value.VariantOptionType.Name}: {vv.Value.Value}"));
		}

		private static IEnumerable<List<VariantOptionValue>> Cartesian(List<List<VariantOptionValue>> groups)
		{
			IEnumerable<List<VariantOptionValue>> result = new[] { new List<VariantOptionValue>() };
			foreach (var group in groups)
			{
				result = result.SelectMany(acc => group.Select(value => acc.Concat(new[] { value }).ToList()));
			}
			return result;
		}
	}
}
