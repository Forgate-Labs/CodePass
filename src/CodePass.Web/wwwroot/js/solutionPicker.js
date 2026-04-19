window.codePass = window.codePass || {};
window.codePass.solutionPicker = window.codePass.solutionPicker || {
    async pickSolution() {
        if (window.showOpenFilePicker) {
            try {
                const [handle] = await window.showOpenFilePicker({
                    multiple: false,
                    excludeAcceptAllOption: true,
                    types: [{
                        description: '.NET solutions',
                        accept: {
                            'text/plain': ['.sln']
                        }
                    }]
                });

                if (!handle) {
                    return null;
                }

                const file = await handle.getFile();
                return {
                    fileName: file?.name ?? '',
                    suggestedPath: file?.name ?? ''
                };
            }
            catch (error) {
                if (error?.name === 'AbortError') {
                    return null;
                }
            }
        }

        return await new Promise((resolve) => {
            const input = document.createElement('input');
            input.type = 'file';
            input.accept = '.sln';
            input.style.display = 'none';

            input.addEventListener('change', () => {
                const file = input.files?.[0];
                const suggestedPath = input.value || file?.name || '';
                resolve(file ? { fileName: file.name, suggestedPath } : null);
                input.remove();
            }, { once: true });

            input.addEventListener('cancel', () => {
                resolve(null);
                input.remove();
            }, { once: true });

            document.body.appendChild(input);
            input.click();
        });
    }
};
