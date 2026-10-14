// Display grouped decimals without converting them to floating point numbers.
// Existing calculation and validation code reads the canonical value property;
// the browser displays the formatted native value.
document.addEventListener('DOMContentLoaded', () => {
    const nativeValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
    const canonical = value => String(value).replace(/[\s\u00a0\u202f]/g, '').replace(',', '.');
    const grouped = value => {
        const raw = canonical(value);
        if (!/^-?\d*(\.\d*)?$/.test(raw)) return String(value);
        const [integer, fraction] = raw.split('.');
        return integer.replace(/\B(?=(\d{3})+(?!\d))/g, ' ') +
            (fraction === undefined ? '' : ',' + fraction);
    };
    const fields = [...document.querySelectorAll('input[type="number"], input[data-money], input[inputmode="decimal"]')];
    for (const field of fields) {
        const wasNumber = field.type === 'number';
        const min = field.getAttribute('min');
        const max = field.getAttribute('max');
        const step = field.getAttribute('step') || (wasNumber ? '1' : 'any');
        if (wasNumber) field.type = 'text';
        field.inputMode = step === '1' ? 'numeric' : 'decimal';
        const validate = () => {
            const raw = canonical(nativeValue.get.call(field));
            let error = '';
            if (raw && !/^-?(?:\d+(?:\.\d*)?|\.\d+)$/.test(raw)) error = 'Введите число, например 9 000,15.';
            else if (raw && min !== null && Number(raw) < Number(min)) error = `Минимальное значение: ${grouped(min)}.`;
            else if (raw && max !== null && Number(raw) > Number(max)) error = `Максимальное значение: ${grouped(max)}.`;
            else if (raw && step === '1' && /\.[0-9]*[1-9]/.test(raw)) error = 'Введите целое число.';
            field.setCustomValidity(error);
        };
        Object.defineProperty(field, 'value', {
            configurable: true,
            get() { return canonical(nativeValue.get.call(this)); },
            set(value) { nativeValue.set.call(this, grouped(value)); validate(); }
        });
        field.value = field.value;
        field.addEventListener('beforeinput', event => {
            if (field.selectionStart !== field.selectionEnd) return;
            const display = nativeValue.get.call(field);
            let caret = field.selectionStart;
            if (event.inputType === 'deleteContentBackward') {
                while (caret > 0 && /\s/.test(display[caret - 1])) caret--;
            } else if (event.inputType === 'deleteContentForward') {
                while (caret < display.length && /\s/.test(display[caret])) caret++;
            }
            if (caret !== field.selectionStart) field.setSelectionRange(caret, caret);
        });
        field.addEventListener('input', () => {
            const display = nativeValue.get.call(field);
            const position = field.selectionStart ?? display.length;
            const significant = display.slice(0, position).replace(/\s/g, '').length;
            field.value = display;
            const formatted = nativeValue.get.call(field);
            let caret = 0, count = 0;
            while (caret < formatted.length && count < significant) {
                if (!/\s/.test(formatted[caret])) count++;
                caret++;
            }
            field.setSelectionRange(caret, caret);
        });
    }
    // Native form serialization does not use the overridden JS getter.
    for (const form of new Set(fields.map(field => field.form).filter(Boolean))) {
        form.addEventListener('formdata', event => {
            for (const field of fields.filter(field => field.form === form)) {
                if (field.name && !field.disabled && event.formData.has(field.name))
                    event.formData.set(field.name, field.value);
            }
        });
        form.addEventListener('reset', () => setTimeout(() => {
            for (const field of fields.filter(field => field.form === form)) field.value = field.value;
        }, 0));
    }
    // Use the user's calendar date when opening a copied operation.
    const form = document.getElementById('operation-form');
    if (form?.dataset.copy === 'true') {
        const today = new Date();
        form.elements['Input.OccurredAt'].value = `${today.getFullYear()}-${String(today.getMonth()+1).padStart(2,'0')}-${String(today.getDate()).padStart(2,'0')}`;
    }
});
