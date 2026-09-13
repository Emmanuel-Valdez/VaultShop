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
    dataTable = $('#tblData').DataTable({
        "ajax": { url: `/${culture}/admin/ProductPrice/GetAllCostByProduct` },
        "columns": [
            {data: 'product.name', "width": "15%" },
            {data: 'categoryName', "width": "15%" },
            {data: 'maxExpectationMonthly', "width": "10%" },
            { data: 'fixedCostAddedByCategory', "width": "15%", render: window.SpanishNumberTables(culture) },
            { data: 'packaging', "width": "10%", render: window.SpanishNumberTables(culture)},
            { data: 'garmentHardware', "width": "10%", render: window.SpanishNumberTables(culture)},
            { data: 'fabric', "width": "10%", render: window.SpanishNumberTables(culture)},
            { data: 'totalCostByProduct', "width": "15%", render: window.SpanishNumberTables(culture)}
        ],
        "language": window.SpanishCultureTables(culture),
        responsive: {
            details: {
                type: 'column',
                target: 'tr'
            }
        },
        "columnDefs": [
            { responsivePriority: 1, targets: 0 },
            { responsivePriority: 2, targets: 4 },
            { responsivePriority: 3, targets: 5 },
            { responsivePriority: 4, targets: 6 },
            { responsivePriority: 5, targets: 7 },
            { responsivePriority: 10, targets: 1 },
            { responsivePriority: 11, targets: 2 },
            { responsivePriority: 12, targets: 3 }
        ],
        layout: { topStart: 'buttons' },

        buttons: {
            buttons: [
               
                {
                    extend: 'colvis',
                    text: `${translations.columns}`,
                    titleAttr: `${translations.columnsVisibility}`,
                }
            ],
        },
    });
}