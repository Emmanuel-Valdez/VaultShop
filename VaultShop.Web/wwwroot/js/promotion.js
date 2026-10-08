var dataTable;
let translations = {};
$(document).ready(function () {
    loadDataTable();
});
function loadDataTable() {
    dataTable = $('#tblData').DataTable({
        "ajax": { url: `/${culture}/admin/promotion/getall` },
        "columns": [
            { data: 'name', "width": "20%" },
            { data: 'kind', "width": "12%" },
            { data: 'scope', "width": "10%" },
            { data: 'target', "width": "15%" },
            {
                data: 'validToUtc',
                "render": function (data) {
                    return data ? new Date(data).toLocaleDateString() : '—';
                },
                "width": "13%"
            },
            {
                data: 'isActive',
                "render": function (data) {
                    return data ? '✓' : '—';
                },
                "width": "8%"
            },
            {
                data: 'id',
                "render": function (data) {
                    return `<div class="w-75 btn-group" role="group">
               <a href="/${culture}/admin/promotion/upsert?id=${data}" class="btn btn-primary mx-2"><i class="bi bi-pencil-square"></i></a>
               <a onClick=Delete('/${culture}/admin/promotion/delete/${data}') class="btn btn-danger mx-2"><i class="bi bi-trash-fill"></i></a>
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
                type: 'DELETE',
                success: function (data) {
                    dataTable.ajax.reload();
                    toastr.success(data.message);
                }
            })
        }
    })

}
