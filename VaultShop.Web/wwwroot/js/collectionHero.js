// ponytail: compositor-only parallax — passive+rAF, prefers-reduced-motion guard, no layout thrash (transform/opacity only)
(function () {
    var hero = document.querySelector('[data-collection-hero]');
    if (!hero) return;
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    var getH = function (nm) {
        return parseFloat(getComputedStyle(hero).getPropertyValue(nm)) || 0;
    };
    var h0 = getH('--hero-h');
    var range = Math.max(1, h0);
    var ticking = false;
    var onScroll = function () {
        if (ticking) return;
        ticking = true;
        requestAnimationFrame(function () {
            var y = window.scrollY || document.documentElement.scrollTop || 0;
            var progress = Math.min(1, Math.max(0, y / range));
            var ty = progress * range * 0.3;
            hero.style.setProperty('--img-ty', ty + 'px');
            hero.style.setProperty('--content-opacity', (1 - Math.pow(progress, 1.2)).toString());
            ticking = false;
        });
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', function () {
        h0 = getH('--hero-h'); range = Math.max(1, h0);
        onScroll();
    }, { passive: true });
    onScroll();
})();
