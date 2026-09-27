$(document).ready(function () {
    const previews = {
        chipFile: document.createElement('div'),
        coverFile: document.createElement('div')
    };

    Object.entries(previews).forEach(([name, container]) => {
        const input = document.querySelector(`input[name="${name}"]`);
        if (!input) return;
        input.parentNode.appendChild(container);
        const hint = document.createElement('div');
        hint.className = 'form-text text-warning';
        hint.setAttribute('role', 'status');
        hint.style.display = 'none';
        input.addEventListener('change', function () {
            container.innerHTML = '';
            hint.style.display = 'none';
            const file = this.files && this.files[0];
            if (!file) return;
            const img = document.createElement('img');
            img.src = URL.createObjectURL(file);
            img.style.cssText = 'max-width:100%;max-height:140px;object-fit:cover;border-radius:.375rem;margin-top:.5rem';
            img.alt = '';
            container.appendChild(img);
            if (name === 'coverFile') {
                container.appendChild(hint);
                img.onload = function () {
                    const ratio = img.naturalWidth / img.naturalHeight;
                    const expected = 16 / 9;
                    if (Math.abs(ratio - expected) / expected > 0.05 && input.dataset.cropHint) {
                        hint.textContent = input.dataset.cropHint;
                        hint.style.display = '';
                    }
                };
            }
        });
    });
});