var dataTable;
let translations = {};

document.addEventListener("DOMContentLoaded", async () => {
    await loadTranslations();
    loadDataTable();
});

async function loadTranslations() {
    const response = await fetch(`/${culture}/customer/home/GetTranslations`);
    translations = await response.json();
}

function loadDataTable() {
    dataTable = $('#tblData').DataTable({
        "ajax": {url: `/${culture}/admin/user/getall` },
            
        "columns": [
        
            { data: 'name', "width": "15%" },
            { data: 'email', "width": "15%" },
            { data: 'phoneNumber', "width": "5%" },
            { data: 'state', "width": "10" },
            { data: 'company.name', "width": "10%" },
            { data: 'role', "width": "10" },
            { data: 'lockoutEnd', "width": "5" },
            {
                data: { id: 'id', lockoutEnd: "lockoutEnd" },
                "render": function (data) {
                    var today = new Date().getTime();
                    var lockout = new Date(data.lockoutEnd).getTime();
                    if (lockout > today) {
                        return `<div class="text-center">
                           <a onclick=LockUnlock('${data.id}')  class="btn btn-danger text-white" style="cursor:pointer; ">
                                <i class="bi bi-lock-fill"></i> 
                            </a>
                            <a href="/${culture}/admin/user/RoleManagment?userId=${data.id}" class="btn btn-warning text-white" style="cursor:pointer ;">
                                <i class="bi bi-pencil-square"></i>
                            </a>
                        </div>`
                    } else {
                        return `<div class="text-center">
                             <a onclick=LockUnlock('${data.id}') class="btn btn-success text-white" style="cursor:pointer;">
                                <i class="bi bi-unlock-fill"></i>
                             </a>
                             <a href="/${culture}/admin/user/RoleManagment?userId=${data.id}" class="btn btn-warning text-white" style="cursor:pointer;">
                                <i class="bi bi-pencil-square"></i> 
                            </a>
                        </div>`
                    }


                },
                "width": "20%"
            }
        ],
        "language": window.SpanishCultureTables(culture),
        layout: { topStart: 'buttons' },
        buttons: {
            buttons: [
                {
                    extend: 'colvis',
                    text: `${translations.columnsVisibility}`,
                }
            ],
        },
        responsive: {
            details: {
                type: 'column',
                target: 'tr'
            }
        },
        "columnDefs": [
            { responsivePriority: 1, targets: 7 },
            { responsivePriority: 2, targets: 0},
            { responsivePriority: 3, targets: 5 },
            { responsivePriority: 4, targets: 3 },
            { target: 6, render: DataTable.render.date() }
        ]

    });
}
function LockUnlock(id) {
    $.ajax({
        type: "POST",
        url: `/${culture}/Admin/User/LockUnlock`,
        data: JSON.stringify(id),
        contentType: "application/json",
        success: function (data) {
            if (data.success) {
                toastr.success(data.message);
                dataTable.ajax.reload();
            }
        }
    });
}