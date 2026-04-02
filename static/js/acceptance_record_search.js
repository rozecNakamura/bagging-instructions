import { fetchAcceptanceDeliveryLocations, searchAcceptanceRecord } from './api.js';

let acceptanceRows = [];
let acceptanceLocationList = [];
/** @type {Set<string>} 選択された customerCode + TAB + locationCode（空＝絞り込みなし） */
let acceptanceSelectedStoreKeys = new Set();

function makeStorePairKey(o) {
    const cc = (o.customerCode || '').trim();
    const lc = (o.locationCode || '').trim();
    return `${cc}\t${lc}`;
}

function displayAcceptanceResults(rows) {
    const section = document.getElementById('acceptanceResultsSection');
    const printSection = document.getElementById('acceptancePrintSection');
    const countEl = document.getElementById('acceptanceResultCount');
    const tbody = document.getElementById('acceptanceResultsBody');

    if (!section || !printSection || !countEl || !tbody) return;

    if (!rows || !rows.length) {
        alert('該当するデータが見つかりませんでした');
        section.style.display = 'none';
        printSection.style.display = 'none';
        return;
    }

    countEl.textContent = `${rows.length}件`;
    tbody.innerHTML = '';

    rows.forEach((row, index) => {
        const tr = tbody.insertRow();
        tr.innerHTML = `
            <td><input type="checkbox" class="acceptance-item-checkbox" data-index="${index}"></td>
            <td>${row.eatDate || '-'}</td>
            <td>${row.mealTime || '-'}</td>
            <td>${row.childItem || '-'}</td>
            <td class="text-right">${row.mealCountDisplay || '-'}</td>
            <td class="text-right">${row.totalQtyDisplay || '-'}</td>
            <td>${row.unitName || '-'}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('acceptance-item-checkbox')) return;
            const cb = tr.querySelector('.acceptance-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'block';
    const headerCheckbox = document.getElementById('acceptanceHeaderCheckbox');
    if (headerCheckbox) headerCheckbox.checked = false;
}

function updateAcceptanceStoreSummary() {
    const label = document.getElementById('acceptanceStoreSelectedLabel');
    if (!label) return;

    if (acceptanceLocationList.length === 0) {
        label.textContent = 'マスタなし';
        return;
    }

    if (acceptanceSelectedStoreKeys.size === 0) {
        label.textContent = 'すべて';
    } else if (acceptanceSelectedStoreKeys.size === acceptanceLocationList.length) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${acceptanceSelectedStoreKeys.size}件選択`;
    }
}

function buildAcceptanceStorePanel() {
    const container = document.getElementById('acceptanceStoreOptions');
    if (!container) return;
    container.innerHTML = '';
    acceptanceLocationList.forEach((o) => {
        const key = makeStorePairKey(o);
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = key;
        if (acceptanceSelectedStoreKeys.has(key)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                acceptanceSelectedStoreKeys.add(key);
            } else {
                acceptanceSelectedStoreKeys.delete(key);
            }
            updateAcceptanceStoreSummary();
        });
        const text = document.createElement('span');
        text.textContent = (o.displayLabel || key || '').trim() || key;
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updateAcceptanceStoreSummary();
}

async function loadAcceptanceDeliveryLocations() {
    try {
        const list = await fetchAcceptanceDeliveryLocations();
        acceptanceLocationList = list || [];
        buildAcceptanceStorePanel();
    } catch (e) {
        console.error(e);
        acceptanceLocationList = [];
        buildAcceptanceStorePanel();
    }
}

/** @returns {string[]} 検索に渡す storePair（未選択のときは空配列＝絞り込みなし） */
export function getSelectedAcceptanceStorePairs() {
    return Array.from(acceptanceSelectedStoreKeys.values());
}

/** PDF ヘッダー用。未選択のときは空文字。 */
export function buildAcceptanceStoreHeaderText() {
    const keys = Array.from(acceptanceSelectedStoreKeys);
    if (keys.length === 0) return '';
    return keys
        .map((k) => {
            const o = acceptanceLocationList.find((x) => makeStorePairKey(x) === k);
            return (o && o.displayLabel) ? o.displayLabel.trim() : k;
        })
        .filter(Boolean)
        .join(', ');
}

export function getSelectedAcceptanceSalesOrderLineIds() {
    const checked = document.querySelectorAll('.acceptance-item-checkbox:checked');
    const ids = [];
    checked.forEach(cb => {
        const index = Number(cb.dataset.index);
        const row = acceptanceRows[index];
        if (row && typeof row.salesOrderLineId === 'number') {
            ids.push(row.salesOrderLineId);
        }
    });
    return ids;
}

document.addEventListener('DOMContentLoaded', () => {
    loadAcceptanceDeliveryLocations();

    const storeDisplay = document.getElementById('acceptanceStoreDisplay');
    if (storeDisplay) {
        storeDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('acceptanceStoreOptions');
            if (!panel) return;
            const isHidden = panel.style.display === 'none' || panel.style.display === '';
            if (isHidden) {
                panel.style.display = 'block';
            } else {
                panel.style.display = 'none';
            }
        });
    }

    document.addEventListener('click', (e) => {
        const dropdown = (e.target instanceof HTMLElement)
            ? e.target.closest('#screen-acceptance-record .multi-select-dropdown')
            : null;
        if (!dropdown) {
            const panel = document.getElementById('acceptanceStoreOptions');
            if (panel) panel.style.display = 'none';
        }
    });

    const searchBtn = document.getElementById('acceptanceSearchBtn');
    const headerCheckbox = document.getElementById('acceptanceHeaderCheckbox');
    const selectAllBtn = document.getElementById('acceptanceSelectAllBtn');
    const deselectAllBtn = document.getElementById('acceptanceDeselectAllBtn');

    if (!searchBtn) return;

    searchBtn.addEventListener('click', async () => {
        const delvDate = document.getElementById('acceptanceDelvDate').value;
        const shipDate = document.getElementById('acceptanceShipDate').value || '';
        const storePairs = getSelectedAcceptanceStorePairs();

        if (!delvDate) {
            alert('納品日を入力してください');
            return;
        }

        try {
            const res = await searchAcceptanceRecord(delvDate, shipDate, storePairs);
            acceptanceRows = res.rows || [];
            displayAcceptanceResults(acceptanceRows);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    headerCheckbox?.addEventListener('change', (e) => {
        const checked = e.target.checked;
        document.querySelectorAll('.acceptance-item-checkbox').forEach(cb => {
            cb.checked = checked;
        });
    });

    selectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.acceptance-item-checkbox').forEach(cb => { cb.checked = true; });
        if (headerCheckbox) headerCheckbox.checked = true;
    });

    deselectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.acceptance-item-checkbox').forEach(cb => { cb.checked = false; });
        if (headerCheckbox) headerCheckbox.checked = false;
    });
});
