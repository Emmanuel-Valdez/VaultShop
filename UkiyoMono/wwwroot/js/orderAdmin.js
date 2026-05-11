var dataTable;
let translations = {};

document.addEventListener("DOMContentLoaded", async () => {
    await loadTranslations();
    var url = window.location.search;
    if (url.includes("inprocess")) {
        loadDataTable("inprocess")
    } else {
        if (url.includes("completed")) {
            loadDataTable("completed");
        } else {
            if (url.includes("pending")) {
                loadDataTable("pending");
            } else {
                if (url.includes("approved")) {
                    loadDataTable("approved");
                } else {
                    loadDataTable("all");
                }
            }
        }
    }
});

async function loadTranslations() {
    const response = await fetch(`/${culture}/customer/home/GetTranslations`);
    translations = await response.json();
}

function loadDataTable(status) {
    dataTable = $('#tblData').DataTable({
        "ajax": { url: `/${culture}/admin/order/getall?status=`+ status },
        "columns": [
            { data: 'id', "width": "5%" },
            { data: 'name', "width": "20%" },
            { data: 'phoneNumber', "width": "10%" },
            { data: 'applicationUser.email', "width": "20%" },
            { data: 'orderStatus', "width": "10%" },
            { data: 'paymentStatus', "width": "10%" }, 
            { data: 'orderTotal', "width": "10%", render: window.SpanishNumberTables(culture) },
            {
                data: 'id',
                "render": function (data) {
                    return `<div class="w-75 btn-group" role="group">
                       <a href="/${culture}/admin/order/details?orderId=${data}" class="btn btn-primary mx-2"><i class="bi bi-pencil-square"></i> </a>
                        
                    </div>`
                },
                "width": "10%"
            }
        ],
        "language": window.SpanishCultureTables(culture),
        responsive: {
            details: {
                type: 'column',
                target: 'tr'
            }
        },
        layout: { top2Start: 'buttons' },
     
        buttons: {
            buttons: [
                {
                    extend: 'pdfHtml5',
                    text: '<i class="bi bi-file-pdf-fill"></i>',
                    exportOptions: {
                        columns: ':visible'
                    },
                    titleAttr: `${translations.exportToPDF}`,
                    className: 'btn btn-danger',
                },
                {
                    extend: 'colvis',
                    text: `${translations.columnsVisibility}`,
                }
            ],
        },
        columnDefs: [
            { responsivePriority: 1, targets: 7 },
            { responsivePriority: 2, targets: 6 },
            { targets: 2, visible: false },
            { targets: 5, visible: false },
        ],

    });
}
