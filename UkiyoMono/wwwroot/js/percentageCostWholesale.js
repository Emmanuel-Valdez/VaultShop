var dataTable;
let translations = {};

$(document).ready(function () {
    loadDataTable();
});

function loadDataTable() {
    const table = $('#tblData');
    const editLabel = table.data('edit-label') || 'Edit';
    const deleteLabel = table.data('delete-label') || 'Delete';

    dataTable = table.DataTable({
        "ajax": { url: `/${culture}/admin/percentageCostWholesale/getall` },

        "columns": [
            { data: 'name', "width": "15%" },
            { data: 'percentage', "width": "15%" },
            {
                data: 'id',
                "render": function (data) {
                    const editLabelText = escapeHtml(editLabel);
                    const deleteLabelText = escapeHtml(deleteLabel);

                    return `<div class="w-75 btn-group" role="group">
                <a href="/${culture}/admin/percentageCostWholesale/upsert?id=${data}" class="btn btn-primary mx-2" title="${editLabelText}" aria-label="${editLabelText}"><i class="bi bi-pencil-square" aria-hidden="true"></i></a>
                <button type="button" onclick="Delete('/${culture}/admin/percentageCostWholesale/delete/${data}')" class="btn btn-danger mx-2" title="${deleteLabelText}" aria-label="${deleteLabelText}"><i class="bi bi-trash-fill" aria-hidden="true"></i></button>
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
        title: translations.areYouSure || 'Are you sure?',
        text: translations.youWontRevert || 'You will not be able to revert this.',
        icon: "warning",
        showCancelButton: true,
        confirmButtonColor: "var(--bs-danger)",
        cancelButtonColor: "var(--bs-warning)",
        confirmButtonText: translations.deleteConfirmation || 'Delete'
    }).then((result) => {
        if (result.isConfirmed) {
            $.ajax({
                url: url,
                type: 'POST',
                success: function (data) {
                    if (!data.success) {
                        toastr.error(data.message);
                        return;
                    }

                    dataTable.ajax.reload();
                    $('#totalWholesalePercentageCost').text(`${data.totalPercentage} %`);
                    toastr.success(data.message);
                }
            })
        }
    })
}

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}
