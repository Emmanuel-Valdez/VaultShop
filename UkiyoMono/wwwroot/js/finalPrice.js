var dataTable;
let translations = {};
const PRICE_TOLERANCE = 0.02;

document.addEventListener("DOMContentLoaded", async () => {
    await loadTranslations();
    loadDataTable();
});

async function loadTranslations() {
    const response = await fetch(`/${culture}/customer/home/GetTranslations`);
    translations = await response.json();
}

function loadDataTable() {
    const table = $('#tblData');
    const labels = {
        priceCurrent: table.data('price-current') || 'Published price matches the suggested price.',
        priceOutdated: table.data('price-outdated') || 'Published price differs from the suggested price.',
        productAvailable: table.data('product-available') || 'Available in store.',
        productUnavailable: table.data('product-unavailable') || 'Not available in store.'
    };
    var isAvailableFilter = false;
    let available = {

        text: `<i class="bi bi-shop" aria-hidden="true"></i><span class="visually-hidden">${translations.availablesInStore}</span>`,
        titleAttr: `${translations.availablesInStore}`,
        className: 'btn btn-success ',
        action: function () {
            if (isAvailableFilter) {
                dataTable.search.fixed('isAvailable', "").draw()
            } else {
                dataTable.search.fixed('isAvailable', "true").draw()
            }
            isAvailableFilter = !isAvailableFilter;
        }
    };

    dataTable = table.DataTable({
        "ajax": { url: `/${culture}/admin/ProductPrice/GetAllProductFinalPrice` },
        "paging": false,
        "info": false,
        "columns": [
            {
                data: 'product',
                "render": function (product, type) {
                    const productName = product?.name || '';

                    if (type !== 'display') {
                        return productName;
                    }

                    const isAvailable = product?.isAvailableInStore;
                    const color = isAvailable ? 'success' : 'warning';
                    const statusText = isAvailable ? labels.productAvailable : labels.productUnavailable;
                    const ariaLabel = `${productName}. ${statusText}`;
                    const productId = product?.id;

                    if (productId === undefined || productId === null) {
                        return `<span class="text-${color}" title="${escapeHtml(statusText)}" aria-label="${escapeHtml(ariaLabel)}">${escapeHtml(productName)}</span>`;
                    }

                    return `<a href="/${culture}/admin/product/upsert?id=${encodeURIComponent(productId)}" class="text-${color} fw-semibold" title="${escapeHtml(statusText)}" aria-label="${escapeHtml(ariaLabel)}">${escapeHtml(productName)}</a>`;
                }
            },
            { data: 'categoryName' },
            { data: 'avgShippingCostByCategory', render: window.SpanishNumberTables(culture) },
            { data: 'totalCost', render: window.SpanishNumberTables(culture) },
            { data: 'retailWithProfit', render: window.SpanishNumberTables(culture) },
            { data: 'retailWithShipping', render: window.SpanishNumberTables(culture) },
            { data: 'actualRetailPrice', render: window.SpanishNumberTables(culture) },
            {
                data: {
                    finalRetail: 'finalRetail', actualListPrice: 'actualListPrice'
                },
                "render": function (data) {
                    return renderPriceStatus(data.finalRetail, data.actualListPrice, 'text-warning', labels);
                }
            },
            { data: 'actualWholesalePrice', render: window.SpanishNumberTables(culture) },
            {
                data: {
                    finalWholesale: 'finalWholesale', actualWholesalePrice: 'actualWholesalePrice'
                },
                "render": function (data) {
                    return renderPriceStatus(data.finalWholesale, data.actualWholesalePrice, 'text-danger', labels);
                }
            },
            {
                data: 'product.isAvailableInStore',
                render: function (data) {
                    return data ? 'true' : 'false';
                }
            }
        ],
        "language": window.SpanishCultureTables(culture),
        layout: { topStart: 'buttons' },

        buttons: {
            buttons: [
                {
                    extend: 'copy',
                    text: `<i class="bi bi-clipboard-fill" aria-hidden="true"></i><span class="visually-hidden">${translations.copy}</span>`,
                    titleAttr: `${translations.copy}`,
                    className: 'btn btn-info',
                    exportOptions: {
                        columns: ':visible'
                    },
                },
                {
                    extend: 'pdfHtml5',
                    text: `<i class="bi bi-file-pdf-fill" aria-hidden="true"></i><span class="visually-hidden">${translations.exportToPDF}</span>`,
                    exportOptions: {
                        columns: ':visible'
                    },
                    titleAttr: `${translations.exportToPDF}`,
                    className: 'btn btn-danger',
                },
                available,
                {
                    extend: 'colvis',
                    text: `${translations.columns}`,
                    titleAttr: `${translations.columnsVisibility}`,
                }
            ],
        },
        columnDefs: [
            { responsivePriority: 1, targets: 9 },
            { responsivePriority: 2, targets: 7 },
            { responsivePriority: 3, targets: 8 },
            { responsivePriority: 4, targets: 6 },
            { responsivePriority: 5, targets: 0 },
            { responsivePriority: 6, targets: 1 },
            { targets: 10, visible: false, searchable: true },
            { targets: 6, visible: false },
            { targets: 2, visible: false },
            { targets: 3, visible: false },
            { targets: 4, visible: false },
            { targets: 5, visible: false }
        ],
        responsive: {
            details: {
                type: 'column',
                target: 'tr'
            }
        }
    });
}

function renderPriceStatus(suggestedPrice, actualPrice, mismatchClass, labels) {
    const isUpdated = PriceUpdated(suggestedPrice, actualPrice, PRICE_TOLERANCE);
    const classColor = isUpdated ? 'text-info' : mismatchClass;
    const statusText = isUpdated ? labels.priceCurrent : labels.priceOutdated;
    const formattedPrice = FormattedPrice(suggestedPrice);
    const ariaLabel = `${formattedPrice}. ${statusText}`;

    return `<span class="${classColor}" title="${escapeHtml(statusText)}" aria-label="${escapeHtml(ariaLabel)}">${escapeHtml(formattedPrice)}</span>`;
}

function PriceUpdated(num1, num2, tol) {
    return (Math.abs(num1 - num2) <= tol)
}

function FormattedPrice(num) {
    let formatted = parseFloat(num).toLocaleString('es-AR', {
        style: 'currency', currency: 'ARS',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });
    return formatted;
}

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}
