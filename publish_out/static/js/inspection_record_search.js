import { fetchInspectionSuppliers, searchInspectionRecord } from './api.js';

let inspectionRows = [];
let inspectionSupplierList = [];
/** @type {Set<string>} 選択された仕入先コード（空＝絞り込みなし） */
let inspectionSelectedSupplierCodes = new Set();

function displayInspectionResults(rows) {
    const section = document.getElementById('inspectionResultsSection');
    const printSection = document.getElementById('inspectionPrintSection');
    const countEl = document.getElementById('inspectionResultCount');
    const tbody = document.getElementById('inspectionResultsBody');

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
        const dateDisplay = row.needDate || '-';
        tr.innerHTML = `
            <td><input type="checkbox" class="inspection-item-checkbox" data-index="${index}"></td>
            <td>${row.orderNo || '-'}</td>
            <td>${row.supplierDisplay || '-'}</td>
            <td>${dateDisplay || '-'}</td>
            <td>${row.itemCode || '-'}</td>
            <td>${row.itemName || '-'}</td>
            <td class="text-right">${row.quantityDisplay || '-'}</td>
            <td>${row.unitName || '-'}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('inspection-item-checkbox')) return;
            const cb = tr.querySelector('.inspection-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'flex';
    const headerCheckbox = document.getElementById('inspectionHeaderCheckbox');
    if (headerCheckbox) headerCheckbox.checked = false;
}

function updateInspectionSupplierSummary() {
    const label = document.getElementById('inspectionSupplierSelectedLabel');
    if (!label) return;

    if (inspectionSupplierList.length === 0) {
        label.textContent = 'マスタなし';
        return;
    }

    if (inspectionSelectedSupplierCodes.size === 0) {
        label.textContent = 'すべて';
    } else if (inspectionSelectedSupplierCodes.size === inspectionSupplierList.length) {
        label.textContent = 'すべて選択';
    } else {
        label.textContent = `${inspectionSelectedSupplierCodes.size}件選択`;
    }
}

function buildInspectionSupplierPanel() {
    const container = document.getElementById('inspectionSupplierOptions');
    if (!container) return;
    container.innerHTML = '';
    inspectionSupplierList.forEach((s) => {
        const code = s.supplierCode || '';
        const label = document.createElement('label');
        const cb = document.createElement('input');
        cb.type = 'checkbox';
        cb.value = code;
        if (inspectionSelectedSupplierCodes.has(code)) cb.checked = true;
        cb.addEventListener('change', () => {
            if (cb.checked) {
                inspectionSelectedSupplierCodes.add(code);
            } else {
                inspectionSelectedSupplierCodes.delete(code);
            }
            updateInspectionSupplierSummary();
        });
        const text = document.createElement('span');
        const nm = (s.supplierName || '').trim();
        text.textContent = nm || code || '';
        label.appendChild(cb);
        label.appendChild(text);
        container.appendChild(label);
    });
    updateInspectionSupplierSummary();
}

async function loadInspectionSuppliers() {
    try {
        const list = await fetchInspectionSuppliers();
        inspectionSupplierList = list || [];
        buildInspectionSupplierPanel();
    } catch (e) {
        console.error(e);
        inspectionSupplierList = [];
        buildInspectionSupplierPanel();
    }
}

/** @returns {string[]} 検索に渡す仕入先コード（未選択のときは空配列＝絞り込みなし） */
export function getSelectedInspectionSupplierCodes() {
    return Array.from(inspectionSelectedSupplierCodes.values());
}

export function getSelectedInspectionOrderTableIds() {
    const checked = document.querySelectorAll('.inspection-item-checkbox:checked');
    const ids = [];
    checked.forEach(cb => {
        const index = Number(cb.dataset.index);
        const row = inspectionRows[index];
        if (row && typeof row.orderTableId === 'number') {
            ids.push(row.orderTableId);
        }
    });
    return ids;
}

document.addEventListener('DOMContentLoaded', () => {
    loadInspectionSuppliers();

    const supplierDisplay = document.getElementById('inspectionSupplierDisplay');
    if (supplierDisplay) {
        supplierDisplay.addEventListener('click', (e) => {
            e.stopPropagation();
            const panel = document.getElementById('inspectionSupplierOptions');
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
            ? e.target.closest('#screen-inspection-record .multi-select-dropdown')
            : null;
        if (!dropdown) {
            const panel = document.getElementById('inspectionSupplierOptions');
            if (panel) panel.style.display = 'none';
        }
    });

    const searchBtn = document.getElementById('inspectionSearchBtn');
    const headerCheckbox = document.getElementById('inspectionHeaderCheckbox');
    const selectAllBtn = document.getElementById('inspectionSelectAllBtn');
    const deselectAllBtn = document.getElementById('inspectionDeselectAllBtn');

    if (!searchBtn) return;

    searchBtn.addEventListener('click', async () => {
        const needDate = document.getElementById('inspectionNeedDate').value;
        const supplierCodes = getSelectedInspectionSupplierCodes();

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        try {
            const res = await searchInspectionRecord(needDate, supplierCodes);
            inspectionRows = res.rows || [];
            displayInspectionResults(inspectionRows);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    headerCheckbox?.addEventListener('change', (e) => {
        const checked = e.target.checked;
        document.querySelectorAll('.inspection-item-checkbox').forEach(cb => {
            cb.checked = checked;
        });
    });

    selectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.inspection-item-checkbox').forEach(cb => { cb.checked = true; });
        if (headerCheckbox) headerCheckbox.checked = true;
    });

    deselectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll('.inspection-item-checkbox').forEach(cb => { cb.checked = false; });
        if (headerCheckbox) headerCheckbox.checked = false;
    });
});
