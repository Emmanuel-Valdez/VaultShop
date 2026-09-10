## Purpose

Preserves catalog integrity by making category deletion a guarded operation: a category cannot disappear while active products still reference it, and its calculator-side packaging data is cleaned atomically when deletion succeeds.

## Requirements

### Requirement: Category deletion is blocked when active products exist

The system SHALL prevent soft-deleting a `Category` (`IsDeleted=true`) when at least one `Product` with `IsDeleted=false` still references it via `CategoryId`. The check SHALL count only non-deleted products; soft-deleted or already-removed products SHALL NOT block deletion.

#### Scenario: Delete blocked with count
- **WHEN** an admin calls `POST /{culture}/admin/category/delete/{id}` for a category that has N >= 1 products where `Product.CategoryId == id && !Product.IsDeleted`
- **THEN** the response is `success=false` with a localized message containing N and guidance to reassign products before deleting, the category remains `IsDeleted=false`, and no packaging rows are removed

#### Scenario: Delete allowed when no active products
- **WHEN** an admin deletes a category with zero active products (`Products.Any(CategoryId==id && !IsDeleted) == false`)
- **THEN** the category is soft-deleted (`IsDeleted=true`), its `PackagingByCategory` + `UnitPackagingByCategory` children for that `CategoryId` are hard-deleted in the same `Save()`, and the response is `success=true`

#### Scenario: Deleted products do not block
- **WHEN** a category has only products where `IsDeleted=true` (or zero products) and an admin deletes it
- **THEN** deletion succeeds as above

#### Scenario: Count reflects only active products
- **WHEN** a category has 2 active + 3 soft-deleted products and a delete is attempted
- **THEN** the error message reports `2`, not `5`

### Requirement: Packaging cascade on successful category deletion (option B)

The system SHALL hard-delete the `PackagingByCategory` row (and its `UnitPackagingByCategory` children) whose `CategoryId` equals the deleted category, atomically with the category soft-delete. If no packaging rows exist for that category, deletion SHALL still succeed.

#### Scenario: Category with packaging is deleted
- **WHEN** a category with one `PackagingByCategory` and two `UnitPackagingByCategory` rows is successfully deleted
- **THEN** after the call `PackagingsByCategory` and `UnitsPackagingByCategory` contain zero rows for that `CategoryId`

#### Scenario: Category without packaging is deleted
- **WHEN** a category with no packaging rows is deleted (and has no active products)
- **THEN** deletion succeeds without error

### Requirement: Admin delete feedback shows block reason with count

The admin category list delete flow SHALL surface the server's `success=false` message as a localized error notification (no table reload) and SHALL keep the table unchanged. On `success=true` it SHALL reload the table and show the success toast as today.

#### Scenario: Blocked delete shows error with count
- **WHEN** the server returns `success=false` with message "Category has 3 products..." for a blocked delete
- **THEN** the UI shows that message via `toastr.error` (or equivalent), stays on the same page, and the DataTable is not reloaded

#### Scenario: Successful delete reloads
- **WHEN** the server returns `success=true`
- **THEN** the UI reloads the DataTable and shows `toastr.success`
