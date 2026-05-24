var dataTable;

$(document).ready(function () {
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
        "ajax": { url: `/${culture}/admin/order/getall?status=`+ status },
        "columns": [
            { data: 'id', "width": "5%" },
            { data: 'name', "width": "20%" },
            { data: 'phoneNumber', "width": "10%" },
            { data: 'applicationUser.email', "width": "20%" },
            {
                data: 'orderStatus',
                "width": "10%",
                render: function (data, type) {
                    const translatedStatus = window.orderStatusTranslations?.[data] ?? data;
                    return type === 'display' || type === 'filter' ? translatedStatus : data;
                }
            },
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
        
        columnDefs: [
            { responsivePriority: 1, targets: 7 },
            { responsivePriority: 2, targets: 6 },
            { targets: 2, visible: false },
            { targets: 5, visible: false },
        ],

    });
}
