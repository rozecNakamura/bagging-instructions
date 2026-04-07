import { searchProductionInstruction } from './api.js';

let gmtRows = [];

const screenSel = '#screen-production-instruction-ganmono';

function displayGmtResults(rows) {
    const section = document.getElementById('gmtResultsSection');
    const printSection = document.getElementById('gmtPrintSection');
    const countEl = document.getElementById('gmtResultCount');
    const tbody = document.getElementById('gmtResultsBody');

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
            <td><input type="checkbox" class="gmt-item-checkbox" data-index="${index}"></td>
            <td>${row.itemCode || '-'}</td>
            <td>${row.itemName || '-'}</td>
            <td>${row.quantityDisplay != null && row.quantityDisplay !== '' ? row.quantityDisplay : '-'}</td>
            <td>${row.unitName || '-'}</td>
        `;
        tr.style.cursor = 'pointer';
        tr.addEventListener('click', (e) => {
            if (e.target.classList.contains('gmt-item-checkbox')) return;
            const cb = tr.querySelector('.gmt-item-checkbox');
            if (cb) cb.checked = !cb.checked;
        });
    });

    section.style.display = 'block';
    printSection.style.display = 'block';
    const headerCheckbox = document.getElementById('gmtHeaderCheckbox');
    if (headerCheckbox) headerCheckbox.checked = false;
}

export function getGmtSelectedOrderIds() {
    const checked = document.querySelectorAll(`${screenSel} .gmt-item-checkbox:checked`);
    const ids = [];
    checked.forEach(cb => {
        const index = Number(cb.dataset.index);
        const row = gmtRows[index];
        if (row && typeof row.orderTableId === 'number') {
            ids.push(row.orderTableId);
        }
    });
    return ids;
}

/** @returns {{ needDate: string, workcenterIds: number[], slotCodes: string[] }} */
export function getGmtReportFilter() {
    const needDate = document.getElementById('gmtNeedDate')?.value || '';
    return {
        needDate,
        workcenterIds: [],
        slotCodes: []
    };
}

document.addEventListener('DOMContentLoaded', () => {
    const searchBtn = document.getElementById('gmtSearchBtn');
    const headerCheckbox = document.getElementById('gmtHeaderCheckbox');
    const selectAllBtn = document.getElementById('gmtSelectAllBtn');
    const deselectAllBtn = document.getElementById('gmtDeselectAllBtn');

    if (!searchBtn) return;

    searchBtn.addEventListener('click', async () => {
        const needDate = document.getElementById('gmtNeedDate').value;
        const itemQuery = document.getElementById('gmtItemSearch')?.value || '';

        if (!needDate) {
            alert('納期を入力してください');
            return;
        }

        try {
            const res = await searchProductionInstruction(needDate, [], [], itemQuery);
            gmtRows = res.rows || [];
            displayGmtResults(gmtRows);
        } catch (e) {
            alert('検索に失敗しました: ' + e.message);
            console.error(e);
        }
    });

    headerCheckbox?.addEventListener('change', (e) => {
        const checked = e.target.checked;
        document.querySelectorAll(`${screenSel} .gmt-item-checkbox`).forEach(cb => {
            cb.checked = checked;
        });
    });

    selectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll(`${screenSel} .gmt-item-checkbox`).forEach(cb => { cb.checked = true; });
        if (headerCheckbox) headerCheckbox.checked = true;
    });

    deselectAllBtn?.addEventListener('click', () => {
        document.querySelectorAll(`${screenSel} .gmt-item-checkbox`).forEach(cb => { cb.checked = false; });
        if (headerCheckbox) headerCheckbox.checked = false;
    });
});
