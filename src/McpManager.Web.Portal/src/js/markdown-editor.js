import Editor from '@toast-ui/editor';
import '@toast-ui/editor/dist/i18n/pt-br';



// Initialize editors when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    // Find all textareas with markdown-editor class
    const textareas = document.getElementsByClassName('markdown-editor');

    // Convert each textarea to a TUI editor
    Array.from(textareas).forEach(textarea => {
        // Create a container div for the editor
        const container = document.createElement('div');
        container.className = 'editor-container';
        textarea.parentNode.insertBefore(container, textarea);


        // Initialize TUI Editor
        const editor = new Editor({
            el: container,
            height: '500px',
            initialValue: textarea.value,
            initialEditType: 'wysiwyg',
            previewStyle: 'vertical',
            hideModeSwitch: false,
            language: 'pt-BR',
            toolbarItems: [
                ['heading', 'bold', 'italic', 'strike'],
                ['hr', 'quote'],
                ['ul', 'ol'/*, 'task'*/],
                ['table'/*, 'image'*/, 'link'],
                // ['code', 'codeblock']
            ],
            autofocus: false
        });

        textarea.editor = editor;

        // Hide original textarea
        textarea.style.display = 'none';

        // Update textarea value when editor content changes
        editor.on('change', () => {
            textarea.value = editor.getMarkdown();
        });

        // Add form submit handler to ensure textarea is updated
        const form = textarea.closest('form');
        if (form) {
            form.addEventListener('submit', () => {
                textarea.value = editor.getMarkdown();
            });
        }
    });
});
