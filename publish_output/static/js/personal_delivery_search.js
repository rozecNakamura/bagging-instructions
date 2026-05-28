/**
 * 個人配送指示書：喫食日で検索・結果表示・選択
 */
import { searchPersonalDelivery } from './api.js';

let personalDeliverySearchResults = [];

function getSelectedVariant() {
    return document.getElementById('personalDeliveryVariant')?.value || 'detail';
}

function updatePrintButtonsVisibility() {
    const variant = getSelectedVariant();
    const detailBtn = document.getElementById('personalDeliveryDetailPrintBtn');
    const summaryBtn = document.getElementById('personalDeliverySummaryPrintBtn');
    if (detailBtn) detailBtn.style.display = variant === 'detail' ? '' : 'none';
    if (summaryBtn) summaryBtn.style.display = variant === 'summary' ? '' : 'none';
}

document.getElementById('personalDeliveryVariant')?.addEventListener('change', updatePrintButtonsVisibility);
updatePrintButtonsVisibility();

document.getElementById('personalDeliverySearchBtn').addEventListener('click', async () => {
    const eatingDate = document.getElementById('personalDeliveryEatingDate').value;
    const variant = getSelectedVariant();

    if (!eatingDate) {
        alert('喫食日を入力してください');
        return;
    }

    try {
        const response = await searchPersonalDelivery(eatingDate, variant);
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
            <td>${formatDate(item.eating_date) || '-'}</td>
            <td>${escapeHtml(item.meal_time_name || item.meal_time) || '-'}</td>
            <td>${escapeHtml(item.course) || '-'}</td>
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
    updatePrintButtonsVisibility();
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
            eating_date: item.eating_date || '',
            meal_time: item.meal_time ?? '',
            course: item.course ?? ''
        }));
}

export function getSelectedPersonalDeliveryVariant() {
    return getSelectedVariant();
}
