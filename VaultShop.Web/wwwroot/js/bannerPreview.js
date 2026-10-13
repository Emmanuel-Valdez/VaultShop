// banner-preview-safe-zone 3.x: admin live preview + safe-zone editor. Plain DOM + Pointer Events.
// Reads the Upsert form state, paints desktop/mobile preview frames, and lets the admin
// draw/move/resize one normalized rectangle on the desktop frame. Writes 3-decimal
// [0,1] values to the Banner.SafeZone* hidden inputs. The zone is desktop-only: the
// mobile frame always uses the per-layout default geometry.
(function () {
    var root = document.querySelector('[data-banner-preview]');
    if (!root) return;

    var colors = ['brandlight', 'branddark', 'white', 'black'];
    var clamp01 = function (v) { return Math.min(1, Math.max(0, v)); };
    var round3 = function (v) { return Math.round(clamp01(v) * 1000) / 1000; };
    // ponytail: single-decimal percents keep 1-(l+w) float noise (9.999...%) out of the CSS.
    var pct = function (v) { return (Math.round(round3(v) * 1000) / 10) + '%'; };

    // Form state
    var text = {
        title: document.getElementById('Banner_Title'),
        hook: document.getElementById('Banner_Hook'),
        body: document.getElementById('Banner_Body'),
        cta: document.getElementById('Banner_CallToAction')
    };
    var layoutSel = document.getElementById('Banner_Layout');
    var colorSel = document.getElementById('Banner_TextColor');
    var coverInput = document.querySelector('input[name="coverFile"]');
    var mobileInput = document.querySelector('input[name="mobileFile"]');
    var zoneInputs = {
        l: document.getElementById('Banner_SafeZoneLeft'),
        t: document.getElementById('Banner_SafeZoneTop'),
        w: document.getElementById('Banner_SafeZoneWidth'),
        h: document.getElementById('Banner_SafeZoneHeight')
    };

    // Preview nodes
    var desktop = root.querySelector('[data-preview-frame="desktop"]');
    var mobile = root.querySelector('[data-preview-frame="mobile"]');
    var desktopImg = root.querySelector('[data-preview-img]');
    var mobileImg = root.querySelector('[data-preview-mobile-img]');
    var desktopEmpty = root.querySelector('[data-preview-empty]');
    var mobileEmpty = root.querySelector('[data-preview-mobile-empty]');
    var editor = root.querySelector('[data-safezone-editor]');
    var rectEl = root.querySelector('[data-safezone-rect]');
    var mirrorEl = root.querySelector('[data-safezone-mirror]');

    var coverObjUrl = null;
    var mobileObjUrl = null;
    var rect = readRect(); // { l, t, w, h } in [0,1] or null

    function readRect() {
        var vals = [zoneInputs.l, zoneInputs.t, zoneInputs.w, zoneInputs.h].map(function (el) {
            return el && el.value !== '' ? Number(el.value) : NaN;
        });
        if (vals.some(isNaN)) return null;
        return { l: vals[0], t: vals[1], w: vals[2], h: vals[3] };
    }

    function writeRect() {
        if (!rect) {
            zoneInputs.l.value = zoneInputs.t.value = zoneInputs.w.value = zoneInputs.h.value = '';
            return;
        }
        var l = round3(rect.l), t = round3(rect.t);
        // Independent 3-decimal rounding of l and w can make l + w exceed 1 (0.124 + 0.877 = 1.001)
        // and trip the controller bounds check: clamp w/h after rounding.
        var w = Math.min(round3(rect.w), round3(1 - l));
        var h = Math.min(round3(rect.h), round3(1 - t));
        zoneInputs.l.value = l.toFixed(3);
        zoneInputs.t.value = t.toFixed(3);
        zoneInputs.w.value = w.toFixed(3);
        zoneInputs.h.value = h.toFixed(3);
    }

    function paintRect() {
        if (mirrorEl) mirrorEl.hidden = true; // safe zone is desktop-only
        if (!rectEl || !desktop) return;
        var show = !!rect;
        rectEl.hidden = !show;
        if (!show) {
            ['--banner-safe-top', '--banner-safe-right', '--banner-safe-bottom', '--banner-safe-left']
                .forEach(function (v) { desktop.style.removeProperty(v); });
            return;
        }
        rectEl.style.left = pct(rect.l);
        rectEl.style.top = pct(rect.t);
        rectEl.style.width = pct(rect.w);
        rectEl.style.height = pct(rect.h);
        // Same vars the storefront uses, so the desktop preview confines exactly like the home page.
        desktop.style.setProperty('--banner-safe-top', pct(rect.t));
        desktop.style.setProperty('--banner-safe-right', pct(1 - (rect.l + rect.w)));
        desktop.style.setProperty('--banner-safe-bottom', pct(1 - (rect.t + rect.h)));
        desktop.style.setProperty('--banner-safe-left', pct(rect.l));
    }

    function setText(base, value) {
        var d = root.querySelector('[data-preview-' + base + ']');
        var m = root.querySelector('[data-preview-mobile-' + base + ']');
        [d, m].forEach(function (el) {
            if (!el) return;
            el.textContent = value;
            el.hidden = !value;
        });
    }

    function setImage(img, empty, url) {
        if (!img) return;
        if (url) {
            if (img.getAttribute('src') !== url) img.setAttribute('src', url);
            img.hidden = false;
            if (empty) empty.hidden = true;
        } else {
            img.removeAttribute('src');
            img.hidden = true;
            if (empty) empty.hidden = false;
        }
    }

    function render() {
        var imageLed = layoutSel && layoutSel.value === '1';
        var color = (colorSel && colors[Number(colorSel.value)]) || 'white';
        var layoutClass = imageLed ? 'banner--layout-imageled' : 'banner--layout-crossed';
        [desktop, mobile].forEach(function (frame) {
            if (!frame) return;
            frame.classList.remove('banner--layout-imageled', 'banner--layout-crossed',
                'banner--color-brandlight', 'banner--color-branddark', 'banner--color-white', 'banner--color-black');
            frame.classList.add(layoutClass, 'banner--color-' + color);
        });
        if (mobile) {
            mobile.classList.remove('banner-preview__mobile--crossed', 'banner-preview__mobile--imageled');
            mobile.classList.add(imageLed ? 'banner-preview__mobile--imageled' : 'banner-preview__mobile--crossed');
        }
        setText('title', text.title ? text.title.value.trim() : '');
        setText('hook', text.hook ? text.hook.value.trim() : '');
        setText('body', text.body ? text.body.value.trim() : '');
        setText('cta', text.cta ? text.cta.value.trim() : '');
        var cover = coverObjUrl || root.getAttribute('data-cover-url') || '';
        var storedMobile = root.getAttribute('data-mobile-url') || '';
        // Art-direction parity with the storefront: the mobile asset is used only by ImageLed.
        var mobileUrl = imageLed ? (mobileObjUrl || storedMobile || cover) : cover;
        setImage(desktopImg, desktopEmpty, cover);
        setImage(mobileImg, mobileEmpty, mobileUrl);
        paintRect();
    }

    function trackFile(input, kind) {
        if (!input) return;
        input.addEventListener('change', function () {
            var file = input.files && input.files[0] ? input.files[0] : null;
            var url = file ? URL.createObjectURL(file) : null;
            if (kind === 'cover') {
                if (coverObjUrl) URL.revokeObjectURL(coverObjUrl);
                coverObjUrl = url;
            } else {
                if (mobileObjUrl) URL.revokeObjectURL(mobileObjUrl);
                mobileObjUrl = url;
            }
            render();
        });
    }

    // Safe-zone editor: draw on empty press, move when starting inside the rect, SE handle resizes.
    var drag = null; // { mode: 'draw'|'move'|'resize', ax, ay, dx, dy }
    function pos(e) {
        var r = editor.getBoundingClientRect();
        return { x: clamp01((e.clientX - r.left) / r.width), y: clamp01((e.clientY - r.top) / r.height) };
    }
    function inside(p) {
        return rect && p.x >= rect.l && p.x <= rect.l + rect.w && p.y >= rect.t && p.y <= rect.t + rect.h;
    }
    if (editor) {
        editor.addEventListener('pointerdown', function (e) {
            e.preventDefault();
            editor.setPointerCapture(e.pointerId);
            var p = pos(e);
            if (e.target && e.target.closest && e.target.closest('[data-safezone-handle]') && rect) {
                drag = { mode: 'resize' };
            } else if (inside(p)) {
                drag = { mode: 'move', dx: p.x - rect.l, dy: p.y - rect.t };
            } else {
                rect = { l: p.x, t: p.y, w: 0, h: 0 };
                drag = { mode: 'draw', ax: p.x, ay: p.y };
            }
            paintRect();
        });
        editor.addEventListener('pointermove', function (e) {
            if (!drag || !rect) return;
            var p = pos(e);
            if (drag.mode === 'draw') {
                rect.l = Math.min(drag.ax, p.x);
                rect.t = Math.min(drag.ay, p.y);
                rect.w = Math.abs(p.x - drag.ax);
                rect.h = Math.abs(p.y - drag.ay);
            } else if (drag.mode === 'move') {
                rect.l = clamp01(p.x - drag.dx);
                rect.t = clamp01(p.y - drag.dy);
                rect.l = Math.min(rect.l, 1 - rect.w);
                rect.t = Math.min(rect.t, 1 - rect.h);
            } else {
                rect.w = Math.max(0.01, p.x - rect.l);
                rect.h = Math.max(0.01, p.y - rect.t);
                rect.w = Math.min(rect.w, 1 - rect.l);
                rect.h = Math.min(rect.h, 1 - rect.t);
            }
            paintRect();
        });
        var end = function () {
            if (!drag) return;
            drag = null;
            if (rect && (rect.w < 0.01 || rect.h < 0.01)) rect = null; // click without drag clears nothing into a sliver
            writeRect();
            paintRect();
        };
        editor.addEventListener('pointerup', end);
        editor.addEventListener('pointercancel', end);
    }

    // Keyboard: arrows move, shift+arrows resize from the SE corner.
    if (rectEl) {
        rectEl.addEventListener('keydown', function (e) {
            if (!rect) return;
            var step = 0.01, handled = true;
            var resize = e.shiftKey;
            if (e.key === 'ArrowLeft') {
                if (resize) rect.w = Math.max(0.01, rect.w - step); else rect.l = Math.max(0, rect.l - step);
            } else if (e.key === 'ArrowRight') {
                if (resize) rect.w = Math.min(1 - rect.l, rect.w + step); else rect.l = Math.min(1 - rect.w, rect.l + step);
            } else if (e.key === 'ArrowUp') {
                if (resize) rect.h = Math.max(0.01, rect.h - step); else rect.t = Math.max(0, rect.t - step);
            } else if (e.key === 'ArrowDown') {
                if (resize) rect.h = Math.min(1 - rect.t, rect.h + step); else rect.t = Math.min(1 - rect.h, rect.t + step);
            } else {
                handled = false;
            }
            if (handled) {
                e.preventDefault();
                writeRect();
                paintRect();
            }
        });
    }

    // "No safe zone" reset: clears the inputs (all-or-nothing postback clears the stored rect).
    var reset = root.querySelector('[data-safezone-reset]');
    if (reset) {
        reset.addEventListener('click', function () {
            rect = null;
            writeRect();
            paintRect();
        });
    }

    // Live re-render from form state, no reload.
    Object.values(text).concat([layoutSel, colorSel]).forEach(function (el) {
        if (!el) return;
        el.addEventListener('input', render);
        el.addEventListener('change', render);
    });
    trackFile(coverInput, 'cover');
    trackFile(mobileInput, 'mobile');

    render();
})();
