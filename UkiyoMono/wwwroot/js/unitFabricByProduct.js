var dataTable;
let translations = {};
$(document).ready(function () {
    loadDataTable();
});
function loadDataTable() {
    dataTable = $('#tblData').DataTable({
        "ajax": { url: `/${culture}/admin/fabricByProduct/getallunitfabrics?productId=${productId}` },
        "columns": [
            { data: 'fabric.name', "width": "30%" },
            { data: 'fabric.priceMeter', "width": "15%", render: window.SpanishNumberTables(culture) },
            { data: 'quantity', "width": "15%" },
            { data: 'unitTotal', "width": "20%", render: window.SpanishNumberTables(culture) },
            {
                data: 'id',
                "render": function (data) {
                    return `<div class="w-75 btn-group" role="group">
               <a href="/${culture}/admin/fabricByProduct/editunit?unitId=${data}&productId=${productId}" class="btn btn-primary mx-2"><i class="bi bi-pencil-square"></i> </a>
               <a onClick=Delete('/${culture}/admin/fabricByProduct/delete/${data}') class="btn btn-danger mx-2"><i class="bi bi-trash-fill"></i>  </a>
                    </div>`
                },
                "width": "20%"
            }
        ],
        "language": window.SpanishCultureTables(culture),
        responsive: {
            details: {
                type: 'column',
                target: 'tr'
            }
        }
    });
}


document.addEventListener("DOMContentLoaded", () => {
    fetch(`/${culture}/customer/home/GetTranslations`)
        .then(response => response.json())
        .then(data => {
            translations = data;

        });
});
function Delete(url) {

    Swal.fire({
        title: translations.areYouSure,
        text: translations.youWontRevert,
        icon: "warning",
        showCancelButton: true,
        confirmButtonColor: "var(--bs-danger)",
        cancelButtonColor: "var(--bs-warning)",
        confirmButtonText: translations.deleteConfirmation
    }).then((result) => {
        if (result.isConfirmed) {
            $.ajax({
                url: url,
                type: 'POST',
                success: function (data) {
                    dataTable.ajax.reload();
                    toastr.success(data.message);
                }
            })
        }
    })
}