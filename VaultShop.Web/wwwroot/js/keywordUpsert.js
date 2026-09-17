$(document).ready(function () {
    const previews = {
        chipFile: document.createElement('div'),
        coverFile: document.createElement('div')
    };

    Object.entries(previews).forEach(([name, container]) => {
        const input = document.querySelector(`input[name="${name}"]`);
        if (!input) return;
        input.parentNode.appendChild(container);
        input.addEventListener('change', function () {
            container.innerHTML = '';
            const file = this.files && this.files[0];
            if (!file) return;
            const img = document.createElement('img');
            img.src = URL.createObjectURL(file);
            img.style.cssText = 'max-width:100%;max-height:140px;object-fit:cover;border-radius:.375rem;margin-top:.5rem';
            img.alt = '';
            container.appendChild(img);
        });
    });
});