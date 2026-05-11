var dataTable;
let translations = {};

document.addEventListener("DOMContentLoaded", async () => {
    await loadTranslations();
    loadDataTable();
});

async function loadTranslations() {
    const response = await fetch(`/${culture}/customer/home/GetTranslations`);
    translations = await response.json();
}

function loadDataTable() {
    var isAvailableFilter = false;
    let available = {

        text: '<i class="bi bi-shop"></i>',
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
    },
    dataTable = $('#tblData').DataTable({
        "ajax": { url: `/${culture}/admin/ProductPrice/GetAllProductFinalPrice` },
        "columns": [
            { data: 'product.id' },
            {
                data: { name: 'product.name', isAvailableInStore: 'product.isAvailableInStore' },
                "render": function (data) {
                    let color = 'success'
                    if (!data.product.isAvailableInStore) {
                        color = 'warning';
                    }
                    return `<span class='text-${color}'>${data.product.name}</span>`;
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
                "render":
                    function (data) {
                        let classColor = 'text-info';
                        if (!PriceUpdated(data.finalRetail, data.actualListPrice, 0.01)) {
                            classColor = 'text-warning'
                        }
                        let formattedPrice = FormattedPrice(data.finalRetail);
                        return `<span class=${classColor}>${formattedPrice}</span>`
                    }
            },
            {
                data: {
                    wholesaleWithProfit: 'wholesaleWithProfit', actualWholesalePrice: 'actualWholesalePrice'
                },
                "render":
                    function (data) {
                        let classColor = 'text-info';
                        if (!PriceUpdated(data.wholesaleWithProfit, data.actualWholesalePrice, 0.01)) {
                            classColor = 'text-danger'
                        }
                        let formattedPrice = FormattedPrice(data.wholesaleWithProfit);
                        return `<span class=${classColor}>${formattedPrice}</span>`
                    }
            },
            { data: 'product.isAvailableInStore' }
        ],
        "language": window.SpanishCultureTables(culture),
        layout: { topStart: 'buttons' },

        buttons: {
            buttons: [
                {
                    extend: 'copy',
                    text: '<i class="bi bi-clipboard-fill"></i>',
                    titleAttr: `${translations.copy}`,
                    className: 'btn btn-info',
                    exportOptions: {
                        columns: ':visible'
                    },
                },
                {
                    extend: 'pdfHtml5',
                    text: '<i class="bi bi-file-pdf-fill"></i>',
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
            { responsivePriority: 2, targets: 8 },
            { responsivePriority: 3, targets: 7 },
            { responsivePriority: 4, targets: 1 },
            { responsivePriority: 5, targets: 0 },
            { targets: 10, visible: false },
            { targets: 3, visible: false },
            { targets: 4, visible: false },
            { targets: 5, visible: false },
            { targets: 6, visible: false }
        ],
        responsive: {
            details: {
                type: 'column',
                target: 'tr'
            }
        }
    });
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