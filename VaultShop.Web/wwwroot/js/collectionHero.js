// ponytail: vanilla scroll collapse — passive+rAF, prefers-reduced-motion guard
(function () {
    var hero = document.querySelector('[data-collection-hero]');
    if (!hero) return;
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    var getH = function (nm) {
        return parseFloat(getComputedStyle(hero).getPropertyValue(nm)) || 0;
    };
    var h0 = getH('--hero-h');
    var hCollapsed = getH('--hero-h-collapsed');
    var range = Math.max(1, h0 - hCollapsed);
    var ticking = false;
    var onScroll = function () {
        if (ticking) return;
        ticking = true;
        requestAnimationFrame(function () {
            var y = window.scrollY || document.documentElement.scrollTop || 0;
            var progress = Math.min(1, Math.max(0, y / range));
            var cur = h0 - progress * range;
            var ty = progress * range * 0.5;
            hero.style.setProperty('--progress', progress.toString());
            hero.style.setProperty('--hero-h-current', cur + 'px');
            hero.style.setProperty('--img-ty', ty + 'px');
            hero.style.setProperty('--content-opacity', (1 - Math.pow(progress, 1.2)).toString());
            ticking = false;
        });
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', function () {
        h0 = getH('--hero-h'); hCollapsed = getH('--hero-h-collapsed'); range = Math.max(1, h0 - hCollapsed);
        onScroll();
    }, { passive: true });
    onScroll();
})();
