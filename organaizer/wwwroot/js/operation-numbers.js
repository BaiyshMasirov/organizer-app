// Display grouped decimals without converting them to floating point numbers.
// Existing calculation and validation code reads the canonical value property;
// the browser displays the formatted native value.
document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('operation-form');
    if (!form) return;
    const nativeValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
    const canonical = value => String(value).replace(/[\s\u00a0\u202f]/g, '').replace(',', '.');
    const grouped = value => {
        const raw = canonical(value);
        if (!/^-?\d*(\.\d*)?$/.test(raw)) return String(value);
        const [integer, fraction] = raw.split('.');
        return integer.replace(/\B(?=(\d{3})+(?!\d))/g, ' ') +
            (fraction === undefined ? '' : ',' + fraction);
    };
    const fields = [...form.querySelectorAll('input[data-money]')];
    for (const field of fields) {
        Object.defineProperty(field, 'value', {
            configurable: true,
            get() { return canonical(nativeValue.get.call(this)); },
            set(value) { nativeValue.set.call(this, grouped(value)); }
        });
        field.value = field.value;
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
    form.addEventListener('formdata', event => {
        for (const field of fields) event.formData.set(field.name, field.value);
    });
    // Use the user's calendar date when opening a copied operation.
    if (form.dataset.copy === 'true') {
        const today = new Date();
        form.elements['Input.OccurredAt'].value = `${today.getFullYear()}-${String(today.getMonth()+1).padStart(2,'0')}-${String(today.getDate()).padStart(2,'0')}`;
    }
});
