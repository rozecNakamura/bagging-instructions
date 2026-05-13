/**
 * 個人配送指示書：配送日で検索・結果表示・選択
 */
import { searchPersonalDelivery } from './api.js';

let personalDeliverySearchResults = [];

document.getElementById('personalDeliverySearchBtn').addEventListener('click', async () => {
    const deliveryDate = document.getElementById('personalDeliveryDate').value;

    if (!deliveryDate) {
        alert('配送日を入力してください');
        return;
    }

    try {
        const response = await searchPersonalDelivery(deliveryDate);
        personalDeliverySearchResults = response.items || [];
        displayPersonalDeliveryResults(personalDeliverySearchResults);
    } catch (error) {
        alert('検索に失敗しました: ' + error.message);
    }
});

function displayPersonalDeliveryResults(items) {
    const section = document.getElementById('personalDeliveryResultsSection');
    const printSection = document.getElementById('personalDeliveryPrintSection');
    const countEl = document.getElementById('personalDeliveryResultCount');
    const tbody = document.getElementById('personalDeliveryResultsBody');

    if (items.length === 0) {
        alert('該当するデータが見つかりませんでした');
        section.style.display = 'none';
        printSection.style.display = 'none';
        return;
    }

    countEl.textContent = `${items.length}件`;
    tbody.innerHTML = '';

    items.forEach((item, index) => {
        const row = tbody.insertRow();
        row.innerHTML = `
            <td><input type="checkbox" class="personal-delivery-item-checkbox" data-index="${index}"></td>
            <td>${formatDate(item.delivery_date) || '-'}</td>
            <td>${escapeHtml(item.time_name) || '-'}</td>
            <td>${escapeHtml(item.area) || '-'}</td>
        `;
        row.style.cursor = 'pointer';
        row.addEventListener('click', (e) => {
            if (e.target.classList.contains('personal-delivery-item-checkbox')) return;
            const cb = row.querySelector('.personal-delivery-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'flex';
    document.getElementById('personalDeliveryHeaderCheckbox').checked = false;
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

document.getElementById('personalDeliveryHeaderCheckbox').addEventListener('change', (e) => {
    document.querySelectorAll('.personal-delivery-item-checkbox').forEach(cb => { cb.checked = e.target.checked; });
});

document.getElementById('personalDeliverySelectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.personal-delivery-item-checkbox').forEach(cb => { cb.checked = true; });
    document.getElementById('personalDeliveryHeaderCheckbox').checked = true;
});

document.getElementById('personalDeliveryDeselectAllBtn').addEventListener('click', () => {
    document.querySelectorAll('.personal-delivery-item-checkbox').forEach(cb => { cb.checked = false; });
    document.getElementById('personalDeliveryHeaderCheckbox').checked = false;
});

/**
 * 選択された行データを取得（PDF印刷用）
 */
export function getSelectedPersonalDeliveryRows() {
    const checked = document.querySelectorAll('.personal-delivery-item-checkbox:checked');
    const indices = new Set(Array.from(checked).map(cb => parseInt(cb.dataset.index, 10)));
    return personalDeliverySearchResults
        .filter((_, i) => indices.has(i))
        .map(item => ({
            delivery_date: item.delivery_date || '',
            time_name: item.time_name ?? '',
            area: item.area ?? ''
        }));
}
