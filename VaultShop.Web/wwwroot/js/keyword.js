var dataTable;
let translations = {};
$(document).ready(function () {
    loadDataTable();
});
function loadDataTable() {
    dataTable = $('#tblData').DataTable({
        "ajax": { url: `/${culture}/admin/keyword/getall` },
        "columns": [
            { data: 'name', "width": "25%" },
            {
                data: 'chipImage',
                "width": "10%",
                "render": function (data) {
                    return data
                        ? `<img src="${data}" alt="" style="width:48px;height:48px;object-fit:cover;border-radius:50%" />`
                        : `<span class="badge text-bg-secondary">-</span>`;
                }
            },
            { data: 'slug', "width": "25%" },
            { data: 'productCount', "width": "10%" },
            {
                data: 'id',
                "render": function (data) {
                    return `<div class="w-75 btn-group" role="group">
               <a href="/${culture}/admin/keyword/upsert?id=${data}" class="btn btn-primary mx-2"><i class="bi bi-pencil-square"></i></a>
               <a onClick=Delete('/${culture}/admin/keyword/delete/${data}') class="btn btn-danger mx-2"><i class="bi bi-trash-fill"></i></a>
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
                headers: { 'RequestVerificationToken': $('meta[name="request-verification-token"]').attr('content') },
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