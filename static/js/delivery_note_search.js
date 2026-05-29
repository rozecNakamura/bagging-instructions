/**
 * 納品書：出荷日で検索・結果表示・選択
 */
import { searchDeliveryNote } from './api.js';

let deliveryNoteSearchResults = [];

document.getElementById('deliveryNoteSearchBtn').addEventListener('click', async () => {
    const eatingDate = document.getElementById('deliveryNoteEatingDate').value;

    if (!eatingDate) {
        alert('出荷日を入力してください');
        return;
    }

    const customerType = document.querySelector('input[name="deliveryNoteCustomerType"]:checked')?.value || 'catering';
    const deliveryRoute = document.getElementById('deliveryNoteDeliveryRoute')?.value || '';

    try {
        const response = await searchDeliveryNote(eatingDate, customerType, deliveryRoute);
        deliveryNoteSearchResults = response.items || [];
        populateLocationFilter(deliveryNoteSearchResults);
        displayDeliveryNoteResults(deliveryNoteSearchResults);
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

document.getElementById('deliveryNoteLocationFilter').addEventListener('change', () => {
    const filterCode = document.getElementById('deliveryNoteLocationFilter').value;
    const filtered = filterCode
        ? deliveryNoteSearchResults.filter(item => (item.location_code || '') === filterCode)
        : deliveryNoteSearchResults;
    renderDeliveryNoteRows(filtered);
});

function populateLocationFilter(items) {
    const sel = document.getElementById('deliveryNoteLocationFilter');
    while (sel.options.length > 1) sel.remove(1);

    const seen = new Set();
    items.forEach(item => {
        const code = item.location_code || '';
        if (code && !seen.has(code)) {
            seen.add(code);
            const opt = document.createElement('option');
            opt.value = code;
            opt.textContent = escapeHtml(item.location_name || code);
            sel.appendChild(opt);
        }
    });

    sel.value = '';
    sel.disabled = items.length === 0;
}

function displayDeliveryNoteResults(items) {
    const section = document.getElementById('deliveryNoteResultsSection');
    const printSection = document.getElementById('deliveryNotePrintSection');
    const countEl = document.getElementById('deliveryNoteResultCount');

    if (items.length === 0) {
        alert('該当するデータが見つかりませんでした');
        section.style.display = 'none';
        printSection.style.display = 'none';
        return;
    }

    countEl.textContent = `${items.length}件`;
    renderDeliveryNoteRows(items);
    section.style.display = 'block';
    printSection.style.display = 'flex';
    document.getElementById('deliveryNoteHeaderCheckbox').checked = false;
}

function renderDeliveryNoteRows(items) {
    const countEl = document.getElementById('deliveryNoteResultCount');
    const tbody = document.getElementById('deliveryNoteResultsBody');

    countEl.textContent = `${items.length}件`;
    tbody.innerHTML = '';

    items.forEach((item, index) => {
        const originalIndex = deliveryNoteSearchResults.indexOf(item);
        const row = tbody.insertRow();
        row.innerHTML = `
            <td><input type="checkbox" class="delivery-note-item-checkbox" data-index="${originalIndex}"></td>
            <td>${formatDate(item.eating_date) || '-'}</td>
            <td>${escapeHtml(item.location_name) || '-'}</td>
        `;
        row.style.cursor = 'pointer';
        row.addEventListener('click', (e) => {
            if (e.target.classList.contains('delivery-note-item-checkbox')) return;
            const cb = row.querySelector('.delivery-note-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    document.getElementById('deliveryNoteHeaderCheckbox').checked = false;
}

function formatDate(yyyymmdd) {
    if (!yyyymmdd || yyyymmdd.length !== 8) return yyyymmdd;
    return `${yyyymmdd.slice(0, 4)}-${yyyymmdd.slice(4, 6)}-${yyyymmdd.slice(6, 8)}`;
}

function escapeHtml(text) {
    if (text == null) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

document.getElementById('deliveryNoteHeaderCheckbox').addEventListener('change', (e) => {
    document.querySelectorAll('.delivery-note-item-checkbox').forEach(cb => { cb.checked = e.target.checked; });
});

document.getElementById('deliveryNoteSelectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.delivery-note-item-checkbox').forEach(cb => { cb.checked = true; });
    document.getElementById('deliveryNoteHeaderCheckbox').checked = true;
});

document.getElementById('deliveryNoteDeselectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.delivery-note-item-checkbox').forEach(cb => { cb.checked = false; });
    document.getElementById('deliveryNoteHeaderCheckbox').checked = false;
});

/**
 * 選択された行データを取得（PDF印刷・納品書.rxz テンプレート用）
 */
export function getSelectedDeliveryNoteRows() {
    const deliveryRoute = document.getElementById('deliveryNoteDeliveryRoute')?.value || '';
    const checked = document.querySelectorAll('.delivery-note-item-checkbox:checked');
    const indices = new Set(Array.from(checked).map(cb => parseInt(cb.dataset.index, 10)));
    return deliveryNoteSearchResults
        .filter((_, i) => indices.has(i))
        .map(item => ({
            eating_date: item.eating_date || '',
            location_code: item.location_code || '',
            customer_code: item.customer_code ?? '',
            delivery_route: deliveryRoute
        }));
}
