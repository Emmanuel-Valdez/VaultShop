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
        "ajax": { url: `/${culture}/admin/garmenthardwareByProduct/getallunitgarmenthardwares?productId=${productId}` },
        "columns": [
            { data: 'garmentHardware.name', "width": "30%" },
            { data: 'garmentHardware.unitPrice', "width": "15%", render: window.SpanishNumberTables(culture) },
            { data: 'quantity', "width": "15%" },
            { data: 'unitTotal', "width": "20%", render: window.SpanishNumberTables(culture) },
            {
                data: 'id',
                "render": function (data) {
                    return `<div class="w-75 btn-group" role="group">
               <a href="/${culture}/admin/garmenthardwareByProduct/editunit?unitId=${data}&productId=${productId}" class="btn btn-primary mx-2"><i class="bi bi-pencil-square"></i> </a>
               <a onClick=Delete('/${culture}/admin/garmenthardwareByProduct/delete/${data}') class="btn btn-danger mx-2"><i class="bi bi-trash-fill"></i>  </a>
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
        title: translations.areYouSure || translations.AreYouSure || "Are you sure?",
        text: translations.youWontRevert || translations.YouWontRevert || "You won't be able to revert this.",
        icon: "warning",
        showCancelButton: true,
        confirmButtonColor: "var(--bs-danger)",
        cancelButtonColor: "var(--bs-warning)",
        confirmButtonText: translations.deleteConfirmation || translations.DeleteConfirmation || "Yes, delete it!"
    }).then((result) => {
        if (result.isConfirmed) {
            $.ajax({
                url: url,
                type: 'DELETE',
                headers: {
                    RequestVerificationToken: $('meta[name="request-verification-token"]').attr('content')
                },
                success: function (data) {
                    if (data.success) {
                        toastr.success(data.message);
                        setTimeout(() => window.location.reload(), 500);
                    } else {
                        toastr.error(data.message);
                    }
                },
                error: function () {
                    toastr.error(translations.errorWhileDeleting || translations.ErrorWhileDeleting || "Error while deleting.");
                }
            })
        }
    })
}
