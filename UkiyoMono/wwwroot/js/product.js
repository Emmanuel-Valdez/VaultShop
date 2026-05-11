var dataTable;
let translations = {};
document.addEventListener("DOMContentLoaded", () => {
    fetch(`/${culture}/customer/home/GetTranslations`)
        .then(response => response.json())
        .then(data => {
            translations = data;

        });
});
$(document).ready(function () {
    loadDataTable();
});
function loadDataTable() {
    dataTable = $('#tblData').DataTable({
        "ajax": { url: `/${culture}/admin/product/getall` },
        "columns": [
            { data: 'id' },
            { data: 'name' },
            { data: 'listPrice',  render: window.SpanishNumberTables(culture) },
            { data: 'finalWholesalePrice', render: window.SpanishNumberTables(culture) },
            { data: 'finalRetailPrice', render: window.SpanishNumberTables(culture) },
            { data: 'category.name' },
            {

                data: { id: 'id', isAvailableInStore: 'isAvailableInStore' },
                "render": function (data) {
                    var isAvailableInStore = data.isAvailableInStore;
                    if (!isAvailableInStore) {
                        return `
                           <a onclick=UpdateProductAvailability('${data.id}')  class="btn btn-danger text-white" style="cursor:pointer; width:100px;">
                                <i class="bi bi-lock-fill"></i> ${translations.no}
                            </a> `
                    } else {
                        return `
                         <a onclick=UpdateProductAvailability('${data.id}') class="btn btn-success text-white" style="cursor:pointer; width:100px;">
                            <i class="bi bi-unlock-fill"></i> ${translations.yes}
                         </a> `
                    }
                }, "width": "5%"

            },

            {
                data: 'id',
                "render": function (data) {
                    return `<div class="w-75 btn-group" role="group">
               <a href="/${culture}/admin/product/upsert?id=${data}" class="btn btn-primary mx-2"><i class="bi bi-pencil-square"></i> </a>
               <a onClick=Delete('/${culture}/admin/product/delete/${data}') class="btn btn-danger mx-2"><i class="bi bi-trash-fill"></i>  </a>
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
        },
        "columnDefs": [
            { responsivePriority: 1, targets: 7},
            { responsivePriority: 2, targets: 6},
            { responsivePriority: 3, targets: 1 },
            { responsivePriority: 4, targets: 4 }
        ],
        layout: {
            topStart: null,
            bottomStart: null,
            bottomEnd: null
        }

    })
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
                    dataTable.ajax.reload();
                    toastr.success(data.message);
                }
            })
        }
    })
}


function UpdateProductAvailability(isAvailable) {
    $.ajax({
        type: "POST",
        url: `/${culture}/Admin/Product/UpdateProductAvailability`,
        data: JSON.stringify(isAvailable),
        contentType: "application/json",
        success: function (data) {
            if (data.success) {
                toastr.success(data.message);
                dataTable.ajax.reload();
            }
        }
    });
}
