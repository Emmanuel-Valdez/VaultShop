# shipping/methods-and-charges Specification

## Purpose
Defines the per-product free-shipping flag and the resulting shipping charge rules for cart and checkout, the Gran Mendoza delivery modalities (Correo branch/postal-unit pickup, motorcycle, vendor-arranged pickup in Microcentro), and the frozen shipping charge and modality every order carries.

## Requirements

### Requirement: Product carries a free-shipping flag

Every `Product` SHALL carry a free-shipping flag. At migration time all existing products SHALL be flagged as free shipping, because their current retail prices already include the category average shipping cost. Products created afterwards SHALL default to no free shipping, and the admin product upsert SHALL expose the flag so it can be changed.

#### Scenario: Existing products are backfilled as free shipping

- **WHEN** the migration runs
- **THEN** every product that existed before the migration has the free-shipping flag set, and no stored retail price changes

#### Scenario: New product defaults to no free shipping

- **WHEN** an admin opens the product creation form for a new product
- **THEN** the free-shipping flag is off and the admin can turn it on before saving

#### Scenario: Admin toggles the flag

- **WHEN** an admin edits an existing product and changes the free-shipping flag
- **THEN** the product is saved with the new flag value

### Requirement: Order shipping charge is computed once from the largest category average

For an order whose lines are all non-free, the shipping charge SHALL be a single amount equal to the largest `Category.AvgShippingCost` among those lines, grossed up by the configured total percentage costs so the amount received net of those costs covers it, rounded to whole cents. The charge SHALL NOT be multiplied by line count, quantity, or number of packages, and SHALL NOT receive any coupon, promotion, or payment-method discount. For an order containing at least one free-shipping line, the charge SHALL be zero.

#### Scenario: Free-shipping line makes the order ship free

- **WHEN** a cart contains one free-shipping product and one non-free product from a category with an average shipping cost of 5000
- **THEN** the order shipping charge is 0

#### Scenario: Paid charge uses the largest average, charged once

- **WHEN** a cart contains 3 units of a category with average shipping cost 2000 and 1 unit of a category with average shipping cost 5000, and no line is free-shipping
- **THEN** the order carries one shipping charge computed from 5000, not a per-unit or per-line sum

#### Scenario: Percentage costs are grossed up into the charge

- **WHEN** the configured total percentage costs are 10%, the largest category average shipping cost is 5000, and the charge applies
- **THEN** the charge is 5555.56

#### Scenario: Discounts never touch the charge

- **WHEN** a coupon, an automatic promotion, or a payment-method discount applies to an order with a shipping charge
- **THEN** the charge amount is identical to the charge without any discount

### Requirement: Cart and checkout show the shipping line

The cart and checkout summary SHALL show shipping as a line of the total. When the cart qualifies for free shipping, the line SHALL read as free shipping with the standard shipping amount (the larger category average among the cart's lines, grossed up the same way) struck through, and SHALL NOT add to the total. Otherwise the charge SHALL be added to the total like one more item. The cart SHALL NOT add explanatory notes beyond the shipping line and the free-shipping label. Choosing vendor-arranged pickup in checkout SHALL remove the charge from the total.

#### Scenario: Free cart shows struck-through amount

- **WHEN** a cart with a free-shipping product is displayed
- **THEN** the summary shows free shipping with the standard amount struck through and the total adds no shipping amount

#### Scenario: Paid cart adds the charge like an item

- **WHEN** a cart whose lines are all non-free is displayed
- **THEN** the shipping charge appears as an amount added to the total, like one more item

#### Scenario: Vendor-arranged pickup removes the charge

- **WHEN** the shopper selects vendor-arranged pickup in checkout for a cart with a shipping charge
- **THEN** the charge line is removed and the total drops by the charge amount

### Requirement: Domicile province and locality are chosen from the branch catalog

Checkout SHALL collect the domicile province and locality from the same catalog that feeds Correo branch lookup (no free-text province or locality), keeping the street address as free text because invoicing requires it. The server SHALL reject a submitted province/locality pair that does not exist in that catalog. Locality names from the catalog SHALL be matched against the Gran Mendoza list case- and accent-insensitively, so spelling variants such as `LAS CANAS` and `Las Cañas` are equivalent. Correo branches and postal units SHALL be treated as equally valid pickup points and SHALL NOT be distinguished in availability.

#### Scenario: Locality list is scoped to the province

- **WHEN** a shopper selects a province in checkout
- **THEN** the locality list contains exactly that province's catalog localities

#### Scenario: Unknown locality is rejected

- **WHEN** a checkout POST carries a province/locality pair absent from the catalog
- **THEN** the submission is rejected with a validation error and no order is created

### Requirement: Gran Mendoza offers motorcycle and vendor-arranged pickup

Exactly these localities SHALL be treated as Gran Mendoza: Bermejo, Capilla del Rosario, Carrodilla, Chacras de Coria, Cieneguita, Dorrego, El Challao, El Resguardo, General Belgrano, Godoy Cruz, Kilómetro 11, Kilómetro 8, Maipú, Las Cañas, Las Heras, Las Tortugas, Luján de Cuyo, Luzuriaga, Mendoza, Rodeo de la Cruz, Rodeo del Medio, San Francisco del Monte, San José, Villa Hipódromo, Villa Marini, and Villa Nueva. For a Gran Mendoza locality whose cart does not qualify for free shipping, checkout SHALL offer motorcycle delivery and vendor-arranged pickup first, followed by Correo pickup; the motorcycle covers all 26 localities, and for Maipú the delivery address is confirmed by a human before dispatch (no automatic center boundary exists). For a Gran Mendoza locality whose cart qualifies for free shipping, checkout SHALL offer motorcycle and Correo pickup at no charge and SHALL NOT show vendor-arranged pickup. For any other locality or province, checkout SHALL offer Correo pickup only.

#### Scenario: Gran Mendoza paid cart shows all modalities

- **WHEN** a shopper in Godoy Cruz completes a cart whose lines are all non-free
- **THEN** checkout offers motorcycle, vendor-arranged pickup, and Correo pickup, with the first two listed before the Correo option

#### Scenario: Gran Mendoza free cart hides vendor-arranged pickup

- **WHEN** a shopper in Godoy Cruz completes a cart containing a free-shipping product
- **THEN** checkout shows motorcycle and Correo pickup at no charge and no vendor-arranged pickup option

#### Scenario: Outside Gran Mendoza only Correo is offered

- **WHEN** a shopper selects a locality that is not in the Gran Mendoza list, in Mendoza or any other province
- **THEN** checkout offers Correo pickup only, free or charged per the cart rule

#### Scenario: Maipú motorcycle is confirmed before dispatch

- **WHEN** a shopper in Maipú selects motorcycle delivery
- **THEN** the order is created with motorcycle as its modality and the store confirms the address with the shopper before dispatch

### Requirement: Correo pickup selection stays manual

For Correo pickup, checkout SHALL list the selected locality's eligible branches and postal units (name, address, hours) for manual selection; no proximity ranking or automatic nearest-branch search SHALL be applied. The chosen branch SHALL be resolved and snapshotted from the catalog by code at order creation.

#### Scenario: Branch list is manual

- **WHEN** a shopper selects Correo pickup for a locality with several branches
- **THEN** all eligible branches for that locality are listed for the shopper to pick, without distance ordering

#### Scenario: Forged branch code rejected

- **WHEN** a checkout POST carries a branch code that is not an eligible pickup branch
- **THEN** the submission is rejected with a validation error and no order is created

### Requirement: Vendor-arranged pickup is recorded and coordinated outside the store

Selecting vendor-arranged pickup SHALL create the order with no shipping charge and SHALL record that the shopper chose delivery to be arranged at the Microcentro of Mendoza; the order receipt SHALL state exactly that wording. If the store later receives a domicile-delivery request for such an order, the shipment SHALL be arranged and charged outside the store, and the recorded modality SHALL remain unchanged.

#### Scenario: Recorded wording on the receipt

- **WHEN** an order is created with vendor-arranged pickup
- **THEN** the order receipt records delivery to be arranged at the Microcentro of Mendoza and the shipping charge is 0

#### Scenario: Later domicile request is charged outside

- **WHEN** a shopper with a vendor-arranged pickup order later requests delivery to their domicile
- **THEN** the order record keeps its recorded modality and no shipping charge is added inside the store

### Requirement: The order freezes the shipping charge and delivery modality

Every order SHALL persist, at creation time, the shipping charge that applied and the chosen delivery modality, and the frozen charge SHALL be part of the order total charged to the customer. Later changes to the product flag, category average shipping cost, or percentage-cost configuration SHALL NOT rewrite past orders.

#### Scenario: Frozen charge survives later changes

- **WHEN** an order was created with a shipping charge and the category's average shipping cost later changes
- **THEN** the order keeps the charge captured at purchase

#### Scenario: Zero charge is frozen as zero

- **WHEN** an order is created for a cart that qualifies for free shipping
- **THEN** its persisted shipping charge is 0 and its total excludes any shipping amount

### Requirement: Order surfaces show the frozen shipping charge and modality

The order confirmation, customer order history, and admin order detail SHALL show the frozen shipping charge and the chosen delivery modality, using persisted values only. Free orders SHALL display as free shipping. Correo pickup orders SHALL keep showing the branch snapshot, and motorcycle or vendor-arranged pickup orders SHALL show their recorded modality instead.

#### Scenario: Charged order shows amount and modality

- **WHEN** an order with a shipping charge is viewed in confirmation, customer history, or admin detail
- **THEN** the frozen charge and the delivery modality are shown

#### Scenario: Free order shows as free shipping

- **WHEN** an order with shipping charge 0 is viewed
- **THEN** shipping is displayed as free rather than as an amount of 0

### Requirement: Free-shipping badge is retail-only

Storefront listings and product details SHALL show a free-shipping badge for products with the flag, and the cart SHALL show the free-shipping label with the standard amount struck through. The badge and label SHALL be shown to retail viewers only; viewers on wholesale prices (Company users and wholesale preview) SHALL NOT see them.

#### Scenario: Retail viewer sees the badge

- **WHEN** a retail shopper views a listing or details page of a free-shipping product
- **THEN** the free-shipping badge is rendered

#### Scenario: Wholesale viewer sees no badge

- **WHEN** a Company user or an admin in wholesale preview views the same product
- **THEN** no free-shipping badge is rendered

### Requirement: Wholesale orders are excluded from shipping charges

Company (wholesale) orders SHALL NOT receive a shipping charge or a free-shipping qualification from the product flag; their shipping SHALL remain coordinated outside the system as today. Company checkout SHALL keep requiring a Correo pickup branch selection.

#### Scenario: Company order carries no shipping charge

- **WHEN** a Company user completes checkout
- **THEN** the created order carries no shipping charge and its total equals the wholesale product total

#### Scenario: Company checkout still requires a branch

- **WHEN** a Company user submits checkout without selecting a pickup branch
- **THEN** the submission is rejected as today
