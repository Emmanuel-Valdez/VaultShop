(() => {
    const menu = document.getElementById('sideNavbarTop');
    if (!menu) {
        return;
    }

    const triggers = document.querySelectorAll('[data-bs-target="#sideNavbarTop"]');
    const setExpanded = (expanded) => {
        triggers.forEach((trigger) => trigger.setAttribute('aria-expanded', expanded ? 'true' : 'false'));
    };

    let focusMenuSearch = false;
    triggers.forEach((trigger) => {
        trigger.addEventListener('click', () => {
            focusMenuSearch = trigger.hasAttribute('data-storefront-search-open');
        });
    });

    menu.addEventListener('show.bs.offcanvas', () => setExpanded(true));
    menu.addEventListener('hide.bs.offcanvas', () => setExpanded(false));
    menu.addEventListener('shown.bs.offcanvas', () => {
        if (!focusMenuSearch) {
            return;
        }

        focusMenuSearch = false;
        menu.querySelector('#storefrontMenuSearch')?.focus();
    });
})();
