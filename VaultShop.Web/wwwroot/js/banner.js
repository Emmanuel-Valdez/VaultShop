var dataTable;
let translations = {};
$(document).ready(function () {
    loadTranslations().finally(loadDataTable);
});
function loadTranslations() {
    return fetch(`/${culture}/customer/home/GetTranslations`)
        .then(response => response.json())
        .then(data => {
            translations = data;
        })
        .catch(() => {
            translations = {};
        });
}
function loadDataTable() {
    dataTable = $('#tblData').DataTable({
        "ajax": { url: `/${culture}/admin/banner/getall` },
        "columns": [
            {
                data: 'coverImage',
                "render": function (data) {
                    return data
                        ? `<img src="${data}" style="max-height:48px; max-width:96px; object-fit:cover" class="rounded" alt="" />`
                        : '';
                },
                "width": "12%", "orderable": false
            },
            { data: 'title', "width": "24%" },
            { data: 'layout', "width": "12%" },
            {
                data: null,
                "render": function (data) {
                    return data.targetId ? `${data.targetKind} #${data.targetId}` : data.targetKind;
                },
                "width": "16%"
            },
            { data: 'sortOrder', "width": "8%" },
            {
                data: 'isActive',
                "render": function (data) {
                    var yesText = translations.yes || translations.Yes || "Sí";
                    var noText = translations.no || translations.No || "No";
                    return data
                        ? `<span class="badge bg-success">${yesText}</span>`
                        : `<span class="badge bg-secondary">${noText}</span>`;
                },
                "width": "8%"
            },
            {
                data: 'id',
                "render": function (data) {
                    return `<div class="w-75 btn-group" role="group">
               <a href="/${culture}/admin/banner/upsert?id=${data}" class="btn btn-primary mx-2"><i class="bi bi-pencil-square"></i></a>
               <a onClick=Delete('/${culture}/admin/banner/delete/${data}') class="btn btn-danger mx-2"><i class="bi bi-trash-fill"></i></a>
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
                    if (data.success) {
                        dataTable.ajax.reload();
                        toastr.success(data.message);
                    } else {
                        toastr.error(data.message);
                    }
                },
                error: function () {
                    toastr.error(translations.errorOccurred || 'An error occurred');
                }
            })
        }
    })

}
