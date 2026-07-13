var dataTable;
const translations = window.orderTableTranslations ?? {};

function statusBadgeClass(status) {
    if (status === "Cancelled" || status === "Refunded" || status === "Rejected") return "bg-danger";
    if (status === "Pending" || status === "ApprovedForDelayedPayment") return "bg-warning text-dark";
    if (status === "Approved" || status === "Shipped") return "bg-success";
    if (status === "Processing") return "bg-info text-dark";
    return "bg-secondary";
}

function renderStatusBadge(data, type, statusTranslations) {
    const text = statusTranslations?.[data] ?? data ?? "";
    return type === 'display' ? `<span class="badge ${statusBadgeClass(data)}">${text}</span>` : text;
}

function renderPaymentMethodBadge(data, type) {
    const text = window.paymentMethodTranslations?.[data] ?? data ?? "";
    return type === 'display' ? `<span class="badge bg-secondary">${text}</span>` : text;
}

document.addEventListener("DOMContentLoaded", () => {
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

function loadDataTable(status) {
    dataTable = $('#tblData').DataTable({
        "ajax": { url: `/${culture}/admin/order/getall?status=` + status },
        "columns": [
            { data: 'id', "width": "5%" },
            { data: 'name', "width": "20%" },
            { data: 'phoneNumber', "width": "10%" },
            { data: 'applicationUser.email', "width": "20%" },
            {
                data: 'company',
                "width": "15%",
                render: function (data) {
                    return data?.name ?? "";
                }
            },
            {
                data: 'orderStatus',
                "width": "10%",
                render: function (data, type) {
                    return renderStatusBadge(data, type, window.orderStatusTranslations);
                }
            },
            {
                data: 'paymentStatus',
                "width": "10%",
                render: function (data, type) {
                    return renderStatusBadge(data, type, window.paymentStatusTranslations);
                }
            },
            {
                data: 'paymentMethod',
                "width": "10%",
                render: function (data, type) {
                    return renderPaymentMethodBadge(data, type);
                }
            },
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
        "order": [],
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
            { responsivePriority: 1, targets: 9 },
            { responsivePriority: 2, targets: 8 },
            { targets: 2, visible: false },
            { targets: 6, visible: false },
        ],

    });
}