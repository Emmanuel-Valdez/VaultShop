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
        "ajax": { url: `/${culture}/admin/product/getall` },
        "columns": [
            {
                data: 'imageUrl',
                "width": "8%",
                "render": function (data) {
                    return data
                        ? `<img src="${data}" alt="" style="width:48px;height:48px;object-fit:cover;border-radius:4px" />`
                        : `<span class="badge text-bg-secondary">-</span>`;
                }
            },
            { data: 'id' },
            { data: 'name' },
            { data: 'listPrice',  render: window.SpanishNumberTables(culture) },
            { data: 'finalWholesalePrice', render: window.SpanishNumberTables(culture) },
            { data: 'finalRetailPrice', render: window.SpanishNumberTables(culture) },
            { data: 'category.name' },
            { data: 'stockQuantity' },
            {
                data: 'id',
                "render": function (data, type, row) {
                    var isAvailableInStore = row.isAvailableInStore;
                    var yesText = translations.yes || translations.Yes || "Yes";
                    var noText = translations.no || translations.No || "No";
                    if (!isAvailableInStore) {
                        return `
                            <a onclick=UpdateProductAvailability('${data}')  class="btn btn-danger text-white" style="cursor:pointer; width:100px;">
                                <i class="bi bi-lock-fill"></i> ${noText}
                            </a> `
                    } else {
                        return `
                          <a onclick=UpdateProductAvailability('${data}') class="btn btn-success text-white" style="cursor:pointer; width:100px;">
                            <i class="bi bi-unlock-fill"></i> ${yesText}
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
        "order": [[1, "asc"]],
        "pageLength": 50,
        "language": window.SpanishCultureTables(culture),
        responsive: {
            details: {
                type: 'column',
                target: 'tr'
            }
        },
        "columnDefs": [
            { responsivePriority: 1, targets: 9 },
            { responsivePriority: 2, targets: 2 },
            { responsivePriority: 3, targets: 0 },
            { responsivePriority: 4, targets: 5 },
            { responsivePriority: 5, targets: 7 },
            { responsivePriority: 10000, targets: 1 },
            { responsivePriority: 11, targets: 3 },
            { responsivePriority: 12, targets: 4 },
            { responsivePriority: 13, targets: 6 },
            { responsivePriority: 10000, targets: 8 }
        ]

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