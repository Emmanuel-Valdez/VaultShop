// home-banners 4.4: JS-owned autoplay (6000ms via data-bs-interval) only when 2+ slides
// exist and no reduced-motion preference; pause/play toggle. No-op when no carousel.
(function () {
    var root = document.querySelector('[data-home-banner-carousel]');
    if (!root) return;
    var toggle = root.querySelector('[data-banner-pause]');
    var multiple = root.querySelectorAll('.carousel-item').length > 1;
    var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var getCarousel = function () {
        return window.bootstrap && window.bootstrap.Carousel
            ? window.bootstrap.Carousel.getOrCreateInstance(root)
            : null;
    };
    var setPaused = function (paused) {
        var carousel = getCarousel();
        if (carousel) {
            if (paused) carousel.pause(); else carousel.cycle();
        }
        if (!toggle) return;
        toggle.setAttribute('aria-pressed', paused.toString());
        var label = paused ? toggle.getAttribute('data-label-play') : toggle.getAttribute('data-label-pause');
        if (label) toggle.setAttribute('aria-label', label);
        var icon = toggle.querySelector('i');
        if (icon) {
            icon.classList.toggle('bi-pause-fill', !paused);
            icon.classList.toggle('bi-play-fill', paused);
        }
    };
    if (multiple && !reduceMotion) {
        var carousel = getCarousel();
        if (carousel) carousel.cycle();
    }
    if (toggle) {
        toggle.addEventListener('click', function () {
            setPaused(toggle.getAttribute('aria-pressed') !== 'true');
        });
    }
})();
