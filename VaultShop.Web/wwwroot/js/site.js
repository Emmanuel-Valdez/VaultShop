(() => {
    const menu = document.getElementById('sideNavbarTop');
    if (menu) {
        const triggers = document.querySelectorAll('[data-bs-target="#sideNavbarTop"]');
        const setExpanded = (expanded) => {
            triggers.forEach((trigger) => trigger.setAttribute('aria-expanded', expanded ? 'true' : 'false'));
        };

        menu.addEventListener('show.bs.offcanvas', () => setExpanded(true));
        menu.addEventListener('hide.bs.offcanvas', () => setExpanded(false));
    }

    const search = document.getElementById('storefrontSearch');
    if (search) {
        search.addEventListener('shown.bs.modal', () => {
            search.querySelector('#storefrontSearchInput')?.focus();
        });
    }
})();
