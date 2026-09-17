// ponytail: vanilla scroll-row nav buttons — scrollBy 0.8*clientWidth, ResizeObserver, data-at-start/end for mask fade
(function () {
    var rows = document.querySelectorAll('.scroll-row');
    if (!rows.length) return;
    rows.forEach(function (row) {
        // wrap once
        if (row.parentElement && row.parentElement.classList.contains('scroll-row-wrap')) return;
        var wrap = document.createElement('div');
        wrap.className = 'scroll-row-wrap';
        row.parentNode.insertBefore(wrap, row);
        wrap.appendChild(row);
        var prev = document.createElement('button');
        prev.className = 'scroll-row-btn scroll-row-btn--prev';
        prev.setAttribute('aria-label', 'Scroll left');
        prev.innerHTML = '&#8249;';
        var next = document.createElement('button');
        next.className = 'scroll-row-btn scroll-row-btn--next';
        next.setAttribute('aria-label', 'Scroll right');
        next.innerHTML = '&#8250;';
        wrap.appendChild(prev);
        wrap.appendChild(next);

        var update = function () {
            var atStart = row.scrollLeft <= 1;
            var atEnd = row.scrollLeft + row.clientWidth >= row.scrollWidth - 1;
            row.setAttribute('data-at-start', atStart.toString());
            row.setAttribute('data-at-end', atEnd.toString());
            var hasOverflow = row.scrollWidth > row.clientWidth + 1;
            prev.classList.toggle('is-visible', hasOverflow && !atStart);
            next.classList.toggle('is-visible', hasOverflow && !atEnd);
        };
        var scrollBy = function (dir) {
            row.scrollBy({ left: dir * row.clientWidth * 0.8, behavior: 'smooth' });
        };
        prev.addEventListener('click', function () { scrollBy(-1); });
        next.addEventListener('click', function () { scrollBy(1); });
        row.addEventListener('scroll', update, { passive: true });
        if (window.ResizeObserver) {
            new ResizeObserver(update).observe(row);
            new ResizeObserver(update).observe(wrap);
        } else {
            window.addEventListener('resize', update, { passive: true });
        }
        update();
    });
})();
