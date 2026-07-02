(function () {
    const editorSelector = 'textarea.js-rich-text';

    const toolbarOptions = [
        [{ header: [2, 3, false] }],
        ['bold', 'italic', 'underline', 'strike'],
        [{ list: 'ordered' }, { list: 'bullet' }],
        [{ indent: '-1' }, { indent: '+1' }],
        [{ align: [] }],
        ['link'],
        ['clean']
    ];

    function initializeEditor(textarea) {
        const editor = document.createElement('div');
        editor.className = 'rich-text-editor shadow';
        editor.innerHTML = textarea.value || '';

        textarea.classList.add('d-none');
        textarea.insertAdjacentElement('afterend', editor);

        const quill = new Quill(editor, {
            modules: {
                toolbar: toolbarOptions
            },
            placeholder: textarea.getAttribute('placeholder') || '',
            theme: 'snow'
        });

        function removeImages() {
            let index = 0;

            quill.getContents().ops.forEach(function (op) {
                if (typeof op.insert === 'string') {
                    index += op.insert.length;
                    return;
                }

                if (op.insert && op.insert.image) {
                    quill.deleteText(index, 1, 'silent');
                    return;
                }

                index += 1;
            });
        }

        quill.on('text-change', removeImages);

        const form = textarea.closest('form');
        if (form) {
            form.addEventListener('submit', function () {
                removeImages();
                const html = quill.root.innerHTML.trim();
                textarea.value = html === '<p><br></p>' ? '' : html;
            });
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof Quill === 'undefined') {
            return;
        }

        document.querySelectorAll(editorSelector).forEach(initializeEditor);
    });
})();
